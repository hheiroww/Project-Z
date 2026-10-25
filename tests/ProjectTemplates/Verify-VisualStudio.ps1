[CmdletBinding()]
param([string]$TemplateDirectory, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo ('artifacts/VS2026/IDE-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a fresh IDE verification directory.' }
if (!$TemplateDirectory) {
    $manifests = Get-ChildItem -Path "$env:LOCALAPPDATA/Microsoft/VisualStudio/18.*/Extensions" -Recurse -Filter extension.vsixmanifest
    $installed = $manifests | Where-Object { Select-String -LiteralPath $_.FullName -Pattern 'Id="ProjectZ.VisualStudio2026.Templates"' -Quiet } | Select-Object -First 1
    if (!$installed) { throw 'Install ProjectZ.VisualStudio2026.vsix first.' }
    $TemplateDirectory = Join-Path $installed.DirectoryName 'ProjectTemplates'
}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$results = [Collections.Generic.List[string]]::new()
function Invoke-VisualStudio([scriptblock]$Action) {
    for ($attempt = 0; $attempt -lt 75; $attempt++) {
        try { return (& $Action) }
        catch {
            if ($_.Exception.ToString() -notmatch '80010001|8001010A') { throw }
            Start-Sleep -Milliseconds 200
        }
    }
    throw 'Visual Studio remained busy for 15 seconds.'
}
$dte = New-Object -ComObject VisualStudio.DTE.18.0
try {
    Invoke-VisualStudio { $dte.SuppressUI = $true }
    Invoke-VisualStudio { $dte.MainWindow.Visible = $false }
    Invoke-VisualStudio { $dte.Solution.Create($OutputDirectory, 'ProjectZTemplateAcceptance') }
    foreach ($language in @('CSharp', 'VisualBasic')) {
        $templates = @(Get-ChildItem -LiteralPath $TemplateDirectory -Recurse -Filter ProjectZ.vstemplate | Where-Object {
            ([xml](Get-Content -LiteralPath $_.FullName -Raw)).VSTemplate.TemplateData.TemplateID -eq "ProjectZ.Desktop.$language"
        })
        if ($templates.Count -ne 1) { throw "Expected one $language template under $TemplateDirectory." }
        $template = $templates[0].FullName
        $folder = Join-Path $OutputDirectory $language
        Invoke-VisualStudio { [void]$dte.Solution.AddFromTemplate($template, $folder, "VsCreated$language", $false) }
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 250
            $count = Invoke-VisualStudio { $dte.Solution.Projects.Count }
        } while (!(Test-Path -LiteralPath "$folder/ProjectZ/Runtime/OpenGL/ProjectZ.dll") -and [DateTime]::UtcNow -lt $deadline)
        foreach ($file in @('ProjectZ/ProjectZ.Graphics.props', 'ProjectZ/ProjectZ.Graphics.targets', 'ProjectZ/ProjectZ.Graphics.xaml', 'ProjectZ/Runtime/OpenGL/ProjectZ.dll', 'ProjectZ/Runtime/DirectX11/ProjectZ.dll', 'README.ProjectZ.md')) {
            if (!(Test-Path -LiteralPath (Join-Path $folder $file))) { throw "Visual Studio did not create $language/$file." }
        }
        $project = Get-ChildItem -LiteralPath $folder -Filter '*.*proj' | Select-Object -First 1
        if (!$project -or (Get-Content -LiteralPath $project.FullName -Raw).Contains('$safeprojectname$')) { throw 'Visual Studio did not replace the project name.' }
        $suffix = if ($language -eq 'CSharp') { 'cs' } else { 'vb' }
        foreach ($file in @("Program.$suffix", "GameWindow.$suffix", "MainScene.$suffix")) {
            if (!(Test-Path -LiteralPath (Join-Path $folder $file))) { throw "Visual Studio did not create $file." }
        }
        $results.Add("PASS Visual Studio $($dte.Version) creates $language source, project properties and both alternate renderer assemblies")
        Write-Host $results[$results.Count - 1]
    }
    foreach ($language in @('CSharp', 'VisualBasic')) {
        $templates = @(Get-ChildItem -LiteralPath $TemplateDirectory -Recurse -Filter ProjectZ.Xaml.vstemplate | Where-Object {
            ([xml](Get-Content -LiteralPath $_.FullName -Raw)).VSTemplate.TemplateData.TemplateID -eq "ProjectZ.Xaml.$language"
        })
        if ($templates.Count -ne 1) { throw "Expected one $language XAML solution template." }
        $template = $templates[0].FullName
        $folder = Join-Path $OutputDirectory "Xaml$language"
        $name = "VsXaml$language"
        Invoke-VisualStudio { [void]$dte.Solution.AddFromTemplate($template, $folder, $name, $false) }
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        do {
            Start-Sleep -Milliseconds 250
            $projects = @(Get-ChildItem -LiteralPath $folder -Recurse -Filter '*.*proj' -ErrorAction SilentlyContinue)
        } while ($projects.Count -lt 2 -and [DateTime]::UtcNow -lt $deadline)
        if ($projects.Count -ne 2) { throw 'Visual Studio did not create both App and Designer.' }
        $app = $projects | Where-Object { $_.BaseName -eq "$name.App" }
        $designer = $projects | Where-Object { $_.BaseName -eq "$name.Designer" }
        if (!$app -or !$designer) { throw 'The XAML solution project names were not substituted.' }
        foreach ($project in @($app, $designer)) {
            if ((Get-Content -LiteralPath $project.FullName -Raw) -match '\$(ext_)?safeprojectname\$') { throw 'A solution template retained an unexpanded project parameter.' }
            $log = Join-Path $OutputDirectory ($project.BaseName + '.build.txt')
            & dotnet build $project.FullName --nologo *> $log
            if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $log -Tail 25; throw 'An IDE-created XAML project did not build.' }
        }
        $markup = Join-Path $designer.DirectoryName 'MainWindow.xaml'
        $document = [IO.File]::ReadAllText($markup)
        if (!$document.Contains('x:Class="' + $name + '.MainWindow"')) { throw 'The shared designer class name was not expanded.' }
        foreach ($file in @('ProjectZ/ProjectZ.Tooling.props', 'ProjectZ/Convert-Project.ps1', 'ProjectZ/Tools/ProjectZ.XamlCodegen.dll', 'ProjectZ/Runtime/ProjectZ.WpfCompatibility.dll')) {
            if (!(Test-Path -LiteralPath (Join-Path $app.DirectoryName $file))) { throw "The IDE did not copy $file." }
        }
        $results.Add("PASS Visual Studio $($dte.Version) creates and builds the $language App and XAML Designer with shared markup and portable tools")
        Write-Host $results[$results.Count - 1]
    }
}
finally { Invoke-VisualStudio { $dte.Quit() } }
$results | Set-Content -LiteralPath (Join-Path $OutputDirectory 'results.txt')
