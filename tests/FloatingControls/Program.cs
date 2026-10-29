using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.RuntimeDiagnostics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.Designer;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing.UI.Layout;
using ProjectZ.Shared.Drawing.UI.Primitives;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using ComboBox = ProjectZ.Shared.Drawing.UI.Input.ComboBox;
using Keys = Microsoft.Xna.Framework.Input.Keys;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("Usage: FloatingControls <output-directory> [--baseline] [--backend DirectX11] [--frames 60]"); return 2; }
        string output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        try
        {
            string Value(string flag, string fallback) { int i = Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
            bool baseline = args.Contains("--baseline");
            // Require a PUBLIC opt-out before creating a Game, device or manager.
            // An original baseline without the observer-only opt-out must fail here.
            var enabled = typeof(DiagnosticOptions).GetProperty("Enabled") ?? throw new InvalidOperationException("Offscreen tests require the explicit diagnostics opt-out; do not run an unmodified baseline.");
            var options = new DiagnosticOptions();
            if (enabled.GetValue(options) is not true) throw new InvalidOperationException("Diagnostics must stay enabled by default.");
            enabled.SetValue(options, false); ApplicationDiagnostics.Initialize(options);
            Parallel.For(0, 16, _ => ApplicationDiagnostics.Initialize());
            if (ApplicationDiagnostics.SessionPath != null || ApplicationDiagnostics.Folder != null) throw new InvalidOperationException("Disabled diagnostics unexpectedly initialized I/O.");
            using var probe = new OffscreenProbe(output, Enum.Parse<ProjectZGraphicsBackend>(Value("--backend", "DirectX11")));
            probe.Benchmark(Math.Clamp(int.Parse(Value("--frames", "60")), 10, 180));
            if (!baseline) probe.CheckBehavior();
            probe.Save(baseline); return 0;
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Console.Error.WriteLine(error.Message); return 1; }
    }
}
sealed class HiddenOwner : System.Windows.Forms.Form
{
    protected override bool ShowWithoutActivation => true;
    public HiddenOwner() { ShowInTaskbar = false; ClientSize = new(720, 480); }
    // Never call Show, ShowDialog, Application.Run or Game.Run.
}
sealed class ServiceGame : Game { }
sealed class TestScene(SceneManager manager) : Scene(manager) { }
sealed class DeviceService(GraphicsDevice device) : IGraphicsDeviceService
{
    public GraphicsDevice GraphicsDevice => device;
    public event EventHandler<EventArgs>? DeviceCreated { add { } remove { } }
    public event EventHandler<EventArgs>? DeviceDisposing { add { } remove { } }
    public event EventHandler<EventArgs>? DeviceReset { add { } remove { } }
    public event EventHandler<EventArgs>? DeviceResetting { add { } remove { } }
}
sealed class OffscreenProbe : IDisposable
{
    readonly string output;
    readonly HiddenOwner owner;
    readonly GraphicsDevice device;
    readonly ServiceGame game;
    readonly SpriteBatch batch;
    readonly SceneManager manager;
    readonly TestScene scene;
    readonly Stopwatch deadline = Stopwatch.StartNew();
    readonly List<string> checks = [];
    readonly List<object> measurements = [];
    RenderTarget2D target;
    ComboBox? combo;
    StackPanel? observedPanel;
    int frame, arrangements;

    public OffscreenProbe(string output, ProjectZGraphicsBackend expected)
    {
        this.output = output;
        if (GraphicsBackendInfo.Current != expected) throw new InvalidOperationException($"Expected {expected}; loaded {GraphicsBackendInfo.Current}.");
        if (expected != ProjectZGraphicsBackend.DirectX11) throw new NotSupportedException("The never-shown WinForms device host currently supports DirectX11 only; other native contexts require separate review.");
        owner = new HiddenOwner();
        var parameters = new PresentationParameters { DeviceWindowHandle = owner.Handle, BackBufferWidth = 720, BackBufferHeight = 480, BackBufferFormat = SurfaceFormat.Color, DepthStencilFormat = DepthFormat.Depth24Stencil8, IsFullScreen = false, PresentationInterval = PresentInterval.Immediate };
        device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, parameters);
        game = new ServiceGame();
        game.Services.AddService(typeof(IGraphicsDeviceService), new DeviceService(device));
        game.Services.AddService(typeof(GraphicsDevice), device);
        batch = new SpriteBatch(device); game.Services.AddService(typeof(SpriteBatch), batch);
        var resources = new ContentContainer(game.Content, device); resources.LoadAllContent();
        game.Services.AddService(typeof(ContentContainer), resources);
        manager = new SceneManager(game, 1000) { UseExternalInput = true, AutoResizeViewport = false, ExternalViewportSize = new Point(720, 480) };
        scene = new TestScene(manager) { EnableDesignTimeControls = false, EnableFrameProfiling = true, BackgroundColor = new Color(12, 18, 22) };
        manager.AddScene("offscreen", scene); manager.ActiveScene = scene;
        scene.Quality.AntiAliasingSamples = 16; scene.Quality.Filtering = TextureFiltering.Anisotropic16x;
        for (int x = 0; x < 720; x += 2) scene.AddElement(new RectangleElement(scene) { Position = new(x, 0), Size = new(2, 480), BackgroundColor = x % 4 == 0 ? Color.Black : Color.White });
        target = NewTarget(720, 480); Frame();
        Check(!owner.Visible, "The render owner stays hidden");
    }
    RenderTarget2D NewTarget(int w, int h) => new(device, w, h, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8, 0, RenderTargetUsage.PreserveContents);
    void Check(bool good, string name) { if (!good) throw new InvalidOperationException(name); checks.Add("PASS " + name); }
    void Frame()
    {
        if (deadline.Elapsed > TimeSpan.FromSeconds(120)) throw new TimeoutException("Offscreen probe exceeded its 120-second budget.");
        var time = new GameTime(TimeSpan.FromSeconds(++frame / 60d), TimeSpan.FromSeconds(1 / 60d));
        manager.Tick(time); device.SetRenderTarget(target); manager.Draw(time); device.SetRenderTarget(null);
        if (owner.Visible) throw new InvalidOperationException("Offscreen owner became visible.");
    }
    void Move(Point p) => manager.InjectMouseMove(p.X, p.Y, target.Width, target.Height);
    void Tap(Keys key) { manager.InjectKeyDown(key); manager.InjectKeyUp(key); }
    void Click(Point p) { Move(p); manager.InjectMouseLeftDown(p.X, p.Y, target.Width, target.Height); manager.InjectMouseLeftUp(p.X, p.Y, target.Width, target.Height); }
    void Arranged() => arrangements++;
    void ObservePanel()
    {
        var panel = combo?.DropDownScrollViewer?.Content as StackPanel;
        if (ReferenceEquals(panel, observedPanel)) return;
        if (observedPanel != null) observedPanel.LayoutUpdated -= Arranged;
        observedPanel = panel; if (panel != null) panel.LayoutUpdated += Arranged;
    }
    void CreateCombo(int count)
    {
        combo?.Dispose(); Frame();
        combo = new ComboBox(scene) { Position = new(20, 45), Size = new(270, 30), Items = Enumerable.Range(0, count).Select(i => $"Instrument {i:0000}").ToList(), SelectedIndex = 0, BackgroundBlur = 12, MaxDropDownHeight = 220, DropDownBackgroundColor = new Color(16, 35, 30, 64) };
        scene.AddElement(combo); Frame();
    }
    Color[] Pixels() { var pixels = new Color[target.Width * target.Height]; target.GetData(pixels); return pixels; }
    void Drain() => target.GetData(0, new Rectangle(0, 0, 1, 1), new Color[1], 0, 1);
    static object Stats(IEnumerable<double> values)
    {
        var a = values.Order().ToArray();
        double At(double p) => a[Math.Clamp((int)Math.Ceiling(p * a.Length) - 1, 0, a.Length - 1)];
        return new { count = a.Length, p50 = At(.5), p95 = At(.95), max = a[^1] };
    }
    void Measure(string name, int count, int iterations, Action<int> action, Action? prepare = null)
    {
        var cpu = new List<double>(); var allocations = new List<double>(); var layout = new List<double>(); var render = new List<double>();
        Drain(); int before = arrangements; var wall = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            prepare?.Invoke();
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            action(i);
            cpu.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds); allocations.Add(GC.GetAllocatedBytesForCurrentThread() - bytes);
            layout.Add(scene.LastLayoutMilliseconds); render.Add(scene.LastRenderMilliseconds);
        }
        Drain(); wall.Stop(); // GPU completion/readback is outside per-operation CPU timing.
        measurements.Add(new { name, items = count, cpuMilliseconds = Stats(cpu), allocatedBytes = Stats(allocations), layoutMilliseconds = Stats(layout), renderSubmissionMilliseconds = Stats(render), arrangements = arrangements - before, completionBatchIncludesSetup = prepare != null, completionBatchMilliseconds = wall.Elapsed.TotalMilliseconds });
    }
    public void Benchmark(int frames)
    {
        foreach (int count in new[] { 90, 160, 1000 })
        {
            Measure("cold-open-and-first-frame", count, 3, _ => { combo!.IsDropDownOpen = true; Frame(); ObservePanel(); }, () => CreateCombo(count));
            for (int i = 0; i < 8; i++) Frame();
            Measure("steady-open", count, frames, _ => Frame());
            Measure("warm-close-reopen-and-frame", count, 20, _ => { combo!.IsDropDownOpen = false; Frame(); combo.IsDropDownOpen = true; ObservePanel(); Frame(); });
            // A baseline recreates the popup on reopen; capture the current one.
            var popup = combo!.DropDownScrollViewer;
            Measure("hover-and-frame", count, frames, i => { Move(new Point(popup.Rectangle.Left + 180, popup.Rectangle.Top + 10 + i % 4 * 28)); Frame(); });
            Measure("wheel-and-frame", count, frames, _ => { manager.InjectMouseWheel(popup.Rectangle.Center.X, popup.Rectangle.Center.Y, target.Width, target.Height, -120); Frame(); });
            popup.StopScrolling(); combo.IsDropDownOpen = false; Frame();
        }
    }
    static PropertyInfo PopupBlur => typeof(ComboBox).GetProperty("DropDownBackgroundBlur") ?? throw new InvalidOperationException("Missing popup-only blur property.");
    void SetBlur(float? radius) => PopupBlur.SetValue(combo, radius);
    static float? GetBlur(ComboBox value) => (float?)PopupBlur.GetValue(value);
    double Contrast(Color[] pixels, Rectangle popup)
    {
        int y = popup.Top + 2 * 28 + 23;
        return Enumerable.Range(popup.Left + 175, 60).Average(x => Math.Abs(pixels[y * target.Width + x].R - pixels[y * target.Width + x + 1].R));
    }
    void Capture(string name, Color[] pixels)
    {
        using var image = new Texture2D(device, target.Width, target.Height); image.SetData(pixels);
        using var file = File.Create(Path.Combine(output, name + ".png")); image.SaveAsPng(file, target.Width, target.Height);
    }
    public void CheckBehavior()
    {
        CreateCombo(160); combo!.ItemIsEnabled = i => i != 3 && i != 159;
        combo.ItemAppearance = (i, row, text) => { text.ForegroundColor = i == 3 || i == 159 ? Color.Gray : Color.White; if (i == 2) text.FontScale = 1.25f; };
        combo.IsDropDownOpen = true; Frame(); ObservePanel();
        var popup = combo.DropDownScrollViewer; var rows = combo.DropDownElements.ToArray();
        Check(popup.VerticalScrollBarIsVisible && popup.Size.Y <= 220 && popup.Rectangle.Bottom <= 480, "Large popup stays bounded and scrollable");
        Check(popup.BackgroundBlur == 12 && GetBlur(combo) == null, "Unset popup blur inherits BackgroundBlur");
        int before = arrangements; Frame(); Frame(); Check(arrangements == before, "Unchanged frames do not rearrange popup rows");
        combo.IsDropDownOpen = false; Frame(); combo.IsDropDownOpen = true; Frame();
        Check(ReferenceEquals(popup, combo.DropDownScrollViewer) && rows.SequenceEqual(combo.DropDownElements), "Reopening reuses popup rows and text");
        Check(rows[2].Children.OfType<TextElement>().Single().FontScale == 1.25f, "Custom row font properties survive reuse");
        manager.InjectMouseWheel(popup.Rectangle.Center.X, popup.Rectangle.Center.Y, 720, 480, -120); Frame();
        Check(popup.ScrollOffset.Y > 0, "Injected wheel scrolls popup"); popup.StopScrolling(); scene.FocusElement(combo);
        Tap(Keys.End); Frame(); Check(combo.HighlightedIndex == 158 && rows[158].Rectangle.Bottom <= popup.Rectangle.Bottom, "End reveals final enabled row");
        Tap(Keys.Enter); Frame(); Check(combo.SelectedIndex == 158 && !combo.IsDropDownOpen, "Enter selects enabled row");
        Tap(Keys.Space); Frame(); Tap(Keys.Home); Frame(); Tap(Keys.Down); Tap(Keys.Down); Tap(Keys.Down); Frame();
        Check(combo.HighlightedIndex == 4, "Keyboard navigation skips disabled rows");
        Tap(Keys.Home); Frame(); int selected = combo.SelectedIndex;
        Click(new Point(popup.Rectangle.Left + 180, popup.Rectangle.Top + 3 * 28 + 10)); Frame();
        Check(combo.SelectedIndex == selected, "Disabled rows cannot be selected");
        var thumb = popup.Children.OfType<RectangleElement>().Single(x => x.zIndex == int.MaxValue && x.isVisible && x.Size.X == 12);
        Move(thumb.Rectangle.Center); manager.InjectMouseLeftDown(thumb.Rectangle.Center.X, thumb.Rectangle.Center.Y, 720, 480);
        Move(new Point(popup.Rectangle.Right - 6, popup.Rectangle.Bottom - 2)); Frame();
        manager.InjectMouseLeftUp(popup.Rectangle.Right - 6, popup.Rectangle.Bottom - 2, 720, 480); Frame();
        Check(popup.ScrollOffset.Y > popup.ScrollableSize.Y * .9f, "Dragging scrollbar reaches end");
        Tap(Keys.Home); Frame();
        SetBlur(0); Frame(); var sharp = Pixels();
        SetBlur(12); Frame(); var blur = Pixels();
        SetBlur(24); Frame(); var stronger = Pixels();
        double sharpContrast = Contrast(sharp, popup.Rectangle), blurContrast = Contrast(blur, popup.Rectangle);
        Check(sharpContrast > 50 && blurContrast < sharpContrast * .45, "Actual ComboBox pixels blur the backdrop; zero disables blur");
        int outside = (popup.Rectangle.Top + 60) * target.Width + popup.Rectangle.Right + 4;
        Check(sharp[outside] == blur[outside], "Popup blur does not spill beyond its clipping bounds");
        Check(!sharp.SequenceEqual(stronger), "Popup blur radius affects pixels");
        Capture("popup-radius-0", sharp); Capture("popup-radius-12", blur); Capture("popup-radius-24", stronger);
        var bar = new RectangleElement(scene) { Size = new(32, 12), Margin = new Thickness(215, 8, 0, 0), BackgroundColor = Color.White }; rows[0].Children.Add(bar); Frame();
        var foreground = Pixels(); var p = bar.Rectangle.Center;
        Check(foreground[p.Y * target.Width + p.X].R > 245, "Backdrop blur keeps foreground geometry sharp");
        var textBounds = rows[1].Children.OfType<TextElement>().Single().Rectangle;
        Check(Enumerable.Range(textBounds.Top, Math.Max(1, Math.Min(24, textBounds.Height))).Any(y => Enumerable.Range(textBounds.Left, Math.Min(150, textBounds.Width)).Any(x => foreground[y * target.Width + x].R > 240)), "Native text retains sharp white glyph coverage");
        scene.Quality.EffectsEnabled = false; scene.RequestRender(); Frame();
        Check(Contrast(Pixels(), popup.Rectangle) > blurContrast * 2, "Disabling effects bypasses popup blur");
        scene.Quality.EffectsEnabled = true; SetBlur(null); combo.BackgroundBlur = 8; Frame();
        Check(popup.BackgroundBlur == 8, "Null restores inherited blur");
        var liveBackdrop = new RectangleElement(scene) { Position = popup.Position, Size = popup.Size, BackgroundColor = Color.Red }; scene.AddElement(liveBackdrop); Frame();
        var redBackdrop = Pixels(); liveBackdrop.BackgroundColor = Color.Blue; Frame(); var blueBackdrop = Pixels();
        int sample = (popup.Rectangle.Top + 2 * 28 + 23) * target.Width + popup.Rectangle.Left + 190;
        Check(redBackdrop[sample].R > blueBackdrop[sample].R + 40, "Popup blur samples the current backdrop instead of a stale snapshot");
        liveBackdrop.Dispose(); Frame();
        combo.MaxDropDownHeight = 140; combo.ItemHeight = 30; combo.Size = new(250, 34); Frame();
        Check(popup.Size.Y <= 140 && rows[0].Size.Y == 30 && rows[0].Size.X < 250, "Live height and width edits relayout cached rows");
        combo.Position = new(20, 440); Frame(); Check(popup.Rectangle.Bottom <= combo.Rectangle.Top, "Low header opens upward");
        target.Dispose(); target = NewTarget(540, 350); manager.ExternalViewportSize = new Point(540, 350); combo.Position = new(500, 300); Frame();
        Check(popup.Rectangle.Right <= 540 && popup.Rectangle.Bottom <= 350, "Viewport resize constrains popup");
        combo.IsDropDownOpen = false; combo.Items[5] = "Renamed instrument"; combo.IsDropDownOpen = true; Frame();
        Check(combo.DropDownElements[5].Children.OfType<TextElement>().Single().Text == "Renamed instrument", "Same-count mutable Items edits invalidate cache");
        Tap(Keys.Escape); Frame(); Check(!combo.IsDropDownOpen && !combo.DropDownScrollViewer.isVisible, "Escape hides retained popup");
        CheckTheme(); CheckBatching(); Check(SceneElement.RenderExceptionCount == 0, "No native rendering exceptions");
    }
    void CheckTheme()
    {
        const string markup = """
            <Scene xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="540" Height="350">
              <Scene.Resources><Style x:Key="PopupTheme" TargetType="ComboBox">
                <Setter Property="BackgroundBlur" Value="12"/><Setter Property="DropDownBackgroundBlur" Value="24"/>
                <Setter Property="MaxDropDownHeight" Value="180"/><Setter Property="DropDownBackgroundColor" Value="#4010231E"/>
              </Style></Scene.Resources>
              <ComboBox x:Name="styled" Style="{StaticResource PopupTheme}" Width="200" Height="30"/>
              <ComboBox x:Name="off" BackgroundBlur="12" DropDownBackgroundBlur="0"/>
              <ComboBox x:Name="inherited" BackgroundBlur="8" DropDownBackgroundBlur="{x:Null}"/>
            </Scene>
            """;
        var parser = new SceneXamlParser(scene); using var root = parser.Parse(markup);
        var styled = (ComboBox)parser.NamedElements["styled"];
        Check(styled.BackgroundBlur == 12 && GetBlur(styled) == 24 && styled.MaxDropDownHeight == 180 && styled.DropDownBackgroundColor.A == 64, "Theme style setters reach native popup properties");
        Check(GetBlur((ComboBox)parser.NamedElements["off"]) == 0 && GetBlur((ComboBox)parser.NamedElements["inherited"]) == null, "Direct XAML preserves zero/null fallback");
        foreach (float invalid in new[] { -1f, 101f, float.NaN, float.PositiveInfinity })
        {
            bool rejected = false;
            try { SetBlur(invalid); } catch (TargetInvocationException e) when (e.InnerException is ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid popup blur rejected: " + invalid);
        }
    }
    void CheckBatching()
    {
        using var panel = new StackPanel(scene); int count = 0; panel.LayoutUpdated += () => count++;
        var defer = typeof(StackPanel).GetMethod("DeferArrangement")!;
        using ((IDisposable)defer.Invoke(panel, null)!)
        {
            using ((IDisposable)defer.Invoke(panel, null)!)
                for (int i = 0; i < 160; i++) panel.Children.Add(new RectangleElement(scene) { Size = new(20, 28) });
            Check(count == 0, "Nested scopes defer arrangement");
        }
        Check(count == 1 && panel.Children[159].Position.Y == panel.Position.Y + panel.Padding.Top + 159 * 32, "Outer scope arranges once");
    }
    public void Save(bool baseline)
    {
        var surface = typeof(Scene).GetField("qualityTarget", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(scene) as RenderTarget2D;
        var effects = typeof(Scene).GetField("effectCanvas", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(scene) as RenderTarget2D;
        var q = scene.Quality;
        var receipt = new { baseline, backend = GraphicsBackendInfo.Current.ToString(), graphicsAssembly = typeof(GraphicsDevice).Assembly.FullName, adapter = device.Adapter.Description, profile = device.GraphicsProfile.ToString(), requestedAA = q.AntiAliasingSamples, supportedSamples = q.SupportedSamples(device, q.AntiAliasingSamples), technique = q.AntiAliasingTechnique, filtering = q.Filtering.ToString(), qualitySampler = new { filter = q.Sampler.Filter.ToString(), q.Sampler.MaxAnisotropy }, lastBoundSampler = device.SamplerStates[0]?.Filter.ToString(), qualitySurface = surface == null ? null : new { surface.Width, surface.Height, surface.MultiSampleCount }, effectSurface = effects == null ? null : new { effects.Width, effects.Height, effects.MultiSampleCount }, elapsedSeconds = deadline.Elapsed.TotalSeconds, measurements, checks };
        File.WriteAllText(Path.Combine(output, "measurements.json"), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllLines(Path.Combine(output, "result.txt"), checks);
        Console.WriteLine($"PASS offscreen {GraphicsBackendInfo.Current}; {measurements.Count} timing groups; {checks.Count} checks");
    }
    public void Dispose() { manager.Dispose(); target.Dispose(); batch.Dispose(); game.Dispose(); device.Dispose(); owner.Dispose(); }
}
