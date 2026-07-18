using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using AudioPlugSharp;
using System.IO;
using System.Reflection;

namespace ProjectZ.Vst;

/// <summary>
/// A manually ticked KNI game. Its WinForms DirectX window is re-parented into
/// the HWND supplied by the VST3 host.
/// </summary>
internal sealed class ProjectZPluginGame : Game
{
    private readonly ProjectZPlugin _plugin;
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _contentDirectory;
    private SpriteBatch? _spriteBatch;
    private ContentContainer? _content;
    private SceneManager? _sceneManager;
    private PluginEditorScene? _editorScene;
    private bool _loggedFirstUpdate;
    private bool _loggedFirstDraw;
    private bool _captureAttempted;
    private int _drawCount;

    public ProjectZPluginGame(ProjectZPlugin plugin, int width, int height)
    {
        _plugin = plugin;
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
        IsMouseVisible = true;
        var pluginDirectory = Path.GetDirectoryName(typeof(ProjectZPluginGame).Assembly.Location)
            ?? AppContext.BaseDirectory;
        _contentDirectory = Path.GetFullPath(Path.Combine(pluginDirectory, "Content"));
        Content.RootDirectory = _contentDirectory;

        _graphics = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.HiDef,
            PreferredBackBufferWidth = Math.Max(width, 1),
            PreferredBackBufferHeight = Math.Max(height, 1),
            PreferMultiSampling = true,
            SynchronizeWithVerticalRetrace = true
        };
    }

    protected override void LoadContent()
    {
        // KNI may recreate its ContentManager while the Game platform is being
        // initialized. Set the root again here and also pass the absolute path
        // to Project Z so a DAW's working directory can never become the root.
        Content.RootDirectory = _contentDirectory;
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _content = new ContentContainer(Content, GraphicsDevice, _contentDirectory);
        _content.LoadAllContent();

        Services.AddService(typeof(SpriteBatch), _spriteBatch);
        Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        Services.AddService(typeof(ContentContainer), _content);

        _sceneManager = new SceneManager(this, 60);
        // KNI's Mouse.GetState() keeps a process-global WinForms control. DAWs
        // repeatedly create and destroy editor HWNDs, leaving that static control
        // disposed and causing the next editor open to fail during its first tick.
        // Project Z's native Windows hook uses this game's current HWND instead.
        _sceneManager.UseHardwareInput = true;
        _editorScene = new PluginEditorScene(_sceneManager, _plugin);
        _sceneManager.AddScene("PluginEditor", _editorScene);
        _sceneManager.ActiveScene = _editorScene;
        Logger.Log($"Project Z DirectX content loaded from '{_contentDirectory}'. " +
                   $"Backbuffer={GraphicsDevice.PresentationParameters.BackBufferWidth}x" +
                   $"{GraphicsDevice.PresentationParameters.BackBufferHeight}. " +
                   $"ActiveScene={_sceneManager.ActiveScene.GetType().FullName}.");
    }

    protected override void Update(GameTime gameTime)
    {
        if (_sceneManager is not null && _editorScene is not null &&
            !ReferenceEquals(_sceneManager.ActiveScene, _editorScene))
        {
            Logger.Log($"Project Z replaced unexpected scene " +
                       $"'{_sceneManager.ActiveScene?.GetType().FullName ?? "<null>"}' with PluginEditorScene.");
            _sceneManager.ActiveScene = _editorScene;
        }
        _sceneManager?.Tick(gameTime);
        if (!_loggedFirstUpdate)
        {
            _loggedFirstUpdate = true;
            Logger.Log("Project Z DirectX first update completed.");
        }
        base.Update(gameTime);
    }

    public Task ImportDroppedFilesAsync(IReadOnlyList<string> paths) =>
        _editorScene?.ImportDroppedFilesAsync(paths) ?? Task.CompletedTask;

    protected override void Draw(GameTime gameTime)
    {
        _sceneManager?.Draw(gameTime);
        _drawCount++;
        if (_drawCount >= 3)
            CaptureSceneIfRequested();
        if (!_loggedFirstDraw)
        {
            _loggedFirstDraw = true;
            Logger.Log("Project Z DirectX first draw completed.");
        }
        base.Draw(gameTime);
    }

    private void CaptureSceneIfRequested()
    {
        if (_captureAttempted || _editorScene is null)
            return;

        var requestedPath = Environment.GetEnvironmentVariable("PROJECTZ_CAPTURE_SCENE");
        if (string.IsNullOrWhiteSpace(requestedPath))
            return;

        _captureAttempted = true;
        var capturePath = Path.GetFullPath(requestedPath);
        var directory = Path.GetDirectoryName(capturePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var wasUsingRenderTarget = _editorScene.UseRenderTarget;
        try
        {
            _editorScene.UseRenderTarget = true;
            var texture = _editorScene.DrawToRenderTarget()
                ?? throw new InvalidOperationException("The granulizer scene did not produce a render target.");
            using var stream = File.Create(capturePath);
            texture.SaveAsPng(stream, texture.Width, texture.Height);
            Logger.Log($"Project Z captured PluginEditorScene to '{capturePath}'.");
        }
        catch (Exception ex)
        {
            Logger.Log("Project Z scene capture failed: " + ex);
        }
        finally
        {
            _editorScene.UseRenderTarget = wasUsingRenderTarget;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sceneManager?.Dispose();
            _sceneManager = null;
            _editorScene = null;
            _spriteBatch?.Dispose();
            _spriteBatch = null;
        }

        base.Dispose(disposing);
    }
}
