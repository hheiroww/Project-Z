#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$WorkDirectory, [switch]$SkipGraphics)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'DesktopBackdropScheduling.csproj'
dotnet build $project -c Release --nologo -m:1 -nr:false -p:ProjectZGraphicsBackend=DirectX11 "-p:ArtifactsPath=$WorkDirectory" -p:GeneratePackageOnBuild=false -p:UseSharedCompilation=false -p:BuildProjectZDesktopCapture=false
if ($LASTEXITCODE -ne 0) { throw 'Backdrop responsiveness fixture build failed.' }
$program = Get-ChildItem (Join-Path $WorkDirectory 'bin/DesktopBackdropScheduling') -Filter DesktopBackdropScheduling.dll -Recurse -File | Select-Object -First 1
if (!$program) { throw 'Backdrop responsiveness executable was not built.' }
dotnet $program.FullName --scheduling-only
if ($LASTEXITCODE -ne 0) { throw 'Backdrop scheduling checks failed.' }
if (!$SkipGraphics) {
    dotnet $program.FullName
    if ($LASTEXITCODE -ne 0) { throw 'Synthetic offscreen backdrop pixel checks failed.' }
}
