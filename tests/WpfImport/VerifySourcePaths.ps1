$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
# Keep the generator replaceable when later checks rebuild it in this process.
$assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $repository 'Project Z XAML Codegen/bin/Release/net8.0/ProjectZ.XamlCodegen.dll')))
$importer = $assembly.GetType('ProjectImporter', $true)
$method = $importer.GetMethod('SourceRelative', [Reflection.BindingFlags]'Static,NonPublic')
$directory = 'D:\a\Project-Z\Project-Z\tests\WpfImport\WebViewEvents\Original'
$destination = 'D:\a\Project-Z\Project-Z\artifacts\wpf-import\webview-events'
$cases = @(
    @{ Directory = $directory; Path = "$directory\Views\View.xaml"; Linked = $false },
    @{ Directory = $directory; Path = 'D:\shared\WebView2Loader.dll'; Linked = $true },
    @{ Directory = $directory; Path = 'C:\Users\runneradmin\.nuget\packages\microsoft.web.webview2\1.0.3124.44\runtimes\win-x64\native\WebView2Loader.dll'; Linked = $true },
    @{ Directory = '\\server\source\Original'; Path = '\\server\packages\WebView2Loader.dll'; Linked = $true }
)
foreach ($case in $cases) {
    $relative = $method.Invoke($null, @($case.Directory, $case.Path))
    $target = [IO.Path]::GetFullPath([IO.Path]::Combine($destination, 'stable-entry', 'Assets', $relative))
    if ([IO.Path]::IsPathRooted($relative) -or !$target.StartsWith($destination + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Source path escaped destination: $($case.Path) -> $target" }
    if ($case.Linked -and $relative -notmatch '^Linked[\\/][0-9A-F]{8}[\\/]WebView2Loader\.dll$') { throw "External asset was not relocated: $relative" }
    if (!$case.Linked -and $relative -ne 'Views\View.xaml') { throw "Local source layout changed: $relative" }
    if ($relative -ne $method.Invoke($null, @($case.Directory, $case.Path))) { throw 'Source path relocation is not deterministic.' }
}
Write-Output 'PASS importer source paths stay inside the destination across drives and UNC shares'
