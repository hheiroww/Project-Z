using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.Designer;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing.UI.Primitives;
using Color = Microsoft.Xna.Framework.Color;
using Keys = Microsoft.Xna.Framework.Input.Keys;

internal static class Program
{
    internal static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    [STAThread]
    private static void Main(string[] args)
    {
#if PROJECTZ_WINDOWS_RUNTIME
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        ProjectZ.RuntimeDiagnostics.ApplicationDiagnostics.Initialize(new ProjectZ.RuntimeDiagnostics.DiagnosticOptions { DirectoryPath = Path.Combine(AppContext.BaseDirectory, "diagnostics"), ShowErrorDialogs = false });
#endif
        ProviderChecks.Run();
        if (OperatingSystem.IsMacOS() && File.Exists(Path.Combine(AppContext.BaseDirectory, "libProjectZDesktopCapture.dylib")))
        {
            var native = System.Runtime.InteropServices.NativeLibrary.Load("libProjectZDesktopCapture.dylib", typeof(Scene).Assembly, null);
            try
            {
                foreach (var export in new[] { "pz_capture_create", "pz_capture_bounds", "pz_capture_read", "pz_capture_destroy", "pz_capture_error", "pz_capture_request_permission" })
                    System.Runtime.InteropServices.NativeLibrary.GetExport(native, export);
                Console.WriteLine("NATIVE_MACOS_CAPTURE_LIBRARY_OK");
            }
            finally { System.Runtime.InteropServices.NativeLibrary.Free(native); }
        }
#if PROJECTZ_WINDOWS_RUNTIME
        var backend = args.FirstOrDefault(x => x.StartsWith("--backend="))?.Split('=')[1] ?? "DirectX12";
        Require(GraphicsBackendInfo.Current.ToString() == backend, "Wrong Windows graphics backend.");
#else
        Require(GraphicsBackendInfo.Current == ProjectZGraphicsBackend.OpenGL, "Wrong graphics backend.");
#endif
        var assembly = typeof(Scene).Assembly;
#if !PROJECTZ_WINDOWS_RUNTIME
        Require(!assembly.GetReferencedAssemblies().Any(a => new[] { "PresentationCore", "PresentationFramework", "WindowsBase", "System.Windows.Forms", "SocketJack", "Microsoft.Web.WebView2.Core" }.Contains(a.Name)), "Windows/private dependencies leaked into DesktopGL.");
#endif
        foreach (var name in new[] { "FXAA", "XamlEffects", "DesktopBlur" })
        {
            using var stream = assembly.GetManifestResourceStream($"ProjectZ.Resources.{name}.mgfx")!;
            Require(stream != null && stream.ReadByte() == 'M' && stream.ReadByte() == 'G' && stream.ReadByte() == 'F' && stream.ReadByte() == 'X', "Embedded shader missing or invalid.");
        }
        using var regular = new VectorFont(12);
        using var bold = new VectorFont("ProjectZ Sans", 700, 12);
        using var fallback = new VectorFont("Missing platform font", 400, 12);
        Require(regular.Measure("Linux / macOS").X > 40 && bold.Measure("Hello").Y > 0, "Compiled font metrics failed.");
#if !PROJECTZ_WINDOWS_RUNTIME
        Require(fallback.FamilyName == "ProjectZ Sans", "Platform font fallback failed.");
#endif
        Require(regular.Measure("A\r\nB").Y > regular.Measure("A").Y, "Multiline text layout failed.");
#if !PROJECTZ_WINDOWS_RUNTIME
        try { using var custom = new VectorFont("missing.ttf", 12); throw new Exception("Custom font did not report platform limitation."); }
        catch (PlatformNotSupportedException) { }
        Require(!OfflineSpellChecker.IsSupported, "Windows spelling advertised on DesktopGL.");
#endif
        if (args.Contains("--graphics"))
        {
            using var game = new SmokeGame(args.Contains("--desktop-blur"), args.Contains("--native-desktop"), args.FirstOrDefault(a => a.StartsWith("--capture="))?[10..], args.Contains("--backdrop-text"));
#if PROJECTZ_WINDOWS_RUNTIME
            if (args.Contains("--moving-desktop")) game.EnableMovingDesktopCheck();
#endif
            game.Run();
            Require(game.Completed, "Graphics smoke did not complete.");
        }
        Console.WriteLine("DESKTOPGL_CONSUMER_OK");
    }
}

internal sealed class SmokeScene(SceneManager manager) : Scene(manager);

internal sealed class SmokeGame : Game
{
    private readonly GraphicsDeviceManager graphics;
    private SceneManager manager = null!;
    private SpriteBatch batch = null!;
    private ContentContainer content = null!;
    private SmokeScene scene = null!;
    private Textbox textbox = null!;
    private int frames;
    private readonly bool testBackdrop;
    private readonly bool nativeBackdrop;
    private readonly bool testBackdropText;
    private readonly string? capturePath;
    private NativeBackdropFixture? nativeFixture;
    public bool Completed { get; private set; }
#if PROJECTZ_WINDOWS_RUNTIME
    private DesktopMotionCheck? motion;

    internal void EnableMovingDesktopCheck()
    {
        Program.Require(testBackdrop && nativeBackdrop, "Moving desktop checks need --desktop-blur and --native-desktop.");
        motion = new DesktopMotionCheck();
        graphics.SynchronizeWithVerticalRetrace = true;
    }
#endif

    public SmokeGame(bool testBackdrop = false, bool nativeBackdrop = false, string? capturePath = null, bool testBackdropText = false)
    {
        this.testBackdrop = testBackdrop;
        this.nativeBackdrop = nativeBackdrop;
        this.capturePath = capturePath;
        this.testBackdropText = testBackdropText;
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 480, PreferredBackBufferHeight = 320,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false
        };
        IsFixedTimeStep = false;
        Window.Title = "Project-Z DesktopGL package verification";
        if (nativeBackdrop) Window.Position = new Microsoft.Xna.Framework.Point(450, 350);
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        content = new ContentContainer(Content, GraphicsDevice);
        content.LoadAllContent();
        Services.AddService(typeof(SpriteBatch), batch);
        Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        Services.AddService(typeof(ContentContainer), content);
        manager = new SceneManager(this, 60);
        if (testBackdrop)
        {
            if (nativeBackdrop)
            {
#if PROJECTZ_WINDOWS_RUNTIME
                nativeFixture = new NativeBackdropFixture(ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(this));
#else
                nativeFixture = new NativeBackdropFixture(Window.Handle);
#endif
                Window.Position = new Microsoft.Xna.Framework.Point(450, 350);
            }
            else manager.DesktopBackdrop.CaptureSourceFactory = () => new BackdropFixture();
            manager.DesktopBackdrop.Settings.Enabled = true;
            manager.DesktopBackdrop.Settings.NoiseOpacity = 0;
            manager.DesktopBackdrop.ShowSetupPrompt = false;
        }
        scene = new SmokeScene(manager) { BackgroundColor = testBackdrop ? Color.Transparent : Color.Black };
        manager.AddScene("Smoke", scene);
        manager.ActiveScene = scene;
#if PROJECTZ_WINDOWS_RUNTIME
        manager.UseExternalInput = true;
#endif
        scene.AddElement(new RectangleElement(scene) { Position = new Vector2(20, 20), Size = new Vector2(180, 70), BackgroundColor = nativeBackdrop ? Color.Red : Color.LimeGreen, CornerRadius = 12 });
        scene.AddElement(new TextElement(scene) { Position = new Vector2(24, 110), Text = "Linux / macOS — ProjectZ Sans" });
        textbox = new Textbox(scene) { Position = new Vector2(20, 180), Size = new Vector2(300, 40), Text = "SDL clipboard ✓" };
        scene.AddElement(textbox);
        ProviderChecks.CheckMedia(scene, GraphicsDevice);
        foreach (var name in new[] { "FXAA", "XamlEffects", "DesktopBlur" })
        {
            using var stream = typeof(Scene).Assembly.GetManifestResourceStream($"ProjectZ.Resources.{name}.mgfx")!;
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            using var effect = new Effect(GraphicsDevice, bytes.ToArray());
            Program.Require(effect.Techniques.Count > 0, "OpenGL effect could not be loaded.");
        }
        var markup = Path.Combine(AppContext.BaseDirectory, "font-weight.xaml");
        File.WriteAllText(markup, "<TextBlock xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" FontWeight=\"SemiBold\" Text=\"Portable XAML\" />");
        var parser = new SceneXamlParser(scene);
        var parsed = (TextElement)parser.ParseFile(markup);
        Program.Require(parsed.FontWeight == 600, "Portable XAML font-weight parsing failed.");
        parsed.Dispose();
        if (!testBackdropText)
        {
            textbox.SelectAll();
            Program.Require(textbox.CopySelection(), "SDL clipboard copy failed.");
            textbox.Text = "";
            Program.Require(textbox.PasteClipboard() && textbox.Text == "SDL clipboard ✓", "SDL UTF-8 clipboard paste failed.");
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (testBackdrop) Thread.Sleep(2);
        if (!testBackdropText && frames == 1)
        {
            scene.FocusElement(textbox);
            textbox.SelectAll();
#if PROJECTZ_WINDOWS_RUNTIME
            manager.InjectTextInput("é");
#else
            Window.GetType().GetMethod("OnTextInput", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Window, [new TextInputEventArgs('é', Keys.None)]);
#endif
        }
#if PROJECTZ_WINDOWS_RUNTIME
        if (motion != null) motion.UpdateSource(nativeFixture);
#endif
        manager.Tick(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        manager.Draw(gameTime);
        frames++;
#if PROJECTZ_WINDOWS_RUNTIME
        if (motion?.Started == true)
        {
            if (motion.CheckFrame(GraphicsDevice, manager.DesktopBackdrop))
            {
                if (testBackdropText) VerifyBackdropText(gameTime);
            Completed = true;
                Exit();
            }
            return;
        }
#endif
        // A static desktop may deliver just one distinct capture.
        if (frames >= 4 && (!testBackdrop || manager.DesktopBackdrop.State == ProjectZ.Shared.Composition.DesktopBackdropState.Active))
        {
            if (!testBackdropText) Program.Require(textbox.Text == "é", $"Unicode TextInput failed: {textbox.Text}");
            Program.Require(SceneElement.RenderExceptionCount == 0, SceneElement.FirstRenderException ?? "Scene rendering failed.");
            var pixels = new Color[GraphicsDevice.PresentationParameters.BackBufferWidth * GraphicsDevice.PresentationParameters.BackBufferHeight];
            GraphicsDevice.GetBackBufferData(pixels);
            if (!nativeBackdrop) Program.Require(pixels.Any(p => p.G > 100 && p.G > p.R * 1.5f), "Scene did not render the green rectangle.");
            Program.Require(pixels.Any(p => p.R > 180 && p.G > 180 && p.B > 180), "Vector text was not rendered.");
            if (testBackdrop && !nativeBackdrop)
            {
                var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
                var left = pixels[290 * width + 30]; var center = pixels[290 * width + 240]; var right = pixels[290 * width + 450];
                Program.Require(left.R > 220 && left.B < 30 && right.B > 220 && right.R < 30, "Desktop material lost its source colors.");
                Program.Require(center.R > 40 && center.B > 40 && center.R < 215 && center.B < 215, "Gaussian desktop transition did not blur.");
                var largestStep = Enumerable.Range(185, 110).Max(x => Math.Max(Math.Abs(pixels[290 * width + x].R - pixels[290 * width + x + 1].R), Math.Abs(pixels[290 * width + x].B - pixels[290 * width + x + 1].B)));
                Program.Require(largestStep <= 8, $"Gaussian transition has visible contour bands: adjacent channel step={largestStep}.");
                Console.WriteLine("DESKTOP_BLUR_SHADER_OK");
            }
            if (nativeBackdrop)
            {
                var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
                var sample = pixels[100 * width + 100];
                Program.Require(sample.G > 225 && sample.R < 30 && sample.B < 30, $"Desktop source included the host or lost the controlled underlying window: RGBA={sample}.");
                var foreground = pixels[50 * width + 100];
                Program.Require(foreground.R > 225 && foreground.G < 30 && foreground.B < 30, "The host foreground did not stay sharp over the native backdrop.");
                Console.WriteLine($"NATIVE_DESKTOP_EXCLUSION_OK captureMs={manager.DesktopBackdrop.LastCaptureMilliseconds:F2}");
#if PROJECTZ_WINDOWS_RUNTIME
                if (motion != null)
                {
                    motion.Start(manager.DesktopBackdrop);
                    return;
                }
#endif
            }
            if (capturePath != null && !nativeBackdrop)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(capturePath))!);
                using var image = new Texture2D(GraphicsDevice, GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
                image.SetData(pixels);
                using var file = File.Create(capturePath);
                image.SaveAsPng(file, image.Width, image.Height);
            }
            if (testBackdropText) VerifyBackdropText(gameTime);
            Completed = true;
            Console.WriteLine("DESKTOPGL_GRAPHICS_OK");
            Exit();
        }
        if (frames > 300) throw new Exception("Desktop backdrop never became active: " + manager.DesktopBackdrop.StatusMessage);
        base.Draw(gameTime);
    }

    private void VerifyBackdropText(GameTime gameTime)
    {
        const int width = 480, height = 320;
        using var target = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        var dark = new Color(20, 20, 20);
        var light = new Color(235, 235, 235);
        scene.BackgroundColor = Color.Transparent;
        var darkPane = new RectangleElement(scene) { Position = new Vector2(0, 240), Size = new Vector2(101, 80), BackgroundColor = dark };
        var lightPane = new RectangleElement(scene) { Position = new Vector2(101, 240), Size = new Vector2(109, 80), BackgroundColor = light };
        scene.AddElement(darkPane);
        scene.AddElement(lightPane);
        scene.AddElement(new TextElement(scene) { Position = new Vector2(5, 250), Text = new string('M', 55), ForegroundColor = Color.Cyan });
        scene.AddElement(new TextElement(scene) { Position = new Vector2(8, 287), Text = "Colored annotation", ForegroundColor = Color.Magenta });
        scene.Tick(gameTime);

        var before = Render();
        int seam = Enumerable.Range(70, 40).FirstOrDefault(x => Enumerable.Range(250, 33)
            .Any(y => before[y * width + x - 1] == Color.Cyan && before[y * width + x] == Color.Cyan));
        Program.Require(seam != 0, "The fixture did not place any glyph stroke across the backdrop seam.");
        darkPane.Size = new Vector2(seam, 80);
        lightPane.Position = new Vector2(seam, 240);
        lightPane.Size = new Vector2(210 - seam, 80);
        scene.Tick(gameTime);
        before = Render();
        scene.BackdropTextContrastColor = Color.Cyan;
        var after = Render();
        var darkInk = 0;
        var lightInk = 0;
        var transparentInk = 0;
        var separateInk = 0;
        for (var y = 245; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                if (before[index] == Color.Cyan)
                {
                    if (x < seam)
                    {
                        Program.Require(after[index] == Color.White, "Dark scene pixels did not turn selected glyph ink white.");
                        darkInk++;
                    }
                    else if (x < 210)
                    {
                        Program.Require(after[index] == Color.Black, "Light scene pixels did not turn selected glyph ink black.");
                        lightInk++;
                    }
                    else
                    {
                        Program.Require(after[index] == Color.Cyan, "Unknown compositor background changed the authored ink.");
                        transparentInk++;
                    }
                }
                else if (before[index] == Color.Magenta)
                {
                    Program.Require(after[index] == before[index], "A foreground color outside the opt-in changed.");
                    separateInk++;
                }
            }
        }

        Program.Require(darkInk > 15 && lightInk > 15 && transparentInk > 15 && separateInk > 15,
            $"Expected solid samples in every region, got dark={darkInk}, light={lightInk}, clear={transparentInk}, other={separateInk}.");
        Program.Require(after[242 * width + 30] == dark && after[242 * width + 180] == light && after[242 * width + 240].A == 0,
            "The shader changed the backdrop outside the glyph drawing.");
        var crossing = Enumerable.Range(250, 33).Count(y =>
            before[y * width + seam - 1] == Color.Cyan && before[y * width + seam] == Color.Cyan &&
            after[y * width + seam - 1] == Color.White && after[y * width + seam] == Color.Black);
        Program.Require(crossing > 0, "A single glyph stroke did not react independently on both sides of the backdrop seam.");
        Console.WriteLine($"BACKDROP_TEXT_CONTRAST_OK dark={darkInk} light={lightInk} clear={transparentInk} other={separateInk} crossing={crossing}");

        Color[] Render()
        {
            GraphicsDevice.SetRenderTarget(target);
            scene.Draw(gameTime);
            GraphicsDevice.SetRenderTarget(null);
            var colors = new Color[width * height];
            target.GetData(colors);
            return colors;
        }
    }

    protected override void UnloadContent()
    {
        manager?.Dispose();
        nativeFixture?.Dispose();
        foreach (var font in content.VectorFonts.Values) font.Dispose();
        batch?.Dispose();
        base.UnloadContent();
    }
}
