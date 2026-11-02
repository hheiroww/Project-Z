#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$PackageDirectory,
    [ValidateSet('linux-x64','linux-arm64','osx-x64','osx-arm64','win-x64')][string]$RuntimeIdentifier = [Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier,
    [switch]$RunGraphics
)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (!$PackageDirectory) { $PackageDirectory = Join-Path $root 'artifacts/desktopgl-packages' }
$PackageDirectory = [IO.Path]::GetFullPath($PackageDirectory)
[xml]$properties = Get-Content (Join-Path $root 'Extensions/ProjectZ.Package.props')
$version = $properties.Project.PropertyGroup.Version
$temp = Join-Path ([IO.Path]::GetTempPath()) ('ProjectZ-DesktopGL-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $temp | Out-Null
$feed = [Security.SecurityElement]::Escape($PackageDirectory)
Set-Content (Join-Path $temp 'NuGet.Config') "<configuration><packageSources><clear/><add key=`"local`" value=`"$feed`"/><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources></configuration>"
foreach ($platform in @('Linux', 'macOS')) {
    $package = Join-Path $PackageDirectory "ProjectZ.$platform.$version.nupkg"
    if (!(Test-Path -LiteralPath $package)) { throw "Missing package: $package" }
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $entries = @($archive.Entries.FullName)
        foreach ($required in @('lib/net8.0/ProjectZ.dll','lib/net8.0/ProjectZ.VectorText.dll','DESKTOPGL.md','THIRD-PARTY-NOTICES.md','ThirdParty/ProjectZSans/OFL.txt')) {
            if ($entries -notcontains $required) { throw "Missing $required in $package" }
        }
        $nuspec = $archive.Entries | Where-Object FullName -Like '*.nuspec'
        $reader = [IO.StreamReader]::new($nuspec.Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $dependencies = @($manifest.SelectNodes('//*[local-name()="dependency"]') | ForEach-Object id | Sort-Object)
        if (($dependencies -join ',') -ne 'LibTessDotNet,MonoGame.Framework.DesktopGL') { throw "Unexpected dependencies in $package" }
        if ($entries | Where-Object { $_ -match '(^tools/|\.exe$|\.pdb$|\.log$|\.vb$|\.cs$|\.fx$)' }) { throw "Unexpected runtime/source payload in $package" }
    } finally { $archive.Dispose() }
    $consumer = Join-Path $temp $platform
    New-Item -ItemType Directory -Force $consumer | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'Consumer.csproj'),(Join-Path $PSScriptRoot 'Program.cs') $consumer
    $project = Join-Path $consumer 'Consumer.csproj'
    $argsForBuild = @("-p:ProjectZPlatform=$platform", "-p:ProjectZVersion=$version", "-p:RestorePackagesPath=$temp/packages", '-p:UseSharedCompilation=false')
    dotnet restore $project --configfile (Join-Path $temp 'NuGet.Config') @argsForBuild --nologo
    if ($LASTEXITCODE -ne 0) { throw "$platform independent consumer restore failed." }
    dotnet build $project -c Release --no-restore @argsForBuild --nologo -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw "$platform independent consumer build failed." }
    dotnet (Join-Path $consumer 'bin/Release/net8.0/Consumer.dll')
    if ($LASTEXITCODE -ne 0) { throw "$platform independent consumer runtime checks failed." }
    $publish = Join-Path $consumer "publish/$RuntimeIdentifier"
    dotnet publish $project -c Release -r $RuntimeIdentifier --self-contained false -o $publish @argsForBuild "-p:RestoreConfigFile=$temp/NuGet.Config" --nologo -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw "$platform $RuntimeIdentifier publish failed." }
    foreach ($name in @('ProjectZ.dll','ProjectZ.VectorText.dll','MonoGame.Framework.dll')) {
        if (!(Test-Path (Join-Path $publish $name))) { throw "$platform publish omitted $name" }
    }
    $sdl = if ($RuntimeIdentifier.StartsWith('osx')) { 'libSDL2-2.0.0.dylib' } elseif ($RuntimeIdentifier.StartsWith('linux')) { 'libSDL2-2.0.so.0' } else { 'SDL2.dll' }
    if (!(Test-Path (Join-Path $publish $sdl))) { throw "$RuntimeIdentifier SDL runtime missing." }
    if (Get-ChildItem $publish -Recurse -File | Where-Object Name -Match '^(PresentationFramework|PresentationCore|WindowsBase|System.Windows.Forms|SocketJack|WebView2Loader|mgruntime)\.') { throw 'Windows-only dependencies leaked into publish output.' }
    if ($RunGraphics) {
        dotnet (Join-Path $publish 'Consumer.dll') --graphics
        if ($LASTEXITCODE -ne 0) { throw "$platform native graphics smoke failed." }
    }
}
Write-Host "DesktopGL package consumers passed ($RuntimeIdentifier). Verification files: $temp"
