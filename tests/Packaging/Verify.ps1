param([Parameter(Mandatory = $true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packageFile = (Resolve-Path -LiteralPath $PackagePath).Path
$feedDirectory = Split-Path -Parent $packageFile
$archive = [IO.Compression.ZipFile]::OpenRead($packageFile)
try {
    $manifest = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
    if ($manifest.Count -ne 1) { throw 'Expected one package manifest.' }
    $reader = [IO.StreamReader]::new($manifest[0].Open())
    try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $forbidden = @('ProjectZ.ModelImport','SocketJack','Silk.NET.Assimp','Microsoft.Web.WebView2')
    foreach ($dependency in $spec.GetElementsByTagName('dependency')) {
        if ($dependency.id -in $forbidden) { throw "Base package has an optional dependency: $($dependency.id)" }
    }
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName -match 'ProjectZ\.ModelImport\.dll$|ProjectZ\.Diagnostics\.(exe|dll)$|tools/xaml-codegen/.+\.(dll|exe)$') {
            throw "Base package contains optional payload: $($entry.FullName)"
        }
    }
    if (!@($archive.Entries | Where-Object FullName -Match '^lib/[^/]+/ProjectZ\.VectorText\.dll$').Count) { throw 'Base vector text is missing.' }
    $version = [string]$spec.package.metadata.version
} finally { $archive.Dispose() }
[xml]$templateProperties = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../ProjectTemplate/VS2026/Common/ProjectZ.Graphics.props') -Raw
if ([string]$templateProperties.Project.PropertyGroup.ProjectZFrameworkVersion -ne $version) { throw 'New application templates must use the current smaller base runtime.' }
foreach ($templateDependency in @($templateProperties.Project.ItemGroup.PackageReference)) {
    if ($templateDependency.Include -in @('SocketJack','Silk.NET.Assimp','Microsoft.Web.WebView2','ProjectZ.Diagnostics','ProjectZ.ModelImport')) { throw 'New application templates must not restore optional runtime features by default.' }
}
foreach ($id in @('ProjectZ.ModelImport','ProjectZ.WebView2','ProjectZ.Diagnostics','ProjectZ.XamlTools')) {
    $optionalPath = Join-Path $feedDirectory "$id.$version.nupkg"
    if (!(Test-Path -LiteralPath $optionalPath)) { throw "Missing co-package: $id $version" }
    if ($id -ne 'ProjectZ.XamlTools') {
        $optionalArchive = [IO.Compression.ZipFile]::OpenRead($optionalPath)
        try {
            $optionalSpecEntry = @($optionalArchive.Entries | Where-Object FullName -Like '*.nuspec')[0]
            $optionalReader = [IO.StreamReader]::new($optionalSpecEntry.Open())
            try { [xml]$optionalSpec = $optionalReader.ReadToEnd() } finally { $optionalReader.Dispose() }
            if (!@($optionalSpec.GetElementsByTagName('dependency') | Where-Object id -EQ 'ProjectZ').Count) { throw "$id must restore its base ProjectZ runtime dependency." }
        } finally { $optionalArchive.Dispose() }
    }
}

# External consumers cannot inherit repository targets, source references or cached packages under test.
$consumerRoot = Join-Path ([IO.Path]::GetTempPath()) ('ProjectZ.PackageConsumer.' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $consumerRoot | Out-Null
$feed = [Security.SecurityElement]::Escape($feedDirectory)
$cache = [Security.SecurityElement]::Escape((Join-Path $consumerRoot 'packages'))
@"
<configuration><packageSources><clear/><add key="package-under-test" value="$feed"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><config><add key="globalPackagesFolder" value="$cache"/></config></configuration>
"@ | Set-Content -LiteralPath (Join-Path $consumerRoot 'NuGet.Config') -Encoding utf8
function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Standalone package check failed: $($Arguments[0]); consumer=$consumerRoot" }
}
function New-Consumer([string]$Name, [string]$References) {
    $directory = Join-Path $consumerRoot $Name
    New-Item -ItemType Directory -Path $directory | Out-Null
    @"
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0-windows7.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><UseWindowsForms>true</UseWindowsForms><PlatformTarget>x64</PlatformTarget><NuGetAudit>true</NuGetAudit><NuGetAuditMode>all</NuGetAuditMode></PropertyGroup><ItemGroup><PackageReference Include="ProjectZ" Version="[$version]" />$References</ItemGroup></Project>
"@ | Set-Content -LiteralPath (Join-Path $directory 'Consumer.csproj') -Encoding utf8
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RuntimeSmoke.cs') -Destination (Join-Path $directory 'Program.cs')
    return $directory
}
$base = New-Consumer 'Base' ''
Invoke-DotNet @('publish',(Join-Path $base 'Consumer.csproj'),'-c','Release','-r','win-x64','--self-contained','false','-o',(Join-Path $base 'publish'),'--nologo')
$assets = Get-Content -LiteralPath (Join-Path $base 'obj/project.assets.json') -Raw | ConvertFrom-Json
foreach ($name in $assets.libraries.PSObject.Properties.Name) {
    if ($name -match '^(SocketJack|Silk\.NET(?:\.[^/]+)?|Ultz\.Native\.Assimp|Microsoft\.Web\.WebView2|Assimp|ProjectZ\.(ModelImport|Diagnostics|XamlTools|WebView2))/') { throw "Base restore included optional package: $name" }
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $base 'publish') -Recurse -File) {
    if ($file.Name -match '(?i)assimp|webview|socketjack|ProjectZ\.Diagnostics|CodeAnalysis|XamlCodegen') { throw "Base publish included optional file: $($file.Name)" }
}
$native = @(Get-ChildItem -LiteralPath (Join-Path $base 'publish') -Recurse -Filter 'mgruntime.dll')
if ($native.Count -ne 1 -or $native[0].DirectoryName -ne (Join-Path $base 'publish')) { throw 'Publish must contain exactly one native graphics runtime at the application root.' }
$expectedNative = Join-Path $consumerRoot "packages/projectz/$version/tools/native/win-x64/mgruntime.dll"
if ((Get-FileHash -LiteralPath $native[0].FullName).Hash -ne (Get-FileHash -LiteralPath $expectedNative).Hash) { throw 'Publish selected the upstream graphics runtime instead of the Project-Z build.' }
Invoke-DotNet @((Join-Path $base 'publish/Consumer.dll'),(Join-Path $base 'logs'))

$model = New-Consumer 'Model' "<PackageReference Include=`"ProjectZ.ModelImport`" Version=`"[$version]`" />"
Invoke-DotNet @('publish',(Join-Path $model 'Consumer.csproj'),'-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-p:EnableCompressionInSingleFile=true','-o',(Join-Path $model 'publish'),'--nologo')
& (Join-Path $model 'publish/Consumer.exe') (Join-Path $model 'logs') '--model'
if ($LASTEXITCODE -ne 0) { throw 'Single-file model importer consumer failed.' }

$browser = New-Consumer 'Browser' "<PackageReference Include=`"ProjectZ.WebView2`" Version=`"[$version]`" />"
'public static class BrowserApi { public static System.Type Host => typeof(ProjectZ.Shared.Html5.Html5EngineHost); }' | Set-Content -LiteralPath (Join-Path $browser 'BrowserApi.cs')
Invoke-DotNet @('build',(Join-Path $browser 'Consumer.csproj'),'-c','Release','--nologo')

$diagnostics = New-Consumer 'Diagnostics' "<PackageReference Include=`"ProjectZ.Diagnostics`" Version=`"[$version]`" />"
Invoke-DotNet @('publish',(Join-Path $diagnostics 'Consumer.csproj'),'-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeAllContentForSelfExtract=true','-p:EnableCompressionInSingleFile=true','-o',(Join-Path $diagnostics 'publish'),'--nologo')
$observer = Join-Path $diagnostics 'publish/ProjectZ.Diagnostics.exe'
if (!(Test-Path -LiteralPath $observer)) { throw 'Optional observer was not deployed beside the bundled host.' }
& (Join-Path $diagnostics 'publish/Consumer.exe') (Join-Path $diagnostics 'logs')
if ($LASTEXITCODE -ne 0) { throw 'Self-contained diagnostics consumer failed.' }
$deadline = [DateTime]::UtcNow.AddSeconds(10)
do {
    $observed = @(Get-ChildItem -LiteralPath (Join-Path $diagnostics 'logs') -Filter '*.observer.log' -ErrorAction SilentlyContinue)
    if ($observed.Count -and (Get-Content -LiteralPath $observed[0].FullName -Raw) -match 'Native observer attached') { break }
    Start-Sleep -Milliseconds 100
} while ([DateTime]::UtcNow -lt $deadline)
if (!$observed.Count -or (Get-Content -LiteralPath $observed[0].FullName -Raw) -notmatch 'Native observer attached') { throw 'Optional observer did not attach to the actual single-file host.' }

$xaml = New-Consumer 'Xaml' "<PackageReference Include=`"ProjectZ.XamlTools`" Version=`"[$version]`" PrivateAssets=`"all`" />"
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../NativeSceneXaml/Program.cs') -Destination (Join-Path $xaml 'Program.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../NativeSceneXaml/View.xaml') -Destination (Join-Path $xaml 'View.xaml')
$taskProjectPath = Join-Path $xaml 'Consumer.csproj'
$taskProjectText = [IO.File]::ReadAllText($taskProjectPath).Replace('</Project>','<ItemGroup><ProjectZSceneXaml Include="View.xaml" /></ItemGroup></Project>')
[IO.File]::WriteAllText($taskProjectPath,$taskProjectText)
Invoke-DotNet @('run','--project',$taskProjectPath,'-c','Release','--','Native XAML')
$baseBytes = (Get-ChildItem -LiteralPath (Join-Path $base 'publish') -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host "PACKAGE_OK ProjectZ=$version; basePublishBytes=$baseBytes; consumer=$consumerRoot"
