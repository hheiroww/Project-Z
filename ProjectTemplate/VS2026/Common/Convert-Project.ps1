[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceProject,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$RootDirectory
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $SourceProject).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
$tool = Join-Path $PSScriptRoot 'Tools/ProjectZ.XamlCodegen.dll'
if (!(Test-Path -LiteralPath $tool)) { throw 'Keep the complete ProjectZ directory with this script.' }
$arguments = @($tool, 'import', $source, '--output', $output)
if ($RootDirectory) { $arguments += @('--root-directory', $RootDirectory) }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Project conversion failed. Review $output/projectz-import.report.json and projectz-import.unsupported.txt." }
$report = Get-Content -LiteralPath (Join-Path $output 'projectz-import.report.json') -Raw | ConvertFrom-Json
Write-Host "Converted project: $($report.RootProject)"
Write-Host 'Open the generated project in Visual Studio. Edit the original project and rerun conversion to regenerate it.'
