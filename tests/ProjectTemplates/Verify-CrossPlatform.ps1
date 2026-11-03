[CmdletBinding()]
param(
    [string]$BuildDirectory,
    [string]$WorkDirectory,
    [string]$PackageDirectory,
    [string[]]$RuntimeIdentifiers = @('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64'),
    [switch]$SkipRuntime,
    [switch]$Resume
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$BuildDirectory) { $BuildDirectory = Join-Path $repo 'artifacts/CrossPlatformTemplates' }
if (!$WorkDirectory) { $WorkDirectory = Join-Path $BuildDirectory 'Verification' }
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$WorkDirectory = [IO.Path]::GetFullPath($WorkDirectory)
if ((Test-Path -LiteralPath $WorkDirectory) -and !$Resume) { throw "Use a fresh template verification directory, or -Resume to reuse this run's dependency cache." }
New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
[xml]$props = Get-Content -LiteralPath "$repo/ProjectTemplate/CrossPlatform/Common/ProjectZ.Platform.props" -Raw
$version = [string]$props.Project.PropertyGroup.ProjectZFrameworkVersion
$pack = "$BuildDirectory/ProjectZ.Templates.$version.nupkg"
$results = [Collections.Generic.List[string]]::new()
$sdk = & dotnet --list-sdks | Where-Object { $_ -match '^8\.\d+\.\d+ \[' } | ForEach-Object { $_.Split(' ')[0] } | Sort-Object { [version]$_ } -Descending | Select-Object -First 1
if (!$sdk) { throw 'Install the .NET 8 SDK to verify the minimum supported SDK.' }
@{ sdk = @{ version = $sdk; rollForward = 'disable'; allowPrerelease = $false } } | ConvertTo-Json | Set-Content -LiteralPath "$WorkDirectory/global.json"
Set-Content -LiteralPath "$WorkDirectory/Directory.Build.props" -Value '<Project />'
Set-Content -LiteralPath "$WorkDirectory/Directory.Build.targets" -Value '<Project />'
$localSource = ''
$mapping = ''
if ($PackageDirectory) {
    $PackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
    foreach ($id in @('ProjectZ', 'ProjectZ.Linux', 'ProjectZ.macOS', 'ProjectZ.XamlTools')) {
        if (!(Test-Path -LiteralPath "$PackageDirectory/$id.$version.nupkg")) { throw "Missing exact release package $id $version." }
    }
    $escaped = [Security.SecurityElement]::Escape($PackageDirectory)
    $localSource = "<add key=`"release`" value=`"$escaped`" />"
    $mapping = '<packageSource key="release"><package pattern="ProjectZ" /><package pattern="ProjectZ.Linux" /><package pattern="ProjectZ.macOS" /><package pattern="ProjectZ.XamlTools" /></packageSource>'
}
@"
<configuration>
  <packageSources><clear />$localSource<add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping><clear />$mapping<packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
  <fallbackPackageFolders><clear /></fallbackPackageFolders>
  <config><add key="globalPackagesFolder" value="packages" /></config>
</configuration>
"@ | Set-Content -LiteralPath "$WorkDirectory/NuGet.Config"

function Run-Dotnet([string[]]$Arguments, [string]$Log, [bool]$ExpectFailure = $false) {
    & dotnet @Arguments *> "$WorkDirectory/$Log.txt"
    if (($LASTEXITCODE -eq 0) -eq $ExpectFailure) {
        Get-Content -LiteralPath "$WorkDirectory/$Log.txt" -Tail 30
        throw "Unexpected dotnet result: $Arguments"
    }
}

function Assert-Output([string]$Folder, [string]$Platform) {
    foreach ($name in @('ProjectZ.dll', 'ProjectZ.VectorText.dll', 'ThirdParty/ProjectZSans/OFL.txt')) {
        if (!(Test-Path -LiteralPath "$Folder/$name")) { throw "Missing $Platform published asset: $name" }
    }
    $native = switch ($Platform) { 'win-x64' { 'ProjectZDesktopCapture.dll' } 'linux-x64' { 'libSDL2-2.0.so.0' } default { 'libProjectZDesktopCapture.dylib' } }
    if (!(Get-ChildItem -LiteralPath $Folder -Recurse -File | Where-Object Name -EQ $native)) { throw "Missing $Platform native asset: $native" }
    if ($Platform -like 'osx-*' -and !(Get-ChildItem -LiteralPath $Folder -Recurse -File | Where-Object Name -Like '*SDL2*.dylib')) { throw 'Missing macOS SDL2.' }
    if (@(Get-ChildItem -LiteralPath $Folder -File -Recurse | Where-Object Name -Match 'ProjectZ\.Diagnostics|Assimp|WebView2|ProjectZ\.XamlCodegen|ProjectZ\.WpfCompatibility').Count) { throw 'Optional/tooling payload leaked into the published app.' }
}

Push-Location $WorkDirectory
try {
    $digest = (Get-FileHash -LiteralPath $pack -Algorithm SHA256).Hash.ToLowerInvariant()
    $hive = "$WorkDirectory/template-hive-$($digest.Substring(0, 12))"
    if (!(Test-Path -LiteralPath "$hive/installed.txt")) {
        Run-Dotnet @('new', 'install', $pack, '--debug:custom-hive', $hive) 'install'
        Set-Content -LiteralPath "$hive/installed.txt" -Value $digest
    }
    $hostRid = if ($IsWindows) { 'win-x64' } elseif ($IsLinux) { 'linux-x64' } elseif ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'osx-arm64' } else { 'osx-x64' }
    foreach ($variant in @('CSharp', 'VisualBasic', 'SceneXaml')) {
        $language = if ($variant -eq 'VisualBasic') { 'VB' } else { 'C#' }
        $template = if ($variant -eq 'SceneXaml') { 'projectz-scene' } else { 'projectz-desktop' }
        $name = "Starter$variant"
        $folder = "$WorkDirectory/$variant"
        Run-Dotnet @('new', $template, '-lang', $language, '-n', $name, '-o', $folder, '--force', '--debug:custom-hive', $hive) "$variant-create"
        $project = Get-ChildItem -LiteralPath $folder -Filter '*.*proj' | Select-Object -First 1
        if (!$project -or (Get-Content -LiteralPath $project.FullName -Raw) -match 'ProjectZStarter|\$safeprojectname\$') { throw 'Template name replacement failed.' }
        # Add the probe only to acceptance output, never to the shipped starter.
        $gameFile = "$folder/GameWindow.$(if ($language -eq 'VB') { 'vb' } else { 'cs' })"
        $text = [IO.File]::ReadAllText($gameFile)
        if ($language -eq 'VB') {
            $probe = @'
        If gameTime.TotalGameTime.TotalSeconds > 1 Then
            If GetType(ProjectZ.Shared.Drawing.UI.SceneElement).GetProperty("RigidBody") Is Nothing OrElse GetType(ProjectZ.Shared.Drawing.UI.SceneElement).GetProperty("FreeMove") Is Nothing Then Throw New Exception("Runtime physics update missing.")
            If GetType(ProjectZ.Shared.Drawing.UI.Advanced.ParticleEmitterElement).Assembly IsNot GetType(ProjectZ.Shared.Drawing.Scene).Assembly Then Throw New Exception("Native particle runtime missing.")
            Using capture As New RenderTarget2D(GraphicsDevice, 960, 600)
                GraphicsDevice.SetRenderTarget(capture)
                scenes.ActiveScene.Draw(gameTime)
                GraphicsDevice.SetRenderTarget(Nothing)
                Dim pixels(960 * 600 - 1) As Microsoft.Xna.Framework.Color
                capture.GetData(pixels)
                If pixels.Count(Function(pixel) pixel.R > 200 AndAlso pixel.G > 200 AndAlso pixel.B > 200) < 300 Then Throw New Exception("Starter text did not render.")
            End Using
            If ProjectZ.Shared.Drawing.UI.SceneElement.RenderExceptionCount <> 0 Then Throw New Exception("Scene rendering failed.")
            System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "starter-ok.txt"), "PASS")
            [Exit]()
        End If
'@
            $text = "Imports System`nImports System.Linq`n" + $text.Replace('        MyBase.Draw(gameTime)', $probe + "`n        MyBase.Draw(gameTime)")
        } else {
            $probe = @'
        if (gameTime.TotalGameTime.TotalSeconds > 1)
        {
            if (typeof(ProjectZ.Shared.Drawing.UI.SceneElement).GetProperty("RigidBody") == null || typeof(ProjectZ.Shared.Drawing.UI.SceneElement).GetProperty("FreeMove") == null) throw new Exception("Runtime physics update missing.");
            if (typeof(ProjectZ.Shared.Drawing.UI.Advanced.ParticleEmitterElement).Assembly != typeof(ProjectZ.Shared.Drawing.Scene).Assembly) throw new Exception("Native particle runtime missing.");
            using var capture = new RenderTarget2D(GraphicsDevice, 960, 600);
            GraphicsDevice.SetRenderTarget(capture);
            scenes!.ActiveScene.Draw(gameTime);
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Microsoft.Xna.Framework.Color[960 * 600];
            capture.GetData(pixels);
            if (pixels.Count(pixel => pixel.R > 200 && pixel.G > 200 && pixel.B > 200) < 300) throw new Exception("Starter text did not render.");
            if (ProjectZ.Shared.Drawing.UI.SceneElement.RenderExceptionCount != 0) throw new Exception("Scene rendering failed.");
            System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "starter-ok.txt"), "PASS");
            Exit();
        }
'@
            $text = $text.Replace('        base.Draw(gameTime);', $probe + "`n        base.Draw(gameTime);")
        }
        [IO.File]::WriteAllText($gameFile, $text)
        Run-Dotnet @('build', $project.FullName, '-c', 'Release', '--nologo') "$variant-host-build"
        foreach ($rid in $RuntimeIdentifiers) {
            $output = "$folder/publish/$rid"
            Run-Dotnet @('publish', $project.FullName, '-c', 'Release', '-r', $rid, '--self-contained', 'false', '-o', $output, '--nologo') "$variant-$rid"
            Assert-Output $output $rid
            $assets = Get-Content -LiteralPath "$folder/obj/project.assets.json" -Raw | ConvertFrom-Json -AsHashtable
            $runtimeIds = @($assets.libraries.Keys | Where-Object { $_ -match '^ProjectZ(?:\.Linux|\.macOS)?/' })
            $expected = switch -Wildcard ($rid) { 'win-*' { 'ProjectZ' } 'linux-*' { 'ProjectZ.Linux' } default { 'ProjectZ.macOS' } }
            if ($runtimeIds.Count -ne 1 -or $runtimeIds[0] -ne "$expected/$version") { throw "Wrong runtime graph for $rid : $runtimeIds" }
            if ($assets.libraries.Keys | Where-Object { $_ -match '^SocketJack/|^ProjectZ\.(Diagnostics|ModelImport|WebView2)/' }) { throw 'Optional runtime dependencies leaked into the starter.' }
            if ($PackageDirectory) {
                foreach ($id in @($expected) + $(if ($variant -eq 'SceneXaml') { @('ProjectZ.XamlTools') } else { @() })) {
                    $cached = "$WorkDirectory/packages/$($id.ToLowerInvariant())/$version/$($id.ToLowerInvariant()).$version.nupkg"
                    if ((Get-FileHash -LiteralPath $cached).Hash -ne (Get-FileHash -LiteralPath "$PackageDirectory/$id.$version.nupkg").Hash) { throw 'Restored package differs from the release asset.' }
                }
            }
            if (!$SkipRuntime -and $rid -eq $hostRid) {
                $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
                $start.ArgumentList.Add("$output/$name.dll")
                $start.WorkingDirectory = $output
                $start.UseShellExecute = $false
                $start.CreateNoWindow = $true
                $start.WindowStyle = 'Hidden'
                $start.RedirectStandardOutput = $true
                $start.RedirectStandardError = $true
                $process = [Diagnostics.Process]::Start($start)
                $stdout = $process.StandardOutput.ReadToEndAsync()
                $stderr = $process.StandardError.ReadToEndAsync()
                if (!$process.WaitForExit(45000)) { $process.Kill(); throw 'Starter runtime timed out.' }
                [IO.File]::WriteAllText("$WorkDirectory/$variant-runtime.txt", $stdout.Result + $stderr.Result)
                if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath "$output/starter-ok.txt")) { throw "Starter rendering failed: $variant ($hostRid)" }
                $results.Add("PASS $variant native $hostRid window and rendered text")
            }
            $results.Add("PASS $variant $rid publish, native assets and exact single-runtime package graph")
        }
        Run-Dotnet @('build', $project.FullName, '-p:ProjectZPlatform=Invalid') "$variant-invalid" $true
        if (![IO.File]::ReadAllText("$WorkDirectory/$variant-invalid.txt").Contains('ProjectZPlatform must be')) { throw 'Invalid platform failed for an unexpected reason.' }
        Run-Dotnet @('build', $project.FullName, '-r', 'linux-x64', '-p:ProjectZPlatform=macOS') "$variant-conflict" $true
        if (![IO.File]::ReadAllText("$WorkDirectory/$variant-conflict.txt").Contains('ProjectZPlatform conflicts with RuntimeIdentifier')) { throw 'Conflicting target failed for an unexpected reason.' }
        if ($variant -eq 'SceneXaml') {
            $markup = "$folder/MainScene.xaml"
            $original = [IO.File]::ReadAllText($markup)
            [IO.File]::WriteAllText($markup, $original.Replace('Welcome to Project-Z', 'Saved native XAML edit'))
            Run-Dotnet @('build', $project.FullName, '-c', 'Release') 'SceneXaml-edit'
            $generated = @(Get-ChildItem -LiteralPath "$folder/obj" -Recurse -Filter '*.cs' | Where-Object { [IO.File]::ReadAllText($_.FullName).Contains('Saved native XAML edit') })
            if (!$generated.Count) { throw 'A saved native XAML edit did not reach generated code.' }
            $results.Add('PASS scene-XAML starter rebuilds saved markup')
        }
    }
    $results | Set-Content -LiteralPath "$WorkDirectory/results.txt"
    $results | Write-Host
    $global:LASTEXITCODE = 0
} finally { Pop-Location }
