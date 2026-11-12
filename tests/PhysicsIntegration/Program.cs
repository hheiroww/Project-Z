using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Animations.Properties;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.Designer;
using ProjectZ.Shared.Drawing.Particles;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Advanced;
using ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing.UI.Primitives;
using ProjectZ.Shared.Physics;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

using var game = new PhysicsProbe(args.Length == 0 ? throw new ArgumentException("An owned output directory is required.") : args[0]);
game.Run();

sealed class ProbeScene(SceneManager manager) : Scene(manager);
sealed class DirectParticleEmitter(Scene scene) : ParticleEmitterElement(scene, 64, 42)
{
    public void DrawDirect() => base.doDraw(new GameTime());
}

sealed class PhysicsProbe : Game
{
    readonly string output;
    readonly GraphicsDeviceManager graphics;
    readonly List<string> results = new();
    SpriteBatch batch;
    ContentContainer resources;
    SceneManager manager;
    double total;

    public PhysicsProbe(string directory)
    {
        output = Path.GetFullPath(directory);
        Directory.CreateDirectory(output);
        graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 320, PreferredBackBufferHeight = 240, GraphicsProfile = GraphicsProfile.HiDef };
        Window.Title = "Project-Z physics and GPU particle integration";
        IsFixedTimeStep = false;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        resources = new ContentContainer(Content, GraphicsDevice);
        resources.LoadAllContent();
        Services.AddService(typeof(SpriteBatch), batch);
        Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        Services.AddService(typeof(ContentContainer), resources);
        manager = new SceneManager(this) { UseExternalInput = true, ExternalViewportSize = new Point(320, 240) };
    }

    ProbeScene NewScene()
    {
        var scene = new ProbeScene(manager) { BackgroundColor = Color.Black, EnableDesignTimeControls = false, UseRenderTarget = false };
        scene.Initialize(new GameTime());
        foreach (var effect in scene.Effects) effect.Dispose();
        scene.Effects.Clear();
        manager.ActiveScene = scene;
        return scene;
    }

    void Tick(Scene scene, double seconds)
    {
        total += seconds;
        scene.Tick(new GameTime(TimeSpan.FromSeconds(total), TimeSpan.FromSeconds(seconds)));
    }

    protected override void Draw(GameTime time)
    {
        try
        {
            GraphicsQuality.ForDevice(GraphicsDevice).AntiAliasingSamples = 0;
            CheckMotionAndParameters();
            CheckScrolling();
            CheckNestedWheelRouting();
            CheckParticles();
            CheckPointerInput();
            CheckCursorResolution();
            CheckXamlAndNativePolling();
            Require(SceneElement.RenderExceptionCount == 0, "No swallowed render errors");
            File.WriteAllLines(Path.Combine(output, "result.txt"), results);
            Console.WriteLine(string.Join(Environment.NewLine, results));
        }
        catch (Exception ex)
        {
            results.Add("FAIL " + ex);
            File.WriteAllLines(Path.Combine(output, "result.txt"), results);
            Console.Error.WriteLine(ex);
            Environment.ExitCode = 1;
        }
        Exit();
    }

    void CheckMotionAndParameters()
    {
        using var scene = NewScene();
        var body = new RectangleElement(scene) { Position = new Vector2(20, 20), Size = new Vector2(10), Gravity = new Vector2(0, 100) };
        var surface = new Surface3DElement(scene) { Position3D = Vector3.Zero, Gravity3D = new Vector3(0, -10, 0) };
        scene.AddElement(body); scene.AddElement(surface);
        Tick(scene, 0.1);
        Require(body.RenderTranslation == Vector2.Zero && surface.Position3D == Vector3.Zero, "Gravity disabled without RigidBody in 2D and 3D");
        body.RigidBody = true; surface.RigidBody = true;
        Tick(scene, 0.1);
        Require(body.RenderTranslation.Y > 0 && body.Position == new Vector2(20, 20), "Physics feeds existing RenderTranslation without layout movement");
        Require(surface.Position3D.Y < 0 && surface.Velocity3D.Y < 0, "3D physics feeds Position3D");
        body.RigidBody = false;
        var positionBody = new RectangleElement(scene) { Position = new Vector2(100, 100), Size = new Vector2(10), RigidBody = true, PhysicsMotion = PhysicsMotion.Position, Gravity = Vector2.Zero, Velocity = new Vector2(20, 0) };
        scene.AddElement(positionBody); Tick(scene, 0.1);
        Require(positionBody.Position.X > 101 && positionBody.RenderTranslation == Vector2.Zero, "Opt-in physics feeds existing Position");
        var driver = body.DriveWithPhysics(new OpacityProperty(body));
        driver.Minimum = 0; driver.Maximum = 1; driver.Stiffness = 100; driver.Damping = 20; driver.Target = 0.25;
        for (int i = 0; i < 120; i++) Tick(scene, 1.0 / 120);
        Require(body.Opacity > 0.24 && body.Opacity < 0.26, "PhysicsScalar drives existing animation opacity parameter");
        var newBody = new RectangleElement(scene) { Position = new Vector2(240, 100), Size = new Vector2(10), CollisionEnabled = true, Gravity = Vector2.Zero };
        var newSurface = new Surface3DElement(scene) { Gravity3D = Vector3.Zero, CollisionEnabled = true };
        scene.AddElement(newBody); scene.AddElement(newSurface); Tick(scene, 0);
        newBody.RigidBody = true; newSurface.RigidBody = true;
        newBody.ApplyPhysicsImpulse(new Vector2(100, 0)); newSurface.ApplyPhysicsImpulse(new Vector3(100, 0, 0));
        Require(newBody.Velocity.X > 0 && newSurface.Velocity3D.X > 0, "Enabling previously static body accepts immediate 2D and 3D impulse");
        var box3d = new Surface3DElement(scene) { CollisionEnabled = true, PhysicsSize3D = new Vector3(10), Position3D = new Vector3(20) };
        var child3d = new Surface3DElement(scene) { CollisionEnabled = true, PhysicsSize3D = new Vector3(2), Position3D = new Vector3(29), RigidBody = true, Gravity3D = Vector3.Zero };
        box3d.Children.Add(child3d); scene.AddElement(box3d); Tick(scene, 0); scene.ResolvePhysicsContacts();
        Require(child3d.Position3D.X <= 28.001f && child3d.Position3D.Y <= 28.001f && child3d.Position3D.Z <= 28.001f, "3D child corrects initial parent-boundary penetration");
        using var separate = NewScene();
        var firstGroup = new RectangleElement(separate) { Size = new Vector2(200) };
        var secondGroup = new RectangleElement(separate) { Size = new Vector2(200) };
        var firstChild = new RectangleElement(separate) { Position = new Vector2(30), Size = new Vector2(20), CollisionEnabled = true, RigidBody = true, Gravity = Vector2.Zero };
        var secondChild = new RectangleElement(separate) { Position = new Vector2(30), Size = new Vector2(20), CollisionEnabled = true, RigidBody = true, Gravity = Vector2.Zero };
        firstGroup.Children.Add(firstChild); secondGroup.Children.Add(secondChild);
        separate.AddElement(firstGroup); separate.AddElement(secondGroup); Tick(separate, 0);
        firstChild.Position = new Vector2(30); secondChild.Position = new Vector2(30); separate.ResolvePhysicsContacts();
        Require(firstChild.RenderTranslation == Vector2.Zero && secondChild.RenderTranslation == Vector2.Zero, "Different nonphysics parent groups do not collide");
    }

    void CheckScrolling()
    {
        using var scene = NewScene();
        var light = new ScrollViewer(scene) { Size = new Vector2(120, 150), Weight = 1, RigidBody = true, Gravity = Vector2.Zero };
        var heavy = new ScrollViewer(scene) { Position = new Vector2(160, 0), Size = new Vector2(120, 150), Weight = 8, RigidBody = true, Gravity = Vector2.Zero };
        light.Content = new RectangleElement(scene) { Size = new Vector2(100, 2000) };
        heavy.Content = new RectangleElement(scene) { Size = new Vector2(100, 2000) };
        scene.AddElement(light); scene.AddElement(heavy); Tick(scene, 0);
        light.ApplyWheelImpulse(-120); heavy.ApplyWheelImpulse(-120);
        Require(light.ScrollVelocity.Y > heavy.ScrollVelocity.Y * 7.9f, "Weight reduces wheel velocity impulse");
        Tick(scene, 0.05);
        Require(light.ScrollOffset.Y > heavy.ScrollOffset.Y && heavy.ScrollOffset.Y > 0, "Weighted scrolling changes existing offset parameter");
        var geometry = new ScrollViewer(scene) { Size = new Vector2(40, 200), FrictionType = FrictionType.Geometry };
        geometry.Content = new RectangleElement(scene) { Size = new Vector2(30, 2000) };
        scene.AddElement(geometry); Tick(scene, 0);
        geometry.ApplyWheelImpulse(-120); Tick(scene, 0.01);
        Require(geometry.PhysicsBody.Size.X == geometry.Size.X && geometry.PhysicsBody.Size.Y == geometry.Size.Y,
            "Geometry scrolling refreshes actual viewport shape without a rigid body");
    }

    void CheckPointerInput()
    {
        using var scene = NewScene();
        var parent = new RectangleElement(scene) { Position = new Vector2(10), Size = new Vector2(200), CollisionEnabled = true };
        var child = new RectangleElement(scene) { Position = new Vector2(30), Size = new Vector2(20), CollisionEnabled = true, FreeMove = true, RigidBody = true, Gravity = Vector2.Zero };
        var wall = new RectangleElement(scene) { Position = new Vector2(110, 20), Size = new Vector2(20, 180), CollisionEnabled = true };
        parent.Children.Add(child); parent.Children.Add(wall); scene.AddElement(parent); Tick(scene, 0);
        // Children participate in normal scene layout; position explicitly after registration.
        child.Position = new Vector2(30); wall.Position = new Vector2(110, 20);
        scene.ResolvePhysicsContacts();
        manager.InjectMouseLeftDown(35, 35, 320, 240);
        Require(scene.CapturedPointerElement == child, "FreeMove mouse press captures child");
        manager.InjectMouseMove(100, 35, 320, 240);
        Require(child.Rectangle.Right <= wall.Rectangle.Left, "Mouse drag stops at first static sibling contact");
        manager.CancelInjectedPointer();
        Require(scene.CapturedPointerElement == null && child.Velocity == Vector2.Zero, "Mouse cancellation releases capture and stops fling");
        child.RenderTranslation = Vector2.Zero; child.Position = new Vector2(30);
        manager.InjectTouchDown(7, 35, 35, 320, 240);
        Require(scene.CapturedPointerElement == child, "FreeMove touch captures child");
        manager.InjectTouchMove(7, 400, 35, 320, 240);
        Require(child.Rectangle.Right <= parent.Rectangle.Right && child.Rectangle.Left >= parent.Rectangle.Left, "Touch drag respects parent bounds");
        Require(child.Rectangle.Right <= wall.Rectangle.Left, "Large touch jump cannot tunnel through static wall");
        manager.CancelTouchInput();
        Require(scene.CapturedPointerElement == null && child.Velocity == Vector2.Zero, "Touch cancellation stops fling");
        child.RenderTranslation = Vector2.Zero; child.Position = new Vector2(30);
        manager.InjectTouchDown(8, 35, 35, 320, 240);
        manager.CancelExternalInput();
        manager.InjectTouchDown(9, 35, 35, 320, 240);
        Require(scene.CapturedPointerElement == child, "External input cancellation releases touch ownership for a new contact");
        manager.CancelInjectedPointer();
        child.RenderTranslation = Vector2.Zero; child.Position = new Vector2(30);
        manager.InjectMouseLeftDown(35, 35, 320, 240);
        manager.InjectMouseMove(55, 35, 320, 240);
        // A held contact has no residual fling after its last movement becomes stale.
        System.Threading.Thread.Sleep(120);
        manager.InjectMouseLeftUp(55, 35, 320, 240);
        Require(child.Velocity == Vector2.Zero, "Stationary release discards stale fling velocity");
    }

    void CheckCursorResolution()
    {
        using var scene = NewScene();
        var parser = new SceneXamlParser(scene);
        var root = parser.Parse("""
            <Canvas xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="320" Height="240">
              <Button x:Name="Action" X="10" Y="10" Width="90" Height="24" Text="Action" />
              <Button x:Name="Disabled" X="10" Y="10" Width="90" Height="24" Text="Disabled" IsEnabled="False" Cursor="Hand" />
              <TextBox x:Name="Editor" X="10" Y="45" Width="90" Height="24" Text="Editable" />
              <TextBlock x:Name="Selectable" X="10" Y="85" Width="100" Height="24" Text="Selectable" IsTextSelectionEnabled="True" />
              <TextBlock x:Name="Plain" X="10" Y="125" Width="90" Height="24" Text="Ordinary" />
              <Rectangle x:Name="ExplicitParent" X="140" Y="10" Width="75" Height="55" Cursor="Arrow" ClipToBounds="True">
                <Button x:Name="Nested" X="6" Y="5" Width="98" Height="22" Text="Nested" />
              </Rectangle>
            </Canvas>
            """);
        var action = parser.FindName<SceneElement>("Action");
        var disabled = parser.FindName<SceneElement>("Disabled");
        var editor = parser.FindName<SceneElement>("Editor");
        var parent = parser.FindName<SceneElement>("ExplicitParent");
        var nested = parser.FindName<SceneElement>("Nested");
        var horizontal = new RectangleElement(scene) { Position = new(10, 160), Size = new(85, 20), isResizable = AlignmentType.Horizontal };
        var vertical = new RectangleElement(scene) { Position = new(110, 160), Size = new(20, 65), isResizable = AlignmentType.Vertical };
        scene.AddElement(root);
        scene.AddElement(horizontal);
        scene.AddElement(vertical);
        Tick(scene, 0);
        // The probe skips Draw's layout pass. Set scene coordinates before testing the nested clip.
        nested.Position = parent.Position + new Vector2(6, 5);

        var actionPoint = action.Rectangle.Center;
        Require(scene.PointToElement(actionPoint) == action && scene.ResolveCursor(actionPoint) == SceneCursor.Arrow, "A disabled surface stays Arrow without changing click hit-testing");
        disabled.isVisible = false;
        Require(scene.ResolveCursor(actionPoint) == SceneCursor.Hand, "An actionable button and its bypassed label use Hand");
        Require(scene.ResolveCursor(editor.Rectangle.Center) == SceneCursor.IBeam, "Editable text uses IBeam");
        Require(scene.ResolveCursor(parser.FindName<SceneElement>("Selectable").Rectangle.Center) == SceneCursor.IBeam, "Selectable display text uses IBeam");
        Require(scene.ResolveCursor(parser.FindName<SceneElement>("Plain").Rectangle.Center) == SceneCursor.Arrow, "Ordinary display text uses Arrow");

        var nestedPoint = new Point(nested.Rectangle.Left + 8, nested.Rectangle.Center.Y);
        Require(scene.PointToElement(nestedPoint) == nested && parent.CursorOverride == SceneCursor.Arrow &&
            scene.ResolveCursor(nestedPoint) == SceneCursor.Arrow, "An explicit ancestor Arrow wins over the nested button's inferred Hand");
        nested.CursorOverride = SceneCursor.Cross;
        Require(scene.ResolveCursor(nestedPoint) == SceneCursor.Cross, "The nearest explicit override wins");
        Require(scene.ResolveCursor(new(parent.Rectangle.Right + 8, nestedPoint.Y)) == SceneCursor.Arrow, "A clipped-off child cannot set its cursor");
        parent.isEnabled = false;
        Require(scene.ResolveCursor(nestedPoint) == SceneCursor.Arrow, "Disabled ancestors suppress cursor overrides");
        parent.isEnabled = true;

        var popup = new Popup(scene) { Position = action.Position, Size = action.Size, IsOpen = true, CursorOverride = SceneCursor.Wait };
        scene.AddElement(popup);
        Tick(scene, 0);
        Require(scene.ResolveCursor(actionPoint) == SceneCursor.Wait, "The topmost popup takes cursor ownership");
        scene.CapturePointer(editor);
        Require(scene.ResolveCursor(actionPoint) == SceneCursor.IBeam && scene.ResolveCursor(new(-5, -5)) == SceneCursor.IBeam, "Capture keeps the text cursor across a popup and outside scene geometry");
        scene.ReleasePointerCapture(editor);
        Require(scene.ResolveCursor(actionPoint) == SceneCursor.Wait, "Releasing capture restores the popup cursor");

        var edge = new Point(horizontal.Rectangle.Left + 1, horizontal.Rectangle.Center.Y);
        Require(scene.ResolveCursor(edge) == SceneCursor.SizeWE &&
            scene.ResolveCursor(new(horizontal.Rectangle.Right - 1, edge.Y)) == SceneCursor.SizeWE &&
            scene.ResolveCursor(horizontal.Rectangle.Center) == SceneCursor.Arrow, "Horizontal resize uses only the existing edge grips");
        Require(scene.ResolveCursor(new(vertical.Rectangle.Center.X, vertical.Rectangle.Top + 1)) == SceneCursor.SizeNS &&
            scene.ResolveCursor(new(vertical.Rectangle.Center.X, vertical.Rectangle.Bottom - 1)) == SceneCursor.SizeNS &&
            scene.ResolveCursor(vertical.Rectangle.Center) == SceneCursor.Arrow, "Vertical resize uses only the existing edge grips");
        manager.InjectMouseMove(edge.X, edge.Y, 320, 240);
        scene.CapturePointer(horizontal);
        Require(scene.ResolveCursor(actionPoint) == SceneCursor.SizeWE, "Captured resize keeps its axis while dragging away from the grip");
        scene.ReleasePointerCapture(horizontal);
    }

    void CheckNestedWheelRouting()
    {
        using var scene = NewScene();
        var outer = new ScrollViewer(scene) { Size = new Vector2(200, 140) };
        var content = new RectangleElement(scene) { Size = new Vector2(180, 560) };
        var inner = new ScrollViewer(scene) { Position = new Vector2(12, 8), Size = new Vector2(140, 90) };
        var innerContent = new RectangleElement(scene) { Size = new Vector2(120, 360) };
        var button = new ProjectZ.Shared.Drawing.UI.Input.Button(scene) { Position = new Vector2(12, 8), Size = new Vector2(80, 28), Text = "Wheel here" };
        innerContent.Children.Add(button);
        inner.Content = innerContent;
        content.Children.Add(inner);
        outer.Content = content;
        scene.AddElement(outer);
        Tick(scene, 0);
        var point = button.Rectangle.Center;
        Require(scene.PointToElement(point) == button, "Wheel test is above a child button");
        manager.InjectMouseWheel(point.X, point.Y, 320, 240, -30);
        Require(inner.ScrollVelocity.Y > 0 && outer.ScrollVelocity.Y == 0, "Precision wheel goes to the nearest panel over a button");
        Tick(scene, 0.02);
        Require(inner.ScrollOffset.Y > 0 && outer.ScrollOffset.Y == 0, "Parent remains still while the child scrolls");
        inner.StopScrolling();
        inner.ScrollTo(inner.ScrollableSize);
        Tick(scene, 0);
        point = inner.Rectangle.Center;
        manager.InjectMouseWheel(point.X, point.Y, 320, 240, -120);
        Require(inner.ScrollVelocity.Y == 0 && outer.ScrollVelocity.Y > 0, "Wheel continues to the parent at the child's bottom");
        outer.StopScrolling();
        outer.ScrollTo(new Vector2(0, 8));
        inner.ScrollTo(Vector2.Zero);
        Tick(scene, 0);
        point = inner.Rectangle.Center;
        manager.InjectMouseWheel(point.X, point.Y, 320, 240, 30);
        Require(inner.ScrollVelocity.Y == 0 && outer.ScrollVelocity.Y < 0, "Reverse precision wheel continues at the child's top");
    }

    void CheckParticles()
    {
        using var scene = NewScene();
        using var target = new RenderTarget2D(GraphicsDevice, 320, 240, false, SurfaceFormat.Color, DepthFormat.Depth24);
        using var atlasTexture = new Texture2D(GraphicsDevice, 2, 1);
        atlasTexture.SetData(new[] { Color.Red, Color.Blue });
        using var atlas = new ParticleSpriteAnimation();
        atlas.AddFrame(atlasTexture, new Rectangle(0, 0, 1, 1), 0.1);
        atlas.AddFrame(atlasTexture, new Rectangle(1, 0, 1, 1), 0.1);
        var emitter = new DirectParticleEmitter(scene) { SpriteAnimation = atlas, FadeOut = false };
        emitter.Simulation.EmissionRate = 0;
        emitter.Simulation.InitialVelocity = Vector3.Zero; emitter.Simulation.VelocitySpread = Vector3.Zero;
        emitter.Simulation.EmissionPosition = new Vector3(60, 80, 0);
        emitter.Simulation.ParticleSize = new Vector2(40);
        emitter.Simulation.ParticleLifetime = 10;
        emitter.Simulation.Emit(20);
        scene.AddElement(emitter);
        var marker = new RectangleElement(scene) { Position = new Vector2(280, 210), Size = new Vector2(20), BackgroundColor = Color.Lime };
        scene.AddElement(marker); Tick(scene, 0);
        var red = Render(scene, target, "particles-2d-red.png");
        Require(red[80 * 320 + 60].R > 240 && red[80 * 320 + 60].B < 10, "GPU 2D atlas particle is visible");
        Require(emitter.LastDrawCallCount == 1, "20 same-atlas particles use one GPU draw");
        Require(red[220 * 320 + 290].G > 240, "Normal scene drawing still works after particle pass");
        emitter.Simulation.Advance(0.12);
        var blue = Render(scene, target, "particles-2d-blue.png");
        Require(blue[80 * 320 + 60].B > 240 && blue[80 * 320 + 60].R < 10, "Animated atlas advances GPU UV frame");
        using var customTexture = new Texture2D(GraphicsDevice, 1, 1);
        customTexture.SetData(new[] { Color.Yellow });
        using var customAnimation = new ParticleSpriteAnimation();
        customAnimation.AddFrame(atlasTexture, new Rectangle(0, 0, 1, 1), 0.1);
        customAnimation.AddFrame(customTexture, new Rectangle(0, 0, 1, 1), 0.2);
        emitter.SpriteAnimation = customAnimation;
        var custom = Render(scene, target, "particles-custom-frames.png");
        Require(custom[80 * 320 + 60].R > 240 && custom[80 * 320 + 60].G > 240 && custom[80 * 320 + 60].B < 10, "Custom managed sprite frames switch textures on GPU");

        var pngPath = Path.Combine(output, "source-atlas.png");
        using (var file = File.Create(pngPath)) atlasTexture.SaveAsPng(file, 2, 1);
        using var loadedSheet = ParticleSpriteAnimation.LoadSpriteSheet(GraphicsDevice, pngPath, 1, 1, 0.1, 2);
        Require(loadedSheet.Frames.Count == 2, "PNG sprite sheet loader");
        using var loadedPng = ParticleSpriteAnimation.LoadPng(GraphicsDevice, pngPath);
        Require(loadedPng.Frames.Count == 1, "PNG static sprite loader");

        var gifPath = Path.Combine(output, "source-animation.gif");
        File.WriteAllBytes(gifPath, AnimatedGif());
        using var gif = ParticleSpriteAnimation.LoadGif(GraphicsDevice, gifPath);
        emitter.SpriteAnimation = gif;
        emitter.Simulation.Clear(); emitter.Simulation.Emit(); emitter.Simulation.Advance(0.07);
        var green = Render(scene, target, "particles-gif.png");
        Require(gif.Frames.Count == 2 && green[80 * 320 + 60].G > 240, "Animated GIF delay, decode and GPU atlas upload");
        Require(emitter.LastDrawCallCount == 1, "GIF atlas remains one draw");

        emitter.Is3D = true; emitter.SpriteAnimation = loadedPng;
        emitter.Position = new Vector2(80, 0); emitter.Size = new Vector2(160, 240);
        emitter.Simulation.Clear(); emitter.Simulation.EmissionPosition = Vector3.Zero;
        emitter.Simulation.ParticleSize = new Vector2(1); emitter.Simulation.Emit();
        var three = Render(scene, target, "particles-3d.png");
        Require(three[120 * 320 + 155].R > 150 || three[120 * 320 + 155].B > 150, "GPU 3D camera-facing sprite visible in scene viewport");
        Require(three[220 * 320 + 290].G > 240, "2D scene and 3D emitter coexist");
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.BlendState = BlendState.Additive;
        GraphicsDevice.DepthStencilState = DepthStencilState.None;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        GraphicsDevice.SamplerStates[0] = SamplerState.PointWrap;
        emitter.DrawDirect();
        Require(ReferenceEquals(GraphicsDevice.BlendState, BlendState.Additive) && ReferenceEquals(GraphicsDevice.DepthStencilState, DepthStencilState.None)
            && ReferenceEquals(GraphicsDevice.RasterizerState, RasterizerState.CullNone) && ReferenceEquals(GraphicsDevice.SamplerStates[0], SamplerState.PointWrap), "Particle pass restores blend, depth, rasterizer and sampler state");
        GraphicsDevice.SetRenderTarget(null);
    }

    void CheckXamlAndNativePolling()
    {
        using var scene = NewScene();
        var parser = new SceneXamlParser(scene);
        var root = parser.Parse("""
            <Canvas xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="320" Height="240">
              <Border x:Name="Body" Width="20" Height="20" RigidBody="True" FreeMove="True" CollisionEnabled="True" Weight="7" Bounce="0.4" FrictionType="Ice" Gravity="0,40" Velocity="3,4" PhysicsMotion="Position"/>
              <Border x:Name="Alias" Width="20" Height="20" RidgidBody="True" Gravity="0,0"/>
              <Surface3D x:Name="Surface" Width="100" Height="100" RigidBody="True" Position3D="1,2,3" PhysicsSize3D="4,5,6" Velocity3D="7,8,9" Gravity3D="0,-2,0"/>
              <ParticleEmitter x:Name="Emitter" Width="100" Height="100" Is3D="True" Position3D="2,3,4" Capacity="12" EmissionRate="3" ParticleLifetime="4" ParticleSize="2,3" InitialVelocity="4,5,6" VelocitySpread="0,0,0" ParticlePhysicsEnabled="True" ParticleGravityEnabled="True" ParticleGravity="0,-4,0" LinearDrag="0.5"/>
            </Canvas>
            """);
        scene.AddElement(root);
        var body = parser.FindName<SceneElement>("Body");
        var alias = parser.FindName<SceneElement>("Alias");
        var surface = parser.FindName<Surface3DElement>("Surface");
        var emitter = parser.FindName<ParticleEmitterElement>("Emitter");
        Require(body.RigidBody && body.FreeMove && body.CollisionEnabled && body.Weight == 7 && body.FrictionType == FrictionType.Ice
            && body.Gravity == new Vector2(0, 40) && body.Velocity == new Vector2(3, 4) && body.PhysicsMotion == PhysicsMotion.Position, "Runtime XAML binds 2D physics properties");
        Require(alias.RigidBody && surface.Position3D == new Vector3(1, 2, 3) && surface.PhysicsSize3D == new Vector3(4, 5, 6)
            && surface.Velocity3D == new Vector3(7, 8, 9) && surface.Gravity3D == new Vector3(0, -2, 0), "Runtime XAML binds RidgidBody alias and 3D properties");
        Require(emitter.Is3D && emitter.Position3D == new Vector3(2, 3, 4) && emitter.Simulation.Capacity == 12 && emitter.Simulation.EmissionRate == 3
            && emitter.Simulation.ParticleLifetime == 4 && emitter.Simulation.ParticleSize == new Vector2(2, 3)
            && emitter.Simulation.InitialVelocity == new Vector3(4, 5, 6) && emitter.Simulation.GravityEnabled && emitter.Simulation.Gravity == new Vector3(0, -4, 0)
            && emitter.Simulation.LinearDrag == 0.5, "Runtime XAML creates configured 3D particle emitter");
        manager.UseExternalInput = false;
        try
        {
            manager.Tick(new GameTime(TimeSpan.FromSeconds(total), TimeSpan.FromSeconds(0.001)));
            Require(true, "Native mouse/touch polling path executes without exception");
        }
        finally { manager.UseExternalInput = true; manager.CancelExternalInput(); }
    }

    Color[] Render(Scene scene, RenderTarget2D target, string file)
    {
        var viewport = GraphicsDevice.Viewport;
        GraphicsDevice.SetRenderTarget(target);
        scene.Draw(new GameTime());
        Require(GraphicsDevice.GetRenderTargets().Single().RenderTarget == target, "Particle scene preserves caller render target");
        Require(GraphicsDevice.Viewport.Bounds == new Rectangle(0, 0, 320, 240), "Particle pass restores scene viewport");
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[320 * 240]; target.GetData(pixels);
        using var stream = File.Create(Path.Combine(output, file)); target.SaveAsPng(stream, 320, 240);
        GraphicsDevice.Viewport = viewport;
        return pixels;
    }

    static byte[] AnimatedGif() => new byte[] {
        71,73,70,56,57,97,1,0,1,0,128,0,0, 255,0,0, 0,255,0,
        33,249,4,0,5,0,0,0, 44,0,0,0,0,1,0,1,0,0,2,2,68,1,0,
        33,249,4,0,10,0,0,0,44,0,0,0,0,1,0,1,0,0,2,2,76,1,0,59 };

    void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        results.Add("PASS " + message);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { manager?.Dispose(); batch?.Dispose(); }
        base.Dispose(disposing);
    }
}
