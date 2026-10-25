function New-ProjectZRestoreConfiguration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ConfigPath,
        [Parameter(Mandatory = $true)][string]$PackagesPath,
        [string]$PackagePath,
        [Parameter(Mandatory = $true)][string]$ExpectedVersion
    )
    $ErrorActionPreference = 'Stop'
    if ($ExpectedVersion -notmatch '^\d+(?:\.\d+){1,3}(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?(?:\+[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
        throw 'ExpectedVersion must be an exact NuGet version.'
    }
    $configurationFile = [IO.Path]::GetFullPath($ConfigPath)
    $packageCache = [IO.Path]::GetFullPath($PackagesPath)
    $configurationDirectory = [IO.Path]::GetDirectoryName($configurationFile)
    $packageHash = $null
    $packageFile = $null
    $feedPath = $null
    if ($PackagePath) {
        $packageFile = Get-Item -LiteralPath $PackagePath -ErrorAction Stop
        if ($packageFile -isnot [IO.FileInfo] -or $packageFile.Extension -ine '.nupkg') {
            throw 'PackagePath must identify one existing .nupkg file.'
        }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [IO.Compression.ZipFile]::OpenRead($packageFile.FullName)
        try {
            $manifests = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
            if ($manifests.Count -ne 1) { throw 'The package must contain exactly one nuspec manifest.' }
            $readerSettings = [Xml.XmlReaderSettings]::new()
            $readerSettings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
            $readerSettings.XmlResolver = $null
            $manifestStream = $manifests[0].Open()
            $manifestReader = [Xml.XmlReader]::Create($manifestStream, $readerSettings)
            try {
                $manifest = [Xml.XmlDocument]::new()
                $manifest.XmlResolver = $null
                $manifest.Load($manifestReader)
            } finally { $manifestReader.Dispose(); $manifestStream.Dispose() }
            if ([string]$manifest.package.metadata.id -ine 'ProjectZ') { throw 'The supplied package must have package ID ProjectZ.' }
            if ([string]$manifest.package.metadata.version -cne $ExpectedVersion) { throw 'The supplied ProjectZ package version does not match the template pin.' }
        } finally { $archive.Dispose() }
        $packageHash = (Get-FileHash -LiteralPath $packageFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    if (Test-Path -LiteralPath $packageCache) {
        if (!(Test-Path -LiteralPath $packageCache -PathType Container) -or @(Get-ChildItem -LiteralPath $packageCache -Force).Count) {
            throw 'Use a fresh, empty package cache for each verification.'
        }
    }
    New-Item -ItemType Directory -Path $configurationDirectory, $packageCache -Force | Out-Null
    $localSource = ''
    $localMapping = ''
    if ($packageFile) {
        $feedPath = Join-Path $configurationDirectory 'package-feed'
        if ((Test-Path -LiteralPath $feedPath) -and @(Get-ChildItem -LiteralPath $feedPath -Force).Count) {
            throw 'Use a fresh local feed for each verification.'
        }
        New-Item -ItemType Directory -Path $feedPath -Force | Out-Null
        $copiedPackage = Join-Path $feedPath "ProjectZ.$ExpectedVersion.nupkg"
        Copy-Item -LiteralPath $packageFile.FullName -Destination $copiedPackage
        if ((Get-FileHash -LiteralPath $copiedPackage -Algorithm SHA256).Hash.ToLowerInvariant() -ne $packageHash) {
            throw 'The copied package does not match the supplied package digest.'
        }
        "$packageHash  $([IO.Path]::GetFileName($copiedPackage))" | Set-Content -LiteralPath (Join-Path $feedPath 'SHA256SUMS.txt') -Encoding utf8
        $escapedFeed = [Security.SecurityElement]::Escape($feedPath)
        $localSource = "<add key=`"projectz-under-test`" value=`"$escapedFeed`" />"
        $localMapping = '<packageSource key="projectz-under-test"><package pattern="ProjectZ" /></packageSource>'
    }
    $escapedCache = [Security.SecurityElement]::Escape($packageCache)
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear />$localSource<add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <fallbackPackageFolders><clear /></fallbackPackageFolders>
  <packageSourceMapping><clear />$localMapping<packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
  <config><add key="globalPackagesFolder" value="$escapedCache" /></config>
</configuration>
"@ | Set-Content -LiteralPath $configurationFile -Encoding utf8
    [pscustomobject]@{
        ConfigPath = $configurationFile
        PackagesPath = $packageCache
        PackageHash = $packageHash
        FeedPath = $feedPath
        PackageVersion = $ExpectedVersion
        Mode = $(if ($packageFile) { 'audited local package' } else { 'public NuGet' })
    }
}

function Enter-ProjectZRestoreEnvironment {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)]$RestoreConfiguration)
    $settings = @{
        NUGET_PACKAGES = $RestoreConfiguration.PackagesPath
        RestorePackagesPath = $RestoreConfiguration.PackagesPath
        RestoreConfigFile = $RestoreConfiguration.ConfigPath
        NUGET_HTTP_CACHE_PATH = (Join-Path ([IO.Path]::GetDirectoryName($RestoreConfiguration.ConfigPath)) 'http-cache')
        RestoreSources = $null
        RestoreAdditionalProjectSources = $null
        RestoreFallbackFolders = $null
        RestoreAdditionalProjectFallbackFolders = $null
        NUGET_FALLBACK_PACKAGES = $null
    }
    $previous = @{}
    foreach ($name in $settings.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        if ($null -eq $settings[$name]) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable($name, $settings[$name], 'Process') }
    }
    return $previous
}

function Exit-ProjectZRestoreEnvironment {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][Collections.IDictionary]$Previous)
    foreach ($name in $Previous.Keys) {
        if ($null -eq $Previous[$name]) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable($name, $Previous[$name], 'Process') }
    }
}

function Assert-ProjectZRestoredPackage {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)]$RestoreConfiguration)
    $version = $RestoreConfiguration.PackageVersion.ToLowerInvariant()
    $archive = Join-Path $RestoreConfiguration.PackagesPath "projectz/$version/projectz.$version.nupkg"
    if (!(Test-Path -LiteralPath $archive -PathType Leaf)) { throw 'ProjectZ was not restored into the isolated verification cache.' }
    if ($RestoreConfiguration.PackageHash -and
        (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $RestoreConfiguration.PackageHash) {
        throw 'The restored ProjectZ package differs from the exact supplied package.'
    }
}
