# Native floating controls

`ComboBox.DropDownBackgroundBlur` is a nullable radius in layout pixels, from 0
through 100. An unset value (`null` in C#, `Nothing` in VB, or `{x:Null}` in
markup) inherits the existing `BackgroundBlur` value. An explicit zero disables
popup blur even when the header has a nonzero background blur. To blur only the
popup, set `BackgroundBlur = 0` and `DropDownBackgroundBlur = 12`.

`DropDownBackgroundColor` tints the popup surface once, including scrollbar
space. Normal rows are transparent; highlights and custom row appearances paint
above that surface. Foreground labels retain native vector text rendering.
Use a translucent tint to keep backdrop blur visible.

The runtime scene parser accepts these values directly and through existing XAML
style setters. Native scene compilation and source extraction preserve blur,
popup tint/highlight/indicator colors, item height and maximum popup height.
The separate WPF compatibility adapter requires its own property forwarding.

```xml
<Style x:Key="PopupTheme" TargetType="ComboBox">
  <Setter Property="BackgroundBlur" Value="0" />
  <Setter Property="DropDownBackgroundBlur" Value="12" />
  <Setter Property="DropDownBackgroundColor" Value="#D210231E" />
  <Setter Property="MaxDropDownHeight" Value="320" />
</Style>
```

The popup and its native rows are retained while closed. `DropDownScrollViewer`
and `DropDownElements` therefore continue to describe the hidden cached popup.
Changing item contents rebuilds the row tree; changing the `ItemAppearance`
delegate rebuilds it so removed custom properties cannot persist. Reopening
reevaluates enabled states and row appearance. Call `RefreshDropDownAppearance`
when an existing delegate's backing data changes while the menu is open.

Opening batches `StackPanel` arrangement. Unchanged frames skip full row
arrangement, hover updates only the previous/current highlight, and offscreen
row descendants skip validation. All entries remain available through wheel,
thumb dragging and keyboard navigation. `StackPanel.DeferArrangement()` supports
nested scopes and performs pending work when the outermost scope is disposed.

## Offscreen checks and measurements

This host creates a never-shown WinForms handle and a DirectX 11 device. It never
calls `Game.Run`, shows a window, activates another application, sends OS input,
or installs native input hooks. Input goes only to its own `SceneManager`.
The public diagnostics opt-out must be present and is applied before a game,
device or manager is created; otherwise the program fails before rendering.
DirectX 12 and OpenGL native-context adapters need separate review; this host
fails explicitly for those backends.

The bounded run covers 90, 160 and 1000 entries, three cold openings per size,
20 warm reopenings and 60 steady, hover and scroll frames by default. The frame
count can be set between 10 and 180; the whole probe has a 120-second deadline.
It requests 16x AA and 16x anisotropic filtering throughout. `measurements.json`
records the loaded backend/graphics assembly/adapter, supported samples, actual
quality/effect surface sample counts and dimensions, quality sampler and last
bound sampler. A hardware fallback must be reported using these observed values.

CPU operation, layout, render submission and current-thread allocations include
p50, p95 and maximum plus sample counts. Three cold samples are a bounded smoke
measurement, not a reliable tail-latency estimate. A one-pixel readback drains
the GPU before and after each timing group, outside individual CPU timings.
The completion batch time includes its final synchronization/readback overhead
and, where indicated, untimed setup; it is not a GPU timestamp measurement.
Arrangement counts start when the current panel can be observed, so cold
construction counts are not inferred from them.

Candidate checks include the actual ComboBox backdrop pixels at radius 0/12/24,
foreground/text sharpness, clipping, effects disable, live backdrop changes,
style/direct properties, null/zero/range validation, retained rows, disabled rows,
wheel/thumb/keyboard navigation, upward opening, viewport/geometry edits and
same-count mutable item changes. PNG readbacks happen outside timing groups.
Existing native XAML and export fixtures independently check compiled appearance
and explicit-zero/null round trips.

## Serialized verification plan

1. Review the source diff and observer opt-out before granting any compiler or
   GPU lane. Do not run the previous visible `Game.Run` host.
2. Build codegen and run `tests/NativeSceneXaml/Verify.ps1` and
   `tests/NativeSceneExport/Verify.ps1` in an isolated export with separate output
   directories; these checks do not create native UI or a graphics device.
3. Export baseline `f1be92f42d9f903a08f0ec033cb2cbd518952db3` into a neutral
   temporary directory, preserving the frozen release checkout. Apply only the
   identical diagnostics opt-out to baseline and candidate. Label that baseline
   overlay explicitly; no layout/render/quality changes belong in it.
4. Build this same test source against each framework root with
   `ProjectZFrameworkRoot`, `ProjectZGraphicsBackend=DirectX11` and separate
   artifact/intermediate paths. Use `UseLocalSocketJack=false`. Run baseline
   with `--baseline --backend DirectX11`, then candidate without `--baseline`,
   each writing to a separate output directory. Run one host at a time.
5. Compare matching counts, cold/warm cases and effective quality. Inspect the
   popup PNGs and retain raw JSON. A build or lower timing alone does not prove
   blur/text acceptance. Extend to other backends only after their never-shown
   native-context host is reviewed.

No measurements or runtime acceptance are implied by adding these test sources.
