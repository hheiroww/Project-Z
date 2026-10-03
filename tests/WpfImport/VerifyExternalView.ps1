param(
    [Parameter(Mandatory)][string]$XamlPath,
    [Parameter(Mandatory)][string]$CodeBehindPath,
    [string]$RootNamespace = 'ExternalView',
    [int]$ExpectedExitCode = 0
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$xaml = (Resolve-Path -LiteralPath $XamlPath).Path
$code = (Resolve-Path -LiteralPath $CodeBehindPath).Path
$vb = $code.EndsWith('.vb', [StringComparison]::OrdinalIgnoreCase)
$extension = if ($vb) { '.vbproj' } else { '.csproj' }
$key = [IO.Path]::GetFileNameWithoutExtension($xaml)
$inputDirectory = Join-Path $repo "artifacts/wpf-import/external-input/$key"
$outputDirectory = Join-Path $repo "artifacts/wpf-import/external-output/$key"
New-Item -ItemType Directory -Path $inputDirectory -Force | Out-Null
$project = Join-Path $inputDirectory ('ExternalView' + $extension)
$escapedXaml = [Security.SecurityElement]::Escape($xaml)
$escapedCode = [Security.SecurityElement]::Escape($code)
$escapedNamespace = [Security.SecurityElement]::Escape($RootNamespace)
$xml = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0-windows</TargetFramework><UseWPF>true</UseWPF><RootNamespace>$escapedNamespace</RootNamespace><EnableDefaultCompileItems>false</EnableDefaultCompileItems><EnableDefaultPageItems>false</EnableDefaultPageItems></PropertyGroup>
  <ItemGroup><Page Include="$escapedXaml" /><Compile Include="$escapedCode" /></ItemGroup>
</Project>
"@
[IO.File]::WriteAllText($project, $xml)
& dotnet run --project (Join-Path $repo 'Project Z XAML Codegen/ProjectZ.XamlCodegen.csproj') -c Release -- import $project --output $outputDirectory
if ($LASTEXITCODE -ne $ExpectedExitCode) { throw "External import returned $LASTEXITCODE; expected $ExpectedExitCode." }
