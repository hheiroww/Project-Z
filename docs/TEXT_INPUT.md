# Vector text and text selection

Windows Project-Z now renders its default Segoe UI text from the installed
`segoeui.ttf`. WPF supplies text shaping and font outlines; LibTessDotNet converts
the outlines, including counters/holes, into triangles rendered by the existing
DX12/DX11 graphics device. The output does not sample a glyph bitmap atlas.
Layout, caret positioning, hit testing and selection use the same font metrics.
The selected AA setting smooths the geometry.

Register an additional TTF or OTF before creating controls:

```vb
content.LoadVectorFont("EditorFont", "C:\Fonts\MyFont.ttf", 12)
textbox.Font = "EditorFont"
```

Sizes are in points (96/72 pixels per point at the framework's logical scale).
Unknown font keys continue to use existing SpriteFont assets. If the default
Windows font file is absent, the shipped bitmap fonts remain the fallback.

Textboxes support drag selection in either direction, scrolling while dragging
outside the input, Shift+click, Shift+arrows, Home/End, Ctrl+Home/End, Ctrl+word
navigation, and Ctrl+Shift word selection. Wrapped and multiline selections draw
separate clipped highlight rows. Typing, Backspace, Delete and paste replace the
selected range, producing one text-change notification per edit.

Clipboard shortcuts: Ctrl+A/C/X/V, Ctrl+Insert, Shift+Insert and Shift+Delete.
Copy/cut do not export masked values; cut removes text only after a successful
clipboard copy. Pasting into a single-line input replaces newlines with spaces.
Public APIs include `Select`, `SelectAll`, `SelectedText`, `CopySelection`,
`CutSelection`, `PasteClipboard`, `ReplaceSelection` and `SelectionBounds`.

Run the native regression host (an STA entry point is required for the Windows
clipboard):

```powershell
dotnet run --project tests/TextSelection -c Release -- artifacts/selection-dx12
dotnet run --project tests/TextSelection -c Release -p:ProjectZGraphicsBackend=DirectX11 -- artifacts/selection-dx11
```
