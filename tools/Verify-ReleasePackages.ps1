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
        $required = switch ($id) {
            'ProjectZ' {
                foreach ($rid in 'win-x64', 'win-arm64') {
                    "runtimes/$rid/native/ProjectZDesktopCapture.dll"
                    "tools/native/$rid/mgruntime.dll"
                    "tools/native/$rid/runtime.json"
                }
            }
            'ProjectZ.Diagnostics' {
                foreach ($rid in 'win-x64', 'win-arm64') { "tools/diagnostics/$rid/ProjectZ.Diagnostics.exe" }
                'licenses/DotNetRuntime/LICENSE.TXT'
                'licenses/DotNetRuntime/THIRD-PARTY-NOTICES.TXT'
            }
            'ProjectZ.macOS' { 'runtimes/osx/native/libProjectZDesktopCapture.dylib' }
        }
        foreach ($entry in $required) {
            if (!$archive.GetEntry($entry)) { throw "Missing $entry in $id" }
        }
        if ($id -eq 'ProjectZ.macOS') {
            $stream = $archive.GetEntry($required).Open()
            try { $magic = [byte[]]::new(4); [void]$stream.Read($magic,0,4) } finally { $stream.Dispose() }
            if ([Convert]::ToHexString($magic) -notin @('CAFEBABE','CAFEBABF')) { throw 'macOS capture helper must be a universal Intel/Apple Silicon binary.' }
        }
    } finally { $archive.Dispose() }
}
Write-Host "Release set verified: $($ids.Count) packages, version $version."
