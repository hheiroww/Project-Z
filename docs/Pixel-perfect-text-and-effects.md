# Vector text pixel alignment and effect controls

`TextElement.PpiPerfectText` is a nullable per-element override. `Nothing` inherits
`GraphicsQuality.ForDevice(device).PpiPerfectText` (or the font's opt-in).
It is also accepted as a native XAML `TextElement`/`TextBlock` attribute.

The vector renderer uses display-mode shaped advances and rounds the final text
origin to output pixels before applying supersampling or effect-target offsets.
Display/ideal geometry and measurements have separate bounded cache keys. Caret
and selection geometry use the same measurement mode. It keeps curves antialiased;
it does not round every outline vertex or replace vectors with a bitmap atlas.
`VectorFont.AlignToPixelGrid(position, pixelsPerUnit)` is available to hosts that
lay out in units other than output pixels. Native Project-Z scenes use output
pixel coordinates; the internal SSAA scale is not a monitor's DPI scale.

Device quality also exposes `EffectsEnabled`, `BlurRadiusScale` (0–3),
`ShadowOpacityScale` (0–2), `ChromaticAberrationScale` (0–3), and
`ShaderMotionSpeed` (0–3). These modify per-visual render instances and preserve
the original animated descriptors. Effective blur padding expands with the
requested radius. Disabling visual effects preserves rotation compositing.
`TextureFiltering.NearestNeighbor` selects a point sampler without changing the
numeric values of existing filtering choices.

heirowSnap's native GPU smoke checks compare rendered images at fractional text
origins with alignment enabled/disabled, toggle a chromatic effect, verify blur
padding, and inspect the point sampler. These supplement the vector-font resolver
checks; they require the Windows DX12 runtime.
