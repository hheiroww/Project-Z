#requires -Version 7.0
[CmdletBinding()]
param([string]$OutputDirectory, [ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts/runtime-packages' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$nativeStage = Join-Path $OutputDirectory 'native-build'
$arm64Runtime = Join-Path $nativeStage 'payload/win-arm64'
& (Join-Path $root 'ThirdParty/MonoGame.Native/Build.ps1') -Architecture arm64 `
    -SourceRoot (Join-Path $nativeStage 'MonoGame') -BuildRoot (Join-Path $nativeStage 'build-arm64') `
    -OutputDirectory $arm64Runtime
foreach ($project in @(
    'Project Z Windows.vbproj',
    'Project Z Model Import/Project Z Model Import.csproj',
    'Project Z WebView2/ProjectZ.WebView2.vbproj',
    'Project Z Diagnostics/ProjectZ.Diagnostics.vbproj',
    'Project Z XAML Codegen/ProjectZ.XamlCodegen.csproj'
)) {
    $platformProperties = @()
    if ($project -eq 'Project Z Windows.vbproj') {
        $platformProperties = @('-p:BuildProjectZWindowsArm64=true', "-p:ProjectZArm64NativeRuntimeDirectory=$arm64Runtime")
    }
    & dotnet pack (Join-Path $root $project) -c $Configuration -o $OutputDirectory --nologo -m:1 -nr:false -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false @platformProperties
    if ($LASTEXITCODE -ne 0) { throw "Package build failed: $project" }
}
Write-Host "Runtime packages: $OutputDirectory"
