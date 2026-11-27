param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [string]$OutputDirectory,
    [string]$SecondExecutable
)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
if ($SecondExecutable) {
    $SecondExecutable = (Resolve-Path -LiteralPath $SecondExecutable).Path
} else {
    $SecondExecutable = $Executable
}
if ($OutputDirectory) {
    $folder = (New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName
    $resultDirectory = $folder
} else {
    $folder = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Project-Z\ProjectZ.Diagnostics.Consumer\Diagnostics'
    $resultDirectory = $env:TEMP
}
$testId = [guid]::NewGuid().ToString('N')
$result = Join-Path $resultDirectory "projectz-diagnostics-$testId.txt"

function Wait-ObserverLog([Diagnostics.Process]$process, [string]$message, [int]$timeoutSeconds = 10) {
    $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
    do {
        if (Test-Path -LiteralPath $folder) {
            $observer = Get-ChildItem -LiteralPath $folder -Filter "*-$($process.Id).observer.log" |
                Sort-Object Name -Descending | Select-Object -First 1
            if ($observer -and (Get-Content -LiteralPath $observer.FullName -Raw) -match $message) { return $observer }
        }
        Start-Sleep -Milliseconds 50
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Observer log for application $($process.Id) did not contain '$message'."
}

function Run-Consumer([string]$argument) {
    $process = Start-Process -FilePath $Executable -ArgumentList $argument -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(20000)) { Stop-Process -Id $process.Id; throw 'Framework diagnostics test timed out.' }
    $observer = Wait-ObserverLog $process 'Observed process exit' 5
    if((Get-Content -LiteralPath $observer.FullName -Raw) -notmatch 'Native observer attached') { throw 'Standalone framework observer did not attach.' }
    $session = $observer.FullName -replace '\.observer\.log$', '.log'
    [pscustomobject]@{Process=$process;Observer=$observer;Session=(Get-Item -LiteralPath $session)}
}

function Get-AttachedObserver([Diagnostics.Process]$application) {
    $entry = Wait-ObserverLog $application 'Native observer attached'
    $text = Get-Content -LiteralPath $entry.FullName -Raw
    $match = [regex]::Match($text, '\| pid=(\d+) thread=\d+ \| Native observer attached')
    if (-not $match.Success) { throw "Observer PID missing for application $($application.Id)." }
    $process = Get-Process -Id ([int]$match.Groups[1].Value)
    if ($process.ProcessName -ne 'ProjectZ.Diagnostics') { throw "Unexpected observer process '$($process.ProcessName)'." }
    return $process
}

function Complete-LifetimeConsumer([Diagnostics.Process]$application, [string]$signal) {
    [IO.File]::WriteAllText($signal, '')
    if ((-not $application.WaitForExit(10000)) -or $application.ExitCode -ne 0) {
        throw "Application $($application.Id) did not exit normally after receiving its signal."
    }
    $null = Wait-ObserverLog $application 'Observed process exit.*code=0x00000000' 5
}

function Test-SharedObserver {
    $firstSignal = Join-Path $resultDirectory "projectz-diagnostics-$testId-first.exit"
    $secondSignal = Join-Path $resultDirectory "projectz-diagnostics-$testId-second.exit"
    $restartSignal = Join-Path $resultDirectory "projectz-diagnostics-$testId-restart.exit"
    $observerPaths = @(
        Join-Path (Split-Path -Parent $Executable) 'ProjectZ.Diagnostics.exe'
        Join-Path (Split-Path -Parent $SecondExecutable) 'ProjectZ.Diagnostics.exe'
    )
    $first = $second = $restart = $null
    $observer = $secondObserver = $restartedObserver = $null
    try {
        $first = Start-Process -FilePath $Executable -ArgumentList ('--wait-for-exit-signal "'+$firstSignal+'"') -WindowStyle Hidden -PassThru
        $second = Start-Process -FilePath $SecondExecutable -ArgumentList ('--wait-for-exit-signal "'+$secondSignal+'"') -WindowStyle Hidden -PassThru
        $observer = Get-AttachedObserver $first
        $secondObserver = Get-AttachedObserver $second
        if ($observer.Id -ne $secondObserver.Id) { throw 'Concurrent Project-Z applications started different observers.' }
        $running = @(Get-Process -Name 'ProjectZ.Diagnostics' -ErrorAction SilentlyContinue | Where-Object { $_.Path -in $observerPaths })
        if ($running.Count -ne 1 -or $running[0].Id -ne $observer.Id) {
            throw 'The fixture paths did not have exactly one active ProjectZ.Diagnostics executable.'
        }

        Complete-LifetimeConsumer $first $firstSignal
        if ($observer.HasExited -or $second.HasExited) { throw 'The first exit interrupted the observer or the second application.' }
        Complete-LifetimeConsumer $second $secondSignal
        if (-not $observer.WaitForExit(5000)) { throw 'The observer stayed running after the last application exited.' }

        $restart = Start-Process -FilePath $Executable -ArgumentList ('--wait-for-exit-signal "'+$restartSignal+'"') -WindowStyle Hidden -PassThru
        $restartedObserver = Get-AttachedObserver $restart
        Complete-LifetimeConsumer $restart $restartSignal
        if (-not $restartedObserver.WaitForExit(5000)) { throw 'The restarted observer stayed running after the application exited.' }
        Write-Output 'PASS: simultaneous applications share one observer, which stays through the first exit, stops after the last exit, and starts again on demand.'
    } finally {
        foreach ($application in @($first, $second, $restart)) {
            if ($application) {
                if (-not $application.HasExited) {
                    $application.Kill()
                    $null = $application.WaitForExit(5000)
                }
                $application.Dispose()
            }
        }
        foreach ($process in @($observer, $secondObserver, $restartedObserver)) {
            if ($process) { $process.Dispose() }
        }
    }
}

$previousOutputDirectory = $env:PROJECTZ_DIAGNOSTICS_TEST_DIRECTORY
try {
    $env:PROJECTZ_DIAGNOSTICS_TEST_DIRECTORY = if ($OutputDirectory) { $folder } else { $null }
    Test-SharedObserver
    $normal = Run-Consumer ('"'+$result+'"')
    if($normal.Process.ExitCode -ne 0 -or (Get-Content -LiteralPath $result -Raw) -notmatch '^PASS:') { throw 'Framework diagnostics startup failed.' }
    $churn = Run-Consumer '--handle-churn'
    $churnText = Get-Content -LiteralPath $churn.Observer.FullName -Raw
    if($churn.Process.ExitCode -ne 0 -or $churnText -notmatch 'Observed process exit.*code=0x00000000' -or $churnText -match 'Native observer failed|FATAL') { throw 'Debugger handle churn corrupted a process or event handle.' }
    Write-Output 'PASS: debugger survives thread and handle churn without closing Windows-owned debug handles.'
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
    $forcedObserver = $null
    try {
        try {
            $forcedObserver = Get-AttachedObserver $forced
        } finally {
            if (-not $forced.HasExited) { $forced.Kill() }
        }
        $null = Wait-ObserverLog $forced 'Observed process exit' 5
        if (-not $forcedObserver.WaitForExit(5000)) { throw 'The observer stayed running after the last application was terminated.' }
    } finally {
        $forced.Dispose()
        if ($forcedObserver) { $forcedObserver.Dispose() }
    }
    if ($OutputDirectory) { Write-Output 'PASS: framework diagnostics started with an explicit output directory.' }
    else { Write-Output 'PASS: the standalone consumer started diagnostics automatically through SceneManager.' }
    Write-Output 'PASS: unhandled stack and inner exception, native crash dump, and forced exit.'
} finally {
    $env:PROJECTZ_DIAGNOSTICS_TEST_DIRECTORY = $previousOutputDirectory
}
