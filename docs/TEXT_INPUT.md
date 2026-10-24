# Vector text and text selection

Project-Z renders its free default ProjectZ Sans from curves and metrics compiled
into ProjectZ.VectorText.dll. There are no TTF/OTF/WOFF files, font installation,
or bitmap atlases. It uses Inter 4.1 outlines under SIL OFL 1.1, renamed for the
converted representation; keep ThirdParty/ProjectZSans/OFL.txt with distributions.
LibTessDotNet converts the curves, including counters/holes, to GPU triangles.
Windows DX12/DX11 and the Linux KNI renderer share the compiled outline data.
Layout, caret positioning, hit testing and selection use the same font metrics.
The selected AA setting smooths the geometry.

Choose a default size without external files:

```vb
content.LoadVectorFontsOnly()
textbox.Font = Fonts.ProjectZSans.GetResourceName(12)
```

Regular, semibold and bold have separate outlines. The resolver accepts old
`SegoeUI_12` names as aliases for the free default, and ignores retired Segoe UI
atlases left in older content folders. Standard Latin, Greek and Cyrillic text
uses the compiled metrics, kerning and NFC accent normalization. This is not an
OpenType shaper: Windows retains installed host shaping for complex/uncovered
scripts; Linux uses the missing-glyph outline for uncovered characters.

Windows callers can still explicitly register an additional licensed TTF or OTF:

```vb
content.LoadVectorFont("EditorFont", "C:\Fonts\MyFont.ttf", 12)
textbox.Font = "EditorFont"
```

Sizes are in points (96/72 pixels per point at the framework's logical scale).
Explicit custom keys can still use caller-supplied SpriteFont assets. Defaults
load even when Content/Fonts does not exist. Reproducible generation and upstream
provenance are in [ProjectZ Sans](../ThirdParty/ProjectZSans/README.md).

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
