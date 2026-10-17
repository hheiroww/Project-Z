using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;

namespace ProjectZ.WpfCompatibility;

/// <summary>A native HWND, graphics device and scene owned by one desktop window.</summary>
public sealed class NativeWindowSurface : IDisposable
{
    readonly SurfaceGame game;
    readonly IntPtr hwnd;
    bool disposed;
    public Scene Scene { get { VerifyAlive(); return game.SurfaceScene; } }
    public IntPtr Handle { get { VerifyAlive(); return hwnd; } }
    public int FrameCount { get { VerifyAlive(); return game.FrameCount; } }
    public bool IsClosed => disposed || game.Closed || !IsWindow(hwnd);
    public string Title { get { VerifyAlive(); return game.Window.Title; } set { VerifyAlive(); game.Window.Title = value; } }
    public NativeWindowSurface(string title, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Threading.Dispatcher.CurrentDispatcher.VerifyAccess();
        game = new(title + " " + Guid.NewGuid().ToString("N"), width, height);
        try { game.RunOneFrame(); }
        catch { game.Dispose(); throw; }
        hwnd = global::ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(game);
        if (!IsWindow(hwnd)) { game.Dispose(); throw new InvalidOperationException("The renderer did not create a native window."); }
        game.Window.Title = title;
    }
    public void RenderFrame() { VerifyAlive(); game.RenderTick(true); }
    internal void RenderFrame(bool force) { VerifyAlive(); game.RenderTick(force); }
    public void Resize(int width, int height) { VerifyAlive(); game.Resize(width, height); }
    public void DispatchClick(int x, int y) { VerifyAlive(); game.DispatchClick(x, y); }
    public void DispatchRightClick(int x, int y) { VerifyAlive(); game.DispatchRightClick(x, y); }
    public void DispatchTextInput(string text) { VerifyAlive(); game.DispatchTextInput(text); }
    public void DispatchKey(Microsoft.Xna.Framework.Input.Keys key) { VerifyAlive(); game.DispatchKey(key); }
    public void SetOwner(NativeWindowSurface? owner)
    {
        VerifyAlive();
        if (ReferenceEquals(owner, this)) throw new ArgumentException("A window cannot own itself.", nameof(owner));
        var handle = owner?.Handle ?? IntPtr.Zero;
        SetOwner(handle);
    }
    public void SetOwner(IntPtr handle)
    {
        VerifyAlive(); Marshal.SetLastPInvokeError(0);
        if (SetWindowLongPtr(Handle, -8, handle) == IntPtr.Zero && Marshal.GetLastWin32Error() != 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    public void SetVisible(bool visible) { VerifyAlive(); ShowWindow(Handle, visible ? 4 : 0); }
    public Color[] CaptureFrame()
    {
        VerifyAlive(); return game.CaptureFrame();
    }
    public void SaveFrame(string path) { VerifyAlive(); game.SaveFrame(path); }
    void VerifyAlive() { ObjectDisposedException.ThrowIf(disposed, this); Threading.Dispatcher.CurrentDispatcher.VerifyAccess(); }
    public void Dispose()
    {
        if (disposed) return; Threading.Dispatcher.CurrentDispatcher.VerifyAccess();
        disposed = true; game.Dispose();
    }
    sealed class SurfaceScene(SceneManager manager) : Scene(manager);
    sealed class SurfaceGame : Game
    {
        readonly GraphicsDeviceManager graphics;
        SceneManager manager = null!;
        SpriteBatch presentation = null!;
        RenderTarget2D? frame;
        readonly string? profilePath = Environment.GetEnvironmentVariable("PROJECTZ_FRAME_PROFILE");
        readonly System.Diagnostics.Stopwatch profileClock = System.Diagnostics.Stopwatch.StartNew();
        double frameMilliseconds;
        double lastDrawMilliseconds;
        int profiledFrames;
        int profiledTicks;
        bool forceFrame;
        long renderedRevision;
        long nextIdleFrame;
        public Scene SurfaceScene { get; private set; } = null!;
        public int FrameCount { get; private set; }
        public bool Closed { get; private set; }
        public SurfaceGame(string title, int width, int height)
        {
            Exiting += (_, _) => Closed = true;
            // The application host owns pacing. Each child game must not wait
            // for its own fixed clock or vertical retrace on the shared UI thread.
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
            graphics = new(this) { PreferredBackBufferWidth = width, PreferredBackBufferHeight = height, GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
            Window.Title = title; Window.AllowUserResizing = true; IsMouseVisible = true; Content.RootDirectory = Application.Active?.StartupOptions.ContentRoot ?? "Content";
        }
        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (graphics.PreferredBackBufferWidth == width && graphics.PreferredBackBufferHeight == height) return;
            graphics.PreferredBackBufferWidth = width; graphics.PreferredBackBufferHeight = height; graphics.ApplyChanges();
            SurfaceScene?.RequestRender();
        }
        protected override void LoadContent()
        {
            var batch = new SpriteBatch(GraphicsDevice); var resources = new ContentContainer(Content, GraphicsDevice); resources.LoadVectorFontsOnly();
            presentation = new SpriteBatch(GraphicsDevice);
            Services.AddService(typeof(SpriteBatch), batch); Services.AddService(typeof(GraphicsDevice), GraphicsDevice); Services.AddService(typeof(ContentContainer), resources);
            manager = new(this); SurfaceScene = new SurfaceScene(manager) { EnableDesignTimeControls = false };
            SurfaceScene.Quality.AntiAliasingSamples = 4;
            SurfaceScene.Quality.Filtering = TextureFiltering.Anisotropic16x;
            SurfaceScene.EnableFrameProfiling = !string.IsNullOrWhiteSpace(profilePath);
            manager.UseWindowMessagesInput = true;
            if (Application.Active is {} app)
            {
                app.StartupOptions.Apply(manager);
                SurfaceScene.Quality.AntiAliasingSamples = app.StartupOptions.AntiAliasingSamples;
                SurfaceScene.Quality.Filtering = app.StartupOptions.TextureFiltering;
                TargetElapsedTime = TimeSpan.FromSeconds(1d / app.StartupOptions.FramesPerSecond);
                SurfaceScene.BackgroundColor = app.StartupOptions.BackgroundColor;
            }
            manager.AddScene("window", SurfaceScene); manager.ActiveScene = SurfaceScene;
        }
        protected override void Update(GameTime time) { manager.Tick(time); base.Update(time); }
        protected override bool BeginDraw()
        {
            // Keep ticking input and animation clocks, but reuse the presented
            // frame while imported UI is unchanged. Periodic refresh preserves
            // native carets and controls that draw from elapsed time.
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!forceFrame && FrameCount > 0 && SurfaceScene.RenderRevision == renderedRevision && now < nextIdleFrame) return false;
            return base.BeginDraw();
        }
        protected override void Draw(GameTime time)
        {
            var started = profilePath == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            var size = GraphicsDevice.PresentationParameters;
            if (frame == null || frame.Width != size.BackBufferWidth || frame.Height != size.BackBufferHeight) { frame?.Dispose(); frame = new(GraphicsDevice, size.BackBufferWidth, size.BackBufferHeight, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents); }
            GraphicsDevice.SetRenderTarget(frame); SurfaceScene.Draw(time); GraphicsDevice.SetRenderTarget(null);
            presentation.Begin(SpriteSortMode.Immediate, BlendState.Opaque); presentation.Draw(frame, new Rectangle(0, 0, frame.Width, frame.Height), Color.White); presentation.End();
            FrameCount++; base.Draw(time);
            renderedRevision = SurfaceScene.RenderRevision;
            nextIdleFrame = System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency / 4;
            if (started != 0) lastDrawMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
        public void RenderTick(bool force)
        {
            forceFrame = force;
            if (string.IsNullOrWhiteSpace(profilePath)) { Tick(); return; }
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            int framesBefore = FrameCount;
            Tick();
            {
                frameMilliseconds += System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                profiledFrames += FrameCount - framesBefore;
                profiledTicks++;
                if (profileClock.ElapsedMilliseconds >= 5000)
                {
                    File.AppendAllText(profilePath!, System.Text.Json.JsonSerializer.Serialize(new { Window = Window.Title, Frames = profiledFrames, FPS = profiledFrames / profileClock.Elapsed.TotalSeconds, Ticks = profiledTicks, TickMs = frameMilliseconds / profiledTicks, DrawMs = lastDrawMilliseconds, LayoutMs = SurfaceScene.LastLayoutMilliseconds, RenderMs = SurfaceScene.LastRenderMilliseconds, ElementTickMs = SurfaceScene.LastElementTickMilliseconds, Elements = SurfaceScene.Elements.Count, Costs = SurfaceScene.ElementDrawProfile, Counts = SurfaceScene.ElementDrawCounts, AA = SurfaceScene.Quality.AntiAliasingSamples, AF = SurfaceScene.Quality.Filtering.ToString() }) + Environment.NewLine);
                    profileClock.Restart(); frameMilliseconds = 0; profiledFrames = 0; profiledTicks = 0;
                }
            }
        }
        public Color[] CaptureFrame() { if (frame == null) throw new InvalidOperationException("The window has not drawn a frame."); var pixels = new Color[frame.Width * frame.Height]; frame.GetData(pixels); return pixels; }
        public void SaveFrame(string path) { if (frame == null) throw new InvalidOperationException("The window has not drawn a frame."); using var file = File.Create(path); frame.SaveAsPng(file, frame.Width, frame.Height); }
        public void DispatchClick(int x, int y) { var previous = manager.UseExternalInput; manager.UseExternalInput = true; var viewport = GraphicsDevice.PresentationParameters; try { manager.InjectMouseLeftDown(x, y, viewport.BackBufferWidth, viewport.BackBufferHeight); manager.InjectMouseLeftUp(x, y, viewport.BackBufferWidth, viewport.BackBufferHeight); } finally { manager.UseExternalInput = previous; } }
        public void DispatchRightClick(int x, int y) { var previous = manager.UseExternalInput; manager.UseExternalInput = true; var viewport = GraphicsDevice.PresentationParameters; try { manager.InjectMouseRightDown(x, y, viewport.BackBufferWidth, viewport.BackBufferHeight); manager.InjectMouseRightUp(x, y, viewport.BackBufferWidth, viewport.BackBufferHeight); } finally { manager.UseExternalInput = previous; } }
        public void DispatchTextInput(string text) { var previous = manager.UseExternalInput; manager.UseExternalInput = true; try { manager.InjectTextInput(text); } finally { manager.UseExternalInput = previous; } }
        public void DispatchKey(Microsoft.Xna.Framework.Input.Keys key) { var previous = manager.UseExternalInput; manager.UseExternalInput = true; try { manager.InjectKeyDown(key); manager.InjectKeyUp(key); } finally { manager.UseExternalInput = previous; } }
        protected override void Dispose(bool disposing) { if (disposing) { manager?.Dispose(); frame?.Dispose(); presentation?.Dispose(); } base.Dispose(disposing); }
    }
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] static extern IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value);
}
