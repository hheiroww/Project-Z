using Microsoft.Xna.Framework;
using Color = Microsoft.Xna.Framework.Color;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Primitives;
using ProjectZ.Shared.Drawing.Designer;

using var game = new QualityProbe(args.Length > 0 ? args[0] : "quality-results");
game.Run();

sealed class ProbeScene(SceneManager manager) : Scene(manager);

sealed class QualityProbe : Game
{
    readonly string output;
    readonly GraphicsDeviceManager graphics;
    readonly List<string> results = new();
    SpriteBatch batch;
    ContentContainer resources;

    public QualityProbe(string directory)
    {
        output = Path.GetFullPath(directory);
        Directory.CreateDirectory(output);
        graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 640, PreferredBackBufferHeight = 300, GraphicsProfile = GraphicsProfile.HiDef };
        Content.RootDirectory = "Content";
        Window.Title = "Project-Z AA / AF regression";
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        resources = new ContentContainer(Content, GraphicsDevice);
        resources.LoadAllContent();
        Services.AddService(typeof(SpriteBatch), batch);
        Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        Services.AddService(typeof(ContentContainer), resources);
    }

    protected override void Draw(GameTime time)
    {
        try
        {
            var quality = GraphicsQuality.ForDevice(GraphicsDevice);
            using var manager = new SceneManager(this);
            using var scene = new ProbeScene(manager) { BackgroundColor = Color.Black, EnableDesignTimeControls = false };
            scene.Initialize(time);
            // Remove unrelated post effects so fractional triangle coverage proves MSAA.
            foreach (var effect in scene.Effects) effect.Dispose();
            scene.Effects.Clear();
            using var effect3d = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true, Projection = Matrix.CreateOrthographicOffCenter(0, 640, 300, 0, 0, 1) };
            using var rasterizer = new RasterizerState { CullMode = CullMode.None, MultiSampleAntiAlias = true };
            var vertices = new[] {
                new VertexPositionColor(new Vector3(20.3f,20.7f,0), Color.White),
                new VertexPositionColor(new Vector3(210.6f,87.2f,0), Color.White),
                new VertexPositionColor(new Vector3(63.1f,200.4f,0), Color.White) };
            scene.PreDraw += _ => {
                GraphicsDevice.BlendState = BlendState.Opaque;
                GraphicsDevice.DepthStencilState = DepthStencilState.None;
                GraphicsDevice.RasterizerState = rasterizer;
                foreach (var pass in effect3d.CurrentTechnique.Passes) {
                    pass.Apply();
                    GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 1);
                }
            };
            var text = new TextElement(scene) { Text = "Screenshot  Settings  Aa Bb 0123456789", Font = "SegoeUI_10", Position = new Vector2(240, 40), MaxWidth = 390 };
            scene.AddElement(text);
            scene.Tick(time);
            using var target = new RenderTarget2D(GraphicsDevice, 640, 300);
            foreach (var offscreen in new[] { false, true })
            foreach (var samples in new[] { 0, 2, 4, 8, 16 })
            {
                scene.UseRenderTarget = offscreen;
                quality.AntiAliasingSamples = samples;
                GraphicsDevice.SetRenderTarget(target);
                scene.Draw(time);
                Require(GraphicsDevice.GetRenderTargets().Single().RenderTarget == target, "scene restores caller target");
                GraphicsDevice.SetRenderTarget(null);
                var pixels = new Color[640 * 300];
                target.GetData(pixels);
                var fractional = Enumerable.Range(0, 210).Sum(y => Enumerable.Range(0, 225).Count(x => pixels[y * 640 + x].R is > 0 and < 255));
                var actual = quality.SupportedSamples(GraphicsDevice, samples);
                Require(pixels[70 * 640 + 70].R == 255, "triangle is visible");
                results.Add($"PASS {quality.AntiAliasingTechnique}: requested {samples}x, effective {actual}x, UseRenderTarget={offscreen}, fractional edge pixels={fractional}");
                using var png = File.Create(Path.Combine(output, $"aa-{samples}-target-{offscreen}.png"));
                target.SaveAsPng(png, 640, 300);
                Require(actual == 0 ? fractional == 0 : fractional > 100, "AA produces fractional edge coverage");
            }
            scene.UseRenderTarget = false;
            quality.AntiAliasingSamples = 0;
            foreach (var filtering in Enum.GetValues<TextureFiltering>())
            {
                quality.Filtering = filtering;
                GraphicsDevice.SetRenderTarget(target);
                scene.Draw(time);
                GraphicsDevice.SetRenderTarget(null);
                var pixels = new Color[640 * 300];
                target.GetData(pixels);
                Require(pixels.Skip(35 * 640).Take(25 * 640).Where((c, i) => i % 640 >= 240).Any(c => c.R > 0), "filtered text remains visible");
                results.Add($"PASS {filtering}: {quality.Sampler.Filter}, AF={quality.Sampler.MaxAnisotropy}");
            }
            Require(resources.Fonts.Count == 0 && resources.VectorFonts.Values.All(f => f.FilePath is null && f.FamilyName == "ProjectZ Sans"), "default text uses compiled free outlines without font files or atlases");
            using (var fontScene = new ProbeScene(manager) { BackgroundColor = new Color(16, 22, 30), EnableDesignTimeControls = false })
            {
                fontScene.Initialize(time);
                fontScene.AddElement(new TextElement(fontScene) { Text = "ProjectZ Sans — vector outlines", Font = "ProjectZSans_24", Position = new Vector2(18, 12), MaxWidth = 600 });
                fontScene.AddElement(new TextElement(fontScene) { Text = "10 pt: Settings  Account  Aa Bb 0123456789", Font = "ProjectZSans_10", Position = new Vector2(20, 72), MaxWidth = 600 });
                fontScene.AddElement(new TextElement(fontScene) { Text = "14 pt: Café — Ελληνικά Русский", Font = "ProjectZSans_14", Position = new Vector2(20, 108), MaxWidth = 600 });
                fontScene.AddElement(new TextElement(fontScene) { Text = "AVATAR  OQ  @&  0123456789", Font = ContentContainer.SystemFontName("ProjectZ Sans", 700, 22.5f), Position = new Vector2(20, 158), MaxWidth = 600 });
                fontScene.AddElement(new TextElement(fontScene) { Text = "Aa 012", Font = "ProjectZSans_48", Position = new Vector2(20, 208), MaxWidth = 600 });
                fontScene.Tick(time);
                quality.AntiAliasingSamples = 16; quality.Filtering = TextureFiltering.Anisotropic16x;
                GraphicsDevice.SetRenderTarget(target); fontScene.Draw(time); GraphicsDevice.SetRenderTarget(null);
                using var png = File.Create(Path.Combine(output, "compiled-vector-font-16x.png")); target.SaveAsPng(png, 640, 300);
            }
            using var fxScene = new ProbeScene(manager) { BackgroundColor = Color.Black, EnableDesignTimeControls = false };
            fxScene.Initialize(time);
            var parser = new SceneXamlParser(fxScene);
            var fxRoot = parser.Parse("""
                <Canvas xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="640" Height="300">
                  <Border x:Name="Blur" Canvas.Left="60" Canvas.Top="60" Width="60" Height="40" Background="White" BorderThickness="0"><Border.Effect><BlurEffect Radius="4"/></Border.Effect></Border>
                  <Canvas x:Name="Clip" Canvas.Left="220" Canvas.Top="70" Width="60" Height="40" ClipToBounds="True">
                    <Border Canvas.Left="40" Canvas.Top="0" Width="60" Height="40" Background="Red"/>
                  </Canvas>
                </Canvas>
                """);
            fxScene.AddElement(fxRoot);
            fxScene.Tick(time);
            foreach (var samples in new[] { 0, 2, 4, 8, 16 })
            {
                quality.AntiAliasingSamples = samples;
                GraphicsDevice.SetRenderTarget(target);
                fxScene.Draw(time);
                GraphicsDevice.SetRenderTarget(null);
                var pixels = new Color[640 * 300];
                target.GetData(pixels);
                using (var png = File.Create(Path.Combine(output, $"effects-{samples}.png"))) target.SaveAsPng(png, 640, 300);
                var blur = parser.FindName<SceneElement>("Blur").Rectangle;
                var clip = parser.FindName<SceneElement>("Clip").Rectangle;
                Require(pixels[(blur.Top + blur.Height / 2) * 640 + blur.Center.X].R > 240, "scaled effect remains at logical position");
                Require(pixels[(blur.Top + blur.Height / 2) * 640 + blur.Left - 2].R > 0, "scaled blur halo remains visible");
                Require(pixels[clip.Center.Y * 640 + clip.Right - 5].R > 240 && pixels[clip.Center.Y * 640 + clip.Right + 5].R == 0, "scaled clipping retains logical bounds");
                results.Add($"PASS {samples}x AA: effect placement, blur halo, parent clipping");
            }
            Require(SceneElement.RenderExceptionCount == 0, "no swallowed rendering errors");
            results.Add("PASS compiled free vector font at 10–48pt, fractional bold, AA 16x / AF 16x; zero font atlases or render errors");
            File.WriteAllLines(Path.Combine(output, "result.txt"), results);
        }
        catch (Exception ex)
        {
            File.WriteAllLines(Path.Combine(output, "result.txt"), results.Append("FAIL " + ex));
            Environment.ExitCode = 1;
        }
        Exit();
    }

    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
