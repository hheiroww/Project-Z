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
  if(!$SkipRuntime) { Run @('run','--project','tests/WpfImport/BuildTime/Runtime/Runtime.csproj','-c','Release','-p:GeneratePackageOnBuild=false',"-p:ProjectZTestTargetFramework=$framework") }
 }
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
 Write-Output 'PASS .NET 8/.NET 10 C#/VB generation, no-op builds, XAML changes, diagnostics, Clean, opt-out and exclusions.'
} finally { Pop-Location }
