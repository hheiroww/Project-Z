using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Composition;
using ProjectZ.Shared.Drawing;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

internal static class Program
{
    internal static int Checks;
    internal static readonly MethodInfo UpdateMethod = typeof(DesktopBackdrop).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal static readonly MethodInfo DrawMethod = typeof(DesktopBackdrop).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [STAThread]
    private static void Main(string[] args)
    {
        ProjectZ.RuntimeDiagnostics.ApplicationDiagnostics.Initialize(new ProjectZ.RuntimeDiagnostics.DiagnosticOptions { Enabled = false });
        // This fixture supplies every captured byte. It never constructs a native
        // desktop source, reads desktop pixels, uses the clipboard, or sends input.
        if (args.Contains("--scheduling-only"))
        {
            using var source = new ControlledSource(returnPixels: false);
            using var backdrop = Create(null!, source);
            Scheduling(backdrop, source);
            RetryIsolation(null!);
        }
        else
        {
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
            using var game = new HiddenProbe();
            game.Run();
            Require(game.Completed, "offscreen graphics fixture completed");
        }
        Console.WriteLine($"DESKTOP_BACKDROP_RESPONSIVENESS_OK checks={Checks}; nativeCaptures=0");
    }

    internal static DesktopBackdrop Create(Game game, ControlledSource source)
    {
        var constructor = typeof(DesktopBackdrop).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(Game)], null)!;
        var result = (DesktopBackdrop)constructor.Invoke([game]);
        result.CaptureSourceFactory = () => source;
        result.ShowSetupPrompt = false;
        result.Settings.Enabled = true;
        result.Settings.CaptureFramesPerSecond = 1;
        result.Settings.NoiseOpacity = 0;
        result.Settings.Saturation = 1;
        return result;
    }

    internal static void RetryIsolation(Game game)
    {
        using var oldSource = new ControlledSource(returnPixels: false);
        using var newSource = new ControlledSource(returnPixels: false);
        using var backdrop = Create(game, oldSource);
        Update(backdrop);
        Require(oldSource.Started.WaitOne(5000), "retry fixture starts an outstanding capture");
        var oldBuffer = oldSource.Buffers.Single();
        var oldRevision = Revision(backdrop);
        backdrop.Retry();
        Require(Revision(backdrop) != oldRevision, "retry invalidates the old desktop frame");
        backdrop.CaptureSourceFactory = () => newSource;
        Update(backdrop);
        Require(newSource.Started.WaitOne(5000), "retry starts its replacement provider without blocking");
        Require(!ReferenceEquals(oldBuffer, newSource.Buffers.Single()), "replacement provider gets a separate pixel buffer");
        Require(!oldSource.IsDisposed, "in-flight provider stays alive until its worker finishes");
        oldSource.Permit.Release();
        Require(SpinWait.SpinUntil(() => oldSource.IsDisposed, 5000), "completed retired provider is disposed");
        Require(!newSource.IsDisposed, "retired-provider disposal leaves the replacement alive");
        Complete(backdrop, newSource);
        backdrop.Settings.Enabled = false;
        Update(backdrop);
    }

    internal static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        Checks++;
        Console.WriteLine("PASS " + message);
    }

    internal static long Revision(DesktopBackdrop backdrop)
    {
        var property = typeof(DesktopBackdrop).GetProperty("RenderRevision");
        Require(property is not null, "backdrop exposes a render revision for paced consumers");
        return (long)property!.GetValue(backdrop)!;
    }

    internal static void Update(DesktopBackdrop backdrop)
    {
        try { UpdateMethod.Invoke(backdrop, null); }
        catch (TargetInvocationException ex) { throw ex.InnerException!; }
        Require(backdrop.State != DesktopBackdropState.Failed, "capture update remains healthy: " + backdrop.StatusMessage);
    }

    internal static void HoldStationaryDeadline(DesktopBackdrop backdrop)
    {
        // A deliberately distant deadline makes this deterministic on a slow CI
        // machine: geometry must bypass cadence, rather than eventually wait it out.
        typeof(DesktopBackdrop).GetField("nextCaptureAt", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(backdrop, long.MaxValue);
    }

    internal static void Complete(DesktopBackdrop backdrop, ControlledSource source)
    {
        source.Permit.Release();
        var field = typeof(DesktopBackdrop).GetField("captureTask", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task?)field.GetValue(backdrop);
        Require(task is not null && SpinWait.SpinUntil(() => task.IsCompleted, 5000), "bounded worker capture completes");
        HoldStationaryDeadline(backdrop);
        Update(backdrop);
    }

    internal static void Scheduling(DesktopBackdrop backdrop, ControlledSource source)
    {
        Update(backdrop);
        Require(source.Started.WaitOne(5000), "initial capture starts");
        Complete(backdrop, source);
        var initialCount = source.Requests.Count;
        HoldStationaryDeadline(backdrop);
        Update(backdrop);
        Require(source.Requests.Count == initialCount, "stationary backdrop respects configured capture cadence");

        var oldRevision = (long?)typeof(DesktopBackdrop).GetProperty("RenderRevision")?.GetValue(backdrop) ?? 0;
        source.Bounds = new Rectangle(120, 110, 160, 96);
        Update(backdrop);
        Require(source.Started.WaitOne(1000), "moving bypasses even an unexpired one-FPS deadline");
        Require(new DesktopBackdropSettings().CaptureFramesPerSecond == 60, "default capture cadence follows a sixty-Hz desktop");
        Require(Revision(backdrop) != oldRevision, "moving invalidates a paced presentation before readback completes");

        // Keep the worker deliberately busy while several positions arrive. Only
        // the newest geometry should start after it, with no unbounded task queue.
        var busyCount = source.Requests.Count;
        source.Bounds = new Rectangle(140, 120, 168, 100);
        Update(backdrop);
        source.Bounds = new Rectangle(180, 130, 184, 108);
        Update(backdrop);
        Require(source.Requests.Count == busyCount && source.MaxActive == 1, "busy capture stays bounded to one worker");
        Complete(backdrop, source);
        Require(source.Started.WaitOne(1000), "latest geometry is scheduled immediately after a busy worker");
        Require(source.Requests.Last().Contains(source.Bounds), "coalesced capture covers the newest moved and resized bounds");
        Complete(backdrop, source);
        Require(source.MaxActive == 1, "no capture calls overlap");

        var resizeCount = source.Requests.Count;
        source.Bounds = new Rectangle(180, 130, 204, 120);
        Update(backdrop);
        Require(source.Started.WaitOne(1000) && source.Requests.Count == resizeCount + 1, "resizing bypasses the stationary deadline");
        Complete(backdrop, source);

        var revision = Revision(backdrop);
        backdrop.Settings.Tint = new Color(20, 40, 60, 100);
        Update(backdrop);
        Require(Revision(backdrop) != revision, "material changes invalidate the paced presentation");
        backdrop.Settings.Enabled = false;
        revision = Revision(backdrop);
        Update(backdrop);
        Require(Revision(backdrop) != revision, "disabling the material invalidates the paced presentation");
    }
}

internal sealed class ControlledSource(bool returnPixels = true) : IDesktopCaptureSource
{
    internal Rectangle Bounds = new(100, 100, 160, 96);
    internal readonly ConcurrentQueue<Rectangle> Requests = new();
    internal readonly ConcurrentQueue<byte[]> Buffers = new();
    internal readonly AutoResetEvent Started = new(false);
    internal readonly SemaphoreSlim Permit = new(0);
    internal int MaxActive;
    private int active;
    private int disposed;
    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;
    public bool ExcludesHostWindow => true;
    public Rectangle WindowBounds => Bounds;
    public float DpiScale => 1;
    public bool TryCapture(Rectangle area, int downsample, byte[] rgba)
    {
        var count = Interlocked.Increment(ref active);
        MaxActive = Math.Max(MaxActive, count);
        Requests.Enqueue(area);
        Buffers.Enqueue(rgba);
        Started.Set();
        try
        {
            if (!Permit.Wait(5000)) throw new TimeoutException("Synthetic capture was not released.");
            if (!returnPixels) return false;
            var width = (area.Width + downsample - 1) / downsample;
            var height = (area.Height + downsample - 1) / downsample;
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                rgba[offset] = (byte)Math.Clamp((area.X + x * downsample) / 4, 0, 255);
                rgba[offset + 1] = (byte)Math.Clamp((area.Y + y * downsample) / 4, 0, 255);
                rgba[offset + 2] = 70;
                rgba[offset + 3] = 255;
            }
            return true;
        }
        finally { Interlocked.Decrement(ref active); }
    }
    public void Dispose()
    {
        // DesktopBackdrop and the fixture both own a disposal scope.
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        Started.Dispose();
        Permit.Dispose();
    }
}

internal sealed class HiddenProbe : Game
{
    private readonly GraphicsDeviceManager graphics;
    private bool ran;
    internal bool Completed;
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);

    internal HiddenProbe()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 160, PreferredBackBufferHeight = 96,
            GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false
        };
        IsFixedTimeStep = false;
        Window.Position = new Point(-30000, -30000);
        Window.Title = "Project-Z synthetic backdrop regression";
    }

    protected override void LoadContent()
    {
        ShowWindow(ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(this), 0);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (ran) return;
        ran = true;
        using (var source = new ControlledSource())
        using (var backdrop = Program.Create(this, source))
        {
            Program.Scheduling(backdrop, source);
        }
        Program.RetryIsolation(this);
        VerifySceneInvalidation();
        using (var source = new ControlledSource())
        using (var backdrop = Program.Create(this, source))
        using (var target = new RenderTarget2D(GraphicsDevice, 160, 96))
        {
            backdrop.Settings.BlurRadius = 20;
            Program.Update(backdrop);
            Program.Require(source.Started.WaitOne(5000), "pixel fixture begins its synthetic capture");
            var revision = Program.Revision(backdrop);
            Program.Complete(backdrop, source);
            Program.Require(backdrop.CapturedFrameCount == 1 && Program.Revision(backdrop) != revision, "uploaded pixels invalidate presentation");
            var before = Render(backdrop, target);
            Program.Require(Math.Abs(before.R - 45) <= 3 && Math.Abs(before.G - 37) <= 3 && Math.Abs(before.B - 70) <= 2, "shader samples known desktop-space gradient");

            source.Bounds = new Rectangle(140, 100, 160, 96);
            Program.Update(backdrop);
            Program.Require(source.Started.WaitOne(1000), "movement starts refresh without waiting for idle cadence");
            var duringMove = Render(backdrop, target);
            Program.Require(Math.Abs(duringMove.R - before.R - 10) <= 2 && Math.Abs(duringMove.B - 70) <= 2, "cached pixels reproject immediately during pending capture");
            var oldInput = typeof(DesktopBackdrop).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(backdrop);
            var oldBuffer = source.Buffers.Last();
            source.Bounds = new Rectangle(140, 100, 172, 108);
            Program.Update(backdrop);
            var duringResize = Render(backdrop, target);
            Program.Require(Math.Abs(duringResize.R - 56) <= 3 && Math.Abs(duringResize.G - 38) <= 3 && Math.Abs(duringResize.B - 70) <= 2, "resizing within cached coverage keeps the desktop material");
            Program.Complete(backdrop, source);
            Program.Require(source.Started.WaitOne(1000), "resize coalesces after in-flight pixel capture");
            Program.Complete(backdrop, source);
            var resizedInput = typeof(DesktopBackdrop).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(backdrop);
            Program.Require(ReferenceEquals(oldInput, resizedInput) && ReferenceEquals(oldBuffer, source.Buffers.Last()), "small resize reuses the uploaded texture and worker pixel buffer");
            var after = Render(backdrop, target);
            Program.Require(Math.Abs(after.R - duringResize.R) <= 3 && Math.Abs(after.G - duringResize.G) <= 3, "fresh capture stays aligned with reprojected cached pixels");

            source.Bounds = new Rectangle(500, 300, 160, 96);
            Program.Update(backdrop);
            Program.Require(source.Started.WaitOne(1000), "large move starts its replacement capture immediately");
            var farMove = Render(backdrop, target);
            Program.Require(Math.Abs(farMove.B - 70) <= 2 && farMove.R > 70, "moving beyond cached coverage holds clamped desktop pixels instead of opaque fallback");
            source.Bounds = new Rectangle(500, 300, 380, 230);
            Program.Update(backdrop);
            var farResize = Render(backdrop, target);
            Program.Require(Math.Abs(farResize.B - 70) <= 2 && farResize.R > 70, "large pending resize keeps the desktop material without a fallback flash");
            Program.Complete(backdrop, source);
            Program.Require(source.Started.WaitOne(1000), "large resize replaces the completed obsolete geometry");
            Program.Complete(backdrop, source);
            var freshLarge = Render(backdrop, target);
            Program.Require(Math.Abs(freshLarge.R - 172) <= 4 && Math.Abs(freshLarge.G - 103) <= 4 && Math.Abs(freshLarge.B - 70) <= 2, "latest capture aligns the desktop gradient after large movement and resize");
            Program.Require(Math.Abs(freshLarge.R - farResize.R) > 20, "fresh desktop pixels replace the clamped placeholder material");
        }
        Completed = true;
        Exit();
    }

    private void VerifySceneInvalidation()
    {
        using var batch = new SpriteBatch(GraphicsDevice);
        Services.AddService(typeof(SpriteBatch), batch);
        Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        try
        {
            using var provider = new ControlledSource(returnPixels: false);
            using var manager = new SceneManager(this, 60) { UseExternalInput = true, AutoResizeViewport = false };
            using var scene = new RevisionScene(manager) { EnableDesignTimeControls = false };
            manager.AddScene("revision", scene);
            manager.ActiveScene = scene;
            manager.DesktopBackdrop.CaptureSourceFactory = () => provider;
            manager.DesktopBackdrop.ShowSetupPrompt = false;
            var time = new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(16));
            manager.Tick(time);
            var stable = scene.RenderRevision;
            manager.Tick(time);
            Program.Require(scene.RenderRevision == stable, "stable disabled backdrop ticks leave scene presentation idle");
            manager.DesktopBackdrop.Retry();
            manager.Tick(time);
            Program.Require(scene.RenderRevision > stable, "retry between manager ticks invalidates the scene presentation");
            stable = scene.RenderRevision;
            manager.Tick(time);
            Program.Require(scene.RenderRevision == stable && provider.Requests.IsEmpty, "subsequent disabled ticks stay idle without starting capture");

            scene.UseDesktopBackdrop = false;
            manager.DesktopBackdrop.Retry();
            stable = scene.RenderRevision;
            manager.Tick(time);
            Program.Require(scene.RenderRevision == stable, "scenes without a desktop material ignore backdrop invalidation");
            scene.UseDesktopBackdrop = true;
            manager.Tick(time);
            Program.Require(scene.RenderRevision > stable, "restoring desktop material observes its pending invalidation");
        }
        finally
        {
            Services.RemoveService(typeof(SpriteBatch));
            Services.RemoveService(typeof(GraphicsDevice));
        }
    }

    private Color Render(DesktopBackdrop backdrop, RenderTarget2D target)
    {
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Color.Magenta);
        try { Program.Require((bool)Program.DrawMethod.Invoke(backdrop, [GraphicsDevice, Color.Transparent])!, "desktop backdrop draws to the owned target"); }
        catch (TargetInvocationException ex) { throw ex.InnerException!; }
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        return pixels[48 * target.Width + 80];
    }
}

// Keep this test focused on the manager's presentation decision: ordinary scene
// initialization, animation and input must not independently request a render.
internal sealed class RevisionScene(SceneManager manager) : Scene(manager)
{
    public override void Tick(GameTime gameTime) { }
}
