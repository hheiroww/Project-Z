#requires -Version 7.0
[CmdletBinding()]
param([string]$OutputDirectory, [ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts/desktopgl-packages' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($project in @('Project Z Linux/ProjectZ.Linux.vbproj', 'Project Z macOS/ProjectZ.macOS.vbproj')) {
    dotnet pack (Join-Path $root $project) -c $Configuration -o $OutputDirectory --nologo -m:1 -nr:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw "DesktopGL package build failed: $project" }
}
Write-Host "DesktopGL packages: $OutputDirectory"
