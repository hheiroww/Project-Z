param([switch]$SkipRuntime)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Push-Location $repo
try {
 function Run([string[]]$Arguments, [int]$Expected = 0) {
  $lines = & dotnet @Arguments 2>&1
  $code = $LASTEXITCODE
  $lines | Write-Output
  if ($code -ne $Expected) { throw "Expected exit $Expected, got $code : $Arguments" }
 }
 $manual = 'tests/WpfImport/BuildTime/Off/Manual.design.cs'
 $before = (Get-FileHash $manual).Hash
 Run @('run','--project','tests/WpfImport/BuildTime/Off/Off.csproj','-c','Release')
 if ((Get-FileHash $manual).Hash -ne $before) { throw 'Opt-out changed hand-edited code.' }
 Run @('build','tests/WpfImport/BuildTime/Off/Off.csproj','-c','Release','-p:ProjectZEnableXaml=true','-p:ProjectZXamlExclude=Manual.xaml','-p:GeneratePackageOnBuild=false')
 if ((Get-FileHash $manual).Hash -ne $before) { throw 'Excluded view changed hand-edited code.' }
 Run @('build','tests/WpfImport/BuildTime/cs/BuildTime.csproj','-c','Release','-p:ProjectZEnableXaml=false','-p:GeneratePackageOnBuild=false')
 foreach($framework in @('net8.0-windows7.0','net10.0-windows7.0')) {
  foreach($language in @('cs','vb')) {
   $project = "tests/WpfImport/BuildTime/$language/BuildTime." + $(if($language -eq 'cs'){'csproj'}else{'vbproj'})
   $arguments = @('build',$project,'-c','Release','-p:GeneratePackageOnBuild=false',"-p:ProjectZTestTargetFramework=$framework")
   Run $arguments
   $generated = @(Get-ChildItem "tests/WpfImport/BuildTime/$language/obj/Release/$framework/ProjectZXaml" -Recurse -Filter "*.design.$language")
   if($generated.Count -ne 1) { throw "Expected one generated designer for $language / $framework" }
   $stamp = $generated[0].LastWriteTimeUtc
   Run $arguments
   if((Get-Item $generated[0].FullName).LastWriteTimeUtc -ne $stamp) { throw 'Unchanged build regenerated the designer.' }
  }
  if(!$SkipRuntime) { Run @('run','--project','tests/WpfImport/BuildTime/Runtime/Runtime.csproj','-c','Release','-p:GeneratePackageOnBuild=false',"-p:ProjectZTestTargetFramework=$framework",'--','--smoke-test') }
 }
 # Designer-only retains original manual code; both languages still compile and wire handlers.
 foreach($language in @('cs','vb')) {
  $folder = "tests/WpfImport/BuildTime/DesignerOnly/$language"
  $extension = if($language -eq 'cs'){'csproj'}else{'vbproj'}
  $source = "$folder/View.xaml.$language"
  $hash = (Get-FileHash $source).Hash
  Run @('build',"$folder/DesignerOnly.$extension",'-c','Release','-p:GeneratePackageOnBuild=false')
  $originals = "$folder/obj/Release/net8.0-windows7.0/ProjectZXaml/original-items"
  if ((Get-Content $originals).Length -gt 0 -or (Get-FileHash $source).Hash -ne $hash) { throw 'Designer-only replaced manual code-behind.' }
 }
 Run @('build','tests/WpfImport/BuildTime/Off/Off.csproj','-p:ProjectZXamlMode=CodeBehindOnly') 1
 Run @('build','tests/WpfImport/BuildTime/Off/Off.csproj','-p:ProjectZEnableXaml=true','-p:ProjectZXamlMode=Disabled')
 # A settings change must refresh IntelliSense even when old generated files exist.
 $modeProject = 'tests/WpfImport/BuildTime/DesignerOnly/cs/DesignerOnly.csproj'
 Run @('build',$modeProject,'-c','ModeSwitch','-p:ProjectZXamlMode=DesignerAndCodeBehind','-p:GeneratePackageOnBuild=false')
 Run @('msbuild',$modeProject,'-t:Compile','-p:Configuration=ModeSwitch','-p:ProjectZXamlMode=DesignerOnly','-p:DesignTimeBuild=true','-p:SkipCompilerExecution=true','-p:ProvideCommandLineArgs=true','-p:BuildProjectReferences=false','-p:GeneratePackageOnBuild=false')
 $modeFolder = 'tests/WpfImport/BuildTime/DesignerOnly/cs/obj/ModeSwitch/net8.0-windows7.0/ProjectZXaml'
 if((Get-Content "$modeFolder/mode" -Raw).Trim() -ne 'DesignerOnly' -or (Get-Item "$modeFolder/original-items").Length -ne 0) { throw 'Design-time mode change retained converted code-behind.' }
 # Match VS: references already built, consumer cleaned and built separately.
 Run @('build','tests/WpfImport/BuildTime/Runtime/Runtime.csproj','-c','Debug','-p:GeneratePackageOnBuild=false')
 Run @('clean','tests/WpfImport/BuildTime/Runtime/Runtime.csproj','-c','Debug','-p:BuildProjectReferences=false','-v:q')
 Run @('build','tests/WpfImport/BuildTime/Runtime/Runtime.csproj','-c','Debug','-p:BuildProjectReferences=false','-p:BuildingInsideVisualStudio=true','-p:GeneratePackageOnBuild=false')
 $markup = @(Get-ChildItem 'tests/WpfImport/BuildTime/Runtime/bin/Debug/net8.0-windows7.0' -Recurse -Filter View.xaml)
 if($markup.Count -ne 4) { throw "Expected all four runtime views after VS-style build, got $($markup.Count)." }
 if(!$SkipRuntime) { Run @('run','--project','tests/WpfImport/BuildTime/Runtime/Runtime.csproj','-c','Debug','--no-build','--','--smoke-test') }
 Write-Output 'PASS designer-only C#/VB preservation, invalid-mode rejection, explicit opt-out and VS runtime content copying.'
 # Edit only this regression fixture, restore it even if validation fails.
 $xaml = 'tests/WpfImport/LinkedViews/cs/View.xaml'
 $original = [IO.File]::ReadAllBytes((Resolve-Path $xaml))
 $arguments = @('build','tests/WpfImport/BuildTime/cs/BuildTime.csproj','-c','Release','-p:GeneratePackageOnBuild=false')
 try {
  $text = [IO.File]::ReadAllText((Resolve-Path $xaml)).Replace('</StackPanel>','<TextBlock x:Name="BuildAddedField" Text="Build update" /></StackPanel>')
  [IO.File]::WriteAllText((Resolve-Path $xaml),$text)
  Run $arguments
  $generated = Get-ChildItem tests/WpfImport/BuildTime/cs/obj/Release/net8.0-windows7.0/ProjectZXaml -Recurse -Filter '*.design.cs'
  if(!(Get-Content $generated.FullName -Raw).Contains('BuildAddedField')) { throw 'XAML edit did not regenerate designer.' }
  [IO.File]::WriteAllText((Resolve-Path $xaml),$text.Replace('Text="Build update"','UnsupportedProperty="bad"'))
  Run $arguments 1
 } finally { [IO.File]::WriteAllBytes((Resolve-Path $xaml),$original) }
 Run $arguments
 Run @('clean','tests/WpfImport/BuildTime/cs/BuildTime.csproj','-c','Release','-v:q')
 if(@(Get-ChildItem tests/WpfImport/BuildTime/cs/obj/Release/net8.0-windows7.0 -Recurse -Filter '*.design.cs').Count) { throw 'Clean left generated designer files.' }
 Run $arguments
 if((Get-FileHash $manual).Hash -ne $before) { throw 'Hand-edited code changed.' }
 foreach($language in @('cs','vb')) {
  $project = "tests/WpfImport/BuildTime/$language/BuildTime." + $(if($language -eq 'cs'){'csproj'}else{'vbproj'})
  Run @('restore',$project)
  Run @('clean',$project,'-c','DesignTimeCheck','-v:q')
  Run @('msbuild',$project,'-t:Compile','-p:Configuration=DesignTimeCheck','-p:DesignTimeBuild=true','-p:SkipCompilerExecution=true','-p:ProvideCommandLineArgs=true','-p:BuildProjectReferences=false','-p:GeneratePackageOnBuild=false')
  $manifest = "tests/WpfImport/BuildTime/$language/obj/DesignTimeCheck/net8.0-windows7.0/ProjectZXaml/compile-items"
  if(!(Test-Path $manifest) -or !(Get-Content $manifest | Where-Object {$_ -like "*.design.$language"})) { throw 'Fresh design-time build did not produce the converted designer.' }
 }
 Write-Output 'PASS fresh C#/VB design-time configurations bootstrap converted view types.'
 Write-Output 'PASS .NET 8/.NET 10 C#/VB generation, no-op builds, XAML changes, diagnostics, Clean, opt-out and exclusions.'
} finally { Pop-Location }
