using ProjectZ.Shared.Drawing;

namespace ProjectZ.WpfCompatibility;
public enum ShutdownMode { OnLastWindowClose, OnMainWindowClose, OnExplicitShutdown }

/// <summary>Install on the game thread before constructing imported views. One instance owns one scene.</summary>
public class Application : IDisposable
{
    static Application? current;
    internal static Application? Active => current;
    readonly SynchronizationContext? previousContext;
    internal static Scene? HostingScene;
    public static Application Current => current ?? throw new InvalidOperationException("Create a compatibility Application with a live Project-Z Scene before constructing imported controls.");
    public Scene Scene { get; }
    public ApplicationStartupOptions StartupOptions { get; internal set; } = new();
    internal Scene? ConstructionScene;
    internal Scene ControlScene => ConstructionScene ?? Scene;
    internal IDisposable ConstructIn(Scene scene)
    {
        var previous=ConstructionScene;ConstructionScene=scene;return new ConstructionScope(()=>ConstructionScene=previous);
    }
    sealed class ConstructionScope(Action restore) : IDisposable { public void Dispose()=>restore(); }
    internal bool UseNativeWindows;
    internal void PumpWindows()
    {
        if (nativeMessageHosts > 0) System.Windows.Forms.Application.DoEvents();
        foreach (var window in Windows.ToArray()) window.RenderNativeFrame();
    }
    public Threading.Dispatcher Dispatcher { get; } = Threading.Dispatcher.CurrentDispatcher;
    public ResourceDictionary Resources { get; } = new();
    public Window? MainWindow { get; set; }
    public Func<Window>? StartupFactory { get; set; }
    public Uri? StartupUri { get; set; }
    public ShutdownMode ShutdownMode { get; set; } = ShutdownMode.OnLastWindowClose;
    internal bool HostPumpsDispatcher, ShutdownRequested;
    int nativeMessageHosts;
    internal void AttachNativeMessageHost() => nativeMessageHosts++;
    internal void DetachNativeMessageHost() => nativeMessageHosts = Math.Max(0, nativeMessageHosts - 1);
    int exitCode;
    internal int ExitCode => exitCode;
    public event StartupEventHandler? Startup;
    public event ExitEventHandler? Exit;
    public event Threading.DispatcherUnhandledExceptionEventHandler? DispatcherUnhandledException;
    public WindowCollection Windows { get; } = new();
    public Application() : this(HostingScene ?? throw new InvalidOperationException("Use Hosting.Run to start an imported application.")) { }
    public Application(Scene scene)
    {
        ResourceUris.RegisterPackScheme();
        if (current != null) throw new InvalidOperationException("A compatibility Application already owns a scene.");
        Scene = scene; current = this; scene.PreDraw += OnFrame;
        Dispatcher.UnhandledException += OnDispatcherException;
        previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new Threading.DispatcherSynchronizationContext(Dispatcher));
    }
    void OnFrame(Microsoft.Xna.Framework.GameTime time)
    {
        Media.CompositionTarget.RenderFrame(time.TotalGameTime);
        if (nativeMessageHosts > 0) System.Windows.Forms.Application.DoEvents();
        if (!HostPumpsDispatcher) Dispatcher.Pump();
    }
    void OnDispatcherException(object sender, Threading.DispatcherUnhandledExceptionEventArgs args) => DispatcherUnhandledException?.Invoke(this, args);
    protected virtual void OnStartup(StartupEventArgs e) => Startup?.Invoke(this, e);
    protected virtual void OnExit(ExitEventArgs e) => Exit?.Invoke(this, e);
    internal void Start(string[] args) { OnStartup(new(args)); if (MainWindow == null && StartupFactory != null) StartupFactory().Show(); }
    internal void WindowClosed(bool wasMain)
    {
        if ((ShutdownMode == ShutdownMode.OnLastWindowClose && Windows.Count == 0) || (ShutdownMode == ShutdownMode.OnMainWindowClose && wasMain)) Shutdown();
    }
    public void Shutdown() => Shutdown(0);
    public void Shutdown(int exitCode)
    {
        Dispatcher.VerifyAccess(); if (ShutdownRequested) return;
        this.exitCode = exitCode; ShutdownRequested = true;
        foreach (var window in Windows.ToArray()) window.CloseForShutdown();
    }
    public void Dispose()
    {
        if (current != this) return;
        try
        {
            Shutdown(); foreach (var window in Windows.ToArray()) window.Dispose(); Windows.Clear();
            var args = new ExitEventArgs { ApplicationExitCode = exitCode }; OnExit(args); exitCode = args.ApplicationExitCode;
        }
        finally
        {
            Scene.PreDraw -= OnFrame; Dispatcher.UnhandledException -= OnDispatcherException; Dispatcher.InvokeShutdown();
            current = null; SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }
}
public sealed class StartupEventArgs(string[] args) : EventArgs { public string[] Args { get; } = args; }
public sealed class ExitEventArgs : EventArgs { public int ApplicationExitCode { get; set; } }
public delegate void StartupEventHandler(object sender, StartupEventArgs e);
public delegate void ExitEventHandler(object sender, ExitEventArgs e);
public sealed class WindowCollection : IReadOnlyList<Window>
{
    readonly List<Window> windows = new();
    public int Count => windows.Count;
    public Window this[int index] => windows[index];
    internal void Add(Window window) => windows.Add(window);
    internal void Remove(Window window) => windows.Remove(window);
    internal void Clear() => windows.Clear();
    public IEnumerator<Window> GetEnumerator() => windows.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    public void CopyTo(Array array, int index) => ((System.Collections.ICollection)windows).CopyTo(array, index);
}
