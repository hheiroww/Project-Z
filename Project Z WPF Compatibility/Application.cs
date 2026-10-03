using ProjectZ.Shared.Drawing;

namespace ProjectZ.WpfCompatibility;

/// <summary>Install on the game thread before constructing imported views. One instance owns one scene.</summary>
public class Application : IDisposable
{
    static Application? current;
    readonly SynchronizationContext? previousContext;
    internal static Scene? HostingScene;
    public static Application Current => current ?? throw new InvalidOperationException("Create a compatibility Application with a live Project-Z Scene before constructing imported controls.");
    public Scene Scene { get; }
    public Threading.Dispatcher Dispatcher { get; } = new();
    public ResourceDictionary Resources { get; } = new();
    public Window? MainWindow { get; set; }
    public Func<Window>? StartupFactory { get; set; }
    public Uri? StartupUri { get; set; }
    public event StartupEventHandler? Startup;
    public event ExitEventHandler? Exit;
    internal List<Window> Windows { get; } = new();
    public Application() : this(HostingScene ?? throw new InvalidOperationException("Use Hosting.Run to start an imported application.")) { }
    public Application(Scene scene)
    {
        if (current != null) throw new InvalidOperationException("A compatibility Application already owns a scene.");
        Scene = scene; current = this; scene.PreDraw += OnFrame;
        previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new Threading.DispatcherSynchronizationContext(Dispatcher));
    }
    void OnFrame(Microsoft.Xna.Framework.GameTime time) => Dispatcher.Pump();
    protected virtual void OnStartup(StartupEventArgs e) => Startup?.Invoke(this, e);
    protected virtual void OnExit(ExitEventArgs e) => Exit?.Invoke(this, e);
    internal void Start(string[] args) { OnStartup(new(args)); if (MainWindow == null && StartupFactory != null) StartupFactory().Show(); }
    public void Shutdown() { foreach (var window in Windows.ToArray()) window.Close(); }
    public void Dispose() { if (current != this) return; Shutdown(); foreach (var window in Windows.ToArray()) window.Dispose(); Windows.Clear(); OnExit(new()); Scene.PreDraw -= OnFrame; current = null; SynchronizationContext.SetSynchronizationContext(previousContext); }
}
public sealed class StartupEventArgs(string[] args) : EventArgs { public string[] Args { get; } = args; }
public sealed class ExitEventArgs : EventArgs { public int ApplicationExitCode { get; set; } }
public delegate void StartupEventHandler(object sender, StartupEventArgs e);
public delegate void ExitEventHandler(object sender, ExitEventArgs e);
