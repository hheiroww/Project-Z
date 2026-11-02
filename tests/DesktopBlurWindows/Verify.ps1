#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$WorkDirectory, [switch]$SkipGraphics)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$project = Join-Path $PSScriptRoot 'DesktopBlurWindows.csproj'
foreach ($backend in @('DirectX12','DirectX11','OpenGL')) {
    $artifacts = Join-Path $WorkDirectory $backend
    $properties = @("-p:ProjectZGraphicsBackend=$backend", "-p:ArtifactsPath=$artifacts", '-p:GeneratePackageOnBuild=false', '-p:UseSharedCompilation=false')
    dotnet build $project -c Release @properties --nologo -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw "$backend consumer build failed." }
    if (!$SkipGraphics) {
        $program = Get-ChildItem (Join-Path $artifacts 'bin/DesktopBlurWindows') -Filter DesktopBlurWindows.dll -Recurse -File | Select-Object -First 1
        if (!$program) { throw "$backend executable was not built." }
        dotnet $program.FullName --graphics --desktop-blur "--backend=$backend" "--capture=$artifacts/desktop-blur.png"
        if ($LASTEXITCODE -ne 0) { throw "$backend Gaussian shader checks failed." }
        dotnet $program.FullName --graphics --desktop-blur --native-desktop "--backend=$backend"
        if ($LASTEXITCODE -ne 0) { throw "$backend desktop exclusion checks failed." }
    }
}
