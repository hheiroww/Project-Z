param([switch]$SkipRuntime)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $repo
try {
    function Invoke-Dotnet([string[]]$Arguments, [int]$Expected = 0) {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne $Expected) { throw "dotnet exited $LASTEXITCODE, expected $Expected : $Arguments" }
    }
    $generator = 'Project Z XAML Codegen/bin/Release/net8.0/ProjectZ.XamlCodegen.dll'
    Invoke-Dotnet @('build', 'Project Z WPF Compatibility/ProjectZ.WpfCompatibility.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-v:q')
    Invoke-Dotnet @('build', 'Project Z XAML Codegen/ProjectZ.XamlCodegen.csproj', '-c', 'Release', '-v:q')
    $linkedSources = Get-ChildItem tests/WpfImport/LinkedViews -File -Recurse
    $linkedHashes = @{}; foreach ($file in $linkedSources) { $linkedHashes[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash }
    foreach ($language in @('cs', 'vb')) {
        $view = "tests/WpfImport/LinkedViews/$language/View.xaml"
        $designer = "tests/WpfImport/LinkedViews/$language/View.Designer.$language"
        $destination = "artifacts/wpf-import/linked-$language"
        Invoke-Dotnet @($generator, 'import-view', $view, '--output', $destination)
        Invoke-Dotnet @($generator, 'import-view', $view, '--designer', $designer, '--output', $destination)
    }
    foreach ($file in $linkedSources) { if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne $linkedHashes[$file.FullName]) { throw "Linked source changed: $file" } }
    $sources = Get-ChildItem tests/WpfImport/CSharp,tests/WpfImport/VisualBasic -File -Recurse | Where-Object FullName -NotMatch '[\\/](obj|bin)[\\/]'
    $before = @{}; foreach ($file in $sources) { $before[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash }
    $import = @($generator, 'import', 'tests/WpfImport/VisualBasic/VisualBasic.vbproj', '--output', 'artifacts/wpf-import/generated')
    Invoke-Dotnet $import
    $manifest = 'artifacts/wpf-import/generated/projectz-import.manifest.json'
    $first = (Get-FileHash $manifest).Hash
    $project = Get-ChildItem artifacts/wpf-import/generated -Recurse -Filter VisualBasic.vbproj | Select-Object -First 1
    $extension = Join-Path $project.DirectoryName 'Extensions/Handwritten.vb'
    New-Item -ItemType Directory -Path (Split-Path $extension) -Force | Out-Null
    Set-Content -LiteralPath $extension -Value "' Preserved extension fixture"
    $extensionHash = (Get-FileHash -LiteralPath $extension).Hash
    Invoke-Dotnet $import
    if ((Get-FileHash $manifest).Hash -ne $first) { throw 'Regeneration is not deterministic.' }
    if ((Get-FileHash $extension).Hash -ne $extensionHash) { throw 'Handwritten extension changed.' }
    foreach ($file in $sources) { if ((Get-FileHash $file.FullName).Hash -ne $before[$file.FullName]) { throw "Source was modified: $file" } }
    $generatedSource = Get-ChildItem artifacts/wpf-import/generated -Recurse -Filter Model.cs | Select-Object -First 1
    $originalBytes = [IO.File]::ReadAllBytes($generatedSource.FullName)
    try {
        [IO.File]::AppendAllText($generatedSource.FullName, "`n// user edit must survive")
        $editedHash = (Get-FileHash $generatedSource.FullName).Hash
        Invoke-Dotnet $import 1
        if ((Get-FileHash $generatedSource.FullName).Hash -ne $editedHash) { throw 'Importer overwrote a modified generated file.' }
    }
    finally { [IO.File]::WriteAllBytes($generatedSource.FullName, $originalBytes) }
    Invoke-Dotnet $import
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/Unsupported/Unsupported.csproj', '--output', 'artifacts/wpf-import/unsupported') 1
    $report = Get-Content artifacts/wpf-import/unsupported/projectz-import.report.json -Raw | ConvertFrom-Json
    if ($report.Success -or 'PZI101' -notin $report.Issues.Code -or 'PZI301' -notin $report.Issues.Code) { throw 'Missing source-located unsupported diagnostics.' }
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/Application/Application.csproj', '--output', 'artifacts/wpf-import/application')
    if (!$SkipRuntime) {
        Invoke-Dotnet @('run', '--project', 'tests/WpfImport/Runtime/Runtime.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false', '--', 'artifacts/wpf-import/runtime-dx12')
        Invoke-Dotnet @('run', '--project', 'tests/WpfImport/Runtime/Runtime.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-p:ProjectZGraphicsBackend=DirectX11', '--', 'artifacts/wpf-import/runtime-dx11')
        # Restore normal DX12 artifacts after the fallback check.
        Invoke-Dotnet @('build', 'tests/WpfImport/Runtime/Runtime.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-v:q')
        $app = Get-ChildItem artifacts/wpf-import/application -Recurse -Filter Application.csproj | Select-Object -First 1
        Invoke-Dotnet @('run', '--project', $app.FullName, '-c', 'Release', '-p:GeneratePackageOnBuild=false')
    }
    Write-Output 'PASS importer regeneration, source preservation, diagnostics, C#/VB compilation and requested runtime checks'
}
finally { Pop-Location }
