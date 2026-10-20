$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$target = Join-Path $root ('artifacts/native-export-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $target 'UI') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Program.cs'),(Join-Path $PSScriptRoot 'NativeSceneExport.csproj') -Destination $target
$definitions = Join-Path $target 'UI/Definitions.xaml'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Definitions.xaml') -Destination $definitions
$project = Join-Path $target 'NativeSceneExport.csproj'
[xml]$xml = Get-Content -LiteralPath $project
$import = $xml.CreateElement('Import')
$import.SetAttribute('Project',(Join-Path $root 'Project Z XAML Codegen/ProjectZ.Xaml.targets'))
$xml.Project.AppendChild($import) | Out-Null
$xml.Save($project)
function Run-Checked([string[]]$arguments) { & dotnet @arguments; if($LASTEXITCODE -ne 0) { throw "dotnet failed: $arguments" } }
Run-Checked @('build',(Join-Path $root 'Project Z XAML Codegen/ProjectZ.XamlCodegen.csproj'),'-c','Release','--nologo','-v:q')
$tool = Join-Path $root 'Project Z XAML Codegen/bin/Release/net8.0/ProjectZ.XamlCodegen.dll'
Run-Checked @($tool,'export-native-scene',$target,$definitions)
Run-Checked @('run','--project',$project,'-c','Release')
Run-Checked @($tool,'export-native-objects',$project,(Join-Path $target 'UI'))
Run-Checked @('run','--project',$project,'-c','Release')
