[CmdletBinding()]
param([string]$BuildDirectory, [string]$VerificationDirectory, [switch]$SkipRuntime,
      [ValidateSet('CSharp', 'VisualBasic')][string[]]$Languages = @('CSharp', 'VisualBasic'))
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$BuildDirectory) { $BuildDirectory = Join-Path $repo 'artifacts/VS2026' }
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$verification = if ($VerificationDirectory) { [IO.Path]::GetFullPath($VerificationDirectory) } else { Join-Path $BuildDirectory ('XamlVerification-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
if (Test-Path -LiteralPath $verification) { throw 'Use a fresh verification directory.' }
New-Item -ItemType Directory -Path $verification | Out-Null
Set-Content -LiteralPath "$verification/Directory.Build.targets" -Value '<Project />'
# Keep verification on the documented stable SDK even when a newer preview is
# installed. Run the tool from this directory so MSBuildLocator uses it too.
$sdkVersion = & dotnet --list-sdks | Where-Object { $_ -match '^10\.\d+\.\d+ \[' } |
    ForEach-Object { $_.Split(' ')[0] } | Sort-Object { [version]$_ } -Descending | Select-Object -First 1
if (!$sdkVersion) { throw 'Install the .NET 10 SDK before verifying the templates.' }
@{ sdk = @{ version = $sdkVersion; rollForward = 'disable'; allowPrerelease = $false } } |
    ConvertTo-Json | Set-Content -LiteralPath "$verification/global.json"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$results = [Collections.Generic.List[string]]::new()

function Run-Dotnet([string[]]$Arguments, [string]$Log, [bool]$Failure = $false) {
    & dotnet @Arguments *> $Log
    if (($LASTEXITCODE -eq 0) -eq $Failure) {
        Get-Content -LiteralPath $Log -Tail 40
        throw "Unexpected build/tool result: $Arguments"
    }
}

function Expand-Project([string]$Folder, [string]$Name, [string]$Parent) {
    [xml]$manifest = Get-Content -LiteralPath "$Folder/ProjectZ.vstemplate" -Raw
    $project = $manifest.VSTemplate.TemplateContent.Project
    foreach ($item in $project.ProjectItem) {
        $path = Join-Path $Folder $item.InnerText
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing template item: $path" }
        if ($item.ReplaceParameters -eq 'true') {
            $text = [IO.File]::ReadAllText($path).Replace('$ext_safeprojectname$', $Parent).Replace('$safeprojectname$', $Name).Replace('$projectname$', $Name)
            [IO.File]::WriteAllText($path, $text)
        }
    }
    $path = Join-Path $Folder $project.File
    $text = [IO.File]::ReadAllText($path).Replace('$ext_safeprojectname$', $Parent).Replace('$safeprojectname$', $Name).Replace('$projectname$', $Name)
    [IO.File]::WriteAllText($path, $text)
    $destination = Join-Path $Folder $project.TargetFileName.Replace('$safeprojectname$', $Name)
    Move-Item -LiteralPath $path -Destination $destination
    return $destination
}

Push-Location $verification
try {
foreach ($language in $Languages) {
    $name = "Xaml$language"
    $suffix = if ($language -eq 'CSharp') { 'cs' } else { 'vb' }
    $folder = Join-Path $verification $language
    $raw = Join-Path $folder 'Template'
    [IO.Compression.ZipFile]::ExtractToDirectory("$BuildDirectory/Templates/ProjectZ.Xaml.$language.zip", $raw)
    [xml]$group = Get-Content -LiteralPath "$raw/ProjectZ.Xaml.vstemplate" -Raw
    if ($group.VSTemplate.Type -ne 'ProjectGroup' -or @($group.VSTemplate.TemplateContent.ProjectCollection.ProjectTemplateLink).Count -ne 2) { throw 'Expected an App and Designer solution template.' }
    foreach ($part in @('App', 'Designer')) {
        Copy-Item -LiteralPath "$raw/$part" -Destination "$folder/$name.$part" -Recurse
    }
    $app = Expand-Project "$folder/$name.App" "$name.App" $name
    $designer = Expand-Project "$folder/$name.Designer" "$name.Designer" $name
    $markup = Join-Path "$folder/$name.Designer" 'MainWindow.xaml'
    $edited = [IO.File]::ReadAllText($markup).Replace('Welcome to Project-Z XAML', 'Edited XAML reaches the native app')
    [IO.File]::WriteAllText($markup, $edited)
    $game = Join-Path "$folder/$name.App" "GameWindow.$suffix"
    if ($language -eq 'CSharp') {
        $probe = @'
        var report = Environment.GetEnvironmentVariable("PROJECTZ_XAML_VERIFICATION");
        if (report != null && gameTime.TotalGameTime.TotalSeconds > 1)
        {
            var view = ((MainScene)scenes!.ActiveScene).View;
            var welcome = (ProjectZ.WpfCompatibility.Controls.TextBlock)view.FindName("Welcome")!;
            if (welcome.Text != "Edited XAML reaches the native app") throw new InvalidOperationException("The edited designer markup was not compiled.");
            var button = (ProjectZ.WpfCompatibility.Controls.Button)view.FindName("RunButton")!;
            button.NativeElement.ValidationCheck();
            var p = button.NativeElement.Position;
            scenes.UseExternalInput = true;
            scenes.InjectMouseLeftDown((int)p.X + 10, (int)p.Y + 10, 960, 600);
            scenes.InjectMouseLeftUp((int)p.X + 10, (int)p.Y + 10, 960, 600);
            if (view.ClickCount != 1 || ((ProjectZ.WpfCompatibility.Controls.TextBlock)view.FindName("Status")!).Text != "Button clicked 1")
                throw new InvalidOperationException("The translated shared event handler did not run.");
            using var capture = new RenderTarget2D(GraphicsDevice, 960, 600);
            GraphicsDevice.SetRenderTarget(capture);
            scenes.Draw(gameTime);
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Microsoft.Xna.Framework.Color[960 * 600];
            capture.GetData(pixels);
            if (System.Linq.Enumerable.Count(pixels, pixel => pixel.R > 200 && pixel.G > 200 && pixel.B > 200) < 300)
                throw new InvalidOperationException("Native XAML text did not render.");
            using (var stream = System.IO.File.Create(report + ".png")) capture.SaveAsPng(stream, 960, 600);
            if (ProjectZ.Shared.Drawing.UI.SceneElement.RenderExceptionCount != 0) throw new InvalidOperationException("A native control failed to render.");
            if (view.GetType().BaseType != typeof(ProjectZ.WpfCompatibility.Window)) throw new InvalidOperationException("The view was not converted to a native Project-Z window.");
            System.IO.File.WriteAllText(report, GraphicsBackendInfo.Current.ToString());
            Exit();
        }
'@
        $text = [IO.File]::ReadAllText($game).Replace('        base.Draw(gameTime);', $probe + [Environment]::NewLine + '        base.Draw(gameTime);')
    } else {
        $probe = @'
        Dim report = System.Environment.GetEnvironmentVariable("PROJECTZ_XAML_VERIFICATION")
        If report IsNot Nothing AndAlso gameTime.TotalGameTime.TotalSeconds > 1 Then
            Dim view = DirectCast(scenes.ActiveScene, MainScene).View
            Dim welcome = DirectCast(view.FindName("Welcome"), ProjectZ.WpfCompatibility.Controls.TextBlock)
            If welcome.Text <> "Edited XAML reaches the native app" Then Throw New System.InvalidOperationException("The edited designer markup was not compiled.")
            Dim button = DirectCast(view.FindName("RunButton"), ProjectZ.WpfCompatibility.Controls.Button)
            button.NativeElement.ValidationCheck()
            Dim p = button.NativeElement.Position
            scenes.UseExternalInput = True
            scenes.InjectMouseLeftDown(CInt(p.X) + 10, CInt(p.Y) + 10, 960, 600)
            scenes.InjectMouseLeftUp(CInt(p.X) + 10, CInt(p.Y) + 10, 960, 600)
            If view.ClickCount <> 1 OrElse DirectCast(view.FindName("Status"), ProjectZ.WpfCompatibility.Controls.TextBlock).Text <> "Button clicked 1" Then Throw New System.InvalidOperationException("The translated shared event handler did not run.")
            Using capture As New RenderTarget2D(GraphicsDevice, 960, 600)
                GraphicsDevice.SetRenderTarget(capture)
                scenes.Draw(gameTime)
                GraphicsDevice.SetRenderTarget(Nothing)
                Dim pixels(960 * 600 - 1) As Microsoft.Xna.Framework.Color
                capture.GetData(pixels)
                If System.Linq.Enumerable.Count(pixels, Function(pixel) pixel.R > 200 AndAlso pixel.G > 200 AndAlso pixel.B > 200) < 300 Then Throw New System.InvalidOperationException("Native XAML text did not render.")
                Using stream = System.IO.File.Create(report & ".png")
                    capture.SaveAsPng(stream, 960, 600)
                End Using
            End Using
            If ProjectZ.Shared.Drawing.UI.SceneElement.RenderExceptionCount <> 0 Then Throw New System.InvalidOperationException("A native control failed to render.")
            If view.GetType().BaseType IsNot GetType(ProjectZ.WpfCompatibility.Window) Then Throw New System.InvalidOperationException("The view was not converted to a native Project-Z window.")
            System.IO.File.WriteAllText(report, GraphicsBackendInfo.Current.ToString())
            [Exit]()
        End If
'@
        $text = [IO.File]::ReadAllText($game).Replace('        MyBase.Draw(gameTime)', $probe + [Environment]::NewLine + '        MyBase.Draw(gameTime)')
    }
    [IO.File]::WriteAllText($game, $text)
    Run-Dotnet @('build', $designer, '--source', 'https://api.nuget.org/v3/index.json', '--nologo') "$folder/designer.build.txt"
    foreach ($case in @(@('Debug', 'DirectX12'), @('Debug-DX11', 'DirectX11'), @('Debug-OpenGL', 'OpenGL'))) {
        $configuration, $backend = $case
        Run-Dotnet @('build', $app, '-c', $configuration, '--source', 'https://api.nuget.org/v3/index.json', '--nologo') "$folder/$backend.build.txt"
        $output = Join-Path "$folder/$name.App" "bin/$configuration/net10.0-windows"
        if ((Get-Content -LiteralPath "$output/$name.App.deps.json" -Raw).Contains("$name.Designer")) { throw 'The native app acquired a WPF designer assembly dependency.' }
        if (!$SkipRuntime) {
            $report = Join-Path $folder "$backend.runtime.txt"
            $start = [Diagnostics.ProcessStartInfo]::new("$output/$name.App.exe")
            $start.WorkingDirectory = $output
            $start.UseShellExecute = $false
            $start.RedirectStandardError = $true
            $start.Environment['PROJECTZ_XAML_VERIFICATION'] = $report
            $process = [Diagnostics.Process]::Start($start)
            $errors = $process.StandardError.ReadToEndAsync()
            if (!$process.WaitForExit(45000)) { $process.Kill(); throw "$language $backend runtime timed out." }
            if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $report)) {
                $stderr = if ($errors.Wait(3000)) { $errors.Result } else { 'Diagnostics observer retained stderr; inspect the session log.' }
                throw "$language $backend runtime failed: $stderr"
            }
            if ((Get-Content -LiteralPath $report -Raw).Trim() -ne $backend) { throw 'The XAML app used the wrong renderer.' }
        }
        $results.Add("PASS $language $backend XAML designer and standalone build" + $(if (!$SkipRuntime) { ', edited markup, shared click handler and native Project-Z pixels' }))
        Write-Host $results[$results.Count - 1]
    }
    $generated = Get-ChildItem -LiteralPath "$folder/$name.App/obj/Debug/net10.0-windows/ProjectZXaml" -Recurse -Filter "*.design.$suffix" | Select-Object -First 1
    if (!$generated) { throw 'Build-time generation did not produce a designer.' }
    # Restore the DX12 dependency graph after testing the alternate renderers.
    Run-Dotnet @('build', $app, '-c', 'Debug', '--nologo') "$folder/default.build.txt"
    $stamp = (Get-Item -LiteralPath $generated.FullName).LastWriteTimeUtc
    Run-Dotnet @('build', $app, '-c', 'Debug', '--nologo') "$folder/unchanged.build.txt"
    if ((Get-Item -LiteralPath $generated.FullName).LastWriteTimeUtc -ne $stamp) { throw 'An unchanged build rewrote its generated designer.' }
    [IO.File]::WriteAllText($markup, $edited.Replace('Edited XAML reaches the native app', 'A second saved XAML edit'))
    Run-Dotnet @('build', $app, '-c', 'Debug', '--nologo') "$folder/edited.build.txt"
    $runtimeXaml = Get-ChildItem -LiteralPath "$folder/$name.App/bin/Debug/net10.0-windows" -Recurse -Filter MainWindow.xaml | Select-Object -First 1
    if (!$runtimeXaml -or !([IO.File]::ReadAllText($runtimeXaml.FullName).Contains('A second saved XAML edit'))) { throw 'A subsequent XAML edit was not included by an ordinary build.' }
    [IO.File]::WriteAllText($markup, $edited.Replace('Text="Ready"', 'UnknownProjectZProperty="Unsupported" Text="Ready"'))
    Run-Dotnet @('build', $app, '-c', 'Debug', '--nologo') "$folder/invalid.build.txt" $true
    [IO.File]::WriteAllText($markup, $edited)
    $results.Add("PASS $language rebuilds saved XAML and rejects unsupported markup")

    # Whole-project conversion must work outside the framework source tree and
    # carry its tools into a new output tree that survives relocation.
    $original = Join-Path $folder 'Original'
    New-Item -ItemType Directory -Path $original | Out-Null
    foreach ($file in @($designer, $markup, "$markup.$suffix")) {
        Copy-Item -LiteralPath $file -Destination $original
    }
    $originalProject = Join-Path $original ([IO.Path]::GetFileName($designer))
    [xml]$xml = Get-Content -LiteralPath $originalProject -Raw
    $node = $xml.CreateElement('OutputType'); $node.InnerText = 'WinExe'
    [void]$xml.Project.PropertyGroup.AppendChild($node)
    $xml.Save($originalProject)
    $application = '<Application xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Class="' + $name + '.App" StartupUri="MainWindow.xaml" />'
    $applicationFile = if ($language -eq 'VisualBasic') { 'Application.xaml' } else { 'App.xaml' }
    Set-Content -LiteralPath "$original/$applicationFile" -Value $application
    Run-Dotnet @('build', $originalProject, '--nologo') "$folder/original.build.txt"
    $before = @(Get-ChildItem -LiteralPath $original -File | Get-FileHash | Select-Object Path, Hash) | ConvertTo-Json -Compress
    $converted = Join-Path $folder 'Converted'
    $converter = Join-Path "$folder/$name.App/ProjectZ" 'Convert-Project.ps1'
    & $converter -SourceProject $originalProject -OutputDirectory $converted -RootDirectory App *> "$folder/converter.txt"
    if (!$?) { Get-Content -LiteralPath "$folder/converter.txt" -Tail 40; throw 'Portable project conversion failed.' }
    $after = @(Get-ChildItem -LiteralPath $original -File | Get-FileHash | Select-Object Path, Hash) | ConvertTo-Json -Compress
    if ($before -ne $after) { throw 'The converter changed its source.' }
    $report = Get-Content -LiteralPath "$converted/projectz-import.report.json" -Raw | ConvertFrom-Json
    if (!$report.Success -or @($report.Issues).Count) { throw 'The converter reported unsupported output.' }
    $relocated = Join-Path $folder 'Relocated'
    Copy-Item -LiteralPath $converted -Destination $relocated -Recurse
    $relocatedProject = Join-Path $relocated ([IO.Path]::GetRelativePath($converted, $report.RootProject))
    Run-Dotnet @('build', $relocatedProject, '-c', 'Release', '--nologo') "$folder/relocated.build.txt"
    $protected = Get-ChildItem -LiteralPath "$converted/App/Source" -Recurse -Filter "*.$suffix" | Select-Object -First 1
    if (!$protected) { throw 'The converter did not preserve source in its generated tree.' }
    [IO.File]::AppendAllText($protected.FullName, [Environment]::NewLine + $(if ($suffix -eq 'cs') { '// User edit' } else { "' User edit" }))
    $protectedHash = (Get-FileHash -LiteralPath $protected.FullName).Hash
    Run-Dotnet @("$folder/$name.App/ProjectZ/Tools/ProjectZ.XamlCodegen.dll", 'import', $originalProject, '--output', $converted, '--root-directory', 'App') "$folder/protected-conversion.txt" $true
    if ((Get-FileHash -LiteralPath $protected.FullName).Hash -ne $protectedHash) { throw 'The converter overwrote an edited generated source.' }
    $results.Add("PASS $language portable whole-project conversion, immutable source, protected edits and relocated output build")
    Write-Host $results[$results.Count - 1]
}
$results | Set-Content -LiteralPath "$verification/results.txt"
$global:LASTEXITCODE = 0
} finally { Pop-Location }
