[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$VsixPath,
    [string]$ExpectedVersion
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$ExpectedVersion) {
    [xml]$properties = Get-Content -LiteralPath "$repo/Extensions/ProjectZ.Package.props" -Raw
    $ExpectedVersion = [string]$properties.Project.PropertyGroup.Version
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $VsixPath).Path)
try {
    $entries = @{}
    foreach ($entry in $archive.Entries) { $entries[$entry.FullName] = $entry }
    function Read-Entry([string]$Name) {
        if (!$entries.ContainsKey($Name)) { throw "VSIX asset missing: $Name" }
        $reader = [IO.StreamReader]::new($entries[$Name].Open())
        try { $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    [xml]$manifest = Read-Entry 'extension.vsixmanifest'
    $identity = $manifest.PackageManifest.Metadata.Identity
    if ($identity.Id -ne 'ProjectZ.VisualStudio2026.Templates' -or $identity.Publisher -ne 'SocketJack') { throw 'The combined VSIX must retain its extension ID and use the established Marketplace publisher.' }
    if ($identity.Version -ne $ExpectedVersion) { throw 'VSIX version differs from the framework release.' }
    if (!$entries.ContainsKey([string]$manifest.PackageManifest.Metadata.Icon)) { throw 'Marketplace icon is missing from the VSIX.' }
    foreach ($target in $manifest.PackageManifest.Installation.InstallationTarget) {
        if ($target.Version -ne '[17.14,)') { throw 'Marketplace builds must use the stable 17.14 API lower bound, which also supports Visual Studio 2026.' }
    }
    $expected = @('ProjectZ.Desktop.CSharp', 'ProjectZ.Desktop.VisualBasic', 'ProjectZ.Xaml.CSharp', 'ProjectZ.Xaml.VisualBasic', 'ProjectZ.CrossPlatform.CSharp', 'ProjectZ.CrossPlatform.VisualBasic', 'ProjectZ.CrossPlatform.SceneXaml')
    $observed = [Collections.Generic.List[string]]::new()
    foreach ($name in $entries.Keys) {
        if ($name -match '/ProjectZ/ProjectZ/|(^|/)(\.codex|\.git|obj|bin|package-feed)/|\.(pdb|log|binlog|nupkg|zip)$') { throw "Unexpected staged payload in combined VSIX: $name" }
        if ($name.EndsWith('.vstemplate')) {
            [xml]$template = Read-Entry $name
            $parent = $name.Substring(0, $name.LastIndexOf('/') + 1)
            $icon = [string]$template.VSTemplate.TemplateData.Icon
            if (!$icon -or !$entries.ContainsKey($parent + $icon.Replace('\', '/'))) { throw "Template icon is missing: $name" }
            if ($template.VSTemplate.TemplateData.Hidden -ne 'true') { $observed.Add([string]$template.VSTemplate.TemplateData.TemplateID) }
            $project = $template.VSTemplate.TemplateContent.Project
            if ($project) {
                $files = @([string]$project.File) + @($project.ProjectItem | ForEach-Object InnerText)
                foreach ($file in $files) {
                    if (!$entries.ContainsKey($parent + $file.Replace('\', '/'))) { throw "Template manifest references a missing file: $file" }
                }
            }
        }
        if ($name.EndsWith('/ProjectZ.Graphics.props') -or $name.EndsWith('/ProjectZ.Platform.props')) {
            [xml]$props = Read-Entry $name
            if ([string]$props.Project.PropertyGroup.ProjectZFrameworkVersion -ne $ExpectedVersion) { throw "Stale runtime pin: $name" }
        }
    }
    if ($observed.Count -ne 7 -or @(Compare-Object $expected @($observed)).Count) { throw 'The installer must contain all seven templates exactly once.' }
    Write-Host "PASS single VSIX $ExpectedVersion, preserved extension ID, Marketplace publisher/icon, seven templates, current runtime pins and complete manifest assets."
} finally { $archive.Dispose() }
