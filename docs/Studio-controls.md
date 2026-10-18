# Native studio controls

The `ProjectZ.Shared.Drawing.UI.Input` namespace includes reusable `RotaryKnob`, `DragNumberBox`, `VerticalSlider`, `DecibelMeter`, `CollectionScrollBar` and `AccentButton` controls. These are native scene elements and render through the normal GPU pipeline; no application renderer or XAML interpreter is required.

Value controls expose `MinimumValue`, `MaximumValue`, `StepSize`, `Value` and `ValueChanged`. Knobs use a 270-degree min/max arc and vertical dragging. Sliders map their vertical track to the range. Numeric inputs accept invariant decimal text, vertical dragging, visible step arrows, Up/Down and mouse wheel; invalid text restores the previous value. Shift slows value dragging. Knobs and sliders also support Home/End. Values reject nonfinite numbers and clamp to the range.

`DecibelMeter.ColorAt` uses the explicitly requested scale: -48 dB red, -24 dB yellow, 0 dB green. Its vertical fill is the only semantic gradient in these controls.

`CollectionScrollBar` exposes `ItemCount`, `VisibleItems`, `FirstIndex` and `OffsetChanged`. Its thumb represents the visible fraction. Bind an application's collection slice to `FirstIndex`; route the list's wheel events through `Scroll(delta)`. Thumb dragging and wheel scrolling share the same clamped row offset.

`AccentButton` uses the existing native drop-shadow shader for an on-demand hover/focus glow. It keeps solid state colors and disposes cached shader resources with the element.

Build-time native XAML can construct these types directly:

```xml
<ObjectFactory x:Name="CreateGainKnob" Type="ProjectZ.Shared.Drawing.UI.Input.RotaryKnob">
  <Argument Type="ProjectZ.Shared.Drawing.Scene" Value="{Expression this}" />
  <Set Property="Position" Type="Vector2" Value="80,100" />
  <Set Property="Size" Type="Vector2" Value="80,80" />
  <Set Property="MinimumValue" Type="double" Value="-48" />
  <Set Property="MaximumValue" Type="double" Value="0" />
  <Set Property="StepSize" Type="double" Value="0.1" />
</ObjectFactory>
```

Attach the created element through the owning scene's usual layout/binding path. A native XAML `Factory` with `Attach` also supports `AutoCreate` for entirely declarative additions. Edits are compiled by `ProjectZ.Xaml.Build` during an ordinary build; no runtime markup file is required.
