param([switch]$SkipRuntime)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $repo
try {
    function Invoke-Dotnet([string[]]$Arguments, [int]$Expected = 0) {
        if ($Arguments[0] -eq 'build') { $Arguments += @('-m:1', '-nodeReuse:false', '-p:UseSharedCompilation=false', '-p:BuildInParallel=false') }
        elseif ($Arguments[0] -eq 'run') { $Arguments = @($Arguments[0], '-p:UseSharedCompilation=false', '-p:BuildInParallel=false') + $Arguments[1..($Arguments.Length - 1)] }
        & dotnet @Arguments
        if ($LASTEXITCODE -ne $Expected) { throw "dotnet exited $LASTEXITCODE, expected $Expected : $Arguments" }
    }
    $generator = 'Project Z XAML Codegen/bin/Release/net8.0/ProjectZ.XamlCodegen.dll'
    Invoke-Dotnet @('build', 'Project Z WPF Compatibility/ProjectZ.WpfCompatibility.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-v:q')
    Invoke-Dotnet @('build', 'Project Z XAML Codegen/ProjectZ.XamlCodegen.csproj', '-c', 'Release', '-v:q')
    Invoke-Dotnet @('run', '--project', 'tests/WpfImport/HeadlessMarkup/HeadlessMarkup.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-p:DeployVst3OnBuild=false')
    $nativeCodegenSources = Get-ChildItem tests/WpfImport/NativeCodegen/Original -File
    $nativeCodegenHashes = @{}; foreach ($file in $nativeCodegenSources) { $nativeCodegenHashes[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash }
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/NativeCodegen/Original/Original.csproj', '--output', 'artifacts/wpf-import/native-codegen', '--root-directory', 'stable-entry')
    $nativeCodegenProject = (Get-Content artifacts/wpf-import/native-codegen/projectz-import.report.json -Raw | ConvertFrom-Json).RootProject
    if ((Split-Path (Split-Path $nativeCodegenProject) -Leaf) -ne 'stable-entry') { throw 'Stable root project directory was not generated.' }
    Invoke-Dotnet @('build', $nativeCodegenProject, '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-v:q')
    Invoke-Dotnet @('build', 'tests/WpfImport/NativeCodegen/WindowsBuild/WindowsBuild.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-v:q')
    foreach ($file in $nativeCodegenSources) { if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne $nativeCodegenHashes[$file.FullName]) { throw "Native codegen source changed: $file" } }
    Write-Output 'PASS native appearance and object items compile through project import and Windows DesignerAndCodeBehind without source changes'
    $controlsCodegenSources = Get-ChildItem tests/WpfImport/ControlsCodegen/Original -File | ForEach-Object { [pscustomobject]@{ Path = $_.FullName; Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } }
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/ControlsCodegen/Original/Original.csproj', '--output', 'artifacts/wpf-import/controls-codegen', '--root-directory', 'stable-entry')
    $controlsCodegenReport = Get-Content artifacts/wpf-import/controls-codegen/projectz-import.report.json -Raw | ConvertFrom-Json
    if (!$controlsCodegenReport.Success -or $controlsCodegenReport.EmptyControls.Count -eq 0) { throw 'Native controls or empty-control mapping failed.' }
    Invoke-Dotnet @('build', $controlsCodegenReport.RootProject, '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-v:q')
    Invoke-Dotnet @('build', 'tests/WpfImport/ControlsCodegen/WindowsBuild/WindowsBuild.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-v:q')
    foreach ($source in $controlsCodegenSources) { if ((Get-FileHash -LiteralPath $source.Path -Algorithm SHA256).Hash -ne $source.Hash) { throw 'Controls codegen changed original fixture source.' } }
    Write-Output 'PASS automatic native controls dialogs vector typography and empty-control mapping in both codegen modes'
    $browserRun = Invoke-Dotnet @('run', '--project', 'tests/WpfImport/WebViewDependency/WebViewDependency.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false') | Out-String
    if ($browserRun -notmatch 'WEBVIEW_DEPENDENCY_OK') { throw 'Compatible direct WebView2 SDK pin failed.' }
    Write-Output 'PASS compatible direct WebView2 pin and installed native browser runtime'
    & (Join-Path $PSScriptRoot 'WebViewEvents/Verify.ps1') -SkipRuntime:$SkipRuntime
    if(!$SkipRuntime) {& (Join-Path $PSScriptRoot 'NativeWindows/Verify.ps1')}
    Invoke-Dotnet @('run', '--project', 'tests/WpfImport/Dispatcher/Dispatcher.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false')
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/DispatcherImport/DispatcherImport.csproj', '--output', 'artifacts/wpf-import/dispatcher-import')
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/GridImport/GridImport.csproj', '--output', 'artifacts/wpf-import/grid-import')
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/TextLayout/TextLayout.csproj', '--output', 'artifacts/wpf-import/text-layout')
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/StackLayout/StackLayout.csproj', '--output', 'artifacts/wpf-import/stack-layout')
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
    Invoke-Dotnet @('build', 'tests/WpfImport/BinaryWpf/BinaryWpf.csproj', '-c', 'Release', '-v:q')
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/BinaryConsumer/BinaryConsumer.csproj', '--output', 'artifacts/wpf-import/binary-consumer') 1
    $binaryIssues = (Get-Content artifacts/wpf-import/binary-consumer/projectz-import.report.json -Raw | ConvertFrom-Json).Issues
    if (!($binaryIssues | Where-Object { $_.Code -eq 'PZI201' -and $_.Message -match 'DerivedWidget' })) { throw 'Indirect binary WPF inheritance was missed.' }
    foreach ($name in @('Elements', 'Array', 'Windows')) {
        if (!($binaryIssues | Where-Object { $_.Code -eq 'PZI202' -and $_.Message -match $name })) { throw "Binary WPF container contract was missed: $name" }
    }
    Write-Output 'PASS binary WPF contracts include Microsoft packages arrays generics nested types and inherited controls'
    $report = Get-Content artifacts/wpf-import/unsupported/projectz-import.report.json -Raw | ConvertFrom-Json
    if ($report.Success -or 'PZI101' -notin $report.Issues.Code -or 'PZI301' -notin $report.Issues.Code) { throw 'Missing source-located unsupported diagnostics.' }
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/Application/Application.csproj', '--output', 'artifacts/wpf-import/application')
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/AsyncApplication/AsyncApplication.csproj', '--output', 'artifacts/wpf-import/async-application')
Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/NonUiLibrary/NonUiLibrary.csproj', '--output', 'artifacts/wpf-import/nonui-library')
Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/CodeCreatedUi/CodeCreatedUi.csproj', '--output', 'artifacts/wpf-import/code-created-ui')
Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/ExecutableIdentity/ExecutableIdentity.csproj', '--output', 'artifacts/wpf-import/executable-identity')
Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/NativeAppearance/NativeAppearance.csproj', '--output', 'artifacts/wpf-import/native-appearance')
Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/DeploymentItems/DeploymentItems.csproj', '--output', 'artifacts/wpf-import/deployment-items')
$path = Get-ChildItem -Path "artifacts/wpf-import/deployment-items" -Filter "DeploymentItems.csproj" -Recurse | Select-Object -ExpandProperty FullName -First 1
if (-not $path) { throw "DeploymentItems.csproj not found." }

$output = dotnet run --project $path --configuration Release 2>&1
if ($LASTEXITCODE -ne 0) { throw "Dotnet run failed with exit code $LASTEXITCODE" }
if ((-join $output) -notmatch "DEPLOYMENT_RESOURCE_OK") { throw "DEPLOYMENT_RESOURCE_OK not found in output." }
Invoke-Dotnet @('publish', $path, '-c', 'Release', '-o', 'artifacts/wpf-import/deployment-publish')
$publishDir = "artifacts/wpf-import/deployment-publish"
$buildOut = Join-Path (Split-Path -Path $path -Parent) "bin\Release\net8.0"
    $buildConfig = Join-Path $buildOut "config/live.txt"
    $buildExternal = Join-Path $buildOut "data/external.txt"
    $pubData = Join-Path $publishDir "data/external.txt"

    if (-not (Test-Path $buildConfig) -or (Test-Path $buildExternal)) { throw "Build output validation failed." }
    if (-not (Test-Path $pubData) -or (Test-Path (Join-Path $publishDir "config/live.txt"))) { throw "Publish output validation failed." }
$pair1 = @($buildConfig, "tests/WpfImport/DeploymentItems/assets/output.txt")
$pair2 = @($pubData, "tests/WpfImport/SharedAssets/deployment-linked.txt")
$nativeResourceSource = 'tests/WpfImport/DeploymentItems/assets/native-resource.txt'
$pair3 = @((Join-Path $buildOut 'assets/native-resource.txt'), $nativeResourceSource)
$pair4 = @((Join-Path $publishDir 'assets/native-resource.txt'), $nativeResourceSource)

foreach ($pair in @($pair1, $pair2, $pair3, $pair4)) {
    $hash1 = (Get-FileHash -Algorithm SHA256 $pair[0]).Hash
    $hash2 = (Get-FileHash -Algorithm SHA256 $pair[1]).Hash

    if ($hash1 -ne $hash2) {
        throw "Hash mismatch detected for pair: $($pair[0]) vs $($pair[1])"
    }
}

Write-Output "PASS deployment metadata output publish and payload hashes."
# Regression: CompilerOptions Implicit Usings & x64 Execution
$compilerProjPath = "tests/WpfImport/CompilerOptions/CompilerOptions.csproj"
Invoke-Dotnet @($generator, "import", $compilerProjPath, "--output", "artifacts/wpf-import/compiler-options")

$generatedProjectFile = Get-ChildItem -Path "artifacts/wpf-import/compiler-options" -Filter "CompilerOptions.csproj" -Recurse | Select-Object -ExpandProperty FullName | Where-Object { $_ -ne $compilerProjPath } | Select-Object -First 1
if (-not $generatedProjectFile) { throw "Generated CompilerOptions.csproj not found in artifacts/wpf-import/" }

$output = Invoke-Dotnet @("run", "--project", $generatedProjectFile, "-c", "Release") 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { throw "CompilerOptions run failed with exit code $LASTEXITCODE" }
if ($output -notmatch "COMPILER_OPTIONS_OK") { throw "Output did not contain marker COMPILER_OPTIONS_OK." }

Write-Host "PASS compiler implicit usings and x64 execution."
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/BuildTargetGraph/BuildTargetGraph.csproj', '--output', 'artifacts/wpf-import/build-target-graph')
    $graphProject = Get-ChildItem artifacts/wpf-import/build-target-graph -Recurse -Filter BuildTargetGraph.csproj | Select-Object -First 1
    $graphXml = [xml](Get-Content -LiteralPath $graphProject.FullName -Raw)
    $graphPackage = @($graphXml.Project.ItemGroup.PackageReference) | Where-Object Include -eq 'System.Text.Json'
    if ($graphPackage.PrivateAssets -ne 'all' -or $graphPackage.ExcludeAssets -ne 'analyzers') { throw 'Package asset contract was not preserved.' }
    $graphRun = Invoke-Dotnet @('run', '--project', $graphProject.FullName, '-c', 'Release') | Out-String
    if ($graphRun -notmatch 'BUILD_TARGET_GRAPH_OK') { throw 'Build target worker or Release hash failed.' }
    $graphOutput = Join-Path $graphProject.DirectoryName 'custom-out/Release/BuildTargetGraph.dll'
    if (!(Test-Path -LiteralPath $graphOutput)) { throw 'Custom output path or framework suffix contract was not preserved.' }
    Invoke-Dotnet @('publish', $graphProject.FullName, '-c', 'Release', '-o', 'artifacts/wpf-import/build-target-publish')
    $graphPublishedRun = Invoke-Dotnet @('artifacts/wpf-import/build-target-publish/BuildTargetGraph.dll') | Out-String
    if ($graphPublishedRun -notmatch 'BUILD_TARGET_GRAPH_OK') { throw 'Published build target contract failed.' }
    Write-Output 'PASS build-only project graph custom targets package metadata Release hash and publish'
    Invoke-Dotnet @($generator, 'import', 'tests/WpfImport/SdkWeb/SdkWeb.csproj', '--output', 'artifacts/wpf-import/sdk-web')
    $webProject = Get-ChildItem artifacts/wpf-import/sdk-web -Recurse -Filter SdkWeb.csproj | Select-Object -First 1
    $webRun = Invoke-Dotnet @('run', '--project', $webProject.FullName, '-c', 'Release') | Out-String
    if ($webRun -notmatch 'SDK_WEB_OK') { throw 'Original Web SDK behavior was not preserved.' }
    Write-Output 'PASS original Web SDK host and evaluated global usings'
    & (Join-Path $PSScriptRoot 'StartupProperties/Verify.ps1') -SkipRuntime:$SkipRuntime
    if (!$SkipRuntime) {
        Invoke-Dotnet @('run', '--project', 'tests/WpfImport/Runtime/Runtime.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false', '--', 'artifacts/wpf-import/runtime-dx12')
        Invoke-Dotnet @('run', '--project', 'tests/WpfImport/Runtime/Runtime.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-p:ProjectZGraphicsBackend=DirectX11', '--', 'artifacts/wpf-import/runtime-dx11')
        # Restore normal DX12 artifacts after the fallback check.
        Invoke-Dotnet @('build', 'tests/WpfImport/Runtime/Runtime.csproj', '-c', 'Release', '-p:GeneratePackageOnBuild=false', '-v:q')
        $app = Get-ChildItem artifacts/wpf-import/application -Recurse -Filter Application.csproj | Select-Object -First 1
        Invoke-Dotnet @('run', '--project', $app.FullName, '-c', 'Release', '-p:GeneratePackageOnBuild=false')
        $asyncApp = Get-ChildItem artifacts/wpf-import/async-application -Recurse -Filter AsyncApplication.csproj | Select-Object -First 1
        $asyncRun = Invoke-Dotnet @('run', '--project', $asyncApp.FullName, '-c', 'Release', '-p:GeneratePackageOnBuild=false') | Out-String
        if ($asyncRun -notmatch 'ASYNC_APP_READY') { throw 'Async startup or explicit shutdown failed.' }
        Write-Output 'PASS native asynchronous startup empty-window lifetime and forced shutdown'
    }
    Write-Output 'PASS importer regeneration, source preservation, diagnostics, C#/VB compilation and requested runtime checks'
}
finally { Pop-Location }
