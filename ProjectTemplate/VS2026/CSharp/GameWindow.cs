using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;

namespace $safeprojectname$;

public sealed class GameWindow : Game
{
    private readonly GraphicsDeviceManager graphics;
    private SceneManager? scenes;
    private SpriteBatch? batch;

    public GameWindow()
    {
        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 960,
            PreferredBackBufferHeight = 600,
            GraphicsProfile = GraphicsProfile.HiDef,
            SynchronizeWithVerticalRetrace = true
        };
        Content.RootDirectory = "Content";
        Window.Title = "$projectname$";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        var resources = new ContentContainer(Content, GraphicsDevice);
        resources.LoadAllContent();
        Services.AddService(typeof(SpriteBatch), batch);
        Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        Services.AddService(typeof(ContentContainer), resources);
        scenes = new SceneManager(this, 60);
        var main = new MainScene(scenes);
        scenes.AddScene("Main", main);
        scenes.ActiveScene = main;
    }

    protected override void Update(GameTime gameTime)
    {
        scenes?.Tick(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        scenes?.Draw(gameTime);
        base.Draw(gameTime);
    }

    protected override void UnloadContent() => batch?.Dispose();
}
