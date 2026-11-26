using System.Globalization;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace ProjectZ.StudioScenes;

public sealed partial class StudioSceneDialog
{
    private static readonly Color Background = new(15, 18, 25);
    private static readonly Color Surface = new(24, 30, 41);
    private static readonly Color SurfaceDeep = new(18, 23, 33);
    private static readonly Color SurfaceHover = new(38, 50, 64);
    private static readonly Color Border = new(48, 60, 76);
    private static readonly Color Accent = new(134, 234, 227);
    private static readonly Color AccentDeep = new(31, 65, 70);
    private static readonly Color Ink = new(236, 243, 246);
    private static readonly Color Muted = new(164, 181, 198);
    private static readonly Color Disabled = new(112, 127, 144);
    private static readonly Color Alert = new(249, 172, 143);
    private const int RowHeight = 38;
    private const int MenuRowHeight = 31;

    private readonly List<UiHit> _hits = [];
    private Rectangle _previewArea;
    private Rectangle _settingsArea;
    private Rectangle _menuArea;
    private Point _pointer = new(-9000, -9000);
    private Point? _orbitMouse;
    private string? _focus;
    private int _visibleRows;
    private MenuState? _popup;
    private NumberEdit? _numberEdit;

    private sealed record UiHit(string Id, Rectangle Bounds, Action Press, Action<int>? Nudge = null, bool Focusable = true);

    private sealed class MenuState(ChoiceRow row, Rectangle anchor, int selected)
    {
        public ChoiceRow Row { get; } = row;
        public Rectangle Anchor { get; } = anchor;
        public int Selected { get; set; } = selected;
        public int First { get; set; } = Math.Max(0, selected - 2);
        public int Count { get; set; }
    }

    private sealed class NumberEdit(NumberRow row, string id)
    {
        public NumberRow Row { get; } = row;
        public string Id { get; } = id;
        public string Value { get; set; } = "";
    }

    private void DrawStudio(ProjectZPainter canvas)
    {
        _hits.Clear();
        canvas.Fill(new Rectangle(0, 0, canvas.Width, canvas.Height), Background);
        const int edge = 20;
        const int gap = 16;
        var inspectorWidth = Math.Clamp((int)(canvas.Width * .388), 316, 430);
        var inspectorX = canvas.Width - edge - inspectorWidth;
        var paneHeight = Math.Max(150, canvas.Height - 192);
        _previewArea = new Rectangle(edge + 1, 106, inspectorX - edge - gap - 2, paneHeight - 2);
        _settingsArea = new Rectangle(inspectorX, 138, inspectorWidth, paneHeight - 34);

        canvas.Text("3D Scene Editor", edge, 18, Ink, StudioTextStyle.Title);
        canvas.Text("Project-Z", edge + 1, 52, Accent, StudioTextStyle.Body);
        var toolX = canvas.Width - edge;
        ToolbarButton("choose-model", "Choose model", 126, ChooseModel);
        ToolbarButton("save-scene", "Save as", 122, SaveScene);
        ToolbarButton("open-scene", "Open scene", 114, OpenScene);

        canvas.Text("Preview", edge + 1, 82, Ink, StudioTextStyle.Medium);
        DrawPreview(canvas);
        DrawInspector(canvas, inspectorX, inspectorWidth);
        DrawFooter(canvas);

        if (_popup is { } menu)
            DrawMenu(canvas, menu);

        void ToolbarButton(string id, string label, int width, Action action)
        {
            toolX -= width;
            DrawButton(canvas, id, label, new Rectangle(toolX, 24, width, 34), action, style: StudioTextStyle.Small);
            toolX -= 8;
        }
    }

    private void DrawPreview(ProjectZPainter canvas)
    {
        var preview = _previewArea;
        canvas.Panel(preview, SurfaceDeep, Border, 12);
        var imageArea = new Rectangle(preview.X + 10, preview.Y + 10, preview.Width - 20, preview.Height - 20);
        var imageScale = Math.Min(1d, Math.Min(640d / imageArea.Width, 520d / imageArea.Height));
        var renderWidth = Math.Max(2, (int)(imageArea.Width * imageScale));
        var renderHeight = Math.Max(2, (int)(imageArea.Height * imageScale));
        UpdatePreview(renderWidth, renderHeight);
        if (_scenePixels.Length > 0)
            canvas.Image(_scenePixels, _sceneWidth, _sceneHeight, imageArea);
        else
        {
            var hint = _ready && Scene.HasModel ? "Choose a model to preview it" : _loadError is null ? "Loading preview…" : "Preview unavailable";
            canvas.Text(hint, preview.Center.X, preview.Center.Y - 8, Muted, StudioTextStyle.Body, .5);
        }
        canvas.Text("Drag to orbit  ·  Scroll to zoom", preview.X + 1, preview.Bottom + 9, Muted, StudioTextStyle.Small);
    }

    private void DrawInspector(ProjectZPainter canvas, int x, int width)
    {
        var tabWidth = (width - (PageNames.Length - 1) * 5) / PageNames.Length;
        for (var index = 0; index < PageNames.Length; index++)
        {
            var page = index;
            var tab = new Rectangle(x + index * (tabWidth + 5), 99, tabWidth, 31);
            DrawButton(canvas, $"tab-{index}", PageNames[index], tab,
                () => SelectPage(page), active: index == _page, style: StudioTextStyle.Small);
        }

        canvas.Panel(_settingsArea, Surface, Border, 9);
        var rows = _rows[_page];
        _visibleRows = Math.Max(1, (_settingsArea.Height - 10) / RowHeight);
        var scrollLimit = Math.Max(0, rows.Length - _visibleRows);
        var first = _scrollRows[_page] = Math.Clamp(_scrollRows[_page], 0, scrollLimit);
        var panel = _settingsArea;
        var contentWidth = panel.Width - (scrollLimit > 0 ? 22 : 14);
        var stop = Math.Min(rows.Length, first + _visibleRows);
        for (var index = first; index < stop; index++)
        {
            var bounds = new Rectangle(panel.X + 7, panel.Y + 5 + (index - first) * RowHeight, contentWidth, RowHeight);
            DrawRow(canvas, $"row-{_page}-{index}", rows[index], bounds);
        }

        if (scrollLimit > 0)
        {
            var track = new Rectangle(panel.Right - 10, panel.Top + 8, 3, panel.Height - 16);
            canvas.Fill(track, Border);
            var thumbHeight = Math.Max(16, track.Height * _visibleRows / rows.Length);
            var thumbY = track.Y + (track.Height - thumbHeight) * first / scrollLimit;
            canvas.Fill(new Rectangle(track.X, thumbY, track.Width, thumbHeight), Accent);
        }
    }

    private void DrawRow(ProjectZPainter canvas, string id, EditorRow row, Rectangle bounds)
    {
        var column = bounds.Width * 45 / 100;
        var labelX = bounds.X + 4;
        var control = new Rectangle(bounds.X + column, bounds.Y + 3, bounds.Width - column - 3, 31);
        WriteWithin(canvas, row.Label, labelX, bounds.Y + 10, column - 8, Muted);

        if (row is NumberRow number)
        {
            var left = new Rectangle(control.X, control.Y, 29, control.Height);
            var right = new Rectangle(control.Right - 29, control.Y, 29, control.Height);
            var edit = new Rectangle(left.Right + 3, control.Y, right.X - left.Right - 6, control.Height);
            DrawButton(canvas, id, "−", left, () => Nudge(number, -1), focusable: false, style: StudioTextStyle.Medium);
            DrawButton(canvas, id, "+", right, () => Nudge(number, 1), focusable: false, style: StudioTextStyle.Medium);
            var editing = _numberEdit is { } current && current.Id == id;
            var numberLabel = editing ? (_numberEdit!.Value.Length > 0 ? _numberEdit.Value + "|" : "|")
                : number.Value().ToString(number.Step >= 1 ? "0" : "0.###", CultureInfo.InvariantCulture);
            DrawButton(canvas, id, numberLabel, edit, () => BeginNumericEdit(number, id),
                nudge: amount => Nudge(number, amount), active: editing, style: StudioTextStyle.Small);
            return;
        }

        if (row is ChoiceRow choice)
        {
            var index = Math.Clamp(choice.Selected(), 0, choice.Options.Length - 1);
            DrawButton(canvas, id, "", control, () => OpenMenu(choice, control),
                nudge: amount => ApplyChoice(choice, Math.Clamp(choice.Selected() + amount, 0, choice.Options.Length - 1)),
                active: _popup?.Row == choice);
            WriteWithin(canvas, choice.Options[index], control.X + 8, control.Y + 9, control.Width - 29, Ink, StudioTextStyle.Small);
            var arrowX = control.Right - 14;
            canvas.Line(arrowX - 4, control.Center.Y - 2, arrowX, control.Center.Y + 2, 1, Muted);
            canvas.Line(arrowX, control.Center.Y + 2, arrowX + 4, control.Center.Y - 2, 1, Muted);
            return;
        }

        if (row is ToggleRow toggle)
        {
            var on = toggle.Value();
            DrawButton(canvas, id, on ? "On" : "Off", control, () => ApplyToggle(toggle),
                active: on, nudge: _ => ApplyToggle(toggle), style: StudioTextStyle.Small);
            return;
        }

        if (row is SwatchRow swatch)
        {
            var color = new Color(unchecked((byte)(swatch.Value() >> 16)), unchecked((byte)(swatch.Value() >> 8)),
                unchecked((byte)swatch.Value()));
            DrawButton(canvas, id, "", control, () => PickColor(swatch));
            canvas.Panel(new Rectangle(control.X + 7, control.Y + 6, 22, 19), color, Border, 5);
            WriteWithin(canvas, "Change…", control.X + 36, control.Y + 9, control.Width - 43, Ink, StudioTextStyle.Small);
            return;
        }

        if (row is ActionRow action)
        {
            var enabled = action.Enabled is null || action.Enabled();
            DrawButton(canvas, id, action.Caption(), control, enabled ? action.Invoke : null,
                style: StudioTextStyle.Small);
            return;
        }

        if (row is TextRow text)
            WriteWithin(canvas, text.Value(), control.X + 4, control.Y + 9, control.Width - 8, Ink, StudioTextStyle.Small);
    }

    private void DrawFooter(ProjectZPainter canvas)
    {
        var x = canvas.Width - 139;
        var y = canvas.Height - 45;
        canvas.Fill(new Rectangle(20, y - 8, canvas.Width - 40, 1), Border);
        WriteWithin(canvas, Status, 20, y + 8, x - 139, _loadError is null && ValidChain() ? Muted : Alert);
        DrawButton(canvas, "cancel", "Cancel", new Rectangle(x - 91, y, 82, 33), () => DialogResult = DialogResult.Cancel);
        DrawButton(canvas, "apply", "Apply scene", new Rectangle(x, y, 119, 33), CanConfirm ? Confirm : null, emphasized: true);
    }

    private void DrawMenu(ProjectZPainter canvas, MenuState menu)
    {
        var count = menu.Row.Options.Length;
        menu.Count = Math.Min(count, Math.Min(8, Math.Max(1, (canvas.Height - 80) / MenuRowHeight)));
        ScrollMenuToSelection(menu);
        var menuHeight = menu.Count * MenuRowHeight + 8;
        var y = Math.Min(menu.Anchor.Bottom + 3, canvas.Height - 52 - menuHeight);
        var x = Math.Clamp(menu.Anchor.X, 8, canvas.Width - menu.Anchor.Width - 8);
        _menuArea = new Rectangle(x, Math.Max(8, y), menu.Anchor.Width, menuHeight);
        canvas.Panel(_menuArea, SurfaceDeep, Accent, 7);
        for (var row = 0; row < menu.Count; row++)
        {
            var choice = menu.First + row;
            var bounds = new Rectangle(_menuArea.X + 4, _menuArea.Y + 4 + row * MenuRowHeight, _menuArea.Width - 8, MenuRowHeight);
            if (choice == menu.Selected || bounds.Contains(_pointer))
                canvas.Panel(bounds, SurfaceHover, Color.Transparent, 4);
            WriteWithin(canvas, menu.Row.Options[choice], bounds.X + 7, bounds.Y + 8, bounds.Width - 13, Ink, StudioTextStyle.Small);
        }
        if (count > menu.Count)
        {
            var lineHeight = _menuArea.Height - 12;
            var marker = Math.Max(12, menu.Count * lineHeight / count);
            var top = _menuArea.Y + 6 + (lineHeight - marker) * menu.First / (count - menu.Count);
            canvas.Fill(new Rectangle(_menuArea.Right - 4, top, 2, marker), Accent);
        }
    }

    private void DrawButton(ProjectZPainter canvas, string id, string label, Rectangle bounds, Action? action,
        bool active = false, bool emphasized = false, Action<int>? nudge = null, bool focusable = true,
        StudioTextStyle style = StudioTextStyle.Body)
    {
        var enabled = action is not null;
        var hovered = enabled && _popup is null && bounds.Contains(_pointer);
        var focused = enabled && focusable && _focus == id;
        canvas.Panel(bounds, emphasized && enabled ? Accent : active ? AccentDeep : hovered ? SurfaceHover : SurfaceDeep,
            focused ? Accent : active ? new Color(82, 143, 142) : Border, 6);
        if (label.Length > 0)
        {
            var color = !enabled ? Disabled : emphasized ? Background : hovered || active ? Ink : Muted;
            var measured = canvas.MeasureText(label, style);
            if (measured.X <= bounds.Width - 8)
                canvas.Text(label, bounds.Center.X, bounds.Y + Math.Max(3, (bounds.Height - measured.Y) / 2 - 1), color, style, .5);
            else
                WriteWithin(canvas, label, bounds.X + 5, bounds.Y + 8, bounds.Width - 10, color, style);
        }
        if (action is not null)
            _hits.Add(new UiHit(id, bounds, action, nudge, focusable));
    }

    private static void WriteWithin(ProjectZPainter canvas, string text, int x, int y, int width, Color color,
        StudioTextStyle style = StudioTextStyle.Body)
    {
        if (width <= 4 || text.Length == 0) return;
        if (canvas.MeasureText(text, style).X > width)
        {
            var best = text.Length;
            while (best > 0 && canvas.MeasureText(text[..best] + "…", style).X > width)
                best--;
            if (best > 0 && char.IsHighSurrogate(text[best - 1]))
                best--;
            text = text[..best] + "…";
        }
        canvas.Text(text, x, y, color, style);
    }

    private void SelectPage(int page)
    {
        _page = page;
        _popup = null;
        _numberEdit = null;
        InvalidateCanvas();
    }

    private void Edited(bool preservePreset = false)
    {
        if (!preservePreset && _page is 1 or 2)
        {
            _terrainPreset = 0;
            if (_page == 2) _materialPreset = 0;
        }
        _message = null;
        InvalidateCanvas(sceneChanged: true);
    }

    private void Nudge(NumberRow number, int amount)
    {
        var current = float.IsFinite(number.Value()) ? number.Value() : number.Minimum;
        SetNumber(number, current + number.Step * amount);
    }

    private void SetNumber(NumberRow number, float value)
    {
        if (!float.IsFinite(value)) return;
        var clipped = Math.Clamp(value, number.Minimum, number.Maximum);
        if (number.Step >= 1) clipped = MathF.Round(clipped);
        number.Set(clipped);
        Edited();
    }

    private void ApplyChoice(ChoiceRow choice, int index)
    {
        _popup = null;
        choice.Select(index);
        Edited(preservePreset: choice.Label is "Terrain preset" or "Altitude preset");
    }

    private void ApplyToggle(ToggleRow toggle)
    {
        toggle.Set(!toggle.Value());
        Edited();
    }

    private void PickColor(SwatchRow swatch) => RunFileAction(() =>
    {
        var prior = swatch.Value();
        using var picker = new ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(prior),
            FullOpen = true
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        swatch.Set((prior & unchecked((int)0xff000000)) | (picker.Color.ToArgb() & 0xffffff));
        Edited();
    });

    private void OpenMenu(ChoiceRow choice, Rectangle anchor)
    {
        _popup = new MenuState(choice, anchor, Math.Clamp(choice.Selected(), 0, choice.Options.Length - 1));
        InvalidateCanvas();
    }

    private static void ScrollMenuToSelection(MenuState menu)
    {
        if (menu.Count <= 0) return;
        menu.First = Math.Clamp(menu.First, 0, Math.Max(0, menu.Row.Options.Length - menu.Count));
        if (menu.Selected < menu.First) menu.First = menu.Selected;
        if (menu.Selected >= menu.First + menu.Count) menu.First = menu.Selected - menu.Count + 1;
    }

    private void BeginNumericEdit(NumberRow number, string id)
    {
        _numberEdit = new NumberEdit(number, id);
        InvalidateCanvas();
    }

    private void FinishNumericEdit(bool commit)
    {
        var input = _numberEdit;
        _numberEdit = null;
        if (commit && input is not null && float.TryParse(input.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            SetNumber(input.Row, value);
        else
            InvalidateCanvas();
    }

    private bool PointerDown(System.Drawing.Point location)
    {
        var point = new Point(location.X, location.Y);
        _pointer = point;
        if (_popup is { } menu)
        {
            if (_menuArea.Contains(point))
            {
                var row = (point.Y - _menuArea.Top - 4) / MenuRowHeight;
                if (row >= 0 && row < menu.Count) ApplyChoice(menu.Row, menu.First + row);
                return false;
            }
            _popup = null;
        }
        if (_numberEdit is not null) FinishNumericEdit(commit: true);
        var hit = _hits.LastOrDefault(item => item.Bounds.Contains(point));
        if (hit is not null)
        {
            _focus = hit.Id;
            hit.Press();
            InvalidateCanvas();
            return false;
        }
        if (_previewArea.Contains(point))
        {
            _orbitMouse = point;
            return true;
        }
        InvalidateCanvas();
        return false;
    }

    private void PointerMove(System.Drawing.Point location)
    {
        _pointer = new Point(location.X, location.Y);
        if (_orbitMouse is { } last)
        {
            InputCamera.Yaw = (InputCamera.Yaw - (_pointer.X - last.X) * .42f) % 360f;
            InputCamera.Pitch = Math.Clamp(InputCamera.Pitch + (_pointer.Y - last.Y) * .34f, -89, 89);
            _orbitMouse = _pointer;
            InvalidateCanvas(sceneChanged: true);
        }
        else
        {
            _surface.Cursor = _hits.Any(hit => hit.Bounds.Contains(_pointer)) ? Cursors.Hand : Cursors.Default;
            InvalidateCanvas();
        }
    }

    private void PointerLeave()
    {
        _pointer = new Point(-9000, -9000);
        InvalidateCanvas();
    }

    private void StopOrbit() => _orbitMouse = null;

    private void PointerWheel(System.Drawing.Point location, int delta)
    {
        if (delta == 0) return;
        var point = new Point(location.X, location.Y);
        if (_popup is { } menu)
        {
            var selection = Math.Clamp(menu.Selected - Math.Sign(delta) * 3, 0, menu.Row.Options.Length - 1);
            menu.Selected = selection;
            ScrollMenuToSelection(menu);
        }
        else if (_settingsArea.Contains(point))
            _scrollRows[_page] -= Math.Sign(delta) * 3;
        else if (_previewArea.Contains(point))
        {
            InputCamera.Zoom = Math.Clamp(InputCamera.Zoom * (delta > 0 ? 1.07f : 1f / 1.07f), .2f, 3);
            InvalidateCanvas(sceneChanged: true);
        }
        InvalidateCanvas();
    }

    private bool RouteKey(Keys keys)
    {
        var code = keys & Keys.KeyCode;
        if (_numberEdit is { } number)
        {
            switch (code)
            {
                case Keys.Enter: FinishNumericEdit(commit: true); return true;
                case Keys.Escape: FinishNumericEdit(commit: false); return true;
                case Keys.Back:
                    if (number.Value.Length > 0) number.Value = number.Value[..^1];
                    InvalidateCanvas();
                    return true;
                case Keys.Tab: FinishNumericEdit(commit: true); break;
                default: return false;
            }
        }
        if (_popup is { } menu)
        {
            switch (code)
            {
                case Keys.Escape: _popup = null; InvalidateCanvas(); return true;
                case Keys.Enter:
                case Keys.Space: ApplyChoice(menu.Row, menu.Selected); return true;
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                    menu.Selected = code switch
                    {
                        Keys.Home => 0, Keys.End => menu.Row.Options.Length - 1,
                        Keys.Up => menu.Selected - 1, Keys.Down => menu.Selected + 1,
                        Keys.PageUp => menu.Selected - menu.Count, _ => menu.Selected + menu.Count
                    };
                    menu.Selected = Math.Clamp(menu.Selected, 0, menu.Row.Options.Length - 1);
                    ScrollMenuToSelection(menu);
                    InvalidateCanvas();
                    return true;
                case Keys.Tab: _popup = null; break;
                default: return false;
            }
        }

        if (code == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            return true;
        }
        if (code == Keys.Tab)
        {
            var targets = _hits.Where(item => item.Focusable).ToArray();
            if (targets.Length == 0) return true;
            var index = Array.FindIndex(targets, item => item.Id == _focus);
            var backwards = (keys & Keys.Shift) != 0;
            _focus = targets[index < 0 ? backwards ? targets.Length - 1 : 0 : (index + (backwards ? targets.Length - 1 : 1)) % targets.Length].Id;
            InvalidateCanvas();
            return true;
        }

        var focused = _hits.FirstOrDefault(item => item.Focusable && item.Id == _focus);
        if (code is Keys.Enter or Keys.Space)
        {
            if (focused is not null) focused.Press();
            else if (code == Keys.Enter) Confirm();
            return true;
        }
        if (code is Keys.Left or Keys.Right && focused?.Nudge is { } change)
        {
            change(code == Keys.Left ? -1 : 1);
            return true;
        }
        if (code is Keys.PageUp or Keys.PageDown)
        {
            _scrollRows[_page] += code == Keys.PageUp ? -Math.Max(1, _visibleRows) : Math.Max(1, _visibleRows);
            InvalidateCanvas();
            return true;
        }
        return false;
    }

    private bool RouteText(char key)
    {
        if (_numberEdit is not { } number || !(char.IsAsciiDigit(key) || key is '.' or '-' or '+'))
            return false;
        if (number.Value.Length < 16)
            number.Value += key;
        InvalidateCanvas();
        return true;
    }
}
