# UI font assets

Run `pwsh -File Resources/Fonts/Rebuild.ps1` from Windows with Segoe UI installed.
The pinned MonoGame content tool rebuilds all eight sizes and updates the four
shipped content roots. heirowSnap and VST hosts link the Application content root.

Use `TextureFormat=Color` and `PremultiplyAlpha=True`: UI glyph coverage needs
full 8-bit alpha and premultiplied RGB for SpriteBatch's AlphaBlend. DXT3 reduces
coverage to 4 bits and compresses the premultiplied RGB separately, damaging small
letter edges. Do not recompress these atlases. Font spacing is zero so the font's
own metrics determine character advances. Bilinear, trilinear, and anisotropic
UI samplers retain linear glyph interpolation; anisotropic filtering and scene
MSAA cannot restore coverage lost in a compressed atlas.
