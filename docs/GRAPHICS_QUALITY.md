# Graphics quality

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
textures. Font atlases have a single mip level and lossless premultiplied
8-bit coverage. See [font generation](../Resources/Fonts/README.md).

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
direct and offscreen scenes, every filter, font alpha precision, and blur/clipping
at each AA level. Each run writes PNG captures and `result.txt`.
