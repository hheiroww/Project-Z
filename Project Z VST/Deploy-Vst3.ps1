param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$DestinationDirectory,

    [Parameter(Mandatory = $true)]
    [ValidateSet('DirectX12', 'DirectX11')]
    [string]$GraphicsBackend,

    [switch]$WaitForFlStudio,

    [switch]$ElevatedChild
)

$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\')
$destination = [IO.Path]::GetFullPath($DestinationDirectory).TrimEnd('\')
$allowedRoot = [IO.Path]::GetFullPath('C:\Program Files\Common Files\VST3').TrimEnd('\')
$legacyEffect = [IO.Path]::GetFullPath('C:\Program Files\Common Files\VST3\Project Z Granulizer').TrimEnd('\')
$flPluginDatabase = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Image-Line\FL Studio\Presets\Plugin database'
$legacyEffectRecords = @(
    (Join-Path $flPluginDatabase 'Effects\Fx-Generator\Project Z Granulizer.fst'),
    (Join-Path $flPluginDatabase 'Installed\Effects\VST3\Project Z Granulizer.fst')
)
$pending = Join-Path $env:LOCALAPPDATA 'ProjectZ\PendingVst3'

if (!(Test-Path -LiteralPath $source -PathType Container)) {
    throw "VST3 build output does not exist: $source"
}
if (!($destination.Equals($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -or
      $destination.StartsWith($allowedRoot + '\', [StringComparison]::OrdinalIgnoreCase))) {
    throw "Refusing to deploy outside the VST3 root: $destination"
}

function Copy-Bundle([string]$from, [string]$to) {
    New-Item -ItemType Directory -Path $to -Force | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $from -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $to -Recurse -Force
    }
}

function Remove-DeprecatedDependencies([string]$root, [string]$backend) {
    # These files were shipped by older Project Z bundles and must not survive
    # an in-place upgrade after their package references have been removed.
    foreach ($name in @(
        'Triangle.dll',
        'AssimpNet.dll',
        'Kni.Platform.dll',
        'Xna.Framework.Audio.dll',
        'Xna.Framework.Content.dll',
        'Xna.Framework.Devices.dll',
        'Xna.Framework.dll',
        'Xna.Framework.Game.dll',
        'Xna.Framework.Graphics.dll',
        'Xna.Framework.Input.dll',
        'Xna.Framework.Media.dll',
        'Xna.Framework.Storage.dll',
        'Xna.Framework.XR.dll'
    )) {
        $candidate = Join-Path $root $name
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            [IO.File]::Delete($candidate)
        }
    }
    if ($backend -eq 'DirectX12') {
        foreach ($candidate in Get-ChildItem -LiteralPath $root -Filter 'SharpDX*.dll' -File -ErrorAction SilentlyContinue) {
            [IO.File]::Delete($candidate.FullName)
        }
    }
}

function Remove-LegacyEffect {
    if (!($legacyEffect.StartsWith($allowedRoot + '\', [StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing to remove a legacy plugin outside the VST3 root: $legacyEffect"
    }
    if (Test-Path -LiteralPath $legacyEffect -PathType Container) {
        [IO.Directory]::Delete($legacyEffect, $true)
    }
    foreach ($record in $legacyEffectRecords) {
        if (Test-Path -LiteralPath $record -PathType Leaf) {
            [IO.File]::Delete($record)
        }
    }
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Start-DeploymentProcess([bool]$waitForFl, [bool]$elevate, [bool]$waitForExit) {
    $waitArgument = if ($waitForFl) { ' -WaitForFlStudio' } else { '' }
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -SourceDirectory "{1}" -DestinationDirectory "{2}" -GraphicsBackend "{3}"{4} -ElevatedChild' -f `
        $PSCommandPath, $source, $destination, $GraphicsBackend, $waitArgument
    $startArguments = @{
        FilePath = 'powershell.exe'
        ArgumentList = $arguments
        WindowStyle = 'Hidden'
        PassThru = $true
    }
    if ($elevate) {
        $startArguments.Verb = 'RunAs'
    }
    if ($waitForExit) {
        $startArguments.Wait = $true
    }
    $process = Start-Process @startArguments
    if ($waitForExit -and $process.ExitCode -ne 0) {
        throw "Elevated VST3 deployment failed with exit code $($process.ExitCode)."
    }
}

$isAdministrator = Test-IsAdministrator
if ($ElevatedChild -and !$isAdministrator) {
    throw 'The elevated VST3 deployment process did not receive administrator rights.'
}

if ($WaitForFlStudio) {
    $mutex = [Threading.Mutex]::new($false, 'Global\ProjectZGranulizerVst3Deployment')
    if (!$mutex.WaitOne(0)) {
        exit 0
    }
    try {
        while (Get-Process -Name FL64 -ErrorAction SilentlyContinue) {
            Start-Sleep -Seconds 1
        }
        Remove-LegacyEffect
        if (Test-Path -LiteralPath $pending -PathType Container) {
            Copy-Bundle $pending $destination
            Remove-DeprecatedDependencies $destination $GraphicsBackend
        }
    }
    finally {
        $mutex.ReleaseMutex()
        $mutex.Dispose()
    }
    exit 0
}

Copy-Bundle $source $pending
Remove-DeprecatedDependencies $pending $GraphicsBackend

if (Get-Process -Name FL64 -ErrorAction SilentlyContinue) {
    Start-DeploymentProcess -waitForFl $true -elevate (!$isAdministrator) -waitForExit $false
    Write-Output "Project Z Granulizer staged; deployment will finish when FL Studio exits."
}
else {
    if (!$isAdministrator) {
        Start-DeploymentProcess -waitForFl $false -elevate $true -waitForExit $true
    }
    else {
        Remove-LegacyEffect
        Copy-Bundle $pending $destination
        Remove-DeprecatedDependencies $destination $GraphicsBackend
        Write-Output "Project Z Granulizer deployed to $destination"
    }
}
