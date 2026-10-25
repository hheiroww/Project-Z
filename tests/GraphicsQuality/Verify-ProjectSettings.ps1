[CmdletBinding()]
param(
    [ValidateSet('DirectX12', 'DirectX11', 'OpenGL')]
    [string[]]$Backends = @('DirectX12'),
    [string]$OutputDirectory,
    [int]$TimeoutSeconds = 30,
    [switch]$SkipRuntime
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repository 'artifacts/quality-project-settings' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$results = [Collections.Generic.List[string]]::new()

function Build-Probe([string]$Project, [string]$Log, [string[]]$Properties, [switch]$ExpectFailure) {
    & dotnet build $Project -c Release -m:1 -nodeReuse:false -p:UseSharedCompilation=false -p:BuildInParallel=false -p:GeneratePackageOnBuild=false -p:ProjectZDiagnosticsPrepared=true --nologo -v:minimal @Properties *> $Log
    $buildExitCode = $LASTEXITCODE
    if ($ExpectFailure) {
        if ($buildExitCode -eq 0 -or (Get-Content -LiteralPath $Log -Raw) -notmatch 'ProjectZ(AntiAliasingSamples|TextureFiltering)') {
            throw "Invalid project setting was not rejected: $Log"
        }
    } elseif ($buildExitCode -ne 0) {
        Get-Content -LiteralPath $Log -Tail 30
        throw "Project settings fixture build failed: $Log"
    }
}

foreach ($backend in $Backends) {
    $buildReferences = $true
    foreach ($language in @('CSharp', 'VisualBasic')) {
        $extension = if ($language -eq 'CSharp') { 'csproj' } else { 'vbproj' }
        $project = Join-Path $PSScriptRoot "ProjectSettings/$language/$language.$extension"
        $program = Join-Path $PSScriptRoot "ProjectSettings/$language/bin/Release/net8.0-windows7.0/$language.dll"
        # Reuse each project's output directory: 0 -> 8 -> defaults must update
        # generated metadata during ordinary incremental builds.
        foreach ($case in @(
            @{ Name = 'off'; Samples = 0; Filtering = 'Bilinear'; Properties = @('-p:ProjectZAntiAliasingSamples=0', '-p:ProjectZTextureFiltering=Bilinear') },
            @{ Name = 'eight'; Samples = 8; Filtering = 'Anisotropic2x'; Properties = @('-p:ProjectZAntiAliasingSamples=8', '-p:ProjectZTextureFiltering=Anisotropic2x', '-p:GenerateAssemblyInfo=false') },
            @{ Name = 'default'; Samples = 4; Filtering = 'Anisotropic16x'; Properties = @() }
        )) {
            $name = "$language-$backend-$($case.Name)"
            $buildProperties = @("-p:ProjectZGraphicsBackend=$backend") + $case.Properties
            if (!$buildReferences) { $buildProperties += '-p:BuildProjectReferences=false' }
            Build-Probe $project "$OutputDirectory/$name.build.log" $buildProperties
            $buildReferences = $false
            $report = Join-Path $OutputDirectory "$name.txt"
            if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
            $probeArguments = @(('"' + $program + '"'))
            if ($SkipRuntime) { $probeArguments += '--metadata' }
            $probeArguments += @($case.Samples, $case.Filtering, ('"' + $report + '"'))
            $runtime = Start-Process dotnet -ArgumentList $probeArguments -WorkingDirectory ([IO.Path]::GetDirectoryName($program)) -WindowStyle Hidden -PassThru -RedirectStandardOutput "$OutputDirectory/$name.stdout.log" -RedirectStandardError "$OutputDirectory/$name.stderr.log"
            try {
                if (!$runtime.WaitForExit($TimeoutSeconds * 1000)) {
                    Stop-Process -Id $runtime.Id
                    throw "Project settings fixture exceeded its runtime deadline: $name"
                }
                $runtime.Refresh()
                if ($runtime.ExitCode -ne 0 -or !(Test-Path -LiteralPath $report) -or (Get-Content -LiteralPath $report -Raw) -notmatch '^PASS ') {
                    if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report }
                    Get-Content -LiteralPath "$OutputDirectory/$name.stderr.log" -Tail 20
                    throw "Project settings runtime verification failed: $name"
                }
            } finally {
                if (!$runtime.HasExited) { Stop-Process -Id $runtime.Id }
                $runtime.Dispose()
            }
            $results.Add((Get-Content -LiteralPath $report -Raw))
            Write-Output $results[$results.Count - 1]
        }
        foreach ($invalid in @('-p:ProjectZAntiAliasingSamples=3', '-p:ProjectZTextureFiltering=Invalid')) {
            $kind = if ($invalid.Contains('AntiAliasing')) { 'AA' } else { 'Filtering' }
            Build-Probe $project "$OutputDirectory/$language-$backend-invalid-$kind.build.log" @("-p:ProjectZGraphicsBackend=$backend", '-p:BuildProjectReferences=false', $invalid) -ExpectFailure
            $results.Add("PASS $language $backend rejects invalid $kind project settings")
        }
    }
}
$results | Set-Content -LiteralPath "$OutputDirectory/results.txt"
# Expected invalid-setting builds leave dotnet's exit code nonzero. A successful
# verification must return success when invoked directly by a CI PowerShell host.
exit 0
