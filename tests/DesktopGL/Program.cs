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

internal static class Program
{
    internal static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        ProviderChecks.Run();
        Require(GraphicsBackendInfo.Current == ProjectZGraphicsBackend.OpenGL, "Wrong graphics backend.");
        var assembly = typeof(Scene).Assembly;
        Require(!assembly.GetReferencedAssemblies().Any(a => new[] { "PresentationCore", "PresentationFramework", "WindowsBase", "System.Windows.Forms", "SocketJack", "Microsoft.Web.WebView2.Core" }.Contains(a.Name)), "Windows/private dependencies leaked into DesktopGL.");
        foreach (var name in new[] { "FXAA", "XamlEffects", "DesktopBlur" })
        {
            using var stream = assembly.GetManifestResourceStream($"ProjectZ.Resources.{name}.mgfx")!;
            Require(stream != null && stream.ReadByte() == 'M' && stream.ReadByte() == 'G' && stream.ReadByte() == 'F' && stream.ReadByte() == 'X', "Embedded shader missing or invalid.");
        }
        using var regular = new VectorFont(12);
        using var bold = new VectorFont("ProjectZ Sans", 700, 12);
        using var fallback = new VectorFont("Missing platform font", 400, 12);
        Require(regular.Measure("Linux / macOS").X > 40 && bold.Measure("Hello").Y > 0, "Compiled font metrics failed.");
        Require(fallback.FamilyName == "ProjectZ Sans", "Platform font fallback failed.");
        Require(regular.Measure("A\r\nB").Y > regular.Measure("A").Y, "Multiline text layout failed.");
        try { using var custom = new VectorFont("missing.ttf", 12); throw new Exception("Custom font did not report platform limitation."); }
        catch (PlatformNotSupportedException) { }
        Require(!OfflineSpellChecker.IsSupported, "Windows spelling advertised on DesktopGL.");
        if (args.Contains("--graphics"))
        {
            using var game = new SmokeGame(args.Contains("--desktop-blur"));
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
    public bool Completed { get; private set; }

    public SmokeGame(bool testBackdrop = false)
    {
        this.testBackdrop = testBackdrop;
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 480, PreferredBackBufferHeight = 320,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false
        };
        IsFixedTimeStep = false;
        Window.Title = "Project-Z DesktopGL package verification";
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
            manager.DesktopBackdrop.CaptureSourceFactory = () => new BackdropFixture();
            manager.DesktopBackdrop.Settings.Enabled = true;
            manager.DesktopBackdrop.Settings.NoiseOpacity = 0;
            manager.DesktopBackdrop.ShowSetupPrompt = false;
        }
        scene = new SmokeScene(manager) { BackgroundColor = testBackdrop ? Color.Transparent : Color.Black };
        manager.AddScene("Smoke", scene);
        manager.ActiveScene = scene;
        scene.AddElement(new RectangleElement(scene) { Position = new Vector2(20, 20), Size = new Vector2(180, 70), BackgroundColor = Color.LimeGreen, CornerRadius = 12 });
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
        textbox.SelectAll();
        Program.Require(textbox.CopySelection(), "SDL clipboard copy failed.");
        textbox.Text = "";
        Program.Require(textbox.PasteClipboard() && textbox.Text == "SDL clipboard ✓", "SDL UTF-8 clipboard paste failed.");
    }

    protected override void Update(GameTime gameTime)
    {
        if (testBackdrop) Thread.Sleep(2);
        if (frames == 1)
        {
            scene.FocusElement(textbox);
            textbox.SelectAll();
            Window.GetType().GetMethod("OnTextInput", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Window, [new TextInputEventArgs('é', Keys.None)]);
        }
        manager.Tick(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        manager.Draw(gameTime);
        frames++;
        if (frames >= 4 && (!testBackdrop || manager.DesktopBackdrop.State == ProjectZ.Shared.Composition.DesktopBackdropState.Active))
        {
            Program.Require(textbox.Text == "é", $"Unicode TextInput failed: {textbox.Text}");
            Program.Require(SceneElement.RenderExceptionCount == 0, SceneElement.FirstRenderException ?? "Scene rendering failed.");
            var pixels = new Color[GraphicsDevice.PresentationParameters.BackBufferWidth * GraphicsDevice.PresentationParameters.BackBufferHeight];
            GraphicsDevice.GetBackBufferData(pixels);
            Program.Require(pixels.Any(p => p.G > 100 && p.G > p.R * 1.5f), "Scene did not render the green rectangle.");
            Program.Require(pixels.Any(p => p.R > 180 && p.G > 180 && p.B > 180), "Vector text was not rendered.");
            if (testBackdrop)
            {
                var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
                var left = pixels[290 * width + 30]; var center = pixels[290 * width + 240]; var right = pixels[290 * width + 450];
                Program.Require(left.R > 220 && left.B < 30 && right.B > 220 && right.R < 30, "Desktop material lost its source colors.");
                Program.Require(center.R > 40 && center.B > 40 && center.R < 215 && center.B < 215, "Gaussian desktop transition did not blur.");
                Console.WriteLine("DESKTOP_BLUR_SHADER_OK");
            }
            Completed = true;
            Console.WriteLine("DESKTOPGL_GRAPHICS_OK");
            Exit();
        }
        if (frames > 300) throw new Exception("Desktop backdrop never became active: " + manager.DesktopBackdrop.StatusMessage);
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        manager?.Dispose();
        foreach (var font in content.VectorFonts.Values) font.Dispose();
        batch?.Dispose();
        base.UnloadContent();
    }
}
