# Select and copy read-only text

Project-Z 2.8.1 adds selection to display text without making it editable.

## Display text

```xml
<TextBlock Text="Select this message to copy it"
           IsTextSelectionEnabled="True" Width="320" />
```

For a native `TextElement`, set `IsTextSelectionEnabled = True` in code. Users can drag in either direction, extend a selection with Shift-click or Shift+Left/Right, select all with Ctrl+A, and copy with Ctrl+C or Ctrl+Insert. Home/End move to the beginning/end of the text. Right-click offers **Copy** and **Select all**, with no Cut or Paste.

Selection follows the rendered lines, font scale and text alignment. Copying uses the original text, preserving spaces and line breaks rather than inserting the display's soft wraps. Changed text clears the old selection; assigning the same value preserves it. Display text never accepts typing or pasted text.

This is a Project-Z extension for native markup. For existing WPF source passed through the WPF importer, use a read-only TextBox as shown below.

## Read-only text boxes

```xml
<TextBox Text="A value supplied by the application" IsReadOnly="True" />
```

`IsReadOnly` prevents user edits through typing, Delete/Backspace, Cut, Paste and native text-input delivery. It also hides Cut and Paste from the menu. Code can still assign `Text` when the application receives a new value. Password controls retain their copy restrictions.

## Enable display selection across imported native views

Set `SceneXamlParser.TextSelectionEnabled = True` before parsing a view to opt in its display text. Text inside buttons and other interactive controls stays non-selectable by default, and `IsHitTestVisible="False"` remains respected. An individual native text block can override the default with `IsTextSelectionEnabled`.

The local heirowSnap port enables this for chat message bodies, clipboard display views and tool-window labels. Editable composers remain editable.

## Validation

`tests/TextSelection` exercises drag direction, keyboard selection, the real Windows clipboard, right-click Copy, blocked read-only edits, source whitespace and rendered highlights. Run it from the repository root:

```powershell
dotnet run --project tests/TextSelection -c Release -- C:\Temp\ProjectZ-TextSelection
```

Small fixes use patch increments (`2.8.1`, `2.8.2`, ...); larger feature releases increment the minor version.
