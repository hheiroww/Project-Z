using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing;

namespace ProjectZ.WpfCompatibility;

/// <summary>Initial native runtime settings emitted from the Project-Z properties page.</summary>
public sealed class ApplicationStartupOptions
{
    public Func<Window>? MainWindowFactory { get; set; }
    public int FramesPerSecond { get; set; } = 60;
    public int AntiAliasingSamples { get; set; } = 4;
    public TextureFiltering TextureFiltering { get; set; } = TextureFiltering.Anisotropic16x;
    public int Width { get; set; } = 1000;
    public int Height { get; set; } = 700;
    public int MinimumWidth { get; set; } = 320;
    public int MinimumHeight { get; set; } = 200;
    public bool AutoResizeViewport { get; set; } = true;
    public bool HardwareInput { get; set; }
    public bool DebugEnabled { get; set; }
    public bool ConsoleEnabled { get; set; }
    public string ContentRoot { get; set; } = "Content";
    public Color BackgroundColor { get; set; } = Color.Black;
    public ShutdownMode? ShutdownMode { get; set; }
    internal void Apply(SceneManager manager)
    {
        if (!new[] { 0, 2, 4, 8, 16 }.Contains(AntiAliasingSamples)) throw new ArgumentOutOfRangeException(nameof(AntiAliasingSamples));
        if (!Enum.IsDefined(TextureFiltering)) throw new ArgumentOutOfRangeException(nameof(TextureFiltering));
        manager.LimitFPS = FramesPerSecond;
        manager.MinimumViewportSize = new(MinimumWidth, MinimumHeight);
        manager.AutoResizeViewport = AutoResizeViewport;
        manager.UseHardwareInput = HardwareInput;
        manager.DebugEnabled = DebugEnabled;
        manager.ConsoleEnabled = ConsoleEnabled;
    }
}
