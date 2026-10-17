using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using ProjectZ.Shared.Drawing.UI;
using Forms = System.Windows.Forms;
using NativeBrowser = Microsoft.Web.WebView2.WinForms.WebView2;
using Native = ProjectZ.Shared.Drawing.UI.Input;

namespace ProjectZ.WpfCompatibility.Controls;

/// <summary>Imports the WPF SDK surface onto heirowSnap's WinForms child-HWND hosting path.</summary>
public class WebView2 : Control
{
    sealed class ChildPanel(IntPtr parent) : Forms.Panel
    {
        protected override Forms.CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.Parent = parent; value.Style |= 0x40000000 | 0x02000000; return value; }
        }
    }
    ChildPanel? panel;
    NativeBrowser? browser;
    Task? initialization;
    Uri? source;
    double zoomFactor = 1;
    Color defaultBackgroundColor = Color.White;
    bool allowExternalDrop = true;
    CoreWebView2CreationProperties? creationProperties;
    readonly Application application;
    bool messageHostAttached;
    public WebView2() : this(new Native.Border(Application.Current.ControlScene) { BackgroundColor = Microsoft.Xna.Framework.Color.Transparent, BorderThickness = 0, Padding = new(0), CanSelect = true }) { }
    internal WebView2(SceneElement native) : base(native)
    {
        application = Application.Current;
        Listen("Loaded", _ => { if (source != null) ObserveInitialization(); });
        native.OwnerScene.PreDraw += ArrangeBeforeDraw;
        native.OwnerScene.PostDraw += ArrangeBeforeDraw;
    }
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterNative(nameof(Source), typeof(Uri), typeof(WebView2));
    public static readonly DependencyProperty ZoomFactorProperty = DependencyProperty.RegisterNative(nameof(ZoomFactor), typeof(double), typeof(WebView2), new(1d));
    public static readonly DependencyProperty DefaultBackgroundColorProperty = DependencyProperty.RegisterNative(nameof(DefaultBackgroundColor), typeof(Color), typeof(WebView2), new(Color.White));
    public static readonly DependencyProperty CreationPropertiesProperty = DependencyProperty.RegisterNative(nameof(CreationProperties), typeof(CoreWebView2CreationProperties), typeof(WebView2));
    public static readonly DependencyProperty AllowExternalDropProperty = DependencyProperty.RegisterNative(nameof(AllowExternalDrop), typeof(bool), typeof(WebView2), new(true));
#nullable disable
    // Match the WPF wrapper's legacy annotation contract, including null before
    // initialization. EnsureCoreWebView2Async remains the readiness boundary.
    public CoreWebView2 CoreWebView2 => browser?.CoreWebView2;
#nullable restore
    public Exception? InitializationException { get; private set; }
    public IntPtr NativeHostHandle => panel?.Handle ?? IntPtr.Zero;
    public Uri? Source
    {
        get => browser?.Source ?? source;
        set { if (value != null && !value.IsAbsoluteUri) throw new ArgumentException("WebView2 needs an absolute URI.", nameof(value)); source = value; if (browser != null && value != null) browser.Source = value; else if (IsLoaded && value != null) ObserveInitialization(); Changed(nameof(Source)); }
    }
    public CoreWebView2CreationProperties? CreationProperties
    {
        get => creationProperties;
        set { if (initialization != null) throw new InvalidOperationException("Set creation properties before WebView2 initialization."); creationProperties = value; }
    }
    public Color DefaultBackgroundColor { get => browser?.DefaultBackgroundColor ?? defaultBackgroundColor; set { defaultBackgroundColor = value; if (browser != null) browser.DefaultBackgroundColor = value; } }
    public double ZoomFactor { get => browser?.ZoomFactor ?? zoomFactor; set { if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); zoomFactor = value; if (browser != null) browser.ZoomFactor = value; } }
    public bool AllowExternalDrop { get => allowExternalDrop; set { allowExternalDrop = value; if (browser != null) browser.AllowExternalDrop = value; } }
    public bool CanGoBack => browser?.CanGoBack == true;
    public bool CanGoForward => browser?.CanGoForward == true;
    public event EventHandler<CoreWebView2InitializationCompletedEventArgs>? CoreWebView2InitializationCompleted;
    public event EventHandler<CoreWebView2NavigationStartingEventArgs>? NavigationStarting;
    public event EventHandler<CoreWebView2NavigationCompletedEventArgs>? NavigationCompleted;
    public event EventHandler<CoreWebView2ContentLoadingEventArgs>? ContentLoading;
    public event EventHandler<CoreWebView2SourceChangedEventArgs>? SourceChanged;
    public event EventHandler<CoreWebView2WebMessageReceivedEventArgs>? WebMessageReceived;
    public Task EnsureCoreWebView2Async(CoreWebView2Environment? environment = null, CoreWebView2ControllerOptions? controllerOptions = null)
    {
        Dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(IsDisposed, this);
        Attach();
        return initialization ??= InitializeAsync(environment, controllerOptions);
    }
    async Task InitializeAsync(CoreWebView2Environment? environment, CoreWebView2ControllerOptions? controllerOptions)
    {
        await browser!.EnsureCoreWebView2Async(environment, controllerOptions);
        if (!IsDisposed && source != null) browser.Source = source;
    }
    async void ObserveInitialization()
    {
        try { await EnsureCoreWebView2Async(); }
        catch (Exception error) when (error is COMException or InvalidOperationException or ObjectDisposedException) { InitializationException = error; }
    }
    void Attach()
    {
        if (browser != null) return;
        var game = NativeElement.OwnerScene?.sender() ?? throw new InvalidOperationException("WebView2 needs a running Project-Z scene.");
        var hwnd = global::ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(game);
        if (hwnd == IntPtr.Zero) throw new InvalidOperationException("Project-Z must create its native window before attaching WebView2.");
        panel = new(hwnd) { Visible = false };
        browser = new() { Dock = Forms.DockStyle.Fill, DefaultBackgroundColor = defaultBackgroundColor, ZoomFactor = zoomFactor, AllowExternalDrop = allowExternalDrop,
            CreationProperties = creationProperties?.Native() ?? new() { UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProjectZ", "WebView2") } };
        browser.CoreWebView2InitializationCompleted += (_, e) => { if (!IsDisposed) { InitializationException = e.InitializationException; CoreWebView2InitializationCompleted?.Invoke(this, e); } };
        browser.NavigationStarting += (_, e) => { if (!IsDisposed) NavigationStarting?.Invoke(this, e); };
        browser.NavigationCompleted += (_, e) => { if (!IsDisposed) NavigationCompleted?.Invoke(this, e); };
        browser.ContentLoading += (_, e) => { if (!IsDisposed) ContentLoading?.Invoke(this, e); };
        browser.SourceChanged += (_, e) => { if (!IsDisposed) { Changed(nameof(Source)); Changed(nameof(CanGoBack)); Changed(nameof(CanGoForward)); SourceChanged?.Invoke(this, e); } };
        browser.WebMessageReceived += (_, e) => { if (!IsDisposed) WebMessageReceived?.Invoke(this, e); };
        browser.Enter += (_, _) => { if (!IsDisposed) Focus(); };
        var context = SynchronizationContext.Current;
        var autoInstall = Forms.WindowsFormsSynchronizationContext.AutoInstall;
        try
        {
            Forms.WindowsFormsSynchronizationContext.AutoInstall = false;
            panel.Controls.Add(browser); panel.CreateControl(); browser.CreateControl();
        }
        finally { Forms.WindowsFormsSynchronizationContext.AutoInstall = autoInstall; SynchronizationContext.SetSynchronizationContext(context); }
        application.AttachNativeMessageHost(); messageHostAttached = true;
        Arrange();
    }
    void ArrangeBeforeDraw(Microsoft.Xna.Framework.GameTime time) { if (panel != null && !IsDisposed) Arrange(); }
    public void Arrange()
    {
        if (panel == null || IsDisposed) return;
        var scene = NativeElement.OwnerScene;
        var bounds = NativeElement.Rectangle;
        var clip = new Microsoft.Xna.Framework.Rectangle(0, 0, scene.InputViewportSize.X, scene.InputViewportSize.Y);
        bool visible = scene.Elements.Contains(NativeElement);
        for (var node = NativeElement; node != null; node = node.RoutedParent)
        {
            visible &= node.isVisible && !node.SuppressVisual && node.Opacity > 0;
            if (node.Clip) clip = Microsoft.Xna.Framework.Rectangle.Intersect(clip, node.ClippingRectangle);
            if (node.VirtualizationViewport != null) clip = Microsoft.Xna.Framework.Rectangle.Intersect(clip, node.VirtualizationViewport.Rectangle);
        }
        var intersection = Microsoft.Xna.Framework.Rectangle.Intersect(bounds, clip);
        panel.Bounds = new(bounds.X, bounds.Y, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
        var region = new Region(new Rectangle(intersection.X - bounds.X, intersection.Y - bounds.Y, Math.Max(0, intersection.Width), Math.Max(0, intersection.Height)));
        var old = panel.Region; panel.Region = region; old?.Dispose();
        panel.Enabled = IsEnabled;
        panel.Visible = visible && intersection.Width > 0 && intersection.Height > 0;
        if (panel.Visible) panel.BringToFront();
    }
    public Task<string> ExecuteScriptAsync(string script) => RequireBrowser().ExecuteScriptAsync(script);
    public void NavigateToString(string htmlContent) => RequireBrowser().NavigateToString(htmlContent);
    public void GoBack() => RequireBrowser().GoBack();
    public void GoForward() => RequireBrowser().GoForward();
    public void Reload() => RequireBrowser().Reload();
    NativeBrowser RequireBrowser() { ObjectDisposedException.ThrowIf(IsDisposed, this); return browser ?? throw new InvalidOperationException("Initialize WebView2 before using its browser API."); }
    public override void Dispose()
    {
        if (IsDisposed) return;
        NativeElement.OwnerScene.PreDraw -= ArrangeBeforeDraw; NativeElement.OwnerScene.PostDraw -= ArrangeBeforeDraw;
        browser?.Dispose(); panel?.Dispose(); browser = null; panel = null;
        if (messageHostAttached) { application.DetachNativeMessageHost(); messageHostAttached = false; }
        base.Dispose();
    }
}

public sealed class WebView2CompositionControl : WebView2 { public WebView2CompositionControl() { } internal WebView2CompositionControl(SceneElement native) : base(native) { } }

public sealed class CoreWebView2CreationProperties : DependencyObject
{
    public string? BrowserExecutableFolder { get; set; }
    public string? UserDataFolder { get; set; }
    public string? Language { get; set; }
    public string? AdditionalBrowserArguments { get; set; }
    public string? ProfileName { get; set; }
    public bool? IsInPrivateModeEnabled { get; set; }
    internal Microsoft.Web.WebView2.WinForms.CoreWebView2CreationProperties Native() => new()
    {
        BrowserExecutableFolder = BrowserExecutableFolder, UserDataFolder = UserDataFolder, Language = Language,
        AdditionalBrowserArguments = AdditionalBrowserArguments, ProfileName = ProfileName, IsInPrivateModeEnabled = IsInPrivateModeEnabled
    };
}
