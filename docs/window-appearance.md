# Shared window appearance

`ProjectZ.Windows.Composition.WindowAppearance` owns the blur, background opacity, and palette for an application's windows. Use `WindowAppearance.Default` or create a controller for an independent group. The controller copies settings and updates existing bindings when `Update` is called. `WindowAppearanceSettings` supports JSON serialization and explicit `Save` / `Load`.

```csharp
var appearance = WindowAppearance.Default;
appearance.Update(new WindowAppearanceSettings {
    DwmBlurEnabled = true,
    BackgroundOpacity = 50,
    AccentColor = Microsoft.Xna.Framework.Color.MediumSeaGreen
});
using var binding = appearance.Bind(hwnd, settings => {
    scene.BackgroundColor = settings.Background(baseBackgroundColor);
});
```

Create, update and dispose bindings on the window UI thread. Bind each new HWND, including secondary editors and popups. Pass `resizable: false` for fixed popups. Background opacity applies to the scene background; controls and text retain their own opacity.

The [shader editor](SHADER-EDITOR.md) follows the active controller's `BackgroundColor`, `SurfaceColor`, `TextColor`, and `AccentColor` while it is open.

For a renderer with its own per-pixel alpha presenter, pass `perPixelPresentation: true`. The binding makes the renderer's owner HWND transparent and supplies a passive DWM companion through `binding.Backdrop`. Call `binding.Tick()` with the host's window update. Dispose the binding when its window is destroyed; hiding the owner hides the companion automatically.

Bindings also install `BorderlessWindowChrome`, which preserves client dimensions when Windows adds resize styles and provides native edge/corner hit tests and resize cursors. A layered presentation HWND should forward non-client hit-test, pointer and `WM_SETCURSOR` messages to its owner. Caption dragging and application commands stay under host control.

When rendering logical UI units into a differently sized native target, set `Scene.PresentationScale` before drawing. Project-Z applies that scale during vector rasterization and antialiasing. Present the resulting client-sized pixels directly, avoiding a second nearest-neighbor resize of text.
