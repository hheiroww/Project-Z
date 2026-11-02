#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Directory, [switch]$DesktopGLOnly, [string]$ExpectedCommit)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$properties = Get-Content (Join-Path $root 'Extensions/ProjectZ.Package.props')
$version = [string]$properties.Project.PropertyGroup.Version
$ids = @('ProjectZ.Linux','ProjectZ.macOS')
if (!$DesktopGLOnly) { $ids = @('ProjectZ','ProjectZ.ModelImport','ProjectZ.WebView2','ProjectZ.Diagnostics','ProjectZ.XamlTools') + $ids }
$packages = @(Get-ChildItem -LiteralPath $Directory -Filter '*.nupkg' -File)
if ($packages.Count -ne $ids.Count) { throw "Expected $($ids.Count) release packages, found $($packages.Count)." }
foreach ($id in $ids) {
    $path = Join-Path $Directory "$id.$version.nupkg"
    if (!(Test-Path -LiteralPath $path)) { throw "Missing release package: $id $version" }
    $archive = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $spec = $archive.Entries | Where-Object FullName -Like '*.nuspec'
        $reader = [IO.StreamReader]::new($spec.Open())
        try { [xml]$metadata = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($metadata.package.metadata.id -ne $id -or $metadata.package.metadata.version -ne $version) { throw "Incorrect package identity: $path" }
        if ($ExpectedCommit -and $metadata.package.metadata.repository.commit -ne $ExpectedCommit) { throw "Package source commit mismatch: $id" }
        $required = if ($id -eq 'ProjectZ') { 'runtimes/win-x64/native/ProjectZDesktopCapture.dll' } elseif ($id -eq 'ProjectZ.macOS') { 'runtimes/osx/native/libProjectZDesktopCapture.dylib' } else { $null }
        if ($required -and $archive.Entries.FullName -notcontains $required) { throw "Missing native capture runtime in $id" }
        if ($id -eq 'ProjectZ.macOS') {
            $stream = $archive.GetEntry($required).Open()
            try { $magic = [byte[]]::new(4); [void]$stream.Read($magic,0,4) } finally { $stream.Dispose() }
            if ([Convert]::ToHexString($magic) -notin @('CAFEBABE','CAFEBABF')) { throw 'macOS capture helper must be a universal Intel/Apple Silicon binary.' }
        }
    } finally { $archive.Dispose() }
}
Write-Host "Release set verified: $($ids.Count) packages, version $version."
