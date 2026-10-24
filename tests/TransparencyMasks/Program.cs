using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing.UI.Primitives;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

using var game = new MaskProbe(args[0]);
game.Run();

sealed class ProbeScene(SceneManager manager) : Scene(manager);
sealed class MaskProbe : Game {
    readonly string output;
    readonly GraphicsDeviceManager graphics;
    readonly List<string> results = [];
    public MaskProbe(string output) {
        this.output = Path.GetFullPath(output); Directory.CreateDirectory(this.output);
        graphics = new(this) { PreferredBackBufferWidth = 420, PreferredBackBufferHeight = 220, GraphicsProfile = GraphicsProfile.HiDef };
        Content.RootDirectory = "Content"; Window.Title = "Project-Z transparent slider masking";
    }
    protected override void LoadContent() {
        var batch = new SpriteBatch(GraphicsDevice);
        var resources = new ContentContainer(Content, GraphicsDevice); resources.LoadAllContent();
        Services.AddService(typeof(SpriteBatch), batch); Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        Services.AddService(typeof(ContentContainer), resources);
    }
    protected override void Draw(GameTime time) {
        try {
            using var manager = new SceneManager(this);
            using var scene = new ProbeScene(manager) { BackgroundColor = Color.Transparent, EnableDesignTimeControls = false, UseRenderTarget = false };
            scene.Initialize(time); foreach (var effect in scene.Effects) effect.Dispose(); scene.Effects.Clear();
            var slider = new Trackbar(scene) { Position = new(40, 40), Size = new(226, 30), ShowValue = false, MinimumValue = 0, MaximumValue = 100 };
            scene.AddElement(slider);
            using var target = new RenderTarget2D(GraphicsDevice, 420, 220);
            foreach (var samples in new[] {0, 16})
            foreach (var percent in new[] {0, 33, 49, 100})
            foreach (var value in new[] {0, 50, 100}) {
                slider.Value = value;
                slider.TrackColor = new Color(60, 75, 85) * (percent / 100f);
                slider.ThumbColor = new Color(57, 255, 20) * (percent / 100f);
                scene.Tick(time); GraphicsQuality.ForDevice(GraphicsDevice).AntiAliasingSamples = samples;
                GraphicsDevice.SetRenderTarget(target); scene.Draw(time); GraphicsDevice.SetRenderTarget(null);
                var pixels = new Color[420 * 220]; target.GetData(pixels);
                var thumb = slider.Children.OfType<RectangleElement>().Last().Rectangle;
                var center = pixels[thumb.Center.Y * 420 + thumb.Center.X];
                using (var image = File.Create(Path.Combine(output, $"slider-{samples}-{percent}-{value}.png"))) target.SaveAsPng(image,420,220);
                var expected = (byte)(255 * percent / 100f);
                Require(Math.Abs(center.A - expected) <= 1, $"thumb has a single {percent}% alpha layer, actual={center.A}, expected={expected}");
                Require(thumb.Left >= slider.Rectangle.Left && thumb.Right <= slider.Rectangle.Right, "thumb stays within the control at both range endpoints");
                results.Add($"PASS {samples}x AA, opacity={percent}%, value={value}: thumb alpha={center.A}");
            }
            var fragments = RectangleMask.Exclude(new(0,0,40,20), [new(10,0,10,20),new(15,0,10,20),new(-5,-5,2,2)]);
            Require(fragments.Sum(r=>r.Width*r.Height)==500, "overlapping masks subtract their union exactly once");
            for(int i=0;i<fragments.Count;i++) for(int j=i+1;j<fragments.Count;j++)
                Require(Rectangle.Intersect(fragments[i],fragments[j]).IsEmpty,"mask fragments never overlap");
            var parent = new RectangleElement(scene) {Position=new(300,110),Size=new(60,40),BackgroundColor=Color.Transparent,Clip=true};
            var masked = new RectangleElement(scene) {Size=new(90,40),BackgroundColor=new Color(50,180,100)*.33f};
            parent.Children.Add(masked); scene.AddElement(parent); scene.Tick(time);
            masked.BackgroundCutouts=()=>[new(masked.Rectangle.Left+20,masked.Rectangle.Top,20,40)];
            var brush = new XamlGradientBrush {StartPoint=Vector2.Zero,EndPoint=Vector2.UnitX};
            brush.Stops.Add(new(){Offset=0,Color=new Color(255,0,0,84)});brush.Stops.Add(new(){Offset=1,Color=new Color(0,0,255,84)});
            var rounded = new RectangleElement(scene) {Position=new(40,110),Size=new(120,40),CornerRadius=8,BackgroundBrush=brush};
            scene.AddElement(rounded);scene.Tick(time);
            foreach(var samples in new[]{0,16}) {
                GraphicsQuality.ForDevice(GraphicsDevice).AntiAliasingSamples=samples;
                rounded.BackgroundCutouts=null;
                Color[] Render() {GraphicsDevice.SetRenderTarget(target);scene.Draw(time);GraphicsDevice.SetRenderTarget(null);var pixels=new Color[420*220];target.GetData(pixels);return pixels;}
                var before=Render();
                rounded.BackgroundCutouts=()=>[new(90,110,20,40)];var after=Render();
                Require(after[130*420+100].A==0,"rounded gradient cutout is transparent");
                foreach(var x in new[]{55,75,125,145})Require(after[130*420+x]==before[130*420+x],"cutouts preserve original gradient coordinates");
                var r=masked.Rectangle;var y=r.Center.Y;
                Require(Math.Abs(after[y*420+r.Left+10].A-84)<=1 && after[y*420+r.Left+30].A==0,"background cutout respects transparent panel fill");
                Require(after[y*420+parent.Rectangle.Right+5].A==0,"mask drawing preserves parent clipping");
                using(var png=File.Create(Path.Combine(output,$"rounded-clipped-{samples}.png")))target.SaveAsPng(png,420,220);
                results.Add($"PASS {samples}x AA: rounded gradient mapping, rectangular holes, parent clipping");
            }
            Require(SceneElement.RenderExceptionCount == 0, "no render errors");
        } catch (Exception e) { results.Add("FAIL " + e); Environment.ExitCode = 1; }
        File.WriteAllLines(Path.Combine(output,"result.txt"),results); Exit();
    }
    static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
}
