using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;

namespace ProjectZ.WpfCompatibility;

public static class Hosting
{
    /// <summary>Run imported application logic in a native Project-Z game window.</summary>
    public static void Run(Func<Application> createApplication, string[]? args = null, ApplicationStartupOptions? options = null)
    {
        var host = new Host(createApplication, args ?? [], options ?? new());
        using (host) host.Run();
        Environment.ExitCode = host.ExitCode;
    }
    sealed class HostedScene(SceneManager manager) : Scene(manager);
    sealed class Host : Game
    {
        readonly Func<Application> factory;
        readonly string[] args;
        readonly GraphicsDeviceManager graphics;
        readonly ApplicationStartupOptions options;
        SceneManager manager = null!;
        Application? application;
        IntPtr coordinatorHandle;
        long nextUpdate;
        public int ExitCode => application?.ExitCode ?? 0;
        public Host(Func<Application> factory, string[] args, ApplicationStartupOptions options)
        {
            this.factory = factory; this.args = args;
            this.options = options;
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
            if (options.FramesPerSecond is < 1 or > 1000 || options.Width < 1 || options.Height < 1) throw new ArgumentOutOfRangeException(nameof(options));
            graphics = new(this) { PreferredBackBufferWidth = options.Width, PreferredBackBufferHeight = options.Height, GraphicsProfile = GraphicsProfile.HiDef, SynchronizeWithVerticalRetrace = false };
            TargetElapsedTime = TimeSpan.FromSeconds(1d / options.FramesPerSecond);
            Content.RootDirectory = options.ContentRoot; IsMouseVisible = true; Window.Title = "Project-Z imported application";
        }
        protected override void LoadContent()
        {
            // Resolve while the coordinator still has its unique title. SDL's
            // Window.Handle may not be an HWND; later title lookup can match an
            // imported window instead, including one still being constructed.
            coordinatorHandle = global::ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(this);
            var batch = new SpriteBatch(GraphicsDevice);
            var resources = new ContentContainer(Content, GraphicsDevice); resources.LoadVectorFontsOnly();
            Services.AddService(typeof(SpriteBatch), batch); Services.AddService(typeof(GraphicsDevice), GraphicsDevice); Services.AddService(typeof(ContentContainer), resources);
            manager = new(this); var scene = new HostedScene(manager) { EnableDesignTimeControls = false };
            options.Apply(manager); scene.BackgroundColor = options.BackgroundColor;
            manager.AddScene("application", scene); manager.ActiveScene = scene;
            Application.HostingScene = scene;
            try
            {
                application = factory(); application.StartupOptions = options;
                application.UseNativeWindows = true; application.HostPumpsDispatcher = true;
                if (options.ShutdownMode is {} shutdown) application.ShutdownMode = shutdown;
                if (options.MainWindowFactory == null) application.Start(args);
                else { application.MainWindow = options.MainWindowFactory(); application.MainWindow.Show(); }
            }
            finally { Application.HostingScene = null; }
            DesktopNative.ShowWindow(coordinatorHandle, 0);
        }
        protected override void Update(GameTime time)
        {
            // A hidden, non-presenting coordinator needs an explicit sleeping
            // clock. Do not spin or replay missed UI updates after callbacks.
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now < nextUpdate)
            {
                Thread.Sleep((int)Math.Ceiling((nextUpdate - now) * 1000d / System.Diagnostics.Stopwatch.Frequency));
                now = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            long period = System.Diagnostics.Stopwatch.Frequency / options.FramesPerSecond;
            nextUpdate = nextUpdate == 0 || now - nextUpdate >= period ? now + period : nextUpdate + period;
            // SDL may show its coordinator after LoadContent returns. Keep the
            // coordinator hidden while the imported native windows own the UI.
            if (DesktopNative.IsWindowVisible(coordinatorHandle)) DesktopNative.ShowWindow(coordinatorHandle, 0);
            application?.Dispatcher.Pump(); manager?.Tick(time);
            if (application?.ShutdownRequested == true) Exit(); base.Update(time);
        }
        protected override bool BeginDraw()
        {
            // Fixed-step catch-up updates must not render every native window
            // repeatedly after an expensive application callback or cold frame.
            application?.PumpWindows();
            // This hidden coordinator supplies the clock and application
            // dispatcher. Only the visible native windows submit/present UI.
            return false;
        }
        protected override void Dispose(bool disposing) { if (disposing) application?.Dispose(); base.Dispose(disposing); }
    }
}
