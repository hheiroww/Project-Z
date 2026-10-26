# Rendering quality checks

Run this GPU fixture with both supported renderers:

```powershell
dotnet run --project tests/GraphicsQuality -c Release -p:GeneratePackageOnBuild=false -p:ProjectZGraphicsBackend=DirectX11 -- artifacts/quality-dx11
dotnet run --project tests/GraphicsQuality -c Release -p:GeneratePackageOnBuild=false -p:ProjectZGraphicsBackend=DirectX12 -- artifacts/quality-dx12
```

The fixture checks edge coverage, filtering, offscreen rendering, effect placement,
blur and clipping. Hover/leave checks activate a native effect on one control and
compare geometry elsewhere in the scene. Requested sampling changes from off to
4x to 16x and back to 4x without changing control bounds. Static edge coverage must
remain within one 8-bit resolve rounding level; enabling a hover effect must not
remove antialiasing. The effect's actual input target must follow sampling changes.

MSAA is retained while rasterizing both the scene's effect canvas and individual
effect inputs. Shader scratch passes remain single-sample. SSAA retains its scaled
coordinates without also allocating multisampled targets. PNGs and results are
written only to the chosen output directory.
