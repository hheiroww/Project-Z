#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
if ($IsWindows) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $installations = @(& $vswhere -all -prerelease -products '*' -property installationPath)
    $compiler = $null
    foreach ($installation in $installations) {
        $versions = @(Get-ChildItem (Join-Path $installation 'VC/Tools/MSVC') -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)
        foreach ($version in $versions) {
            $candidate = Join-Path $version.FullName 'bin/Hostx64/x64/cl.exe'
            if (Test-Path -LiteralPath $candidate) { $compiler = $candidate; $vc = $version.FullName; break }
        }
        if ($compiler) { break }
    }
    if (!$compiler) { throw 'Building Windows desktop capture requires Visual C++ x64 build tools.' }
    $sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
    $sdk = @(Get-ChildItem (Join-Path $sdkRoot 'Include') -Directory | Sort-Object Name -Descending | Where-Object { Test-Path (Join-Path $_.FullName 'cppwinrt/winrt/Windows.Graphics.Capture.h') })[0]
    if (!$sdk) { throw 'Building desktop capture requires a Windows 10/11 SDK with C++/WinRT.' }
    $argsForCompiler = @('/nologo','/std:c++20','/EHsc','/MT','/O2','/LD','/DWIN32_LEAN_AND_MEAN','/DNOMINMAX',"/I$vc/include")
    foreach ($include in @('shared','um','winrt','cppwinrt','ucrt')) { $argsForCompiler += "/I$($sdk.FullName)/$include" }
    $argsForCompiler += @("/Fo$OutputDirectory/WindowsCapture.obj", "/Fe$OutputDirectory/ProjectZDesktopCapture.dll", (Join-Path $root 'Native/DesktopCapture/WindowsCapture.cpp'), '/link', "/LIBPATH:$vc/lib/x64", "/LIBPATH:$sdkRoot/Lib/$($sdk.Name)/um/x64", "/LIBPATH:$sdkRoot/Lib/$($sdk.Name)/ucrt/x64", 'd3d11.lib','dxgi.lib','windowsapp.lib','user32.lib','runtimeobject.lib','shcore.lib')
    & $compiler @argsForCompiler
    if ($LASTEXITCODE -ne 0) { throw 'Windows desktop capture compilation failed.' }
} elseif ($IsMacOS) {
    foreach ($arch in @('arm64','x86_64')) {
        & xcrun swiftc -swift-version 5 -O -emit-library -target "$arch-apple-macosx13.0" -module-name ProjectZDesktopCapture -framework ScreenCaptureKit -framework AppKit (Join-Path $root 'Native/DesktopCapture/MacCapture.swift') -o (Join-Path $OutputDirectory "$arch.dylib")
        if ($LASTEXITCODE -ne 0) { throw "macOS $arch desktop capture compilation failed." }
    }
    & xcrun lipo -create (Join-Path $OutputDirectory 'arm64.dylib') (Join-Path $OutputDirectory 'x86_64.dylib') -output (Join-Path $OutputDirectory 'libProjectZDesktopCapture.dylib')
    if ($LASTEXITCODE -ne 0) { throw 'Universal macOS desktop capture library creation failed.' }
}
