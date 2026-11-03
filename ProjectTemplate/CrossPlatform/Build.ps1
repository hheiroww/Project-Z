[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts/CrossPlatformTemplates' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[xml]$framework = Get-Content -LiteralPath "$repo/Extensions/ProjectZ.Package.props" -Raw
$version = [string]$framework.Project.PropertyGroup.Version
[xml]$platform = Get-Content -LiteralPath "$PSScriptRoot/Common/ProjectZ.Platform.props" -Raw
if ($platform.Project.PropertyGroup.ProjectZFrameworkVersion -ne $version) { throw 'Portable template pin differs from the release version.' }
New-Item -ItemType Directory -Force -Path "$OutputDirectory/Content", "$OutputDirectory/Starters", "$OutputDirectory/Templates" | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-Zip([string]$Folder, [string]$Destination) {
    $stream = [IO.File]::Open($Destination, [IO.FileMode]::Create)
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $Folder -File -Recurse -Force | Sort-Object FullName) {
            $relative = [IO.Path]::GetRelativePath($Folder, $file.FullName).Replace('\', '/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $relative)
        }
    } finally { $zip.Dispose(); $stream.Dispose() }
}

foreach ($variant in @('CSharp', 'VisualBasic', 'SceneXaml')) {
    $language = if ($variant -eq 'VisualBasic') { 'VisualBasic' } else { 'CSharp' }
    $suffix = if ($language -eq 'CSharp') { 'cs' } else { 'vb' }
    $extension = $suffix + 'proj'
    $folder = "$OutputDirectory/Content/$variant"
    New-Item -ItemType Directory -Force -Path "$folder/.template.config" | Out-Null
    foreach ($file in @("ProjectZStarter.$extension", "Program.$suffix", "GameWindow.$suffix", "MainScene.$suffix")) {
        Copy-Item -LiteralPath "$PSScriptRoot/$variant/$file" -Destination $folder -Force
    }
    if ($variant -eq 'SceneXaml') { Copy-Item -LiteralPath "$PSScriptRoot/$variant/MainScene.xaml" -Destination $folder -Force }
    Copy-Item -LiteralPath "$PSScriptRoot/$variant/.template.config/template.json" -Destination "$folder/.template.config" -Force
    foreach ($name in @('ProjectZ.Platform.props', 'ProjectZ.Platform.targets', 'README.ProjectZ.md')) {
        Copy-Item -LiteralPath "$PSScriptRoot/Common/$name" -Destination $folder -Force
    }
    New-Item -ItemType Directory -Force -Path "$folder/ThirdParty/ProjectZSans" | Out-Null
    foreach ($name in @('OFL.txt', 'README.md')) { Copy-Item -LiteralPath "$repo/ThirdParty/ProjectZSans/$name" -Destination "$folder/ThirdParty/ProjectZSans" -Force }
    Copy-Item -LiteralPath "$repo/THIRD-PARTY-NOTICES.md" -Destination $folder -Force
    # Standalone ZIPs are ready-to-build projects; CLI metadata stays in the pack.
    $starter = "$OutputDirectory/Starters/$variant"
    New-Item -ItemType Directory -Force -Path $starter | Out-Null
    Get-ChildItem -LiteralPath $folder -File | Copy-Item -Destination $starter -Force
    Copy-Item -LiteralPath "$folder/ThirdParty" -Destination $starter -Recurse -Force
    Write-Zip $starter "$OutputDirectory/ProjectZ.CrossPlatform.$variant.zip"

    # The same sources also appear in Visual Studio's New Project dialog.
    $vsFolder = "$OutputDirectory/VisualStudioSources/$variant"
    New-Item -ItemType Directory -Force -Path $vsFolder | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $starter -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($starter, $file.FullName)
        New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName("$vsFolder/$relative")) | Out-Null
        $text = [IO.File]::ReadAllText($file.FullName).Replace('ProjectZStarter', '$safeprojectname$')
        [IO.File]::WriteAllText("$vsFolder/$relative", $text)
    }
    Copy-Item -LiteralPath "$repo/ProjectTemplate/VS2026/project-z-icon.png" -Destination "$vsFolder/__TemplateIcon.png" -Force
    $display = if ($variant -eq 'VisualBasic') { 'Desktop (Visual Basic)' } elseif ($variant -eq 'SceneXaml') { 'Scene XAML (C#)' } else { 'Desktop (C#)' }
    $manifest = [xml]@"
<VSTemplate Version="3.0.0" Type="Project" xmlns="http://schemas.microsoft.com/developer/vstemplate/2005">
  <TemplateData>
    <Name>Project-Z Cross-Platform $display</Name>
    <Description>Native desktop UI for Windows, Linux and macOS using the matching Project-Z runtime package.</Description>
    <Icon>__TemplateIcon.png</Icon>
    <ProjectType>$language</ProjectType><DefaultName>ProjectZApp</DefaultName><SortOrder>80</SortOrder>
    <CreateNewFolder>true</CreateNewFolder><ProvideDefaultName>true</ProvideDefaultName><CreateInPlace>true</CreateInPlace>
    <LanguageTag>$language</LanguageTag><PlatformTag>Windows</PlatformTag><PlatformTag>Linux</PlatformTag><PlatformTag>macOS</PlatformTag><ProjectTypeTag>Desktop</ProjectTypeTag>
    <TemplateID>ProjectZ.CrossPlatform.$variant</TemplateID>
  </TemplateData>
  <TemplateContent><Project File="ProjectZStarter.$extension" ReplaceParameters="true" TargetFileName="`$safeprojectname`$.$extension" /></TemplateContent>
</VSTemplate>
"@
    foreach ($file in Get-ChildItem -LiteralPath $vsFolder -File -Recurse | Sort-Object FullName) {
        if ($file.Extension -eq '.vstemplate') { continue }
        if ($file.Extension -eq ".$extension") { continue }
        $item = $manifest.CreateElement('ProjectItem', $manifest.DocumentElement.NamespaceURI)
        $item.SetAttribute('ReplaceParameters', ($file.Extension -ne '.png').ToString().ToLowerInvariant())
        if ($file.BaseName -eq 'MainScene') { $item.SetAttribute('OpenInEditor', 'true') }
        $item.InnerText = [IO.Path]::GetRelativePath($vsFolder, $file.FullName).Replace('\', '/')
        [void]$manifest.VSTemplate.TemplateContent.Project.AppendChild($item)
    }
    $manifest.Save("$vsFolder/ProjectZ.vstemplate")
    $vsZip = "$OutputDirectory/VisualStudio/$language/ProjectZCrossPlatform"
    New-Item -ItemType Directory -Force -Path $vsZip | Out-Null
    Write-Zip $vsFolder "$vsZip/ProjectZ.CrossPlatform.$variant.zip"
    Copy-Item -LiteralPath "$vsZip/ProjectZ.CrossPlatform.$variant.zip" -Destination "$OutputDirectory/Templates" -Force
}
& dotnet pack "$PSScriptRoot/ProjectZ.Templates.csproj" "-p:Version=$version" "-p:TemplateContentRoot=$OutputDirectory/Content" --artifacts-path "$OutputDirectory/Build" -o $OutputDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw 'Cross-platform template pack failed.' }
$files = @(Get-ChildItem -LiteralPath $OutputDirectory -File | Where-Object Extension -In @('.zip', '.nupkg'))
$files | Sort-Object Name | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
} | Set-Content -LiteralPath "$OutputDirectory/ProjectZ.Templates.SHA256SUMS.txt" -Encoding ascii
Write-Host "Built $($files.Count) portable template release assets for Project-Z $version."
