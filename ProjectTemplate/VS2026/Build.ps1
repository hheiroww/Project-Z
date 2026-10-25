[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [switch]$SkipFrameworkBuild,
    [string]$PackagePath
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts/VS2026' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$staging = Join-Path $OutputDirectory ('Staging/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path "$OutputDirectory/Templates" | Out-Null
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[xml]$graphics = Get-Content -LiteralPath "$PSScriptRoot/Common/ProjectZ.Graphics.props" -Raw
$frameworkVersion = [string]$graphics.Project.PropertyGroup.ProjectZFrameworkVersion
$restoreArguments = @()
if ($PackagePath) {
    . "$PSScriptRoot/PackageFeed.ps1"
    $restoreRoot = Join-Path $OutputDirectory ('Restore/' + [Guid]::NewGuid().ToString('N'))
    $restoreConfiguration = New-ProjectZRestoreConfiguration -ConfigPath "$restoreRoot/NuGet.Config" -PackagesPath "$restoreRoot/Packages" -PackagePath $PackagePath -ExpectedVersion $frameworkVersion
    $restoreArguments = @("-p:RestoreConfigFile=$($restoreConfiguration.ConfigPath)", "-p:RestorePackagesPath=$($restoreConfiguration.PackagesPath)", '-p:RestoreFallbackFolders=')
    Write-Host "Building templates with exact local ProjectZ $frameworkVersion; public NuGet indexing is not required."
}

function Copy-XamlTooling([string]$Payload) {
    New-Item -ItemType Directory -Force -Path "$Payload/Tools" | Out-Null
    foreach ($name in @('ProjectZ.Xaml.targets', 'ProjectZ.NativeXaml.targets', 'ProjectZ.Application.targets', 'ProjectZ.RenderingQuality.targets', 'ProjectZ.Settings.xaml')) {
        Copy-Item -LiteralPath "$repo/Project Z XAML Codegen/$name" -Destination $Payload
    }
    # Publish output only; exclude symbols, logs, source and machine settings.
    foreach ($file in Get-ChildItem -LiteralPath "$OutputDirectory/Build/XamlTools" -File -Recurse) {
        if ($file.Extension -notin @('.dll', '.exe', '.json', '.config')) { continue }
        $relative = [IO.Path]::GetRelativePath("$OutputDirectory/Build/XamlTools", $file.FullName)
        $destination = Join-Path "$Payload/Tools" $relative
        New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($destination)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
    Copy-Item -LiteralPath "$OutputDirectory/Build/Compatibility/ProjectZ.WpfCompatibility.dll" -Destination "$Payload/Runtime"
    New-Item -ItemType Directory -Force -Path "$Payload/Runtime/licenses/XamlCodegen" | Out-Null
    Copy-Item -Path "$repo/ThirdParty/XamlCodegen/*" -Destination "$Payload/Runtime/licenses/XamlCodegen"
    Get-ChildItem -LiteralPath $Payload -File -Recurse |
        ForEach-Object { [IO.Path]::GetRelativePath($Payload, $_.FullName).Replace('\', '/') } |
        Sort-Object | Set-Content -LiteralPath "$Payload/ProjectZ.Tooling.files"
}

function Write-ManifestItems([xml]$Template, [string]$Folder) {
    $projectNode = $Template.VSTemplate.TemplateContent.Project
    foreach ($child in @($projectNode.ChildNodes)) { [void]$projectNode.RemoveChild($child) }
    foreach ($file in Get-ChildItem -LiteralPath $Folder -File -Recurse | Sort-Object FullName) {
        $relative = [IO.Path]::GetRelativePath($Folder, $file.FullName).Replace('\', '/')
        if ($relative -eq $projectNode.GetAttribute('File') -or $file.Extension -eq '.vstemplate') { continue }
        $node = $Template.CreateElement('ProjectItem', $Template.DocumentElement.NamespaceURI)
        $node.SetAttribute('ReplaceParameters', ($file.Extension -in @('.cs', '.vb', '.xaml')).ToString().ToLowerInvariant())
        if ($file.BaseName -in @('MainScene', 'MainWindow')) { $node.SetAttribute('OpenInEditor', 'true') }
        $node.InnerText = $relative
        [void]$projectNode.AppendChild($node)
    }
    $Template.Save("$Folder/ProjectZ.vstemplate")
}

Push-Location $repo
$restoreEnvironment = $null
try {
    if ($PackagePath) { $restoreEnvironment = Enter-ProjectZRestoreEnvironment -RestoreConfiguration $restoreConfiguration }
    foreach ($backend in @('DirectX11', 'OpenGL')) {
        $buildRoot = Join-Path $OutputDirectory "Build/$backend"
        if (!$SkipFrameworkBuild) {
            & dotnet build 'Project Z Windows.vbproj' -c Release "-p:ProjectZGraphicsBackend=$backend" -p:GeneratePackageOnBuild=false -p:ProjectZDiagnosticsPrepared=true -p:UseLocalSocketJack=false --artifacts-path $buildRoot --nologo @restoreArguments
            if ($LASTEXITCODE -ne 0) { throw "$backend framework build failed." }
        }
        $dll = Join-Path $buildRoot 'bin/Project Z Windows/release/ProjectZ.dll'
        if (!(Test-Path -LiteralPath $dll)) { throw "Missing backend assembly: $dll" }
    }

    & dotnet publish 'Project Z XAML Codegen/ProjectZ.XamlCodegen.csproj' -c Release -o "$OutputDirectory/Build/XamlTools" -p:DebugType=None -p:DebugSymbols=false -p:GeneratePackageOnBuild=false --nologo @restoreArguments
    if ($LASTEXITCODE -ne 0) { throw 'XAML tooling publish failed.' }
    & dotnet build 'Project Z WPF Compatibility/ProjectZ.WpfCompatibility.csproj' -c Release "-p:ProjectZFrameworkPackageVersion=$frameworkVersion" -o "$OutputDirectory/Build/Compatibility" -p:GeneratePackageOnBuild=false --nologo @restoreArguments
    if ($LASTEXITCODE -ne 0) { throw 'Portable compatibility build failed.' }
    if ($PackagePath) { Assert-ProjectZRestoredPackage -RestoreConfiguration $restoreConfiguration }

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
        $fontLicense = Join-Path $folder 'ProjectZ/Runtime/licenses/ProjectZSans'
        New-Item -ItemType Directory -Force -Path $fontLicense | Out-Null
        Copy-Item -LiteralPath "$repo/ThirdParty/ProjectZSans/OFL.txt" -Destination "$fontLicense/OFL.txt" -Force
        Copy-XamlTooling "$folder/ProjectZ"

        $template = [xml]@"
<VSTemplate Version="3.0.0" Type="Project" xmlns="http://schemas.microsoft.com/developer/vstemplate/2005">
  <TemplateData>
    <Name>Project-Z Desktop ($display)</Name>
    <Description>Project-Z desktop with DX12, DX11 and OpenGL, build-time XAML and a portable project converter.</Description>
    <ProjectType>$language</ProjectType><SortOrder>100</SortOrder><DefaultName>ProjectZApp</DefaultName>
    <CreateNewFolder>true</CreateNewFolder><ProvideDefaultName>true</ProvideDefaultName><CreateInPlace>true</CreateInPlace>
    <LanguageTag>$language</LanguageTag><PlatformTag>Windows</PlatformTag><ProjectTypeTag>Desktop</ProjectTypeTag>
    <TemplateID>ProjectZ.Desktop.$language</TemplateID>
  </TemplateData>
  <TemplateContent><Project File="ProjectZApp.$extension" ReplaceParameters="true" TargetFileName="`$safeprojectname`$.$extension" /></TemplateContent>
</VSTemplate>
"@
        Write-ManifestItems $template $folder
        $zipFolder = Join-Path $staging "ProjectTemplates/$language/ProjectZ"
        New-Item -ItemType Directory -Force -Path $zipFolder | Out-Null
        $zipPath = Join-Path $zipFolder "ProjectZ.$language.zip"
        if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath }
        [IO.Compression.ZipFile]::CreateFromDirectory($folder, $zipPath)
        Copy-Item -LiteralPath $zipPath -Destination "$OutputDirectory/Templates/ProjectZ.$language.zip" -Force

        $xamlFolder = Join-Path $staging "XamlSources/$language"
        New-Item -ItemType Directory -Force -Path "$xamlFolder/App", "$xamlFolder/Designer" | Out-Null
        Copy-Item -LiteralPath "$folder/ProjectZ" -Destination "$xamlFolder/App/ProjectZ" -Recurse
        $suffix = if ($language -eq 'CSharp') { 'cs' } else { 'vb' }
        foreach ($part in @('App', 'Designer')) {
            Copy-Item -Path "$PSScriptRoot/Xaml/$language/$part/*" -Destination "$xamlFolder/$part"
        }
        foreach ($name in @("Program.$suffix", "GameWindow.$suffix")) {
            $source = [IO.File]::ReadAllText("$PSScriptRoot/$language/$name").Replace('$safeprojectname$', '$ext_safeprojectname$')
            if ($language -eq 'VisualBasic') {
                $start = if ($name -eq 'Program.vb') { 'Friend Module' } else { 'Public NotInheritable Class' }
                $source = $source.Replace($start, 'Namespace $ext_safeprojectname$' + [Environment]::NewLine + $start) + [Environment]::NewLine + 'End Namespace' + [Environment]::NewLine
            }
            [IO.File]::WriteAllText("$xamlFolder/App/$name", $source)
        }
        Copy-Item -LiteralPath "$PSScriptRoot/Xaml/MainWindow.xaml" -Destination "$xamlFolder/Designer"
        Copy-Item -LiteralPath "$PSScriptRoot/Xaml/README.ProjectZ.md" -Destination "$xamlFolder/App"
        foreach ($part in @('App', 'Designer')) {
            [xml]$child = $template.CloneNode($true)
            $child.VSTemplate.TemplateData.Name = $part
            $child.VSTemplate.TemplateData.TemplateID = "ProjectZ.Xaml.$part.$language"
            $hidden = $child.CreateElement('Hidden', $child.DocumentElement.NamespaceURI)
            $hidden.InnerText = 'true'
            [void]$child.VSTemplate.TemplateData.AppendChild($hidden)
            $file = if ($part -eq 'App') { "ProjectZApp.$extension" } else { "ProjectZDesigner.$extension" }
            $child.VSTemplate.TemplateContent.Project.SetAttribute('File', $file)
            Write-ManifestItems $child "$xamlFolder/$part"
        }
        $group = @'
<VSTemplate Version="3.0.0" Type="ProjectGroup" xmlns="http://schemas.microsoft.com/developer/vstemplate/2005">
  <TemplateData>
    <Name>Project-Z XAML Desktop (__DISPLAY__)</Name>
    <Description>Native Project-Z app plus a Visual Studio XAML designer project, automatic build-time conversion and portable project converter.</Description>
    <ProjectType>__LANGUAGE__</ProjectType><DefaultName>ProjectZXamlApp</DefaultName><SortOrder>90</SortOrder>
    <CreateNewFolder>true</CreateNewFolder><ProvideDefaultName>true</ProvideDefaultName>
    <LanguageTag>__LANGUAGE__</LanguageTag><PlatformTag>Windows</PlatformTag><ProjectTypeTag>Desktop</ProjectTypeTag>
    <TemplateID>ProjectZ.Xaml.__LANGUAGE__</TemplateID>
  </TemplateData>
  <TemplateContent><ProjectCollection>
    <ProjectTemplateLink ProjectName="$safeprojectname$.App" CopyParameters="true">App/ProjectZ.vstemplate</ProjectTemplateLink>
    <ProjectTemplateLink ProjectName="$safeprojectname$.Designer" CopyParameters="true">Designer/ProjectZ.vstemplate</ProjectTemplateLink>
  </ProjectCollection></TemplateContent>
</VSTemplate>
'@
        $group.Replace('__DISPLAY__', $display).Replace('__LANGUAGE__', $language) | Set-Content -LiteralPath "$xamlFolder/ProjectZ.Xaml.vstemplate"
        $xamlZip = Join-Path $zipFolder "ProjectZ.Xaml.$language.zip"
        [IO.Compression.ZipFile]::CreateFromDirectory($xamlFolder, $xamlZip)
        Copy-Item -LiteralPath $xamlZip -Destination "$OutputDirectory/Templates/ProjectZ.Xaml.$language.zip" -Force
    }

    $vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
    if (!(Test-Path -LiteralPath $vswhere)) { throw 'Install Visual Studio or Build Tools 2022 or later.' }
    $msbuild = & $vswhere -latest -prerelease -products '*' -version '[17.0,)' -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1
    if (!$msbuild) { throw 'Visual Studio MSBuild 17 or later was not found.' }
    # VSSDK's .NET Framework ZIP extractor still enforces MAX_PATH. Keep the
    # extraction root short enough for Roslyn's nested MSBuild host filenames.
    $packaging = Join-Path ([IO.Path]::GetTempPath()) ('pzv-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    & $msbuild "$PSScriptRoot/ProjectZ.Templates.csproj" /restore /t:Build /p:Configuration=Release "/p:ProjectZTemplateStaging=$staging" "/p:BaseIntermediateOutputPath=$packaging/" /nologo /v:minimal @restoreArguments
    if ($LASTEXITCODE -ne 0) { throw 'VSIX packaging failed.' }
    Copy-Item -LiteralPath "$PSScriptRoot/bin/Release/net472/ProjectZ.Templates.vsix" -Destination "$OutputDirectory/ProjectZ.VisualStudio2026.vsix" -Force
    Set-Content -LiteralPath "$OutputDirectory/LatestStaging.txt" -Value $staging
    $hash = Get-FileHash -LiteralPath "$OutputDirectory/ProjectZ.VisualStudio2026.vsix" -Algorithm SHA256
    ($hash.Hash.ToLowerInvariant() + '  ProjectZ.VisualStudio2026.vsix') | Set-Content -LiteralPath "$OutputDirectory/SHA256SUMS.txt" -Encoding ascii
    $hash
}
finally {
    if ($null -ne $restoreEnvironment) { Exit-ProjectZRestoreEnvironment -Previous $restoreEnvironment }
    Pop-Location
}
