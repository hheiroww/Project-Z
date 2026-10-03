using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;

namespace ProjectZ.WpfCompatibility;

public static class Hosting
{
    /// <summary>Run imported application logic in a native Project-Z game window.</summary>
    public static void Run(Func<Application> createApplication, string[]? args = null)
    {
        using var host = new Host(createApplication, args ?? []);
        host.Run();
    }
    sealed class HostedScene(SceneManager manager) : Scene(manager);
    sealed class Host : Game
    {
        readonly Func<Application> factory;
        readonly string[] args;
        readonly GraphicsDeviceManager graphics;
        SceneManager manager = null!;
        Application? application;
        public Host(Func<Application> factory, string[] args)
        {
            this.factory = factory; this.args = args;
            graphics = new(this) { PreferredBackBufferWidth = 1000, PreferredBackBufferHeight = 700, GraphicsProfile = GraphicsProfile.HiDef };
            Content.RootDirectory = "Content"; IsMouseVisible = true; Window.Title = "Project-Z imported application";
        }
        protected override void LoadContent()
        {
            var batch = new SpriteBatch(GraphicsDevice);
            var resources = new ContentContainer(Content, GraphicsDevice); resources.LoadAllContent();
            Services.AddService(typeof(SpriteBatch), batch); Services.AddService(typeof(GraphicsDevice), GraphicsDevice); Services.AddService(typeof(ContentContainer), resources);
            manager = new(this); var scene = new HostedScene(manager) { EnableDesignTimeControls = false };
            manager.AddScene("application", scene); manager.ActiveScene = scene;
            Application.HostingScene = scene;
            try { application = factory(); application.Start(args); }
            finally { Application.HostingScene = null; }
            if (application.MainWindow != null) Window.Title = application.MainWindow.Title;
        }
        protected override void Update(GameTime time) { application?.Dispatcher.Pump(); manager?.Tick(time); if (application != null && application.Windows.Count == 0) Exit(); base.Update(time); }
        protected override void Draw(GameTime time) { manager.ActiveScene?.Draw(time); base.Draw(time); }
        protected override void Dispose(bool disposing) { if (disposing) application?.Dispose(); base.Dispose(disposing); }
    }
}
