using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Windows.Composition;
using Color = Microsoft.Xna.Framework.Color;
using Game = Microsoft.Xna.Framework.Game;
using GameTime = Microsoft.Xna.Framework.GameTime;
using Matrix = Microsoft.Xna.Framework.Matrix;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace ProjectZ.StudioScenes;

public enum StudioTextStyle { Small, Body, Medium, Heading, Title }

/// <summary>Renders a Project-Z scene to premultiplied BGRA pixels for a native window host.</summary>
/// <remarks>Create, render, and dispose on the host window's UI thread.</remarks>
public sealed class ProjectZCanvas : IDisposable
{
    private readonly Action<ProjectZPainter> _draw;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private CanvasGame? _game;
    private bool _disposed;

    public ProjectZCanvas(Action<ProjectZPainter> draw) =>
        _draw = draw ?? throw new ArgumentNullException(nameof(draw));

    /// <summary>The returned pixels are valid until the next render or disposal.</summary>
    public ReadOnlySpan<int> Render(int width, int height) => Render(width, height, 1);

    /// <summary>Renders at an integer multiple of the logical viewport for high-resolution captures.</summary>
    public ReadOnlySpan<int> Render(int width, int height, int scale)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        VerifyThread();
        if (scale is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(scale));
        if (width <= 0 || height <= 0 || width > 4096 / scale || height > 4096 / scale ||
            (long)width * height * scale * scale > 8_388_608)
            throw new ArgumentOutOfRangeException(nameof(width), "Use a viewport up to 4,096 pixels per side and 8,388,608 pixels total.");
        _game ??= new CanvasGame(_draw, CanvasGame.GetForegroundWindow());
        return _game.Render(width, height, scale);
    }

    public void Dispose()
    {
        if (_disposed) return;
        VerifyThread();
        _disposed = true;
        _game?.Dispose();
        _game = null;
    }

    private void VerifyThread()
    {
        if (Environment.CurrentManagedThreadId != _thread)
            throw new InvalidOperationException("Project-Z canvas calls belong to the UI thread that created the canvas.");
    }

    private sealed class CanvasGame : Game
    {
        private readonly Action<ProjectZPainter> _draw;
        private readonly nint _foregroundBeforeConstruction;
        private SceneManager? _manager;
        private SpriteBatch? _sprites;
        private ProjectZPainter? _painter;
        private RenderTarget2D? _target;
        private int[] _pixels = [];
        private int _width;
        private int _height;
        private int _scale;
        private bool _started;

        public CanvasGame(Action<ProjectZPainter> draw, nint foregroundBeforeConstruction)
        {
            _draw = draw;
            _foregroundBeforeConstruction = foregroundBeforeConstruction;
            Window.Title = "Project-Z canvas " + Guid.NewGuid().ToString("N");
            IsFixedTimeStep = false;
            InactiveSleepTime = TimeSpan.Zero;
            _ = new Microsoft.Xna.Framework.GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 128,
                PreferredBackBufferHeight = 128,
                GraphicsProfile = GraphicsProfile.HiDef,
                PreferMultiSampling = false,
                SynchronizeWithVerticalRetrace = false
            };
        }

        public ReadOnlySpan<int> Render(int width, int height, int scale)
        {
            _width = width * scale;
            _height = height * scale;
            _scale = scale;
            if (_started)
            {
                Tick();
            }
            else
            {
                var initial = WindowsComposition.ResolveHwnd(this);
                var foreground = GetForegroundWindow();
                if (foreground == initial || foreground == 0)
                    foreground = _foregroundBeforeConstruction;
                PreventActivation(initial);
                // Position the HWND without asking MonoGame to refresh a device before startup.
                if (initial != 0) SetWindowPos(initial, 0, -24000, -24000, 0, 0, 0x15);
                try
                {
                    RunFirstFrameWithoutActivation(initial);
                    _started = true;
                }
                finally
                {
                    var handle = WindowsComposition.ResolveHwnd(this);
                    var current = GetForegroundWindow();
                    var stoleForeground = current != 0 && (current == handle || current == initial);
                    PreventActivation(handle);
                    if (handle != 0) ShowWindow(handle, 0);
                    if (stoleForeground && foreground != 0 && foreground != handle && foreground != initial && IsWindow(foreground))
                        SetForegroundWindow(foreground);
                }
            }

            if (_target is null)
                throw new InvalidOperationException("Project-Z did not produce a canvas frame.");
            _target.GetData(_pixels);
            for (var index = 0; index < _pixels.Length; index++)
                _pixels[index] = SwapRedBlue(_pixels[index]);
            return _pixels;
        }

        protected override void LoadContent()
        {
            _sprites = new SpriteBatch(GraphicsDevice);
            _painter = new ProjectZPainter(GraphicsDevice, _sprites);
            Services.AddService(typeof(SpriteBatch), _sprites);
            Services.AddService(typeof(ContentContainer), new ContentContainer(Content, GraphicsDevice));
            if (Services.GetService(typeof(GraphicsDevice)) is null)
                Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
            _manager = new SceneManager(this, 60) { UseExternalInput = true, AutoResizeViewport = false };
            var scene = new CanvasScene(_manager, _painter, _draw);
            _manager.AddScene("Editor", scene);
            _manager.ActiveScene = scene;
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_manager is null) return;
            if (_target is null || _target.Width != _width || _target.Height != _height)
            {
                var target = new RenderTarget2D(GraphicsDevice, _width, _height, false, SurfaceFormat.Color,
                    DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
                _target?.Dispose();
                _target = target;
                _pixels = new int[checked(_width * _height)];
            }

            _manager.ExternalViewportSize = new Point(_width, _height);
            _painter!.Scale = _scale;
            GraphicsDevice.SetRenderTarget(_target);
            try
            {
                GraphicsDevice.Clear(Color.Transparent);
                _manager.Draw(gameTime);
            }
            finally
            {
                GraphicsDevice.SetRenderTarget(null);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _manager?.Dispose();
                _painter?.Dispose();
                _sprites?.Dispose();
                _target?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void RunFirstFrameWithoutActivation(nint initial)
        {
            const int cbtHook = 5;
            const int cbtActivate = 5;
            var title = Window.Title;
            var text = new StringBuilder(Math.Max(128, title.Length + 1));
            HookProcedure denyRendererActivation = (code, window, detail) =>
            {
                if (code == cbtActivate && (window == initial ||
                    GetWindowTextW(window, text, text.Capacity) > 0 && text.ToString() == title))
                    return 1;
                return CallNextHookEx(0, code, window, detail);
            };
            var hook = SetWindowsHookExW(cbtHook, denyRendererActivation, 0, GetCurrentThreadId());
            try { RunOneFrame(); }
            finally
            {
                if (hook != 0) UnhookWindowsHookEx(hook);
                GC.KeepAlive(denyRendererActivation);
            }
        }

        private delegate nint HookProcedure(int code, nint window, nint detail);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern nint SetWindowsHookExW(int kind, HookProcedure callback, nint module, uint thread);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern nint CallNextHookEx(nint hook, int code, nint window, nint detail);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool UnhookWindowsHookEx(nint hook);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int GetWindowTextW(nint window, StringBuilder text, int length);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint GetCurrentThreadId();

        private static void PreventActivation(nint hwnd)
        {
            if (hwnd == 0 || !IsWindow(hwnd)) return;
            const int extendedStyle = -20;
            const int offscreenStyle = 0x08000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            var style = GetWindowLongW(hwnd, extendedStyle);
            if ((style & offscreenStyle) == offscreenStyle) return;
            SetWindowLongW(hwnd, extendedStyle, style | offscreenStyle);
            SetWindowPos(hwnd, 0, 0, 0, 0, 0, 0x37); // Keep bounds, z-order, and activation; refresh style.
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int GetWindowLongW(nint window, int index);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int SetWindowLongW(nint window, int index, int value);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern nint GetForegroundWindow();

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool SetForegroundWindow(nint window);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool IsWindow(nint window);

        [DllImport("user32.dll", ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool ShowWindow(nint window, int command);
    }

    private sealed class CanvasScene(SceneManager manager, ProjectZPainter painter, Action<ProjectZPainter> draw) : Scene(manager)
    {
        public override void Draw(GameTime gameTime)
        {
            painter.Begin();
            try { draw(painter); }
            finally { painter.End(); }
        }
    }

    internal static int SwapRedBlue(int pixel)
    {
        var value = (uint)pixel;
        return unchecked((int)((value & 0xff00ff00u) | ((value & 255u) << 16) | ((value >> 16) & 255u)));
    }
}

/// <summary>Project-Z drawing operations for a canvas render callback.</summary>
public sealed class ProjectZPainter
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _sprites;
    private readonly Texture2D _pixel;
    private readonly BasicEffect _geometry;
    private readonly SamplerState _captureSampler = new()
    {
        Filter = TextureFilter.Anisotropic,
        AddressU = TextureAddressMode.Clamp,
        AddressV = TextureAddressMode.Clamp,
        MaxAnisotropy = 16
    };
    private readonly VertexPositionColor[] _triangles = new VertexPositionColor[144];
    private readonly List<ImageSlot> _images = [];
    private readonly VectorFont[] _fonts =
    [
        new(11, 550), new(13, 550), new(15, 650), new(18, 720), new(27, 760)
    ];
    private bool _batch;
    private int _nextImage;

    private sealed record ImageSlot(Texture2D Texture, int[] Pixels);

    internal ProjectZPainter(GraphicsDevice device, SpriteBatch sprites)
    {
        _device = device;
        _sprites = sprites;
        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _geometry = new BasicEffect(device) { VertexColorEnabled = true, LightingEnabled = false };
    }

    internal int Scale { get; set; } = 1;
    public int Width => _device.Viewport.Width / Scale;
    public int Height => _device.Viewport.Height / Scale;

    internal void Begin() => _nextImage = 0;
    internal void End() => Flush();

    public void Fill(Rectangle bounds, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || color.A == 0) return;
        StartBatch();
        _sprites.Draw(_pixel, bounds, Premultiply(color));
    }

    /// <summary>Draws a panel with an optional one-pixel border and rounded corners.</summary>
    public void Panel(Rectangle bounds, Color fill, Color border, int radius = 8)
    {
        if (border.A == 0)
        {
            Rounded(bounds, radius, fill);
            return;
        }

        Rounded(bounds, radius, border);
        var inset = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
        Rounded(inset, radius - 1, fill);
    }

    public Vector2 MeasureText(string text, StudioTextStyle style = StudioTextStyle.Body) =>
        Font(style).Measure(text);

    public void Text(string text, float x, float y, Color color, StudioTextStyle style = StudioTextStyle.Body, double align = 0)
    {
        if (string.IsNullOrEmpty(text) || color.A == 0) return;
        Flush();
        var font = Font(style);
        if (align != 0)
            x -= font.Measure(text).X * (float)Math.Clamp(align, 0, 1);
        font.Draw(_device, text, new Vector2(x * Scale, y * Scale), color, Vector2.Zero, Scale);
    }

    public void Image(ReadOnlySpan<int> pixels, int sourceWidth, int sourceHeight, Rectangle target)
    {
        if (sourceWidth is <= 0 or > 4096 || sourceHeight is <= 0 or > 4096 ||
            (long)sourceWidth * sourceHeight != pixels.Length)
            throw new ArgumentException("The image dimensions must match its packed BGRA pixels.", nameof(pixels));
        if (target.Width <= 0 || target.Height <= 0) return;
        var slot = _nextImage++;
        if (slot == _images.Count)
            _images.Add(NewImage(sourceWidth, sourceHeight));
        else if (_images[slot].Texture.Width != sourceWidth || _images[slot].Texture.Height != sourceHeight)
        {
            _images[slot].Texture.Dispose();
            _images[slot] = NewImage(sourceWidth, sourceHeight);
        }

        var image = _images[slot];
        for (var index = 0; index < image.Pixels.Length; index++)
            image.Pixels[index] = ProjectZCanvas.SwapRedBlue(pixels[index]);
        image.Texture.SetData(image.Pixels);
        StartBatch();
        _sprites.Draw(image.Texture, target, Color.White);
    }

    public void Line(float x1, float y1, float x2, float y2, float thickness, Color color)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        var length = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (length <= 0 || thickness <= 0 || color.A == 0) return;
        StartBatch();
        _sprites.Draw(_pixel, new Vector2(x1, y1), null, Premultiply(color), MathF.Atan2(deltaY, deltaX),
            new Vector2(0, .5f), new Vector2(length, thickness), SpriteEffects.None, 0);
    }

    public void Circle(float x, float y, float radius, Color color)
    {
        if (radius <= 0 || color.A == 0) return;
        var center = new Vector2(x, y);
        var last = center + new Vector2(radius, 0);
        var count = Math.Clamp((int)radius * 2, 18, 48);
        for (var index = 0; index < count; index++)
        {
            var angle = (index + 1) * (MathF.Tau / count);
            var next = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            Triangle(index * 3, center, last, next, color);
            last = next;
        }
        DrawTriangles(count);
    }

    private void Rounded(Rectangle bounds, int radius, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || color.A == 0) return;
        radius = Math.Clamp(radius, 0, Math.Min(bounds.Width, bounds.Height) / 2);
        if (radius == 0)
        {
            Fill(bounds, color);
            return;
        }

        const int segments = 8;
        const float quarter = MathF.PI / 2;
        var center = new Vector2(bounds.Center.X, bounds.Center.Y);
        Span<Vector2> corners =
        [
            new(bounds.Right - radius, bounds.Top + radius), new(bounds.Right - radius, bounds.Bottom - radius),
            new(bounds.Left + radius, bounds.Bottom - radius), new(bounds.Left + radius, bounds.Top + radius)
        ];
        var first = new Vector2(bounds.Right - radius, bounds.Top);
        var last = first;
        var triangles = 0;
        for (var corner = 0; corner < corners.Length; corner++)
        for (var segment = 0; segment <= segments; segment++)
        {
            if (corner == 0 && segment == 0) continue;
            var angle = (corner - 1 + segment / (float)segments) * quarter;
            var next = corners[corner] + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            Triangle(triangles++ * 3, center, last, next, color);
            last = next;
        }
        Triangle(triangles++ * 3, center, last, first, color);
        DrawTriangles(triangles);
    }

    private void Triangle(int index, Vector2 first, Vector2 second, Vector2 third, Color tint)
    {
        _triangles[index] = new(new Vector3(first, 0), tint);
        _triangles[index + 1] = new(new Vector3(second, 0), tint);
        _triangles[index + 2] = new(new Vector3(third, 0), tint);
    }

    private void DrawTriangles(int count)
    {
        Flush();
        _device.BlendState = BlendState.AlphaBlend;
        _device.DepthStencilState = DepthStencilState.None;
        _device.RasterizerState = RasterizerState.CullNone;
        _geometry.Projection = Matrix.CreateOrthographicOffCenter(0, Width, Height, 0, 0, 1);
        for (var index = 0; index < count * 3; index++)
            _triangles[index].Color = Premultiply(_triangles[index].Color);
        foreach (var pass in _geometry.CurrentTechnique.Passes)
        {
            pass.Apply();
            _device.DrawUserPrimitives(PrimitiveType.TriangleList, _triangles, 0, count);
        }
    }

    private void StartBatch()
    {
        if (_batch) return;
        _sprites.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Scale == 1 ? SamplerState.LinearClamp : _captureSampler,
            DepthStencilState.None, RasterizerState.CullNone, transformMatrix: Matrix.CreateScale(Scale));
        _batch = true;
    }

    private void Flush()
    {
        if (!_batch) return;
        _sprites.End();
        _batch = false;
    }

    private VectorFont Font(StudioTextStyle style) => _fonts[(int)style];
    private ImageSlot NewImage(int width, int height) => new(new Texture2D(_device, width, height), new int[width * height]);
    private static Color Premultiply(Color color) => color.A == 255 ? color :
        Color.FromNonPremultiplied(color.R, color.G, color.B, color.A);

    internal void Dispose()
    {
        foreach (var image in _images)
            image.Texture.Dispose();
        _images.Clear();
        foreach (var font in _fonts)
            font.Dispose();
        _geometry.Dispose();
        _captureSampler.Dispose();
        _pixel.Dispose();
    }
}
