$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repoRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Content tool restore failed.' }
    dotnet mgcb /@:Resources/Fonts/Fonts.mgcb /workingDir:Resources/Fonts /rebuild
    if ($LASTEXITCODE -ne 0) { throw 'Font atlas build failed.' }

    # Keep all shipped content roots identical; consumers also link the Application root.
    foreach ($project in @('Project Z Application', 'Project Z Linux Test', 'Project Z Tower Defense', 'Project Z Video FX')) {
        Copy-Item -LiteralPath (Get-ChildItem -LiteralPath 'artifacts/fonts' -Filter '*.xnb').FullName `
            -Destination (Join-Path $repoRoot "$project/Content/Fonts/Segoe UI")
    }
} finally {
    Pop-Location
}
