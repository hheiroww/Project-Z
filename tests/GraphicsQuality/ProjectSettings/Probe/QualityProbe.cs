using System.Globalization;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using Color = Microsoft.Xna.Framework.Color;

namespace ProjectSettingsProbe;

// C# launches this reusable Game directly; VB derives its Game in the executable.
// Executable project settings must win over this library's inherited defaults.
public class QualityProbe : Game
{
    const int Width = 320, Height = 240;
    readonly GraphicsDeviceManager graphics;
    readonly int expectedSamples;
    readonly TextureFiltering expectedFiltering;
    readonly string report;
    SpriteBatch batch = null!;

    public static bool CheckMetadata(string[] args)
    {
        if (args.Length == 0 || args[0] != "--metadata") return false;
        var attributes = Assembly.GetEntryAssembly()!.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(a => a.Key, a => a.Value);
        Require(attributes["ProjectZ.AntiAliasingSamples"] == args[1], "AA metadata matches project");
        Require(attributes["ProjectZ.TextureFiltering"] == args[2], "filtering metadata matches project");
        File.WriteAllText(args[3], $"PASS metadata: {Assembly.GetEntryAssembly()!.GetName().Name}, project {args[1]}x / {args[2]}");
        return true;
    }

    public QualityProbe(string[] args)
    {
        expectedSamples = int.Parse(args[0], CultureInfo.InvariantCulture);
        expectedFiltering = Enum.Parse<TextureFiltering>(args[1]);
        report = Path.GetFullPath(args[2]);
        graphics = new(this)
        {
            PreferredBackBufferWidth = Width,
            PreferredBackBufferHeight = Height,
            GraphicsProfile = GraphicsProfile.HiDef,
            SynchronizeWithVerticalRetrace = false
        };
        // Existing hosts may construct their manager before Game.Run creates
        // the graphics device; project quality must remain lazy until rendering.
        using (var earlyManager = new SceneManager(this, 60)) { }
        Window.Title = "Project-Z project settings regression";
    }

    protected override void LoadContent()
    {
        batch = new(GraphicsDevice);
        var resources = new ContentContainer(Content, GraphicsDevice);
        resources.LoadVectorFontsOnly();
        Services.AddService(typeof(SpriteBatch), batch);
        Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        Services.AddService(typeof(ContentContainer), resources);
    }

    protected override void Draw(GameTime time)
    {
        try
        {
            using var manager = new SceneManager(this, 60);
            using var scene = new ProbeScene(manager) { BackgroundColor = Color.Black, EnableDesignTimeControls = false };
            var quality = scene.Quality;
            Require(quality.AntiAliasingSamples == expectedSamples,
                $"project AA reached native scene: expected {expectedSamples}, got {quality.AntiAliasingSamples}");
            Require(quality.Filtering == expectedFiltering,
                $"project filtering reached native scene: expected {expectedFiltering}, got {quality.Filtering}");
            var attributes = Assembly.GetEntryAssembly()!.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(a => a.Key, a => a.Value);
            Require(attributes["ProjectZ.AntiAliasingSamples"] == expectedSamples.ToString(CultureInfo.InvariantCulture), "AA metadata matches project");
            Require(attributes["ProjectZ.TextureFiltering"] == expectedFiltering.ToString(), "filtering metadata matches project");

            // No runtime AA assignment precedes this draw. Fractional edge pixels
            // distinguish actual smoothing from merely reading project metadata.
            scene.Tick(time);
            foreach (var effect in scene.Effects) effect.Dispose();
            scene.Effects.Clear();
            using var triangle = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreateOrthographicOffCenter(0, Width, Height, 0, 0, 1)
            };
            using var rasterizer = new RasterizerState { CullMode = CullMode.None, MultiSampleAntiAlias = true };
            var vertices = new[]
            {
                new VertexPositionColor(new(20.3f, 20.7f, 0), Color.White),
                new VertexPositionColor(new(210.6f, 87.2f, 0), Color.White),
                new VertexPositionColor(new(63.1f, 200.4f, 0), Color.White)
            };
            scene.PreDraw += _ =>
            {
                GraphicsDevice.BlendState = BlendState.Opaque;
                GraphicsDevice.DepthStencilState = DepthStencilState.None;
                GraphicsDevice.RasterizerState = rasterizer;
                foreach (var pass in triangle.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 1);
                }
            };
            using var target = new RenderTarget2D(GraphicsDevice, Width, Height);
            GraphicsDevice.SetRenderTarget(target);
            scene.Draw(time);
            Require(GraphicsDevice.GetRenderTargets().Single().RenderTarget == target, "scene restores caller render target");
            GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[Width * Height];
            target.GetData(pixels);
            int fractional = pixels.Count(pixel => pixel.R is > 0 and < 255);
            Require(pixels[70 * Width + 70].R == 255, "triangle is visible");
            Require(expectedSamples == 0 ? fractional == 0 : fractional > 100,
                $"project AA changes real edge coverage: {fractional} fractional pixels");
            using (var png = File.Create(report + ".png")) target.SaveAsPng(png, Width, Height);

            // Application/host overrides must survive new scenes and managers.
            quality.AntiAliasingSamples = 2;
            quality.Filtering = TextureFiltering.NearestNeighbor;
            using var nextManager = new SceneManager(this);
            using var nextScene = new ProbeScene(nextManager);
            Require(ReferenceEquals(nextScene.Quality, quality), "quality remains device scoped");
            Require(nextScene.Quality.AntiAliasingSamples == 2 && nextScene.Quality.Filtering == TextureFiltering.NearestNeighbor,
                "later runtime overrides are preserved");
            File.WriteAllText(report,
                $"PASS {Assembly.GetEntryAssembly()!.GetName().Name}: {GraphicsBackendInfo.Current}, {quality.AntiAliasingTechnique}, project {expectedSamples}x / {expectedFiltering}, fractional pixels={fractional}; runtime override preserved");
        }
        catch (Exception error)
        {
            File.WriteAllText(report, "FAIL " + error);
            Environment.ExitCode = 1;
        }
        Exit();
    }

    protected override void UnloadContent() => batch?.Dispose();

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    sealed class ProbeScene(SceneManager manager) : Scene(manager);
}
