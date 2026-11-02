#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'Project Z DesktopGL/Shaders'
New-Item -ItemType Directory -Force $output | Out-Null
Push-Location $root
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'MonoGame tool restore failed.' }
    foreach ($shader in @('XamlEffects', 'FXAA')) {
        dotnet tool run mgfxc -- "Resources/$shader.fx" "$output/$shader.mgfx" /Profile:OpenGL /Defines:OPENGL,PROJECTZ_OPENGL
        if ($LASTEXITCODE -ne 0) { throw "OpenGL shader compilation failed: $shader" }
    }
    $lines = @('<Project>', '  <ItemGroup>')
    foreach ($path in @('Resources/XamlEffects.fx', 'Resources/FXAA.fx', '.config/dotnet-tools.json', 'Project Z DesktopGL/Shaders/XamlEffects.mgfx', 'Project Z DesktopGL/Shaders/FXAA.mgfx')) {
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        $lines += '    <ProjectZShaderInput Include="$(ProjectZRepositoryRoot)' + $path + '" ExpectedHash="' + $hash + '" />'
    }
    $lines += @('  </ItemGroup>', '</Project>')
    Set-Content -LiteralPath (Join-Path $output 'ShaderInputs.props') -Value $lines -Encoding utf8
} finally { Pop-Location }
