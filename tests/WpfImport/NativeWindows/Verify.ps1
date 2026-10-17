param([int]$TimeoutSeconds=30)
$ErrorActionPreference='Stop'
$repository=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$evidence=Join-Path $repository 'artifacts/wpf-import'
Push-Location $repository
try {
    New-Item -ItemType Directory -Path $evidence -Force | Out-Null
    & dotnet build (Join-Path $PSScriptRoot 'NativeWindows.csproj') -c Release -m:1 -nodeReuse:false -p:UseSharedCompilation=false -p:BuildInParallel=false -p:GeneratePackageOnBuild=false -p:DeployVst3OnBuild=false -v:q *> (Join-Path $evidence 'continuation-windows-build.log')
    if($LASTEXITCODE -ne 0) {Get-Content (Join-Path $evidence 'continuation-windows-build.log') -Tail 20;throw 'Native window fixture build failed.'}
    $fixture=Join-Path $PSScriptRoot 'bin/Release/net8.0-windows7.0/NativeWindows.dll'
    $runtime=Start-Process dotnet -ArgumentList @('"'+$fixture+'"') -WorkingDirectory $repository -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $evidence 'continuation-windows-runtime.log') -RedirectStandardError (Join-Path $evidence 'continuation-windows-runtime.err.log')
    try {
        if(!$runtime.WaitForExit($TimeoutSeconds*1000)) {Stop-Process -Id $runtime.Id;throw 'Native window fixture exceeded its runtime deadline.'}
        $runtime.Refresh()
        if($runtime.ExitCode -ne 0) {Get-Content (Join-Path $evidence 'continuation-windows-runtime.err.log') -Tail 20;throw "Native window fixture failed with exit code $($runtime.ExitCode)."}
        if((Get-Content -LiteralPath (Join-Path $evidence 'continuation-windows-runtime.log') -Raw) -notmatch 'NATIVE_WINDOWS_READY') {throw 'Native window fixture did not complete all checks.'}
    }
    finally {if(!$runtime.HasExited) {Stop-Process -Id $runtime.Id};$runtime.Dispose()}
    Write-Output 'PASS native imported windows, rendering, owned animation clocks, item selection and closing events'
}
finally {Pop-Location}
