param(
    [Parameter(Mandatory)][string]$CaptureFolder,
    [Parameter(Mandatory)][string]$OutputFolder,
    [string]$FFmpeg = 'ffmpeg'
)
$ErrorActionPreference = 'Stop'
$capture = (Resolve-Path -LiteralPath $CaptureFolder).Path
if (!(Get-Content -LiteralPath (Join-Path $capture 'capture.txt') -Raw).StartsWith('PASS 150 live frames;')) {
    throw 'Only a complete live capture can be exported.'
}
New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
$output = (Resolve-Path -LiteralPath $OutputFolder).Path
Copy-Item -LiteralPath (Join-Path $capture 'gallery.png') -Destination (Join-Path $output 'heirowsnap-gallery.png')
Copy-Item -LiteralPath (Join-Path $capture 'glass-settings.png') -Destination (Join-Path $output 'heirowsnap-glass.png')
& $FFmpeg -hide_banner -loglevel error -y -framerate 10 -start_number 18 -i (Join-Path $capture 'frame-%04d.png') -frames:v 90 `
    -filter_complex 'scale=1024:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=full:reserve_transparent=0[p];[b][p]paletteuse=dither=bayer:bayer_scale=2:diff_mode=rectangle' `
    -loop 0 (Join-Path $output 'heirowsnap-in-action.gif')
if ($LASTEXITCODE -ne 0) { throw 'GIF encoding failed.' }
Get-ChildItem -LiteralPath $output -File | Where-Object Name -In @('heirowsnap-gallery.png','heirowsnap-glass.png','heirowsnap-in-action.gif') |
    Select-Object Name, Length
