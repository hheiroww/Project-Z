using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Input;
using Point = Microsoft.Xna.Framework.Point;
using Color = Microsoft.Xna.Framework.Color;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var game = new SpellingProbe(args.FirstOrDefault() ?? "artifacts/SpellChecking");
        game.Run();
    }
}
sealed class InputScene(SceneManager manager) : Scene(manager);
sealed class SpellingProbe : Game
{
    readonly string output;
    readonly GraphicsDeviceManager graphics;
    readonly List<string> checks = new();
    SceneManager manager;
    InputScene scene;
    Textbox box;
    OfflineSpellChecker checker;
    public SpellingProbe(string directory)
    {
        output = Path.GetFullPath(directory);
        Directory.CreateDirectory(output);
        graphics = new(this) { PreferredBackBufferWidth = 720, PreferredBackBufferHeight = 420, GraphicsProfile = GraphicsProfile.HiDef };
        Content.RootDirectory = "Content";
        Window.Title = "Project-Z offline spelling";
    }
    protected override void LoadContent()
    {
        var batch = new SpriteBatch(GraphicsDevice);
        var resources = new ContentContainer(Content, GraphicsDevice);
        resources.LoadAllContent();
        Services.AddService(typeof(SpriteBatch), batch);
        Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        Services.AddService(typeof(ContentContainer), resources);
        manager = new(this) { UseExternalInput = true, AutoResizeViewport = false, ExternalViewportSize = new(720, 420) };
        scene = new(manager) { BackgroundColor = new Color(15, 20, 28), EnableDesignTimeControls = false };
        manager.AddScene("spelling", scene);
        manager.ActiveScene = scene;
        checker = new(Path.Combine(output, Guid.NewGuid() + ".dictionary.txt"));
        box = new(scene) { Position = new(30, 50), Size = new(430, 150), TextPadding = new(8, 8), Font = "SegoeUI_12", SpellChecker = checker, SpellCheckLanguage = "en-US" };
        scene.AddElement(box);
    }
    void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks.Add("PASS " + description);
    }
    void WaitFor(GameTime time, Func<bool> ready)
    {
        var until = Environment.TickCount64 + 15000;
        do
        {
            manager.Tick(time);
            if (ready()) return;
            if (box.SpellCheckError != null) throw new InvalidOperationException(box.SpellCheckError);
            Thread.Sleep(10);
        } while (Environment.TickCount64 < until);
        throw new TimeoutException("Timed out waiting for offline spelling");
    }
    ContextMenu Open(GameTime time, int index)
    {
        var x = (int)(box.Position.X + box.TextPadding.X + scene.MeasureText(box.Font, box.Text[..index]).X + 2);
        var y = (int)(box.Position.Y + box.TextPadding.Y + 6);
        manager.InjectMouseRightDown(x, y, 720, 420);
        manager.InjectMouseRightUp(x, y, 720, 420);
        manager.Tick(time);
        return scene.Elements.OfType<ContextMenu>().Single(m => m.IsOpen);
    }
    void Click(MenuItem item)
    {
        var p = item.Rectangle.Center;
        manager.InjectMouseLeftDown(p.X, p.Y, 720, 420);
        manager.InjectMouseLeftUp(p.X, p.Y, 720, 420);
    }
    protected override void Draw(GameTime time)
    {
        try
        {
            manager.Tick(time);
            var errors = checker.CheckAsync("hello wrld world", "en-US").GetAwaiter().GetResult();
            Require(errors.Count == 1 && errors[0].Start == 6 && errors[0].Word == "wrld", "real local engine finds misspelling and exact source range");
            Require(errors[0].Suggestions.Contains("world"), "real local engine suggests world for wrld");
            foreach (var newline in new[] { "\n", "\r\n", "\r" })
            {
                var unicode = "hello" + newline + "🙂 speling";
                var issue = checker.CheckAsync(unicode, "en-US").Result.Single(e => e.Word == "speling");
                Require(issue.Start == unicode.IndexOf("speling", StringComparison.Ordinal), "source indices preserve Unicode and " + string.Join("/", newline.Select(c => (int)c)) + " line endings");
            }
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                try { checker.CheckAsync("speling", "en-US", cancellation.Token).GetAwaiter().GetResult(); Require(false, "cancelled check must not run"); }
                catch (OperationCanceledException) { Require(true, "superseded queued checks support cancellation"); }
            }
            box.Text = "hello wrld world";
            scene.FocusElement(box);
            WaitFor(time, () => box.SpellingErrors.Count == 1);
            Require(box.IsSpellCheckEnabled, "ordinary inputs enable spelling automatically");
            var menu = Open(time, 7);
            var items = menu.Children.OfType<MenuItem>().ToArray();
            Require(items.Any(i => i.Header == "Add to dictionary") && items.Any(i => i.Header == "world"), "routed right-click offers suggestions and Add to dictionary");
            Require(items.All(i => menu.Rectangle.Contains(i.Rectangle)) && menu.Rectangle.Bottom <= 420, "all suggestion and edit rows fit inside the clamped menu");
            Require(items.TakeLast(4).Select(i => i.Header).SequenceEqual(new[] { "Cut", "Copy", "Paste", "Select all" }), "spelling menu preserves edit commands");
            Click(items.Single(i => i.Header == "world"));
            Require(box.Text == "hello world world" && box.CaretPosition == 11, "clicking suggestion replaces only the clicked word and restores caret");
            Require(box.SpellingErrors.Count == 0, "edited text immediately drops stale spelling results");
            box.Text = "hello wrld";
            WaitFor(time, () => box.SpellingErrors.Count == 1);
            menu = Open(time, 7);
            Click(menu.Children.OfType<MenuItem>().Single(i => i.Header == "Add to dictionary"));
            Require(checker.ContainsWord("WRLD") && box.SpellingErrors.Count == 0, "Add to dictionary accepts case-insensitively and immediately removes marks");
            var reopened = new OfflineSpellChecker(checker.DictionaryPath);
            Require(reopened.CheckAsync("wrld", "en-US").Result.Count == 0, "custom dictionary persists across service recreation");
            File.WriteAllText(checker.DictionaryPath, "# editable custom words\nheirowword\n");
            checker.ReloadDictionary();
            Require(!checker.ContainsWord("wrld") && checker.ContainsWord("heirowword"), "external dictionary edits support adding and removing entries");
            WaitFor(time, () => box.SpellingErrors.Count == 1);
            var second = new Textbox(scene) { Position = new(480, 50), Size = new(200, 60), Text = "wrld", SpellChecker = checker, SpellCheckLanguage = "en-US" };
            scene.AddElement(second);
            WaitFor(time, () => second.SpellingErrors.Count == 1);
            checker.AddWord("wrld");
            Require(box.SpellingErrors.Count == 0 && second.SpellingErrors.Count == 0, "shared dictionary changes invalidate every input immediately");
            box.IsSpellCheckEnabled = false;
            box.Text = "speling";
            manager.Tick(time);
            Require(box.SpellingErrors.Count == 0, "per-input disable suppresses spelling");
            box.IsSpellCheckEnabled = true;
            box.IsReadOnly = true;
            Require(box.SpellingErrors.Count == 0, "read-only text does not offer spelling edits");
            box.IsReadOnly = false;
            box.MaskCharacter = '*';
            manager.Tick(time);
            Require(box.SpellingErrors.Count == 0 && !Open(time, 2).Children.OfType<MenuItem>().Any(i => i.Header == "Add to dictionary"), "masked fields never show spelling suggestions or dictionary actions");
            scene.Elements.OfType<ContextMenu>().Single(m => m.IsOpen).IsOpen = false;
            box.MaskCharacter = null;
            box.Text = "hello speling";
            WaitFor(time, () => box.SpellingErrors.Count == 1);
            box.Text = "hello world";
            manager.Tick(time);
            Require(box.SpellingErrors.Count == 0, "changing text hides outstanding results before next check");
            box.Text = "speling speling speling speling";
            box.Size = new(110, 150);
            WaitFor(time, () => box.SpellingErrors.Count == 4);
            Require(box.SpellingUnderlineBounds.Count > 1 && box.SpellingUnderlineBounds.Select(r => r.Y).Distinct().Count() > 1, "squiggles follow wrapped visual rows");
            box.Size = new(110, 45);
            box.Select(box.Text.Length, 0);
            manager.Tick(time);
            var area = new Microsoft.Xna.Framework.Rectangle((int)(box.Position.X + box.TextPadding.X), (int)(box.Position.Y + box.TextPadding.Y), (int)(box.Size.X - 2 * box.TextPadding.X), (int)(box.Size.Y - 2 * box.TextPadding.Y));
            Require(box.SpellingUnderlineBounds.Count > 0 && box.SpellingUnderlineBounds.All(r => area.Contains(r)), "scrolling and clipping keep squiggles inside input padding");
            box.Size = new(430, 150);
            box.Text = "Right click speling to fix it.\r\nYour custom dictionary stays on this computer.";
            box.Select(0, 0);
            WaitFor(time, () => box.SpellingErrors.Any(e => e.Word == "speling"));
            using var target = new RenderTarget2D(GraphicsDevice, 720, 420);
            GraphicsDevice.SetRenderTarget(target);
            scene.Draw(time);
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[720 * 420];
            target.GetData(pixels);
            Require(pixels.Count(c => c.R > 180 && c.G < 100 && c.B < 110) > 20, "red squiggle pixels render in native DX12 scene");
            using (var png = File.Create(Path.Combine(output, "spelling.png"))) target.SaveAsPng(png, 720, 420);
            menu = Open(time, 13);
            GraphicsDevice.SetRenderTarget(target);
            scene.Draw(time);
            GraphicsDevice.SetRenderTarget(null);
            using (var png = File.Create(Path.Combine(output, "suggestions.png"))) target.SaveAsPng(png, 720, 420);
            Require(SceneElement.RenderExceptionCount == 0, "no swallowed rendering exceptions");
            box.Dispose();
            Require(!scene.Elements.OfType<ContextMenu>().Any(m => m.IsOpen), "disposing input cleans up its open spelling menu");
            File.WriteAllLines(Path.Combine(output, "result.txt"), checks);
        }
        catch (Exception ex)
        {
            File.WriteAllLines(Path.Combine(output, "result.txt"), checks.Append("FAIL " + ex));
            Environment.ExitCode = 1;
        }
        Exit();
    }
}
