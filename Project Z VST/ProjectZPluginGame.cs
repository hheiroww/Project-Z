using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using XnaKeys = Microsoft.Xna.Framework.Input.Keys;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using AudioPlugSharp;
using System.IO;
using System.Reflection;

namespace ProjectZ.Vst;

/// <summary>
/// A manually ticked MonoGame instance. Its native DirectX window is re-parented into
/// the HWND supplied by the VST3 host.
/// </summary>
internal sealed class ProjectZPluginGame : Game
{
    private readonly ProjectZPlugin _plugin;
    private readonly GraphicsDeviceManager _graphics;
    private readonly string _contentDirectory;
    private readonly string _renderWindowTitle;
    private ContentManager? _pluginContentManager;
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
        _renderWindowTitle = $"Project Z Render {Guid.NewGuid():N}";
        Window.Title = _renderWindowTitle;
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
        IsMouseVisible = true;
        var pluginDirectory = Path.GetDirectoryName(typeof(ProjectZPluginGame).Assembly.Location)
            ?? AppContext.BaseDirectory;
        _contentDirectory = Path.GetFullPath(Path.Combine(pluginDirectory, "Content"));
        Content.RootDirectory = "Content";

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
        // The native backend deliberately restricts TitleContainer to relative
        // paths. A VST host's base directory belongs to the DAW, so use a small
        // ContentManager override that can open the plugin's absolute path.
        _pluginContentManager = new PluginContentManager(Services, _contentDirectory);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _content = new ContentContainer(_pluginContentManager, GraphicsDevice, _contentDirectory);
        _content.LoadAllContent();

        Services.AddService(typeof(SpriteBatch), _spriteBatch);
        Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        Services.AddService(typeof(ContentContainer), _content);

        _sceneManager = new SceneManager(this, 60);
        // The VST host injects input from the real embedded child HWND. Do not
        // poll process-global input state and do not install a desktop-wide hook.
        _sceneManager.UseHardwareInput = false;
        _sceneManager.UseExternalInput = true;
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

    internal string PromptTextForTesting => _editorScene?.PromptTextForTesting ?? string.Empty;
    internal bool PromptSelectedForTesting => _editorScene?.PromptSelectedForTesting == true;
    internal bool PromptMouseOverForTesting => _editorScene?.PromptMouseOverForTesting == true;
    internal Microsoft.Xna.Framework.Rectangle PromptBoundsForTesting =>
        _editorScene?.PromptBoundsForTesting ?? Microsoft.Xna.Framework.Rectangle.Empty;
    internal string RenderWindowTitle => _renderWindowTitle;

    public void InjectMouseMove(int x, int y, int clientWidth, int clientHeight) =>
        _sceneManager?.InjectMouseMove(x, y, clientWidth, clientHeight);

    public void InjectMouseLeftDown(int x, int y, int clientWidth, int clientHeight) =>
        _sceneManager?.InjectMouseLeftDown(x, y, clientWidth, clientHeight);

    public void InjectMouseLeftUp(int x, int y, int clientWidth, int clientHeight) =>
        _sceneManager?.InjectMouseLeftUp(x, y, clientWidth, clientHeight);

    public void InjectMouseRightDown(int x, int y, int clientWidth, int clientHeight) =>
        _sceneManager?.InjectMouseRightDown(x, y, clientWidth, clientHeight);

    public void InjectMouseRightUp(int x, int y, int clientWidth, int clientHeight) =>
        _sceneManager?.InjectMouseRightUp(x, y, clientWidth, clientHeight);

    public void InjectMouseLeave() => _sceneManager?.InjectMouseLeave();

    public void CancelInput() => _sceneManager?.CancelExternalInput();

    public void InjectKeyDown(int virtualKey, bool isRepeat)
    {
        if ((uint)virtualKey <= 254)
            _sceneManager?.InjectKeyDown((XnaKeys)virtualKey, isRepeat);
    }

    public void InjectKeyUp(int virtualKey)
    {
        if ((uint)virtualKey <= 254)
            _sceneManager?.InjectKeyUp((XnaKeys)virtualKey);
    }

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
            _pluginContentManager?.Dispose();
            _pluginContentManager = null;
        }

        base.Dispose(disposing);
    }

    private sealed class PluginContentManager : ContentManager
    {
        public PluginContentManager(IServiceProvider serviceProvider, string rootDirectory)
            : base(serviceProvider, rootDirectory)
        {
        }

        protected override Stream OpenStream(string assetName)
        {
            var assetPath = Path.Combine(RootDirectory, assetName) + ".xnb";
            return Path.IsPathRooted(assetPath)
                ? File.OpenRead(assetPath)
                : base.OpenStream(assetName);
        }
    }
}
