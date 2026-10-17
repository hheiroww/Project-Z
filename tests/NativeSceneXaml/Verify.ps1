$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'NativeSceneXaml.csproj'
$markup = Join-Path $PSScriptRoot 'View.xaml'
$original = [IO.File]::ReadAllText($markup)
function Run-Checked([string[]]$arguments) {
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $arguments" }
}
try {
    Run-Checked @('run','--project',$project,'-c','Release','--','Native XAML')
    [IO.File]::WriteAllText($markup,$original.Replace('Value="Native XAML"','Value="Edited in XAML"'))
    Run-Checked @('run','--project',$project,'-c','Release','--','Edited in XAML')
    [IO.File]::WriteAllText($markup,$original.Replace('Factory="Button"','Factory="Missing"'))
    & dotnet build $project -c Release --nologo -v:q
    if ($LASTEXITCODE -eq 0) { throw 'Unknown factory silently accepted.' }
    [IO.File]::WriteAllText($markup,$original.Replace('Property="Width"','Property="DoesNotExist"'))
    & dotnet build $project -c Release --nologo -v:q
    if ($LASTEXITCODE -eq 0) { throw 'Unknown native property silently accepted.' }
} finally { [IO.File]::WriteAllText($markup,$original) }
Run-Checked @('run','--project',$project,'-c','Release','--','Native XAML')
