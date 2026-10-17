param([switch]$SkipRuntime)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$evidence = Join-Path $repository 'artifacts/wpf-import'
$sourcePaths = @('Original/Original.csproj','Original/View.xaml','Original/View.xaml.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$before = @{}; foreach ($path in $sourcePaths) { $before[$path] = (Get-FileHash -LiteralPath $path).Hash }
function Invoke-Checked([string[]]$Arguments, [string]$Log) {
    & dotnet @Arguments *> (Join-Path $evidence $Log)
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath (Join-Path $evidence $Log) -Tail 30; throw "dotnet failed with exit code $LASTEXITCODE" }
}
$build = @('-c','Release','-m:1','-nodeReuse:false','-p:UseSharedCompilation=false','-p:BuildInParallel=false','-p:GeneratePackageOnBuild=false','-p:DeployVst3OnBuild=false','-v:q')
Push-Location $repository
try {
    New-Item -ItemType Directory -Path $evidence -Force | Out-Null
    Invoke-Checked (@('build','tests/WpfImport/HeadlessMarkup/HeadlessMarkup.csproj') + $build) 'webview-events-headless-build.log'
    Invoke-Checked @('tests/WpfImport/HeadlessMarkup/bin/Release/net8.0-windows7.0/HeadlessMarkup.dll') 'webview-events-headless.log'
    Invoke-Checked (@('build','Project Z XAML Codegen/ProjectZ.XamlCodegen.csproj') + $build) 'webview-events-generator-build.log'
    & (Join-Path $PSScriptRoot '../VerifySourcePaths.ps1')
    Invoke-Checked @('restore','tests/WpfImport/WebViewEvents/Original/Original.csproj','-v:q') 'webview-events-original-restore.log'
    Invoke-Checked @('Project Z XAML Codegen/bin/Release/net8.0/ProjectZ.XamlCodegen.dll','import','tests/WpfImport/WebViewEvents/Original/Original.csproj','--output','artifacts/wpf-import/webview-events','--root-directory','stable-entry') 'webview-events-fixture-import.log'
    $report = Get-Content -Raw -LiteralPath (Join-Path $evidence 'webview-events/projectz-import.report.json') | ConvertFrom-Json
    if (!$report.Success -or $report.EmptyControls.Count -ne 0) { throw 'Browser or event fixture was not translated.' }
    $translatedSource=Get-Content (Join-Path $evidence 'webview-events/stable-entry/Source/View.xaml.cs') -Raw
    if ([regex]::Matches($translatedSource,'(?m)^using ProjectZ\.WpfCompatibility\.Controls;').Count -ne 1) { throw 'Converging browser and WPF imports were not deduplicated.' }
    Invoke-Checked (@('build','tests/WpfImport/WebViewEvents/WindowsBuild/WindowsBuild.csproj') + $build) 'webview-events-windows-build.log'
    if (!$SkipRuntime) {
        Invoke-Checked (@('build','tests/WpfImport/WebViewEvents/Runtime/Runtime.csproj') + $build) 'webview-events-runtime-build.log'
        Invoke-Checked @('tests/WpfImport/WebViewEvents/Runtime/bin/Release/net8.0-windows/Runtime.dll','artifacts/wpf-import/webview-events-runtime') 'webview-events-runtime.log'
        if ((Get-Content -Raw -LiteralPath (Join-Path $evidence 'webview-events-runtime.log')) -notmatch 'WEBVIEW_EVENTS_RUNTIME_OK') { throw 'Native browser and input checks did not complete.' }
    }
    foreach ($path in $sourcePaths) { if ((Get-FileHash -LiteralPath $path).Hash -ne $before[$path]) { throw "Original fixture source changed: $path" } }
    Write-Output 'PASS WebView2 HWND hosting and routed event translation without source edits in both codegen modes'
}
finally { Pop-Location }
