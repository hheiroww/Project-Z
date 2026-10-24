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
    $iconName = [string]$spec.package.metadata.icon
    $icon = if ($iconName) { $archive.GetEntry($iconName.Replace('\', '/')) } else { $null }
    if (!$icon -or $icon.Length -gt 1MB) { throw 'The package must embed its declared icon, at most 1 MB.' }
    $iconStream = $icon.Open()
    try {
        $header = [byte[]]::new(24)
        if ($iconStream.Read($header, 0, 24) -ne 24 -or
            [Convert]::ToHexString($header[0..7]) -ne '89504E470D0A1A0A' -or
            [Convert]::ToHexString($header[16..23]) -ne '0000008000000080') {
            throw 'The package icon must be a 128 by 128 PNG.'
        }
    } finally { $iconStream.Dispose() }
    if (@($spec.GetElementsByTagName('dependency') | Where-Object id -EQ 'ProjectZ.ModelImport').Count) {
        throw 'The internal model-import bridge must not require an unpublished NuGet package.'
    }
    if (!@($archive.Entries | Where-Object FullName -Match '^lib/[^/]+/ProjectZ\.ModelImport\.dll$').Count) {
        throw 'The package must include ProjectZ.ModelImport.dll.'
    }
    if (!@($archive.Entries | Where-Object FullName -Match '^lib/[^/]+/ProjectZ\.VectorText\.dll$').Count) {
        throw 'The package must include its compiled vector outlines.'
    }
    if (@($spec.GetElementsByTagName('dependency') | Where-Object id -EQ 'ProjectZ.VectorText').Count) {
        throw 'The internal vector renderer must not require an unpublished NuGet package.'
    }
    foreach ($required in @('tools/diagnostics/ProjectZ.Diagnostics.exe', 'tools/diagnostics/ProjectZ.Diagnostics.dll',
        'tools/diagnostics/ProjectZ.Diagnostics.deps.json', 'tools/diagnostics/ProjectZ.Diagnostics.runtimeconfig.json',
        'tools/native/win-x64/mgruntime.dll', 'buildTransitive/ProjectZ.targets',
        'tools/xaml-codegen/ProjectZ.XamlCodegen.dll', 'tools/xaml-codegen/ProjectZ.XamlCodegen.deps.json',
        'tools/xaml-codegen/ProjectZ.XamlCodegen.runtimeconfig.json',
        'tools/xaml-codegen/BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll',
        'THIRD-PARTY-NOTICES.md', 'licenses/WPFPixelShaderLibrary/LICENSE',
        'ThirdParty/ProjectZSans/OFL.txt', 'ThirdParty/ProjectZSans/README.md')) {
        if (!@($archive.Entries | Where-Object FullName -EQ $required).Count) { throw "Missing package runtime asset: $required" }
    }
    if (@($archive.Entries | Where-Object FullName -Match '\.(ttf|otf|woff2?|spritefont)$|(^|/)SegoeUI_[^/]*\.xnb$').Count) {
        throw 'Default text must use compiled free vectors without font containers or Segoe UI atlases.'
    }
    $version = [string]$spec.package.metadata.version
    if (@($archive.Entries | Where-Object FullName -Match '(^|/)(artifacts|obj|bin|\.codex)/|\.pdb$').Count) {
        throw 'Generated artifacts, machine state and debug symbols must not enter the package.'
    }
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
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0-windows7.0</TargetFramework><UseWPF>true</UseWPF><UseWindowsForms>true</UseWindowsForms></PropertyGroup>
  <ItemGroup><PackageReference Include="ProjectZ" Version="[$version]" /><ProjectZSceneXaml Include="View.xaml" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumerRoot 'Consumer.csproj') -Encoding utf8
'public partial class PackageSmoke { [System.STAThread] public static void Main() { using var font = new global::ProjectZ.Shared.Content.VectorFont(12); if (font.FilePath != null || font.FamilyName != "ProjectZ Sans" || font.Measure("AVATAR").X <= 0 || GeneratedCaption != "package-native-xaml") throw new System.InvalidOperationException("Packaged vector font or native XAML runtime failed."); System.Console.WriteLine("COMPILED_FONT_OK asset-free vector outlines and native XAML."); } public static System.Type FrameworkType => typeof(global::ProjectZ.Shared.Drawing.SceneManager); public static System.Type SpellingType => typeof(global::ProjectZ.Shared.Drawing.UI.Input.OfflineSpellChecker); public static bool SpellingEnabled(global::ProjectZ.Shared.Drawing.UI.Input.Textbox input) => input.IsSpellCheckEnabled; public static string GeneratedCaption => CreateCaption().ToString(); }' |
    Set-Content -LiteralPath (Join-Path $consumerRoot 'PackageSmoke.cs') -Encoding utf8
'<SceneTemplates xmlns="urn:projectz:native-scene" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Class="PackageSmoke"><ObjectFactory x:Name="CreateCaption" Type="System.Text.StringBuilder" Static="true"><Argument Type="string" Value="package-native-xaml" /></ObjectFactory></SceneTemplates>' |
    Set-Content -LiteralPath (Join-Path $consumerRoot 'View.xaml') -Encoding utf8

dotnet restore (Join-Path $consumerRoot 'Consumer.csproj') --configfile (Join-Path $consumerRoot 'NuGet.Config') --packages (Join-Path $consumerRoot 'packages')
if ($LASTEXITCODE -ne 0) { throw "Standalone package restore failed. Consumer: $consumerRoot" }
dotnet build (Join-Path $consumerRoot 'Consumer.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw "Standalone package build failed. Consumer: $consumerRoot" }
$consumerOutput = Join-Path $consumerRoot 'bin/Release/net8.0-windows7.0'
foreach ($asset in @('ProjectZ.VectorText.dll', 'ThirdParty/ProjectZSans/OFL.txt', 'ProjectZ.Native/mgruntime.dll', 'ProjectZ.Diagnostics.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $consumerOutput $asset))) { throw "Consumer is missing deployed asset: $asset" }
}
dotnet (Join-Path $consumerOutput 'Consumer.dll')
if ($LASTEXITCODE -ne 0) { throw 'Packaged vector font runtime check failed.' }
# Exercise only the packaged EXE in an isolated folder, without a host CLR/config beside it.
$observerRoot = Join-Path $consumerRoot 'observer-isolation'
New-Item -ItemType Directory -Path $observerRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $consumerOutput 'ProjectZ.Diagnostics.exe') -Destination $observerRoot
$target = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList '-NoProfile -NonInteractive -Command "Start-Sleep -Seconds 30"' -WindowStyle Hidden -PassThru
$observer = $null
try {
    $session = Join-Path $observerRoot 'probe.log'
    [IO.File]::WriteAllText($session, 'observer isolation')
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $observerRoot 'ProjectZ.Diagnostics.exe'))
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WindowStyle = 'Hidden'
    foreach ($argument in @('--diagnostics-watch', "$($target.Id)", '--diagnostics-session', $session, '--diagnostics-no-dialog')) { $start.ArgumentList.Add($argument) }
    $start.Environment['DOTNET_ROOT_X64'] = Join-Path $observerRoot 'absent-runtime'
    $start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $start.Environment['DOTNET_DISABLE_GUI_ERRORS'] = '1'
    $start.Environment['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = Join-Path $observerRoot 'extract'
    $observer = [Diagnostics.Process]::Start($start)
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (!(Test-Path -LiteralPath ($session + '.ready')) -and !$observer.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 50 }
    if (!(Test-Path -LiteralPath ($session + '.ready'))) { throw 'Standalone observer did not attach without an installed runtime.' }
    Stop-Process -Id $target.Id -Force
    if (!$observer.WaitForExit(5000)) { throw 'Standalone observer did not exit after the target.' }
    $observerText = [IO.File]::ReadAllText([IO.Path]::ChangeExtension($session, '.observer.log'))
    if ($observerText -notmatch 'Native observer attached' -or $observerText -notmatch 'Observed process exit') { throw 'Standalone observer missed process lifetime.' }
    Write-Host 'OBSERVER_ISOLATION_OK packaged executable carries its own CLR.'
} finally {
    if (!$target.HasExited) { Stop-Process -Id $target.Id -Force }
    if ($observer -and !$observer.HasExited) { Stop-Process -Id $observer.Id -Force }
}
Write-Host "PACKAGE_OK ProjectZ=$version; consumer=$consumerRoot"
