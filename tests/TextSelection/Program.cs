using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing.UI.Primitives;
using Color = Microsoft.Xna.Framework.Color;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using Point = Microsoft.Xna.Framework.Point;
using Forms = System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var game = new SelectionProbe(args.Length > 0 ? args[0] : "selection-results");
        game.Run();
    }
}

sealed class InputScene(SceneManager manager) : Scene(manager);
sealed class SelectionProbe : Game
{
    readonly string output;
    readonly GraphicsDeviceManager graphics;
    readonly List<string> checks = new();
    SceneManager manager;
    InputScene scene;
    Textbox box;
    ContentContainer resources;
    SpriteBatch batch;

    public SelectionProbe(string directory)
    {
        output = Path.GetFullPath(directory);
        Directory.CreateDirectory(output);
        graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 720, PreferredBackBufferHeight = 360, GraphicsProfile = GraphicsProfile.HiDef };
        Content.RootDirectory = "Content";
        Window.Title = "Project-Z vector text / selection";
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        resources = new ContentContainer(Content, GraphicsDevice);
        resources.LoadAllContent();
        Services.AddService(typeof(SpriteBatch), batch);
        Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        Services.AddService(typeof(ContentContainer), resources);
        manager = new SceneManager(this) { UseExternalInput = true, AutoResizeViewport = false, ExternalViewportSize = new Point(720, 360) };
        scene = new InputScene(manager) { BackgroundColor = new Color(4, 18, 14), EnableDesignTimeControls = false };
        manager.AddScene("selection", scene);
        manager.ActiveScene = scene;
        box = new Textbox(scene) { Position = new Vector2(30, 90), Size = new Vector2(420, 150), TextPadding = new Vector2(8, 8), Font = "SegoeUI_12", BackgroundColor = new Color(7, 43, 31) };
        scene.AddElement(box);
        scene.AddElement(new TextElement(scene) { Text = "Font-file vector text — selection and clipboard", Font = "SegoeUI_18", Position = new Vector2(30, 25) });
        GraphicsQuality.ForDevice(GraphicsDevice).AntiAliasingSamples = 4;
    }

    void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks.Add("PASS " + description);
    }
    void Tap(Keys key) { manager.InjectKeyDown(key); manager.InjectKeyUp(key); }
    void Chord(Keys modifier, Keys key) { manager.InjectKeyDown(modifier); Tap(key); manager.InjectKeyUp(modifier); }
    void Set(string text, int caret = 0)
    {
        box.Text = text;
        box.Select(caret, 0);
        box.ValidationCheck();
        scene.FocusElement(box);
    }
    Point At(int index)
    {
        var prefix = box.Text[..index];
        return new Point((int)Math.Round(box.Position.X + box.TextPadding.X + scene.MeasureText(box.Font, prefix).X), (int)(box.Position.Y + box.TextPadding.Y + 5));
    }
    void Drag(Point start, Point end)
    {
        manager.InjectMouseLeftDown(start.X, start.Y, 720, 360);
        manager.InjectMouseMove(end.X, end.Y, 720, 360);
        manager.InjectMouseLeftUp(end.X, end.Y, 720, 360);
    }
    protected override void Draw(GameTime time)
    {
        Forms.IDataObject previousClipboard = null;
        try
        {
            manager.Tick(time);
            var parser = new ProjectZ.Shared.Drawing.Designer.SceneXamlParser(scene);
            var hitRoot = parser.Parse("<Rectangle Width=\"160\" Height=\"45\"><Button Name=\"CloseProbe\" Width=\"28\" Height=\"28\" Text=\"x\"/><TextBlock Text=\"overlay\" Width=\"160\" Height=\"45\" IsHitTestVisible=\"False\"/></Rectangle>");
            scene.AddElement(hitRoot);
            manager.Tick(time);
            var closeProbe = parser.FindName<ProjectZ.Shared.Drawing.UI.Input.Button>("CloseProbe");
            var closePoint = closeProbe.Rectangle.Center;
            Require(ReferenceEquals(scene.PointToElement(closePoint), closeProbe), "bypassed overlay cannot promote parent above close button");
            Require(ReferenceEquals(scene.PointToElement(closePoint), scene.PointToElements(closePoint).First()), "single and multiple hit tests agree on topmost interactive element");
            var closed = false;
            closeProbe.MouseLeftClick += _ => closed = true;
            manager.InjectMouseLeftDown(closePoint.X, closePoint.Y, 720, 360);
            manager.InjectMouseLeftUp(closePoint.X, closePoint.Y, 720, 360);
            Require(closed, "close button receives click through noninteractive overlay");
            hitRoot.isVisible = false;
            Require(resources.VectorFonts.Count == 8, "default sizes load real Segoe UI font outlines");
            resources.LoadVectorFont("CustomFileFont", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "consola.ttf"), 16);
            Require(scene.MeasureText("CustomFileFont", "Font file").X > 0, "custom TTF works without a SpriteFont atlas");
            Set("alpha beta gamma");
            Drag(At(2), At(9));
            Require(box.SelectedText == "pha bet" && box.CaretPosition == 9, "forward drag selects exact source range");
            Drag(At(12), At(3));
            Require(box.SelectedText == "ha beta g" && box.CaretPosition == 3, "backward drag keeps caret at moving end");
            Tap(Keys.Right);
            Require(box.SelectionLength == 0 && box.CaretPosition == 12, "Right collapses selection to end");
            Chord(Keys.LeftShift, Keys.Left);
            Chord(Keys.LeftShift, Keys.Left);
            Require(box.SelectionStart == 10 && box.SelectionLength == 2, "Shift+Left extends selection from stable anchor");
            Chord(Keys.LeftShift, Keys.Right);
            Require(box.SelectionLength == 1 && box.CaretPosition == 11, "reversing Shift navigation shrinks selection");
            Set("alpha beta gamma", 0);
            manager.InjectKeyDown(Keys.LeftControl);
            manager.InjectKeyDown(Keys.LeftShift);
            Tap(Keys.Right);
            manager.InjectKeyUp(Keys.LeftShift);
            manager.InjectKeyUp(Keys.LeftControl);
            Require(box.SelectedText == "alpha ", "Ctrl+Shift+Right selects a word");
            var end = At(10);
            manager.InjectKeyDown(Keys.LeftShift);
            manager.InjectMouseLeftDown(end.X, end.Y, 720, 360);
            manager.InjectMouseLeftUp(end.X, end.Y, 720, 360);
            manager.InjectKeyUp(Keys.LeftShift);
            Require(box.SelectionStart == 0 && box.SelectionLength == 10, "Shift+click extends existing anchor");
            var changes = 0;
            box.OnTextChanged += _ => changes++;
            box.Select(0, 5);
            changes = 0;
            Tap(Keys.Z);
            Require(box.Text == "z beta gamma" && changes == 1 && box.CaretPosition == 1, "typing replaces selection with one change notification");

            var clipboardSource = Forms.Clipboard.GetDataObject();
            var clipboardSnapshot = new Forms.DataObject();
            if (clipboardSource != null)
                foreach (var format in clipboardSource.GetFormats(false))
                    try { clipboardSnapshot.SetData(format, clipboardSource.GetData(format, false)); } catch { }
            previousClipboard = clipboardSnapshot;
            Set("copy this text");
            box.Select(5, 4);
            Chord(Keys.LeftControl, Keys.C);
            Require(Forms.Clipboard.GetText() == "this", "Ctrl+C copies only selected text to Windows clipboard");
            Chord(Keys.LeftControl, Keys.X);
            Require(box.Text == "copy  text", "Ctrl+X removes copied range");
            box.Select(0, 4);
            changes = 0;
            Chord(Keys.LeftControl, Keys.V);
            Require(box.Text == "this  text" && changes == 1, "Ctrl+V atomically replaces selection");
            box.AcceptsReturn = false;
            Forms.Clipboard.SetText("one\r\ntwo\nthree");
            Chord(Keys.LeftControl, Keys.A);
            Chord(Keys.LeftShift, Keys.Insert);
            Require(box.Text == "one two three", "single-line paste normalizes CRLF and LF");
            box.MaskCharacter = '*';
            box.SelectAll();
            Forms.Clipboard.SetText("sentinel");
            Chord(Keys.LeftControl, Keys.C);
            Chord(Keys.LeftControl, Keys.X);
            Require(Forms.Clipboard.GetText() == "sentinel" && box.Text == "one two three", "masked input does not expose or cut password text");
            box.MaskCharacter = null;
            box.AcceptsReturn = true;
            Set("first\r\nsecond\nthird", 5);
            Chord(Keys.LeftShift, Keys.Right);
            Require(box.SelectedText == "\r\n" && box.CaretPosition == 7, "CRLF is selected atomically");
            Tap(Keys.Back);
            Require(box.Text == "firstsecond\nthird", "Backspace removes selected CRLF");
            Set("first\r\nsecond\nthird", 9);
            Tap(Keys.Home);
            Require(box.CaretPosition == 7, "Home moves to visual line start");
            Chord(Keys.LeftShift, Keys.End);
            Require(box.SelectedText == "second", "Shift+End selects to line end without newline");
            Tap(Keys.Delete);
            Require(box.Text == "first\r\n\nthird", "Delete replaces selected range");
            box.Size = new Vector2(130, 90);
            Set("Wrapped selection keeps its highlight aligned with the glyphs.");
            box.Select(2, 28);
            Require(box.SelectionBounds.Count >= 2, "soft-wrapped selection yields multiple highlight rows");
            box.AcceptsReturn = false;
            box.Size = new Vector2(120, 35);
            Set("012345678901234567890123456789", 0);
            var begin = At(2);
            manager.InjectMouseLeftDown(begin.X, begin.Y, 720, 360);
            manager.InjectMouseMove(250, begin.Y, 720, 360);
            for (var i = 0; i < 12; i++) box.Tick(time);
            manager.InjectMouseLeftUp(250, begin.Y, 720, 360);
            Require(box.SelectionStart == 2 && box.CaretPosition == box.Text.Length, $"dragging beyond input scrolls while preserving anchor (start={box.SelectionStart}, caret={box.CaretPosition})");

            box.AcceptsReturn = true;
            box.IsReadOnly = true;
            Set("read-only original"); box.SelectAll();
            Require(box.CopySelection(), "read-only textbox copies selected text");
            Require(!box.CutSelection() && !box.PasteClipboard(), "read-only cut/paste APIs refuse edits");
            box.ReplaceSelection("bad"); Tap(Keys.Delete); Tap(Keys.Back); Tap(Keys.Z); Chord(Keys.LeftControl, Keys.V);
            Require(box.Text == "read-only original", "read-only keyboard and native replacement paths cannot modify text");
            var parsedReadOnly = (Textbox)parser.Parse("<TextBox Text=\"fixed\" IsReadOnly=\"True\"/>");
            Require(parsedReadOnly.IsReadOnly, "native XAML applies IsReadOnly"); parsedReadOnly.Dispose();
            manager.InjectMouseRightDown(40,100,720,360); manager.InjectMouseRightUp(40,100,720,360); manager.Tick(time);
            var readOnlyMenu=scene.Elements.OfType<ProjectZ.Shared.Drawing.UI.Input.ContextMenu>().Single(m=>m.IsOpen);
            Require(readOnlyMenu.Children.OfType<ProjectZ.Shared.Drawing.UI.Input.MenuItem>().Select(m=>m.Header).SequenceEqual(new[]{"Copy","Select all"}),"read-only textbox menu contains no Cut or Paste");
            readOnlyMenu.IsOpen=false;
            var label = (TextElement)parser.Parse("<TextBlock Text=\"alpha beta gamma\" IsTextSelectionEnabled=\"True\" Width=\"350\" Height=\"40\"/>");
            label.Position = new Vector2(30, 280); scene.AddElement(label); manager.Tick(time);
            Point LabelAt(int i) => new((int)Math.Round(label.Position.X + label.Padding.Left + scene.MeasureText(label.Font, label.Text[..i]).X * label.FontScale), (int)label.Position.Y + 5);
            Drag(LabelAt(2), LabelAt(9));
            Require(label.SelectedText == "pha bet", $"display text supports exact forward selection: [{label.SelectedText}], scale={label.FontScale}, pos={label.Position}");
            Drag(LabelAt(12), LabelAt(3));
            Require(label.SelectedText == "ha beta g", "display text supports reverse selection");
            manager.InjectMouseRightDown(45,285,720,360); manager.InjectMouseRightUp(45,285,720,360); manager.Tick(time);
            var menu=scene.Elements.OfType<ProjectZ.Shared.Drawing.UI.Input.ContextMenu>().Single(m=>m.IsOpen);
            var menuItems=menu.Children.OfType<ProjectZ.Shared.Drawing.UI.Input.MenuItem>().ToArray();
            Require(menuItems.Select(m=>m.Header).SequenceEqual(new[]{"Copy","Select all"}), "display text menu has Copy and Select all, without Cut or Paste");
            var copyPoint=menuItems[0].Rectangle.Center;
            manager.InjectMouseLeftDown(copyPoint.X,copyPoint.Y,720,360); manager.InjectMouseLeftUp(copyPoint.X,copyPoint.Y,720,360);
            Require(Forms.Clipboard.GetText()=="ha beta g", "right-click Copy places the exact display selection on the real clipboard");
            scene.FocusElement(label); Chord(Keys.LeftControl,Keys.A); Chord(Keys.LeftControl,Keys.C); Tap(Keys.Z); Chord(Keys.LeftControl,Keys.V);
            Require(label.Text=="alpha beta gamma" && Forms.Clipboard.GetText()==label.Text,"display text Ctrl+A/C works and typing/paste cannot edit it");
            label.SelectAll(); Tap(Keys.Right); Chord(Keys.LeftShift, Keys.Left);
            Require(label.SelectedText=="a", "display Shift+Left extends selection after collapsing to end");
            label.TextAlignment=ProjectZ.Shared.Drawing.UI.HorizontalAlignment.Center; label.FontScale=1.5f; label.ValidationCheck();
            var offset=(label.Size.X-label.Padding.Left-label.Padding.Right-scene.MeasureText(label.Font,label.Text).X*label.FontScale)/2;
            var centeredStart=LabelAt(2); centeredStart.X+=(int)Math.Round(offset);
            var centeredEnd=LabelAt(9); centeredEnd.X+=(int)Math.Round(offset);
            Drag(centeredStart,centeredEnd);
            Require(label.SelectedText=="pha bet", "centered and scaled display selection follows rendered glyphs");
            label.TextAlignment=ProjectZ.Shared.Drawing.UI.HorizontalAlignment.Left; label.FontScale=1;
            label.Text="alpha  beta\r\ngamma delta"; label.MaxWidth=75; label.Size=new Vector2(75,75); label.ValidationCheck(); label.SelectAll();
            Require(label.SelectedText=="alpha  beta\r\ngamma delta", "wrapped display copy preserves original whitespace and CRLF");
            label.Text=label.Text; Require(label.SelectionLength==label.Text.Length,"same-value updates preserve display selection");
            using (var selectionTarget = new RenderTarget2D(GraphicsDevice,720,360)) {
                GraphicsDevice.SetRenderTarget(selectionTarget); scene.Draw(time); GraphicsDevice.SetRenderTarget(null);
                var selectedPixels=new Color[720*360]; selectionTarget.GetData(selectedPixels);
                Require(selectedPixels.Skip(280*720).Count(c=>c.B>100 && c.B>c.G*1.3 && c.G>c.R)>100,"display text selection highlight renders behind wrapped glyphs");
                using var png=File.Create(Path.Combine(output,"readonly-selection.png"));selectionTarget.SaveAsPng(png,720,360);
            }
            label.Text="short"; Require(label.SelectionLength==0,"changed display text clears stale selection safely");
            label.Dispose(); box.IsReadOnly=false; manager.Tick(time);
            box.Size = new Vector2(620, 160);
            Set("Select text by dragging or holding Shift.\r\nCtrl+A selects all; Ctrl+C / X / V copy, cut and paste.\r\nText is drawn from font-file outlines.");
            box.Select(12, 75);
            using var target = new RenderTarget2D(GraphicsDevice, 720, 360);
            GraphicsDevice.SetRenderTarget(target);
            scene.Draw(time);
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[720 * 360];
            target.GetData(pixels);
            Require(pixels.Count(c => c.B > 100 && c.B > c.G * 1.3 && c.G > c.R) > 500, "selection highlight is visibly rendered behind text");
            Require(SceneElement.RenderExceptionCount == 0, "no swallowed vector or selection rendering errors");
            using (var png = File.Create(Path.Combine(output, "vector-selection.png"))) target.SaveAsPng(png, 720, 360);
            File.WriteAllLines(Path.Combine(output, "result.txt"), checks);
        }
        catch (Exception ex)
        {
            File.WriteAllLines(Path.Combine(output, "result.txt"), checks.Append("FAIL " + ex));
            Environment.ExitCode = 1;
        }
        finally
        {
            if (previousClipboard != null) try { Forms.Clipboard.SetDataObject(previousClipboard, true); } catch { }
        }
        Exit();
    }
}
