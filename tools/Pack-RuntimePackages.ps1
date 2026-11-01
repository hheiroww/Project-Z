#requires -Version 7.0
[CmdletBinding()]
param([string]$OutputDirectory, [ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts/runtime-packages' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($project in @(
    'Project Z Windows.vbproj',
    'Project Z Model Import/Project Z Model Import.csproj',
    'Project Z WebView2/ProjectZ.WebView2.vbproj',
    'Project Z Diagnostics/ProjectZ.Diagnostics.vbproj',
    'Project Z XAML Codegen/ProjectZ.XamlCodegen.csproj'
)) {
    & dotnet pack (Join-Path $root $project) -c $Configuration -o $OutputDirectory --nologo -m:1 -nr:false -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false
    if ($LASTEXITCODE -ne 0) { throw "Package build failed: $project" }
}
Write-Host "Runtime packages: $OutputDirectory"
