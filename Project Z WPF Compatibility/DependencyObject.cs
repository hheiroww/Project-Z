using System.ComponentModel;

namespace ProjectZ.WpfCompatibility;

public delegate void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e);
public readonly record struct DependencyPropertyChangedEventArgs(DependencyProperty Property, object? OldValue, object? NewValue);
public class PropertyMetadata(object? defaultValue = null, PropertyChangedCallback? propertyChangedCallback = null)
{
    public object? DefaultValue { get; } = defaultValue;
    public PropertyChangedCallback? PropertyChangedCallback { get; } = propertyChangedCallback;
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
    readonly Dictionary<DependencyProperty, object?> values = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public Threading.Dispatcher Dispatcher => Application.Current.Dispatcher;
    public object? GetValue(DependencyProperty dp) => dp.NativeBacked ? GetType().GetProperty(dp.Name)!.GetValue(this) : values.TryGetValue(dp, out var value) ? value : dp.DefaultMetadata.DefaultValue;
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
    protected internal void Changed(string name) => PropertyChanged?.Invoke(this, new(name));
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
public class PropertyPath(string path) { public string Path { get; set; } = path; }
public class ResourceDictionary : Dictionary<object, object> { }
public class RoutedEventArgs : EventArgs
{
    public bool Handled { get; set; }
    public object? Source { get; set; }
    public object? OriginalSource { get; internal set; }
}
public delegate void RoutedEventHandler(object sender, RoutedEventArgs e);
public class SizeChangedEventArgs(Size oldSize, Size newSize) : RoutedEventArgs
{ public Size PreviousSize { get; } = oldSize; public Size NewSize { get; } = newSize; }
public delegate void SizeChangedEventHandler(object sender, SizeChangedEventArgs e);
