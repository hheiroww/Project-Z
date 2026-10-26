using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing.UI.Primitives;
using Color = Microsoft.Xna.Framework.Color;
using Button = ProjectZ.Shared.Drawing.UI.Input.Button;

static class HoverQualityChecks
{
    public static void Run(Game game, GameTime time, string output, List<string> results)
    {
        var device = game.GraphicsDevice;
        var quality = GraphicsQuality.ForDevice(device);
        using var manager = new SceneManager(game);
        using var scene = new HoverScene(manager) { BackgroundColor = Color.Black, EnableDesignTimeControls = false };
        scene.Initialize(time);
        foreach (var post in scene.Effects) post.Dispose();
        scene.Effects.Clear();
        using var triangle = new CoverageTriangle(scene, device);
        scene.AddElement(triangle);
        using var effect = new XamlNativeEffect { Kind = XamlEffectKind.ChromaticAberration, Amount = 0 };
        var button = new Button(scene) { Position = new Vector2(350, 120), Size = new Vector2(150, 36), Text = "Hover", VisualEffect = effect };
        button.MouseEnter += () => effect.Kind = XamlEffectKind.PassThrough;
        button.MouseLeave += () => effect.Kind = XamlEffectKind.ChromaticAberration;
        scene.AddElement(button);
        scene.Tick(time);
        using var target = new RenderTarget2D(device, 640, 300);
        Color[] Capture(string name)
        {
            device.SetRenderTarget(target);
            scene.Draw(time);
            Require(device.GetRenderTargets().Single().RenderTarget == target, "hover restores caller render target");
            device.SetRenderTarget(null);
            var pixels = new Color[640 * 300]; target.GetData(pixels);
            using var file = File.Create(Path.Combine(output, name + ".png")); target.SaveAsPng(file, 640, 300);
            return pixels;
        }
        foreach (int samples in new[] { 0, 4, 16, 4 })
        {
            quality.AntiAliasingSamples = samples;
            button.isMouseOver = false;
            var idle = Capture("hover-idle-" + samples);
            button.isMouseOver = true;
            var hover = Capture("hover-active-" + samples);
            button.isMouseOver = false;
            var leave = Capture("hover-leave-" + samples);
            int changed = 0, fractional = 0, maximumDelta = 0;
            for (int y = 0; y < 210; y++) for (int x = 0; x < 225; x++)
            {
                int index = y * 640 + x;
                if (idle[index] != hover[index] || idle[index] != leave[index]) changed++;
                maximumDelta = Math.Max(maximumDelta, Math.Max(Math.Abs(idle[index].R - hover[index].R), Math.Abs(idle[index].R - leave[index].R)));
                if (hover[index].R is > 0 and < 255) fractional++;
            }
            // An additional resolve can round a coverage value by one 8-bit level.
            Require(maximumDelta <= 1, "hover must preserve coverage outside the reactive control; changed=" + changed + "; maximumDelta=" + maximumDelta);
            int actual = quality.SupportedSamples(device, samples);
            Require(actual == 0 ? fractional == 0 : fractional > 100, "hover retains fractional antialiased edge coverage");
            var renderers = (ConditionalWeakTable<SceneElement, XamlNativeEffect>)typeof(XamlNativeEffect).GetField("renderers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(effect)!;
            Require(renderers.TryGetValue(button, out var renderer), "hover draws the actual effect source");
            var source = (RenderTarget2D)typeof(XamlNativeEffect).GetField("source", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer)!;
            Require(source.MultiSampleCount == (quality.AntiAliasingTechnique == "MSAA" ? actual : 0), "reactive control source retains AA and follows changed quality settings");
            results.Add($"PASS hover/leave: requested {samples}x; static coverage delta<={maximumDelta}/255; fractional edges={fractional}; source samples={source.MultiSampleCount}");
        }
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    sealed class HoverScene(SceneManager manager) : Scene(manager);
    sealed class CoverageTriangle : SceneElement, IDisposable
    {
        readonly GraphicsDevice device;
        readonly BasicEffect effect;
        readonly RasterizerState rasterizer = new() { CullMode = CullMode.None, MultiSampleAntiAlias = true };
        readonly VertexPositionColor[] vertices = [new(new(20.3f,20.7f,0),Color.White),new(new(210.6f,87.2f,0),Color.White),new(new(63.1f,200.4f,0),Color.White)];
        public CoverageTriangle(Scene scene, GraphicsDevice device) : base(scene)
        {
            this.device = device; Size = new Vector2(225,210);
            effect = new(device) { VertexColorEnabled = true, Projection = Matrix.CreateOrthographicOffCenter(0,640,300,0,0,1) };
        }
        protected override void Draw(GameTime time)
        {
            device.BlendState = BlendState.Opaque; device.DepthStencilState = DepthStencilState.None; device.RasterizerState = rasterizer;
            foreach (var pass in effect.CurrentTechnique.Passes) { pass.Apply(); device.DrawUserPrimitives(PrimitiveType.TriangleList,vertices,0,1); }
        }
        protected override void Dispose(bool disposing) { if (disposing) { effect.Dispose(); rasterizer.Dispose(); } base.Dispose(disposing); }
    }
}
