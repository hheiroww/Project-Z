using System.ComponentModel;

namespace ProjectZ.WpfCompatibility;

public delegate void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e);
public delegate void DependencyPropertyChangedEventHandler(object sender,DependencyPropertyChangedEventArgs e);
public readonly record struct DependencyPropertyChangedEventArgs(DependencyProperty Property, object? OldValue, object? NewValue);
public class PropertyMetadata(object? defaultValue = null, PropertyChangedCallback? propertyChangedCallback = null)
{
    public object? DefaultValue { get; } = defaultValue;
    public PropertyChangedCallback? PropertyChangedCallback { get; } = propertyChangedCallback;
}
[Flags] public enum FrameworkPropertyMetadataOptions { None=0, AffectsMeasure=1, AffectsArrange=2, AffectsParentMeasure=4, AffectsParentArrange=8, AffectsRender=16, Inherits=32, BindsTwoWayByDefault=256 }
public class FrameworkPropertyMetadata : PropertyMetadata
{
    public FrameworkPropertyMetadataOptions Options { get; }
    public FrameworkPropertyMetadata(object? defaultValue) : base(defaultValue) { }
    public FrameworkPropertyMetadata(object? defaultValue,FrameworkPropertyMetadataOptions options) : base(defaultValue) => Options=options;
    public FrameworkPropertyMetadata(object? defaultValue,FrameworkPropertyMetadataOptions options,PropertyChangedCallback callback) : base(defaultValue,callback) => Options=options;
}
public sealed class DependencyProperty
{
    public static readonly object UnsetValue = new();
    public string Name { get; }
    public Type PropertyType { get; }
    public Type OwnerType { get; }
    public PropertyMetadata DefaultMetadata { get; }
    internal bool NativeBacked { get; private set; }
    private DependencyProperty(string name, Type type, Type owner, PropertyMetadata? metadata)
    { Name = name; PropertyType = type; OwnerType = owner; DefaultMetadata = metadata ?? new(type.IsValueType ? Activator.CreateInstance(type) : null); }
    public static DependencyProperty Register(string name, Type propertyType, Type ownerType, PropertyMetadata? typeMetadata = null) => new(name, propertyType, ownerType, typeMetadata);
    public static DependencyProperty RegisterAttached(string name, Type propertyType, Type ownerType, PropertyMetadata? defaultMetadata = null) => Register(name, propertyType, ownerType, defaultMetadata);
    internal static DependencyProperty RegisterNative(string name, Type type, Type owner, PropertyMetadata? metadata = null) => new(name, type, owner, metadata) { NativeBacked = true };
}
public class DependencyObject : INotifyPropertyChanged
{
    internal global::ProjectZ.Shared.Drawing.Scene? AnimationScene;
    readonly Dictionary<string, List<global::ProjectZ.Shared.Animations.PropertyAnimationClock>> animations = new();
    internal void TrackAnimation(string name, global::ProjectZ.Shared.Animations.PropertyAnimationClock clock) { if (!animations.TryGetValue(name, out var tracks)) animations[name] = tracks = new(); tracks.Add(clock); }
    internal void RemoveAnimation(string name) { if (!animations.Remove(name, out var tracks)) return; foreach (var track in tracks) track.Stop(); }
    internal void ClearAnimations() { foreach (var name in animations.Keys.ToArray()) RemoveAnimation(name); }
    public void BeginAnimation(DependencyProperty property, Media.Animation.AnimationTimeline? animation) => BeginAnimation(property, animation, Media.Animation.HandoffBehavior.SnapshotAndReplace);
    public void BeginAnimation(DependencyProperty property, Media.Animation.AnimationTimeline? animation, Media.Animation.HandoffBehavior handoffBehavior) => Media.Animation.AnimationHost.Start(this, new PropertyPath(property), animation, handoffBehavior);
    readonly Dictionary<DependencyProperty, object?> values = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public Threading.Dispatcher Dispatcher => Application.Current.Dispatcher;
#nullable disable
    // WPF's legacy GetValue contract permits null without nullable annotations.
    public object GetValue(DependencyProperty dp) => dp.NativeBacked ? GetType().GetProperty(dp.Name).GetValue(this) : values.TryGetValue(dp, out var value) ? value : dp.DefaultMetadata.DefaultValue;
#nullable restore
    public virtual void SetValue(DependencyProperty dp, object? value)
    {
        Dispatcher.VerifyAccess();
        if (value != null && !dp.PropertyType.IsInstanceOfType(value)) throw new ArgumentException($"{dp.Name} requires {dp.PropertyType}.");
        var old = GetValue(dp);
        if (Equals(old, value)) return;
        if (dp.NativeBacked) { GetType().GetProperty(dp.Name)!.SetValue(this, value); return; }
        values[dp] = value;
        dp.DefaultMetadata.PropertyChangedCallback?.Invoke(this, new(dp, old, value));
        Changed(dp.Name);
    }
    public virtual void ClearValue(DependencyProperty dp) { SetValue(dp, dp.DefaultMetadata.DefaultValue); values.Remove(dp); }
    protected internal void Changed(string name)
    {
        if (this is FrameworkElement element) element.NativeElement?.OwnerScene?.RequestRender();
        PropertyChanged?.Invoke(this, new(name));
    }
}
public enum Visibility { Visible, Hidden, Collapsed }
public enum HorizontalAlignment { Left, Center, Right, Stretch }
public enum VerticalAlignment { Top, Center, Bottom, Stretch }
public struct Thickness
{
    public double Left { get; set; } public double Top { get; set; } public double Right { get; set; } public double Bottom { get; set; }
    public Thickness(double value) : this(value, value, value, value) { }
    public Thickness(double left, double top, double right, double bottom) { Left = left; Top = top; Right = right; Bottom = bottom; }
    public override readonly string ToString() => FormattableString.Invariant($"{Left},{Top},{Right},{Bottom}");
}
public readonly record struct Point(double X, double Y);
public readonly record struct Size(double Width, double Height);
public class PropertyPath { public string Path { get; set; } public PropertyPath(string path) => Path = path; public PropertyPath(DependencyProperty property) => Path = property.Name; }
public class ResourceDictionary : Dictionary<object, object> { }
public class RoutedEventArgs : EventArgs
{
    public RoutedEventArgs() { }
    public RoutedEventArgs(RoutedEvent routedEvent) => RoutedEvent = routedEvent;
    public RoutedEventArgs(RoutedEvent routedEvent, object source) { RoutedEvent = routedEvent; Source = source; }
    public RoutedEvent? RoutedEvent { get; set; }
    public bool Handled { get; set; }
    public object? Source { get; set; }
    public object? OriginalSource { get; internal set; }
}
public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);
public class SizeChangedEventArgs(Size oldSize, Size newSize) : RoutedEventArgs
{ public Size PreviousSize { get; } = oldSize; public Size NewSize { get; } = newSize; }
public delegate void SizeChangedEventHandler(object sender, SizeChangedEventArgs e);
