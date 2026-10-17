param([string]$Executable = 'C:\Program Files\heirow\heirowSnap\heirowSnap.ProjectZ.exe')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = Join-Path $repo ('artifacts/textbox-focus-check-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$previous = $env:PROJECTZ_INPUT_FOCUS_PROBE
try {
    $env:PROJECTZ_INPUT_FOCUS_PROBE = '1'
    $process = Start-Process -FilePath $Executable -ArgumentList @('--desktop-ui-smoke', ('"' + $output + '"')) -PassThru
    if (-not $process.WaitForExit(30000)) {
        Stop-Process -Id $process.Id
        throw 'The native textbox-focus check timed out.'
    }
    $result = Get-Content -LiteralPath (Join-Path $output 'result.txt') -Raw
    if ($process.ExitCode -ne 0 -or -not $result.StartsWith('PASS')) { throw $result }
    $result.Trim()
    "Caret previews: $output"
} finally {
    $env:PROJECTZ_INPUT_FOCUS_PROBE = $previous
}
