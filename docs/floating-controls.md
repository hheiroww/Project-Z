# Floating controls

`SceneElement.BackgroundBlur` sets the backdrop blur radius in layout pixels (0–100, default 0). The renderer samples content already drawn behind the element, applies the GPU blur, then draws the element's background and foreground normally. Text and child controls remain sharp. Rectangle elements retain their corner radii, ancestor clipping and opacity. Rendering-quality settings can disable effects or scale the blur radius.

```csharp
var menu = new ContextMenu(scene) {
    BackgroundBlur = 12,
    BackgroundColor = new Color(18, 28, 25, 210),
    CornerRadius = 8
};
```

`Popup` and `ContextMenu` inherit this property. `ComboBox` also forwards its `BackgroundBlur` to the floating dropdown.

ComboBox dropdowns use a `ScrollViewer` with a vertical scrollbar when needed. `MaxDropDownHeight` defaults to 320 layout pixels; the popup also fits the viewport and opens above the header when that provides more room. Mouse wheel, scrollbar dragging, arrows, Home/End and Page Up/Down reach the entire list. Opening the popup reveals the current selection. `DropDownScrollViewer` exposes the active popup for inspection.

Run the native GPU acceptance checks on Windows:

```powershell
dotnet run --project tests/FloatingControls/FloatingControls.csproj -c Release -p:ProjectZGraphicsBackend=DirectX11 -p:GeneratePackageOnBuild=false -- artifacts/floating-controls
dotnet run --project tests/ComboBoxInput/ComboBoxInput.csproj -c Release -p:ProjectZGraphicsBackend=DirectX11 -p:GeneratePackageOnBuild=false -- artifacts/combobox-input
```
