[CmdletBinding()]
param([string]$BuildDirectory, [string]$VerificationDirectory, [switch]$SkipRuntime)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$BuildDirectory) { $BuildDirectory = Join-Path $repo 'artifacts/VS2026' }
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$verification = if ($VerificationDirectory) { [IO.Path]::GetFullPath($VerificationDirectory) } else { Join-Path $BuildDirectory ('Verification-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Force -Path $verification | Out-Null
# Stop repository-only imports: these projects must work as standalone consumers.
Set-Content -LiteralPath "$verification/Directory.Build.targets" -Value '<Project />'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$results = [Collections.Generic.List[string]]::new()
foreach ($language in @('CSharp', 'VisualBasic')) {
    $folder = Join-Path $verification $language
    if (Test-Path -LiteralPath $folder) { throw "Use a fresh verification directory: $folder" }
    $zip = Join-Path $BuildDirectory "Templates/ProjectZ.$language.zip"
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $folder)
    [xml]$manifest = Get-Content -LiteralPath "$folder/ProjectZ.vstemplate" -Raw
    if ($manifest.VSTemplate.TemplateData.CreateInPlace -ne 'true') { throw 'SDK templates must create their files in place.' }
    $project = $manifest.VSTemplate.TemplateContent.Project
    foreach ($item in $project.ProjectItem) {
        $path = Join-Path $folder $item.InnerText
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Template file is missing: $path" }
        if ($item.ReplaceParameters -eq 'true') {
            $text = [IO.File]::ReadAllText($path).Replace('$safeprojectname$', "Template$language").Replace('$projectname$', "Template$language")
            [IO.File]::WriteAllText($path, $text)
        }
    }
    $projectPath = Join-Path $folder $project.File
    $text = [IO.File]::ReadAllText($projectPath).Replace('$safeprojectname$', "Template$language").Replace('$projectname$', "Template$language")
    [IO.File]::WriteAllText($projectPath, $text)
    $programFile = Join-Path $folder $(if ($language -eq 'CSharp') { 'Program.cs' } else { 'Program.vb' })
    $program = [IO.File]::ReadAllText($programFile)
    if ($language -eq 'CSharp') {
        $program = $program.Replace('        using var game', '        ProjectZ.RuntimeDiagnostics.ApplicationDiagnostics.Initialize(new ProjectZ.RuntimeDiagnostics.DiagnosticOptions { ShowErrorDialogs = false });' + "`n        using var game")
    } else {
        $program = $program.Replace('        Using game', '        ProjectZ.RuntimeDiagnostics.ApplicationDiagnostics.Initialize(New ProjectZ.RuntimeDiagnostics.DiagnosticOptions With {.ShowErrorDialogs = False})' + "`n        Using game")
    }
    [IO.File]::WriteAllText($programFile, $program)
    if ($language -eq 'CSharp') {
        $gameFile = Join-Path $folder 'GameWindow.cs'
        $probe = @'
        var report = Environment.GetEnvironmentVariable("PROJECTZ_TEMPLATE_VERIFICATION");
        if (report != null && gameTime.TotalGameTime.TotalSeconds > 1)
        {
            using var capture = new RenderTarget2D(GraphicsDevice, 960, 600);
            GraphicsDevice.SetRenderTarget(capture);
            scenes!.ActiveScene.Draw(gameTime);
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Microsoft.Xna.Framework.Color[960 * 600];
            capture.GetData(pixels);
            var distinct = pixels.Select(pixel => pixel.PackedValue).Distinct().Count();
            using (var stream = System.IO.File.Create(report + ".png")) capture.SaveAsPng(stream, capture.Width, capture.Height);
            var lightPixels = pixels.Count(pixel => pixel.R > 200 && pixel.G > 200 && pixel.B > 200);
            if (distinct < 2 || lightPixels < 300) throw new InvalidOperationException("The starter scene did not render its text: colors=" + distinct + ", light pixels=" + lightPixels);
            if (ProjectZ.Shared.Drawing.UI.SceneElement.RenderExceptionCount != 0) throw new InvalidOperationException("A scene control failed to render.");
            System.IO.File.WriteAllText(report, GraphicsBackendInfo.Current + "\n" + distinct);
            Exit();
        }
'@
        $text = [IO.File]::ReadAllText($gameFile).Replace('        base.Draw(gameTime);', $probe + "`n        base.Draw(gameTime);")
    } else {
        $gameFile = Join-Path $folder 'GameWindow.vb'
        $probe = @'
        Dim report = System.Environment.GetEnvironmentVariable("PROJECTZ_TEMPLATE_VERIFICATION")
        If report IsNot Nothing AndAlso gameTime.TotalGameTime.TotalSeconds > 1 Then
            Dim distinct As Integer
            Using capture As New RenderTarget2D(GraphicsDevice, 960, 600)
                GraphicsDevice.SetRenderTarget(capture)
                scenes.ActiveScene.Draw(gameTime)
                GraphicsDevice.SetRenderTarget(Nothing)
                Dim pixels(960 * 600 - 1) As Microsoft.Xna.Framework.Color
                capture.GetData(pixels)
                distinct = pixels.Select(Function(pixel) pixel.PackedValue).Distinct().Count()
                Using stream = System.IO.File.Create(report & ".png")
                    capture.SaveAsPng(stream, capture.Width, capture.Height)
                End Using
                Dim lightPixels = pixels.Count(Function(pixel) pixel.R > 200 AndAlso pixel.G > 200 AndAlso pixel.B > 200)
                If distinct < 2 OrElse lightPixels < 300 Then Throw New System.InvalidOperationException("The starter scene did not render its text: colors=" & distinct & ", light pixels=" & lightPixels)
                If ProjectZ.Shared.Drawing.UI.SceneElement.RenderExceptionCount <> 0 Then Throw New System.InvalidOperationException("A scene control failed to render.")
            End Using
            System.IO.File.WriteAllText(report, GraphicsBackendInfo.Current.ToString() & vbLf & distinct)
            [Exit]()
        End If
'@
        $text = "Imports System.Linq`n" + [IO.File]::ReadAllText($gameFile).Replace('        MyBase.Draw(gameTime)', $probe + "`n        MyBase.Draw(gameTime)")
    }
    [IO.File]::WriteAllText($gameFile, $text)
    foreach ($case in @(@('Debug', 'DirectX12'), @('Debug-DX11', 'DirectX11'), @('Debug-OpenGL', 'OpenGL'))) {
        $configuration, $backend = $case
        $buildLog = Join-Path $verification "$language-$backend.build.txt"
        & dotnet build $projectPath -c $configuration "-p:RestorePackagesPath=$BuildDirectory/PublicPackages" --source https://api.nuget.org/v3/index.json --nologo -v:minimal *> $buildLog
        if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $buildLog -Tail 25; throw "$language $backend build failed." }
        $output = Join-Path $folder "bin/$configuration/net10.0-windows"
        $assets = Get-Content -LiteralPath "$folder/obj/project.assets.json" -Raw
        if ($assets.Contains('ProjectReference')) { throw 'A standalone template acquired a source dependency.' }
        if (!(Test-Path -LiteralPath "$output/Content/Fonts/Segoe UI/SegoeUI_24.xnb")) { throw 'Compiled font payload missing.' }
        if (!(Test-Path -LiteralPath "$output/ProjectZ.Diagnostics.exe")) { throw 'Diagnostics observer missing.' }
        if (!$SkipRuntime) {
            $report = Join-Path $verification "$language-$backend.runtime.txt"
            $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $output "Template$language.exe"))
            $start.WorkingDirectory = $output
            $start.UseShellExecute = $false
            $start.Environment['PROJECTZ_TEMPLATE_VERIFICATION'] = $report
            $start.RedirectStandardOutput = $true
            $start.RedirectStandardError = $true
            $process = [Diagnostics.Process]::Start($start)
            if (!$process.WaitForExit(45000)) { $process.Kill(); throw "$language $backend runtime timed out." }
            $errorTask = $process.StandardError.ReadToEndAsync()
            $stderr = if ($errorTask.Wait(3000)) { $errorTask.Result } else { 'The diagnostics observer retained the stderr pipe; inspect its session log.' }
            [IO.File]::WriteAllText("$report.stderr.txt", $stderr)
            if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $report)) { throw "$language $backend runtime failed: $stderr" }
            $observed = (Get-Content -LiteralPath $report)[0]
            if ($observed -ne $backend) { throw "Selected $backend but runtime reports $observed." }
        }
        $results.Add("PASS $language $backend standalone restore, build, fonts, diagnostics" + $(if (!$SkipRuntime) { ', native window and rendered pixels' }))
        Write-Host $results[$results.Count - 1]
    }
    & dotnet build $projectPath -p:ProjectZGraphicsBackend=Invalid --nologo -v:quiet *> "$verification/$language-invalid.txt"
    if ($LASTEXITCODE -eq 0) { throw 'An invalid graphics backend was accepted.' }
    $results.Add("PASS $language rejects an invalid renderer")
}
$results | Set-Content -LiteralPath "$verification/results.txt"
