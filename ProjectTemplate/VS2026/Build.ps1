[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [switch]$SkipFrameworkBuild
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts/VS2026' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$staging = Join-Path $OutputDirectory ('Staging/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path "$OutputDirectory/Templates" | Out-Null
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

Push-Location $repo
try {
    foreach ($backend in @('DirectX11', 'OpenGL')) {
        $buildRoot = Join-Path $OutputDirectory "Build/$backend"
        if (!$SkipFrameworkBuild) {
            & dotnet build 'Project Z Windows.vbproj' -c Release "-p:ProjectZGraphicsBackend=$backend" -p:GeneratePackageOnBuild=false -p:ProjectZDiagnosticsPrepared=true -p:UseLocalSocketJack=false --artifacts-path $buildRoot --nologo
            if ($LASTEXITCODE -ne 0) { throw "$backend framework build failed." }
        }
        $dll = Join-Path $buildRoot 'bin/Project Z Windows/release/ProjectZ.dll'
        if (!(Test-Path -LiteralPath $dll)) { throw "Missing backend assembly: $dll" }
    }

    foreach ($language in @('CSharp', 'VisualBasic')) {
        $extension = if ($language -eq 'CSharp') { 'csproj' } else { 'vbproj' }
        $display = if ($language -eq 'CSharp') { 'C#' } else { 'Visual Basic' }
        $folder = Join-Path $staging "Sources/$language"
        New-Item -ItemType Directory -Force -Path "$folder/ProjectZ/Runtime" | Out-Null
        Copy-Item -Path "$PSScriptRoot/$language/*" -Destination $folder -Recurse -Force
        Copy-Item -Path "$PSScriptRoot/Common/*" -Destination "$folder/ProjectZ" -Force
        Move-Item -LiteralPath "$folder/ProjectZ/README.ProjectZ.md" -Destination "$folder/README.ProjectZ.md" -Force
        foreach ($backend in @('DirectX11', 'OpenGL')) {
            New-Item -ItemType Directory -Force -Path "$folder/ProjectZ/Runtime/$backend" | Out-Null
            Copy-Item -LiteralPath "$OutputDirectory/Build/$backend/bin/Project Z Windows/release/ProjectZ.dll" -Destination "$folder/ProjectZ/Runtime/$backend/ProjectZ.dll" -Force
        }
        Copy-Item -LiteralPath "$repo/THIRD-PARTY-NOTICES.md" -Destination "$folder/ProjectZ/Runtime/THIRD-PARTY-NOTICES.md" -Force
        $license = Join-Path $folder 'ProjectZ/Runtime/licenses/WPFPixelShaderLibrary'
        New-Item -ItemType Directory -Force -Path $license | Out-Null
        Copy-Item -LiteralPath "$repo/ThirdParty/WPFPixelShaderLibrary/LICENSE" -Destination "$license/LICENSE" -Force

        $template = [xml]@"
<VSTemplate Version="3.0.0" Type="Project" xmlns="http://schemas.microsoft.com/developer/vstemplate/2005">
  <TemplateData>
    <Name>Project-Z Desktop ($display)</Name>
    <Description>Project-Z Windows desktop application with DirectX 12, DirectX 11 and OpenGL graphics configurations.</Description>
    <ProjectType>$language</ProjectType><SortOrder>100</SortOrder><DefaultName>ProjectZApp</DefaultName>
    <CreateNewFolder>true</CreateNewFolder><ProvideDefaultName>true</ProvideDefaultName><CreateInPlace>true</CreateInPlace>
    <LanguageTag>$language</LanguageTag><PlatformTag>Windows</PlatformTag><ProjectTypeTag>Desktop</ProjectTypeTag>
    <TemplateID>ProjectZ.Desktop.$language</TemplateID>
  </TemplateData>
  <TemplateContent><Project File="ProjectZApp.$extension" ReplaceParameters="true" TargetFileName="`$safeprojectname`$.$extension" /></TemplateContent>
</VSTemplate>
"@
        $namespace = $template.DocumentElement.NamespaceURI
        $projectNode = $template.DocumentElement.LastChild.FirstChild
        foreach ($file in Get-ChildItem -LiteralPath $folder -File -Recurse | Sort-Object FullName) {
            $relative = $file.FullName.Substring($folder.Length + 1).Replace('\', '/')
            if ($relative -eq "ProjectZApp.$extension" -or $relative -eq 'ProjectZ.vstemplate') { continue }
            $node = $template.CreateElement('ProjectItem', $namespace)
            $replace = $file.Extension -in @('.cs', '.vb')
            $node.SetAttribute('ReplaceParameters', $replace.ToString().ToLowerInvariant())
            if ($file.BaseName -eq 'MainScene') { $node.SetAttribute('OpenInEditor', 'true') }
            $node.InnerText = $relative
            [void]$projectNode.AppendChild($node)
        }
        $template.Save("$folder/ProjectZ.vstemplate")
        $zipFolder = Join-Path $staging "ProjectTemplates/$language/ProjectZ"
        New-Item -ItemType Directory -Force -Path $zipFolder | Out-Null
        $zipPath = Join-Path $zipFolder "ProjectZ.$language.zip"
        if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath }
        [IO.Compression.ZipFile]::CreateFromDirectory($folder, $zipPath)
        Copy-Item -LiteralPath $zipPath -Destination "$OutputDirectory/Templates/ProjectZ.$language.zip" -Force
    }

    $vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
    if (!(Test-Path -LiteralPath $vswhere)) { throw 'Install Visual Studio or Build Tools 2022 or later.' }
    $msbuild = & $vswhere -latest -prerelease -products '*' -version '[17.0,)' -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1
    if (!$msbuild) { throw 'Visual Studio MSBuild 17 or later was not found.' }
    & $msbuild "$PSScriptRoot/ProjectZ.Templates.csproj" /restore /t:Build /p:Configuration=Release "/p:ProjectZTemplateStaging=$staging" /nologo /v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'VSIX packaging failed.' }
    Copy-Item -LiteralPath "$PSScriptRoot/bin/Release/net472/ProjectZ.Templates.vsix" -Destination "$OutputDirectory/ProjectZ.VisualStudio2026.vsix" -Force
    Set-Content -LiteralPath "$OutputDirectory/LatestStaging.txt" -Value $staging
    Get-FileHash -LiteralPath "$OutputDirectory/ProjectZ.VisualStudio2026.vsix" -Algorithm SHA256
}
finally { Pop-Location }
