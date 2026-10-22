# ToggleSwitch

`ProjectZ.Shared.Drawing.UI.Input.ToggleSwitch` is a native two-state slider.
It uses Project-Z scene elements for its rounded track, sliding thumb, label,
hover state, and keyboard focus indicator; it does not host a system checkbox.

```vb
Dim previews As New ToggleSwitch(scene) With {
    .Content = "Show previews",
    .IsChecked = True,
    .Size = New Microsoft.Xna.Framework.Vector2(152, 28)
}
AddHandler previews.CheckedChanged, Sub(enabled) SavePreviewPreference(enabled)
scene.AddElement(previews)
```

Runtime scene XAML and generated scene fields recognize the standard control:

```xml
<ToggleSwitch x:Name="Previews" Content="Show previews" IsChecked="True"
              Width="152" Height="28" OnTrackColor="#1FA388"
              OffTrackColor="#314448" ThumbColor="#EBF8F5" />
```

- `IsChecked` is Boolean; `CheckedChanged(isChecked As Boolean)` fires once per
  actual state change, including programmatic changes. Assign the initial value
  before attaching persistence handlers when constructing a settings view.
- `Content` and inherited `Text` both set the optional label. Font and foreground
  color use the standard Button properties. An empty label makes a standalone
  switch; give the control at least the requested switch width and height.
- Click the switch or label to toggle. Drag the switch horizontally to choose
  a side; releasing commits one value. Cancelling or losing capture reverts the
  uncommitted drag. Disabled controls do not accept gestures or keyboard input.
- Tab focuses the control. Space/Enter toggle through the standard Button
  activation path; Left/Home select off and Right/End select on.
- `OnTrackColor`, `OffTrackColor`, `ThumbColor`, and `FocusColor` customize the
  palette. `SwitchWidth` and `SwitchHeight` default to 36 and 20 logical pixels;
  actual geometry is clamped to the control bounds. `AnimationDuration` defaults
  to 120 ms; set it to `TimeSpan.Zero` for immediate state changes.

The scene parser previously treated `ToggleSwitch` as a `ToggleButton` alias.
That name now constructs the slider while retaining `IsChecked` and
`CheckedChanged`; `ToggleButton` retains its existing presentation.
