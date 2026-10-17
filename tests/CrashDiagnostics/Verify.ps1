param([Parameter(Mandatory=$true)][string]$Executable, [switch]$IncludeGraphicsLifecycle)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$logs = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'heirowSnap/Diagnostics'
function Run-Diagnostic([string]$Mode) {
    $probe = Start-Process -FilePath $Executable -ArgumentList $Mode -WindowStyle Hidden -PassThru
    if (-not $probe.WaitForExit(20000)) {
        Stop-Process -Id $probe.Id -Force
        throw "Diagnostic did not finish: $Mode"
    }
    $probe.Refresh()
    $entries = @(Get-ChildItem -LiteralPath $logs -Filter "*-$($probe.Id)*.log")
    if ($entries.Count -ne 2) { throw "Missing application/observer log: $Mode" }
    $text = ($entries | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
    if ($text -notmatch 'Native observer attached' -or $text -notmatch 'Observed process exit') { throw 'Observer did not capture process lifetime' }
    [pscustomobject]@{Process=$probe;Entries=$entries;Text=$text}
}
$managed = Run-Diagnostic '--diagnostics-managed-check'
if ($managed.Process.ExitCode -ne 1 -or $managed.Text -notmatch 'Inner diagnostic test' -or $managed.Text -notmatch 'Program.Run') { throw 'Managed exception/inner exception/stack missing' }
$native = Run-Diagnostic '--diagnostics-native-check'
if ($native.Text -notmatch 'FATAL native exception; code=0xC0000602' -or $native.Text -notmatch 'Crash dump saved') { throw 'Native crash dump failed' }
$session = $native.Entries | Where-Object Name -notlike '*.observer.log'
$dump = [IO.Path]::ChangeExtension($session.FullName, '.dmp')
$reader = [IO.BinaryReader]::new([IO.File]::OpenRead($dump))
try {
    if ($reader.ReadUInt32() -ne 0x504D444D) { throw 'Invalid minidump signature' }
    $reader.BaseStream.Position = 8
    $count = $reader.ReadUInt32()
    $directory = $reader.ReadUInt32()
    $found = $false
    for ($index=0; $index -lt $count; $index++) {
        $reader.BaseStream.Position = $directory + $index * 12
        $type = $reader.ReadUInt32()
        $size = $reader.ReadUInt32()
        $rva = $reader.ReadUInt32()
        if ($type -eq 6) {
            $reader.BaseStream.Position = $rva + 8
            if ($reader.ReadUInt32() -ne [Convert]::ToUInt32('C0000602',16)) { throw 'Wrong exception in dump' }
            $reader.BaseStream.Position = $rva + 160
            if ($reader.ReadUInt32() -lt 1232) { throw 'Missing x64 exception context' }
            $found = $true
        }
    }
    if (-not $found) { throw 'Missing exception stream' }
} finally { $reader.Dispose() }
$forced = Start-Process -FilePath $Executable -ArgumentList '--diagnostics-exit-check' -WindowStyle Hidden -PassThru
$deadline = (Get-Date).AddSeconds(10)
do {
    $entry = Get-ChildItem -LiteralPath $logs -Filter "*-$($forced.Id).observer.log" | Select-Object -First 1
    if ($entry -and (Get-Content -LiteralPath $entry.FullName -Raw) -match 'Native observer attached') { break }
    Start-Sleep -Milliseconds 50
} while ((Get-Date) -lt $deadline)
Stop-Process -Id $forced.Id -Force
$deadline = (Get-Date).AddSeconds(5)
do {
    if ($entry -and (Get-Content -LiteralPath $entry.FullName -Raw) -match 'Observed process exit') { break }
    Start-Sleep -Milliseconds 50
} while ((Get-Date) -lt $deadline)
if (-not $entry -or (Get-Content -LiteralPath $entry.FullName -Raw) -notmatch 'Observed process exit') { throw 'Forced termination was not logged' }
Write-Output 'PASS: managed stack and inner exception; native dump signature, exception code and x64 context; externally forced process exit.'
if ($IncludeGraphicsLifecycle) {
    $output = Join-Path $PSScriptRoot 'lifecycle-result.txt'
    $probe = Start-Process -FilePath $Executable -ArgumentList ('--graphics-lifecycle-check "'+$output+'"') -WindowStyle Hidden -PassThru
    while (-not $probe.WaitForExit(30000)) { Write-Output "Graphics lifecycle running; PID=$($probe.Id)" }
    $probe.Refresh()
    if ($probe.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw 'Graphics lifecycle failed; inspect Diagnostics logs' }
    Get-Content -LiteralPath $output
}
