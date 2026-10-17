param([string]$SourceRoot, [string]$BuildRoot, [switch]$Tests)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if(-not $SourceRoot) { $SourceRoot = Join-Path $repo 'artifacts\NativeRuntime\MonoGame' }
if(-not $BuildRoot) { $BuildRoot = Join-Path $repo 'artifacts\NativeRuntime\build' }
$SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
$BuildRoot = [IO.Path]::GetFullPath($BuildRoot)
$commit = '5e9fe9b100ffa67026dff97cc8adffae1414534e'
function Check-NativeBuild([string]$step) { if($LASTEXITCODE -ne 0) { throw "$step failed ($LASTEXITCODE)." } }
if(-not (Test-Path (Join-Path $SourceRoot '.git'))) {
    New-Item -ItemType Directory (Split-Path $SourceRoot) -Force | Out-Null
    git clone --depth 1 --branch v3.8.5.1 https://github.com/MonoGame/MonoGame.git $SourceRoot
    Check-NativeBuild 'MonoGame source checkout'
}
if((git -C $SourceRoot rev-parse HEAD) -ne $commit) { throw 'Unexpected upstream MonoGame commit.' }
git -C $SourceRoot submodule update --init --depth 1 native/monogame/external/sdl2 external/stb
Check-NativeBuild 'MonoGame dependencies'
git -C (Join-Path $SourceRoot 'native\monogame\external\sdl2') submodule update --init --depth 1 sdl
Check-NativeBuild 'SDL source'
$patch = Join-Path $PSScriptRoot 'GpuFence.patch'
git -C $SourceRoot apply --reverse --check $patch 2>$null
if($LASTEXITCODE -ne 0) {
    git -C $SourceRoot apply --check $patch
    Check-NativeBuild 'Graphics patch validation'
    git -C $SourceRoot apply $patch
    Check-NativeBuild 'Graphics patch application'
}
Push-Location $repo
try {
    dotnet tool restore
    Check-NativeBuild 'Pinned shader compiler restore'
    foreach($effect in Get-ChildItem (Join-Path $SourceRoot 'MonoGame.Framework\Platform\Graphics\Effect\Resources') -Filter '*.fx') {
        $output = Join-Path $SourceRoot ('native\monogame\directx12\'+$effect.BaseName+'.dx12.mgfxo.h')
        dotnet tool run mgfxc -- $effect.FullName $output /Profile:DirectX_12
        Check-NativeBuild 'Native shader compilation'
    }
} finally { Pop-Location }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = @(& $vswhere -latest -prerelease -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -format json | ConvertFrom-Json)[0]
if(-not $vs) { throw 'Visual Studio C++ build tools are required.' }
$cmake = Join-Path $vs.installationPath 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
$generator = if([version]$vs.installationVersion -ge [version]'18.0') { 'Visual Studio 18 2026' } else { 'Visual Studio 17 2022' }
& $cmake -S $PSScriptRoot -B $BuildRoot -G $generator -A x64 "-DMONOGAME_SOURCE=$($SourceRoot.Replace('\','/'))" "-DPROJECTZ_NATIVE_TESTS=$($Tests.IsPresent)"
Check-NativeBuild 'Native configuration'
& $cmake --build $BuildRoot --config Release --parallel 8
Check-NativeBuild 'Native compilation'
if($Tests) {
    & (Join-Path $BuildRoot 'Release\ProjectZFenceWait.exe')
    Check-NativeBuild 'Real GPU fence tests'
}
$payload = Join-Path $PSScriptRoot 'win-x64'
New-Item -ItemType Directory $payload -Force | Out-Null
Copy-Item (Join-Path $BuildRoot 'Release\mgruntime.dll') $payload -Force
Copy-Item (Join-Path $BuildRoot 'Release\mgruntime.pdb') $payload -Force
$hash = (Get-FileHash (Join-Path $payload 'mgruntime.dll')).Hash
[ordered]@{version='3.8.5.1-projectz.1';upstreamCommit=$commit;sha256=$hash} | ConvertTo-Json | Set-Content (Join-Path $payload 'runtime.json') -Encoding utf8
Write-Output "Built Project-Z native DX12 runtime; SHA256=$hash"
