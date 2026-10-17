param([switch]$SkipRuntime)
$ErrorActionPreference='Stop'
$repository=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$evidence=Join-Path $repository 'artifacts/wpf-import'
$output=Join-Path $evidence 'startup-properties'
$projectPath=Join-Path $output 'stable-entry/Original.csproj'
$generator=Join-Path $repository 'Project Z XAML Codegen/bin/Release/net8.0/ProjectZ.XamlCodegen.dll'
$original=Join-Path $PSScriptRoot 'Original/Original.csproj'
$build=@('-c','Release','-m:1','-nodeReuse:false','-p:UseSharedCompilation=false','-p:BuildInParallel=false','-p:GeneratePackageOnBuild=false','-p:DeployVst3OnBuild=false','-v:q')
function Checked([string[]]$Arguments,[string]$Log) {
    & dotnet @Arguments *> (Join-Path $evidence $Log)
    if($LASTEXITCODE -ne 0) {Get-Content (Join-Path $evidence $Log) -Tail 20;throw "dotnet failed: $Log"}
}
function Settings([hashtable]$Values) {
    if(!(Test-Path -LiteralPath $projectPath)) {return}
    $document=[System.Xml.Linq.XDocument]::Load($projectPath)
    foreach($group in @($document.Root.Elements('PropertyGroup'))) {
        foreach($property in @($group.Elements() | Where-Object {$_.Name.LocalName -in $Values.Keys})) {$property.Remove()}
        if(!$group.HasElements) {$group.Remove()}
    }
    $group=[System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]'PropertyGroup')
    foreach($entry in $Values.GetEnumerator()) {$group.Add([System.Xml.Linq.XElement]::new([System.Xml.Linq.XName]$entry.Key,[string]$entry.Value))}
    $document.Root.Add($group);$document.Save($projectPath)
}
function Runtime([string]$Mode) {
    Checked (@('build',(Join-Path $PSScriptRoot 'Runtime/Runtime.csproj'))+$build) "startup-$Mode-build.log"
    $fixture=Join-Path $PSScriptRoot 'Runtime/bin/Release/net8.0-windows/Runtime.dll'
    $probe=Start-Process dotnet -ArgumentList @('"'+$fixture+'"',$Mode) -WorkingDirectory $repository -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $evidence "startup-$Mode-runtime.log") -RedirectStandardError (Join-Path $evidence "startup-$Mode-runtime.err.log")
    try {
        if(!$probe.WaitForExit(30000)) {Stop-Process -Id $probe.Id;throw 'Startup fixture exceeded its deadline.'}
        $probe.Refresh()
        if($probe.ExitCode -ne 0) {Get-Content (Join-Path $evidence "startup-$Mode-runtime.err.log") -Tail 15;throw 'Native startup failed.'}
        if((Get-Content (Join-Path $evidence "startup-$Mode-runtime.log") -Raw) -notmatch 'STARTUP_PROPERTIES_RUNTIME_OK') {throw 'Native startup checks did not complete.'}
    }
    finally {if(!$probe.HasExited) {Stop-Process -Id $probe.Id};$probe.Dispose()}
}
Push-Location $repository
try {
    $sources=Get-ChildItem (Join-Path $PSScriptRoot 'Original') -File | ForEach-Object {[pscustomobject]@{Path=$_.FullName;Hash=(Get-FileHash -LiteralPath $_.FullName).Hash}}
    New-Item -ItemType Directory -Path $evidence -Force | Out-Null
    Checked (@('build','Project Z XAML Codegen/ProjectZ.XamlCodegen.csproj')+$build) 'startup-generator-build.log'
    Checked @('restore',$original,'-v:q') 'startup-original-restore.log'
    $values=@{ProjectZMainWindow='Default';ProjectZFramesPerSecond='60';ProjectZAntiAliasingSamples='4';ProjectZTextureFiltering='Anisotropic16x';ProjectZInitialWidth='1000';ProjectZInitialHeight='700';ProjectZMinimumWidth='320';ProjectZMinimumHeight='200';ProjectZAutoResizeViewport='true';ProjectZBackgroundColor='#000000';ProjectZShutdownMode='Default'}
    Settings $values
    Checked @($generator,'import',$original,'--output',$output,'--root-directory','stable-entry') 'startup-default-import.log'
    $schema=[xml](Get-Content (Join-Path $output 'stable-entry/Generated/ProjectZ.Settings.xaml') -Raw)
    $main=@($schema.Rule.EnumProperty) | Where-Object Name -eq 'ProjectZMainWindow'
    if(@($main.EnumValue.Name) -notcontains 'StartupProperties.AlternateWindow') {throw 'Properties page has no main window choices.'}
    if(@(($schema.Rule.EnumProperty | Where-Object Name -eq 'ProjectZAntiAliasingSamples').EnumValue.Name) -notcontains '4') {throw 'Properties page has no AA controls.'}
    if(@(($schema.Rule.EnumProperty | Where-Object Name -eq 'ProjectZTextureFiltering').EnumValue.Name) -notcontains 'Anisotropic16x') {throw 'Properties page has no AF controls.'}
    if(!$SkipRuntime) {Runtime 'default'}
    $values.ProjectZMainWindow='StartupProperties.AlternateWindow';$values.ProjectZFramesPerSecond='45';$values.ProjectZAntiAliasingSamples='2';$values.ProjectZTextureFiltering='Anisotropic8x';$values.ProjectZInitialWidth='760';$values.ProjectZInitialHeight='420';$values.ProjectZMinimumWidth='280';$values.ProjectZMinimumHeight='160';$values.ProjectZAutoResizeViewport='false';$values.ProjectZBackgroundColor='#123456';$values.ProjectZShutdownMode='OnMainWindowClose'
    Settings $values
    Checked @($generator,'import',$original,'--output',$output,'--root-directory','stable-entry') 'startup-selected-import.log'
    $saved=[xml](Get-Content $projectPath -Raw)
    foreach($entry in $values.GetEnumerator()) {if(@($saved.Project.PropertyGroup.($entry.Key))[-1] -ne $entry.Value) {throw "Regeneration lost property $($entry.Key)."}}
    Checked @($generator,'verify-import',$output) 'startup-verify-import.log'
    if(!$SkipRuntime) {Runtime 'selected'}
    $projectBytes=[IO.File]::ReadAllBytes($projectPath)
    try {
        $document=[System.Xml.Linq.XDocument]::Load($projectPath)
        $document.Root.Add([System.Xml.Linq.XElement]::new('PropertyGroup',[System.Xml.Linq.XElement]::new('AllowUnsafeBlocks','true')))
        $document.Save($projectPath)
        & dotnet $generator verify-import $output *> (Join-Path $evidence 'startup-protected-project-edit.log')
        if($LASTEXITCODE -eq 0 -or (Get-Content (Join-Path $evidence 'startup-protected-project-edit.log') -Raw) -notmatch 'Refusing to verify modified generated file') {throw 'An unrelated generated project edit escaped ownership protection.'}
    }
    finally {[IO.File]::WriteAllBytes($projectPath,$projectBytes)}
    foreach($source in $sources) {if((Get-FileHash -LiteralPath $source.Path).Hash -ne $source.Hash) {throw 'Startup generation modified original fixture source.'}}
    Write-Output 'PASS Project-Z properties startup selection native scene settings regeneration and ownership protection'
    $global:LASTEXITCODE=0
}
finally {Pop-Location}
