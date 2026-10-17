param([Parameter(Mandatory = $true)][string]$PackagePath)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packageFile = (Resolve-Path -LiteralPath $PackagePath).Path
$archive = [IO.Compression.ZipFile]::OpenRead($packageFile)
try {
    $manifest = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
    if ($manifest.Count -ne 1) { throw 'Expected one package manifest.' }
    $reader = [IO.StreamReader]::new($manifest[0].Open())
    try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if (@($spec.GetElementsByTagName('dependency') | Where-Object id -EQ 'ProjectZ.ModelImport').Count) {
        throw 'The internal model-import bridge must not require an unpublished NuGet package.'
    }
    if (!@($archive.Entries | Where-Object FullName -Match '^lib/[^/]+/ProjectZ\.ModelImport\.dll$').Count) {
        throw 'The package must include ProjectZ.ModelImport.dll.'
    }
    $version = [string]$spec.package.metadata.version
} finally {
    $archive.Dispose()
}

# Build outside the checkout so repository targets and source references cannot mask a broken package.
$consumerRoot = Join-Path ([IO.Path]::GetTempPath()) ('ProjectZ.PackageConsumer.' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $consumerRoot | Out-Null
$feed = [Security.SecurityElement]::Escape((Split-Path -Parent $packageFile))
@"
<configuration><packageSources><clear/><add key="package-under-test" value="$feed"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>
"@ | Set-Content -LiteralPath (Join-Path $consumerRoot 'NuGet.Config') -Encoding utf8
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0-windows7.0</TargetFramework><UseWPF>true</UseWPF><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
  <ItemGroup><PackageReference Include="ProjectZ" Version="$version" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumerRoot 'Consumer.csproj') -Encoding utf8
'public static class PackageSmoke { public static System.Type FrameworkType => typeof(global::ProjectZ.Shared.Drawing.SceneManager); }' |
    Set-Content -LiteralPath (Join-Path $consumerRoot 'PackageSmoke.cs') -Encoding utf8

dotnet restore (Join-Path $consumerRoot 'Consumer.csproj') --configfile (Join-Path $consumerRoot 'NuGet.Config')
if ($LASTEXITCODE -ne 0) { throw "Standalone package restore failed. Consumer: $consumerRoot" }
dotnet build (Join-Path $consumerRoot 'Consumer.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw "Standalone package build failed. Consumer: $consumerRoot" }
Write-Host "PACKAGE_OK ProjectZ=$version; consumer=$consumerRoot"
