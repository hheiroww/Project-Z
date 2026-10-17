namespace ProjectZ.WpfCompatibility;

public enum TextDataFormat { Text, UnicodeText, Rtf, Html, CommaSeparatedValue, Xaml }

/// <summary>Clipboard access is a desktop service; no WPF clipboard dependency.</summary>
public static class Clipboard
{
    public static void Clear() => System.Windows.Forms.Clipboard.Clear();
    public static string GetText() => System.Windows.Forms.Clipboard.GetText();
    public static string GetText(TextDataFormat format) => format == TextDataFormat.Xaml ? System.Windows.Forms.Clipboard.GetData("Xaml") as string ?? "" : System.Windows.Forms.Clipboard.GetText(NativeFormat(format));
    public static bool ContainsText() => System.Windows.Forms.Clipboard.ContainsText();
    public static bool ContainsText(TextDataFormat format) => format == TextDataFormat.Xaml ? System.Windows.Forms.Clipboard.ContainsData("Xaml") : System.Windows.Forms.Clipboard.ContainsText(NativeFormat(format));
    public static void SetText(string text) => System.Windows.Forms.Clipboard.SetText(text);
    public static void SetText(string text, TextDataFormat format) { if (format == TextDataFormat.Xaml) System.Windows.Forms.Clipboard.SetData("Xaml", text); else System.Windows.Forms.Clipboard.SetText(text, NativeFormat(format)); }
    static System.Windows.Forms.TextDataFormat NativeFormat(TextDataFormat format) => format switch
    {
        TextDataFormat.Text => System.Windows.Forms.TextDataFormat.Text,
        TextDataFormat.UnicodeText => System.Windows.Forms.TextDataFormat.UnicodeText,
        TextDataFormat.Rtf => System.Windows.Forms.TextDataFormat.Rtf,
        TextDataFormat.Html => System.Windows.Forms.TextDataFormat.Html,
        TextDataFormat.CommaSeparatedValue => System.Windows.Forms.TextDataFormat.CommaSeparatedValue,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
    public static void SetDataObject(object data) => System.Windows.Forms.Clipboard.SetDataObject(DesktopDataAdapter.Unwrap(data));
    public static void SetDataObject(object data, bool copy) => System.Windows.Forms.Clipboard.SetDataObject(DesktopDataAdapter.Unwrap(data), copy);
    public static System.Windows.Forms.IDataObject? GetDataObject() => System.Windows.Forms.Clipboard.GetDataObject() is { } data ? new DesktopDataAdapter(data) : null;
}

public enum RoutingStrategy { Tunnel, Bubble, Direct }
public sealed class RoutedEvent
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RoutedEvent> registry = new(StringComparer.Ordinal);
    public string Name { get; }
    public RoutingStrategy RoutingStrategy { get; }
    internal global::ProjectZ.Shared.Drawing.UI.SceneRoutedEvent Native { get; }
    public RoutedEvent(string name) : this(name, Strategy(name)) { }
    public RoutedEvent(string name, RoutingStrategy routingStrategy)
    {
        Name = name; RoutingStrategy = routingStrategy;
        Native = global::ProjectZ.Shared.Drawing.UI.SceneRoutedEvent.Register(name, routingStrategy switch
        {
            RoutingStrategy.Tunnel => global::ProjectZ.Shared.Drawing.UI.SceneEventRouting.Tunnel,
            RoutingStrategy.Direct => global::ProjectZ.Shared.Drawing.UI.SceneEventRouting.Direct,
            _ => global::ProjectZ.Shared.Drawing.UI.SceneEventRouting.Bubble
        });
        registry.TryAdd(name, this);
    }
    internal static RoutingStrategy Strategy(string name) => name.StartsWith("Preview", StringComparison.Ordinal) ? RoutingStrategy.Tunnel : name is "Loaded" or "Unloaded" or "SizeChanged" or "MouseEnter" or "MouseLeave" ? RoutingStrategy.Direct : RoutingStrategy.Bubble;
    internal static RoutedEvent For(string name) => registry.TryGetValue(name, out var routedEvent) ? routedEvent : new(name);
}
public static class EventManager
{
    public static RoutedEvent RegisterRoutedEvent(string name, RoutingStrategy routingStrategy, Type handlerType, Type ownerType) => new(name, routingStrategy);
}
