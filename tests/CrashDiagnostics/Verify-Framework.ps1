param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$folder = Join-Path $env:LOCALAPPDATA 'Project-Z\ProjectZ.Diagnostics.Consumer\Diagnostics'
$result = Join-Path $env:TEMP ('projectz-diagnostics-'+[guid]::NewGuid().ToString('N')+'.txt')
function Run-Consumer([string]$argument) {
    $process = Start-Process -FilePath $Executable -ArgumentList $argument -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(20000)) { Stop-Process -Id $process.Id; throw 'Framework diagnostics test timed out.' }
    $deadline = (Get-Date).AddSeconds(5)
    do {
        $observer = Get-ChildItem -LiteralPath $folder -Filter "*-$($process.Id).observer.log" | Select-Object -First 1
        if($observer -and (Get-Content -LiteralPath $observer.FullName -Raw) -match 'Observed process exit') { break }
        Start-Sleep -Milliseconds 50
    } while((Get-Date) -lt $deadline)
    if(-not $observer -or (Get-Content -LiteralPath $observer.FullName -Raw) -notmatch 'Native observer attached') { throw 'Standalone framework observer did not attach.' }
    [pscustomobject]@{Process=$process;Observer=$observer;Session=(Get-ChildItem -LiteralPath $folder -Filter "*-$($process.Id).log" | Select-Object -First 1)}
}
try {
    $normal = Run-Consumer ('"'+$result+'"')
    if($normal.Process.ExitCode -ne 0 -or (Get-Content -LiteralPath $result -Raw) -notmatch '^PASS:') { throw 'Automatic framework startup failed.' }
    $managed = Run-Consumer '--managed'
    $text = (Get-Content -LiteralPath $managed.Session.FullName -Raw) + (Get-Content -LiteralPath $managed.Observer.FullName -Raw)
    if($managed.Process.ExitCode -eq 0 -or $text -notmatch 'Unhandled; terminating=True|FATAL native exception; code=0xE0434352' -or $text -notmatch 'Project-Z inner diagnostic test' -or $text -notmatch 'Program.Main') { throw 'Framework unhandled exception details are missing.' }
    $native = Run-Consumer '--native'
    $text = Get-Content -LiteralPath $native.Observer.FullName -Raw
    if($text -notmatch 'FATAL native exception; code=0xC0000602') { throw 'Framework native fault was not captured.' }
    $dump = [IO.File]::ReadAllBytes([IO.Path]::ChangeExtension($native.Session.FullName,'.dmp'))
    if($dump.Length -lt 1000 -or [BitConverter]::ToUInt32($dump,0) -ne 0x504D444D) { throw 'Framework crash dump is invalid.' }
    if(Test-Path -LiteralPath (Join-Path (Split-Path $Executable) 'ProjectZ.NativeExceptionFixture.dll')) {
        $origin = Run-Consumer '--native-graphics-origin'
        $text = Get-Content -LiteralPath $origin.Observer.FullName -Raw
        if($origin.Process.ExitCode -ne 0 -or $text -notmatch 'Native graphics exception before cleanup.*HRESULT=0x887A0006') { throw 'Original native graphics HRESULT was not captured.' }
        $originDump = [IO.Path]::ChangeExtension($origin.Session.FullName,'.native-origin.dmp')
        if(-not (Test-Path -LiteralPath $originDump)) { throw 'Original native graphics dump is missing.' }
        $originBytes = [IO.File]::ReadAllBytes($originDump)
        if($originBytes.Length -lt 1000 -or [BitConverter]::ToUInt32($originBytes,0) -ne 0x504D444D) { throw 'Original native dump has an invalid signature.' }
        $directory = [BitConverter]::ToUInt32($originBytes,12)
        $exceptionFound = $false
        for($index=0; $index -lt [BitConverter]::ToUInt32($originBytes,8); $index++) {
            $offset = $directory + $index*12
            if([BitConverter]::ToUInt32($originBytes,$offset) -eq 6) {
                $exceptionOffset = [BitConverter]::ToUInt32($originBytes,$offset+8)
                if([BitConverter]::ToUInt32($originBytes,$exceptionOffset+8) -ne [Convert]::ToUInt32('E06D7363',16) -or [BitConverter]::ToUInt32($originBytes,$exceptionOffset+160) -lt 1232) { throw 'Original native exception or x64 context is missing.' }
                $exceptionFound = $true
            }
        }
        if(-not $exceptionFound) { throw 'Original native dump has no exception stream.' }
        Write-Output 'PASS: original native graphics HRESULT and dump are captured before cleanup, while leaving application exception handling intact.'
    }
    $forced = Start-Process -FilePath $Executable -ArgumentList '--force-exit' -WindowStyle Hidden -PassThru
    $deadline = (Get-Date).AddSeconds(10)
    do {
        $observer = Get-ChildItem -LiteralPath $folder -Filter "*-$($forced.Id).observer.log" | Select-Object -First 1
        if($observer -and (Get-Content -LiteralPath $observer.FullName -Raw) -match 'Native observer attached') { break }
        Start-Sleep -Milliseconds 50
    } while((Get-Date) -lt $deadline)
    Stop-Process -Id $forced.Id
    $deadline = (Get-Date).AddSeconds(5)
    do {
        if($observer -and (Get-Content -LiteralPath $observer.FullName -Raw) -match 'Observed process exit') { break }
        Start-Sleep -Milliseconds 50
    } while((Get-Date) -lt $deadline)
    if(-not $observer -or (Get-Content -LiteralPath $observer.FullName -Raw) -notmatch 'Observed process exit') { throw 'Framework forced exit was not logged.' }
    Write-Output 'PASS: independent consumer automatic startup, unhandled stack and inner exception, native crash dump and forced exit; no downstream application references.'
} finally {
    if(Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result }
}
