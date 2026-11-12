[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Ffmpeg,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(30, 93)][int]$MinimumDistinctFrames = 30
)

$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -Type Directory -Path $destination -Force | Out-Null
$fixture = Join-Path $destination 'media-fixture.mp4'
& $Ffmpeg -hide_banner -loglevel error -y `
    -f lavfi -i 'color=c=red:s=640x360:r=30:d=1' `
    -f lavfi -i 'color=c=lime:s=640x360:r=30:d=1' `
    -f lavfi -i 'color=c=blue:s=640x360:r=30:d=1' `
    -f lavfi -i 'testsrc2=size=640x360:rate=30:duration=4' `
    -filter_complex '[0:v][1:v][2:v][3:v]concat=n=4:v=1:a=0' `
    -c:v libx264 -pix_fmt yuv420p -movflags +faststart $fixture
if ($LASTEXITCODE -ne 0) { throw "FFmpeg could not generate the Windows media fixture." }

dotnet run --project (Join-Path $PSScriptRoot 'MediaSession.vbproj') -c Release --artifacts-path (Join-Path $destination 'build') -- $fixture $MinimumDistinctFrames
if ($LASTEXITCODE -ne 0) { throw "Windows media session check failed." }
