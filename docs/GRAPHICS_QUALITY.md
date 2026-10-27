# Graphics quality

In Project-Z **2026.10.25.2** and later, choose **Project Properties > Project-Z > Rendering quality** and rebuild. The defaults are **4x anti-aliasing** and **16x anisotropic filtering**. Both apply to native `Game`/`SceneManager` applications, including desktop and XAML templates, without assignments in `GameWindow` or generated WPF startup.

The same build settings can be set in the application project:

```xml
<PropertyGroup>
  <ProjectZAntiAliasingSamples>4</ProjectZAntiAliasingSamples>
  <ProjectZTextureFiltering>Anisotropic16x</ProjectZTextureFiltering>
</PropertyGroup>
```

`ProjectZAntiAliasingSamples` accepts `0`, `2`, `4`, `8` or `16`; `0` disables AA. `ProjectZTextureFiltering` accepts `Bilinear`, `Trilinear`, `Anisotropic2x`, `Anisotropic4x`, `Anisotropic8x` or `Anisotropic16x`. The package writes these settings into the application assembly at build time. Each graphics device reads them once before its first scene renders. Later runtime changes remain effective when another scene or manager uses the same device.

For an existing DX12 template app, update `ProjectZFrameworkVersion` in `ProjectZ/ProjectZ.Graphics.props` to `2026.10.25.2` and rebuild. A direct package consumer can update its `ProjectZ` package reference instead. The package supplies the quality build target even when the app retains an older copied `ProjectZ.Application.targets`. DX11/OpenGL apps also need the matching updated `ProjectZ/Runtime/<renderer>/ProjectZ.dll` from the new template. Remove manual quality assignments when the project properties should control rendering.

heirowSnap's **Settings > Graphics** provides Off, 2x, 4x, 8x and 16x AA,
plus bilinear, trilinear, and anisotropic 2x/4x/8x/16x filtering. Save applies
the choices across the device's windows and persists them in the Project-Z
settings file. Defaults are 4x AA and 16x AF in heirowSnap.

The native DX12 backend uses spatial supersampling (SSAA), with 2x1, 2x2,
4x2, and 4x4 subpixel grids and an exact box resolve. This also samples glyph
and image coverage. It costs additional GPU memory and fill rate. MonoGame
3.8.5.1's DX12 render-target creation ignores its multisample argument, so
merely requesting MSAA there does not antialias the result.

The DX11 fallback uses hardware MSAA. The effective count comes from an actual
render-target allocation, not just the adapter's permissive format query.
Unsupported selections display their effective value. On the tested GPU,
16x MSAA falls back to 8x. DX12's 16x SSAA performs all sixteen samples.

DirectX sampler anisotropy is limited to 16; there is no 32x AF setting.
Bilinear uses linear minification/magnification and point mip selection;
trilinear also blends mip levels. Their difference is visible on mipmapped
textures. Default text uses compiled free vector outlines and samples no atlas.
See [font generation](../ThirdParty/ProjectZSans/README.md).

Applications can configure their graphics device without the settings UI:

```vb
Dim quality = GraphicsQuality.ForDevice(GraphicsDevice)
quality.AntiAliasingSamples = 4
quality.Filtering = TextureFiltering.Anisotropic16x
```

Build/run the native pixel regression host:

```powershell
dotnet run --project tests/GraphicsQuality -c Release -p:ProjectZGraphicsBackend=DirectX12 -- artifacts/quality-dx12
dotnet run --project tests/GraphicsQuality -c Release -p:ProjectZGraphicsBackend=DirectX11 -- artifacts/quality-dx11
```

It checks actual fractional triangle coverage, caller render-target restoration,
direct and offscreen scenes, every filter, compiled vector text, and blur/clipping
at each AA level. Each run writes PNG captures and `result.txt`.

Verify that C# and VB project properties reach native rendering without an initial code override:

```powershell
./tests/GraphicsQuality/Verify-ProjectSettings.ps1 -Backends DirectX12,DirectX11,OpenGL
```

This checks default 4x, explicit off and 8x settings, filtering, incremental builds, invalid values, and projects with `GenerateAssemblyInfo=false`. It also checks fractional triangle coverage and that later runtime overrides survive another `SceneManager`. Use `-SkipRuntime` for build and assembly-metadata checks on machines without an interactive graphics session.
