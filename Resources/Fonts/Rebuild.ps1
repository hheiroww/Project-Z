# Compatibility entry point: defaults are compiled vector outlines, never bitmap atlases.
param([Parameter(Mandatory = $true)][string]$CacheDirectory)
$ErrorActionPreference = 'Stop'
$generator = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../tools/generate-vector-font.py'))
python $generator --cache $CacheDirectory
if ($LASTEXITCODE -ne 0) { throw 'Compiled vector outline generation failed. See ThirdParty/ProjectZSans/README.md.' }
