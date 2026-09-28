param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,

    [Parameter(Mandatory = $true)]
    [string]$DestinationDirectory,

    [switch]$WaitForFlStudio
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
        }
    }
    finally {
        $mutex.ReleaseMutex()
        $mutex.Dispose()
    }
    exit 0
}

Copy-Bundle $source $pending

if (Get-Process -Name FL64 -ErrorAction SilentlyContinue) {
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -SourceDirectory "{1}" -DestinationDirectory "{2}" -WaitForFlStudio' -f `
        $PSCommandPath, $source, $destination
    Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -ArgumentList $arguments
    Write-Output "Project Z Granulizer staged; deployment will finish when FL Studio exits."
}
else {
    Remove-LegacyEffect
    Copy-Bundle $pending $destination
    Write-Output "Project Z Granulizer deployed to $destination"
}
