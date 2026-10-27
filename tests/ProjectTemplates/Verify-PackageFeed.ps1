[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. "$repository/ProjectTemplate/VS2026/PackageFeed.ps1"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$testRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('ProjectZ.PackageFeed.' + [Guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $testRoot | Out-Null

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (!$Condition) { throw $Message }
}
function Assert-Throws([scriptblock]$Action, [string]$Message, [string]$ExpectedText) {
    $failure = $null
    try { & $Action | Out-Null } catch { $failure = $_.Exception.Message }
    if (!$failure -or ($ExpectedText -and !$failure.Contains($ExpectedText))) { throw $Message }
}
function New-TestPackage([string]$Path, [string]$Id, [string]$Version, [switch]$DuplicateManifest) {
    $archive = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in @('ProjectZ.nuspec') + $(if ($DuplicateManifest) { @('Second.nuspec') } else { @() })) {
            $writer = [IO.StreamWriter]::new($archive.CreateEntry($name).Open())
            try { $writer.Write("<package><metadata><id>$Id</id><version>$Version</version><authors>ProjectZ regression</authors><description>Isolated package feed regression.</description></metadata></package>") } finally { $writer.Dispose() }
        }
    } finally { $archive.Dispose() }
}

try {
    $version = '2099.1.2-fixture.' + [Guid]::NewGuid().ToString('N')
    $package = Join-Path $testRoot 'input.nupkg'
    New-TestPackage $package ProjectZ $version
    Assert-Throws {
        New-ProjectZRestoreConfiguration -ConfigPath "$testRoot/missing/NuGet.Config" -PackagesPath "$testRoot/missing/packages" -PackagePath "$testRoot/absent.nupkg" -ExpectedVersion $version
    } 'A missing package was accepted.'
    Assert-Throws {
        New-ProjectZRestoreConfiguration -ConfigPath "$testRoot/wrong-version/NuGet.Config" -PackagesPath "$testRoot/wrong-version/packages" -PackagePath $package -ExpectedVersion '2099.1.2.4'
    } 'A package with a different version was accepted.' 'version'
    Assert-Condition (!(Test-Path -LiteralPath "$testRoot/wrong-version")) 'Invalid package validation created a restore cache.'
    $otherPackage = Join-Path $testRoot 'other.nupkg'
    New-TestPackage $otherPackage OtherLibrary $version
    Assert-Throws {
        New-ProjectZRestoreConfiguration -ConfigPath "$testRoot/wrong-id/NuGet.Config" -PackagesPath "$testRoot/wrong-id/packages" -PackagePath $otherPackage -ExpectedVersion $version
    } 'A different package ID was accepted.' 'package ID'
    $duplicatePackage = Join-Path $testRoot 'duplicate.nupkg'
    New-TestPackage $duplicatePackage ProjectZ $version -DuplicateManifest
    Assert-Throws {
        New-ProjectZRestoreConfiguration -ConfigPath "$testRoot/duplicate/NuGet.Config" -PackagesPath "$testRoot/duplicate/packages" -PackagePath $duplicatePackage -ExpectedVersion $version
    } 'A package with multiple manifests was accepted.' 'exactly one'

    $local = New-ProjectZRestoreConfiguration -ConfigPath "$testRoot/local/NuGet.Config" -PackagesPath "$testRoot/local/packages" -PackagePath $package -ExpectedVersion $version
    [xml]$localConfig = Get-Content -LiteralPath $local.ConfigPath -Raw
    Assert-Condition (@($localConfig.configuration.packageSources.add).Count -eq 2) 'Local mode must expose only its exact local feed and official NuGet.'
    $localMapping = @($localConfig.configuration.packageSourceMapping.packageSource | Where-Object key -EQ 'projectz-under-test')
    Assert-Condition ($localMapping.Count -eq 1 -and $localMapping[0].package.pattern -ceq 'ProjectZ') 'The exact ProjectZ package is not mapped to its audited local source.'
    Assert-Condition (@(Get-ChildItem -LiteralPath $local.FeedPath -Filter '*.nupkg').Count -eq 1) 'The isolated local feed must contain exactly the supplied package.'
    Assert-Condition ((Get-Content -LiteralPath (Join-Path $local.FeedPath 'SHA256SUMS.txt') -Raw).Contains($local.PackageHash)) 'The local feed is missing its package digest receipt.'
    $public = New-ProjectZRestoreConfiguration -ConfigPath "$testRoot/public/NuGet.Config" -PackagesPath "$testRoot/public/packages" -ExpectedVersion $version
    [xml]$publicConfig = Get-Content -LiteralPath $public.ConfigPath -Raw
    Assert-Condition (@($publicConfig.configuration.packageSources.add).Count -eq 1 -and $publicConfig.configuration.packageSources.add.value -ceq 'https://api.nuget.org/v3/index.json') 'Default verification must use only official NuGet.'
    Assert-Condition (!$public.PackageHash -and !$public.FeedPath -and @($publicConfig.configuration.packageSourceMapping.packageSource).Count -eq 1) 'Default verification retained a local package source or mapping.'
    $previousEnvironment = Enter-ProjectZRestoreEnvironment -RestoreConfiguration $local
    try {
        Assert-Condition ($env:NUGET_PACKAGES -ceq $local.PackagesPath -and $env:RestoreConfigFile -ceq $local.ConfigPath) 'Restore configuration was not propagated to subprocesses.'
        Assert-Condition (!$env:RestoreSources -and !$env:RestoreFallbackFolders -and !$env:NUGET_FALLBACK_PACKAGES) 'An ambient source or fallback can bypass the isolated configuration.'
    } finally { Exit-ProjectZRestoreEnvironment -Previous $previousEnvironment }
    foreach ($name in $previousEnvironment.Keys) {
        Assert-Condition ([Environment]::GetEnvironmentVariable($name, 'Process') -ceq $previousEnvironment[$name]) 'Scoped restore environment was not restored.'
    }

    # Exercise NuGet itself: an unpublished unique package must restore only in
    # explicit local mode, including when ambient caches/fallbacks are populated.
    $consumer = Join-Path $testRoot 'Consumer.csproj'
    "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include=`"ProjectZ`" Version=`"[$version]`" /></ItemGroup></Project>" | Set-Content -LiteralPath $consumer
    $localEnvironment = Enter-ProjectZRestoreEnvironment -RestoreConfiguration $local
    try {
        & dotnet restore $consumer --force --no-http-cache --nologo -p:NuGetAudit=false *> "$testRoot/local-restore.log"
        if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath "$testRoot/local-restore.log" -Tail 20; throw 'NuGet failed to restore the exact mapped local package.' }
        Assert-ProjectZRestoredPackage -RestoreConfiguration $local
        $assets = Get-Content -LiteralPath "$testRoot/obj/project.assets.json" -Raw | ConvertFrom-Json
        Assert-Condition ($null -ne $assets.libraries.PSObject.Properties["ProjectZ/$version"]) 'The unique local package was not restored.'
    } finally { Exit-ProjectZRestoreEnvironment -Previous $localEnvironment }
    Assert-Throws {
        New-ProjectZRestoreConfiguration -ConfigPath "$testRoot/reused/NuGet.Config" -PackagesPath $local.PackagesPath -ExpectedVersion $version
    } 'Public verification accepted a package cache populated by local-mode verification.' 'fresh'
    $ambientNames = @('NUGET_PACKAGES', 'RestoreSources', 'RestoreFallbackFolders', 'NUGET_FALLBACK_PACKAGES')
    $ambientPrevious = @{}
    foreach ($name in $ambientNames) { $ambientPrevious[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
    try {
        $env:NUGET_PACKAGES = $local.PackagesPath
        $env:RestoreSources = $local.FeedPath
        $env:RestoreFallbackFolders = $local.PackagesPath
        $env:NUGET_FALLBACK_PACKAGES = $local.PackagesPath
        $publicEnvironment = Enter-ProjectZRestoreEnvironment -RestoreConfiguration $public
        try {
            & dotnet restore $consumer --force --no-http-cache --nologo -p:NuGetAudit=false *> "$testRoot/public-restore.log"
            $publicExitCode = $LASTEXITCODE
            Assert-Condition ($publicExitCode -ne 0 -and (Get-Content -LiteralPath "$testRoot/public-restore.log" -Raw) -match 'NU1101|NU1102') 'Public-only mode consumed the unpublished local package or failed for an unrelated reason.'
            Assert-Condition (!(Test-Path -LiteralPath (Join-Path $public.PackagesPath "projectz/$version"))) 'Public-only verification populated its cache from an ambient local source.'
        } finally { Exit-ProjectZRestoreEnvironment -Previous $publicEnvironment }
    } finally { Exit-ProjectZRestoreEnvironment -Previous $ambientPrevious }
    $cachedArchive = Join-Path $local.PackagesPath "projectz/$version/projectz.$version.nupkg"
    [IO.File]::AppendAllText($cachedArchive, 'changed package bytes')
    Assert-Throws { Assert-ProjectZRestoredPackage -RestoreConfiguration $local } 'A substituted restored package was accepted.' 'differs'
    Write-Output 'PASS exact local package validation, live mapped restore, SHA256 receipt/cache digest, public-only rejection despite ambient fallback and scoped restore environment'
    $global:LASTEXITCODE = 0
} finally {
    $resolvedTestRoot = (Resolve-Path -LiteralPath $testRoot).Path
    $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (!$resolvedTestRoot.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolvedTestRoot) -notlike 'ProjectZ.PackageFeed.*') { throw 'Unexpected package feed test cleanup target.' }
    Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
}
