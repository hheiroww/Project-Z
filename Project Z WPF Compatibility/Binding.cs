using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ProjectZ.WpfCompatibility.Data;

public enum BindingMode { Default, OneTime, OneWay, TwoWay }
public enum UpdateSourceTrigger { Default, PropertyChanged, LostFocus, Explicit }
public enum RelativeSourceMode { Self, FindAncestor }
public class RelativeSource
{
    public RelativeSourceMode Mode { get; set; }
    public Type? AncestorType { get; set; }
    public int AncestorLevel { get; set; } = 1;
    public static RelativeSource Self => new(RelativeSourceMode.Self);
    public RelativeSource() { }
    public RelativeSource(RelativeSourceMode mode) => Mode = mode;
}
#nullable disable
// Preserve the original WPF converter contract's oblivious nullability so
// unchanged legacy converters do not acquire annotation mismatch warnings.
public interface IValueConverter
{
    object Convert(object value, Type targetType, object parameter, CultureInfo culture);
    object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
}
#nullable restore
public class Binding
{
    public static readonly object DoNothing = new();
    object? source;
    internal bool HasSource;
    public PropertyPath Path { get; set; } = new("");
    public object? Source { get => source; set { source = value; HasSource = true; } }
    public string? ElementName { get; set; }
    public string? StringFormat { get; set; }
    public RelativeSource? RelativeSource { get; set; }
    public BindingMode Mode { get; set; }
    public UpdateSourceTrigger UpdateSourceTrigger { get; set; }
    public IValueConverter? Converter { get; set; }
    public object? ConverterParameter { get; set; }
    public object? FallbackValue { get; set; } = DependencyProperty.UnsetValue;
    public object? TargetNullValue { get; set; } = DependencyProperty.UnsetValue;
    public Binding() { }
    public Binding(string path) => Path = new(path);
}
public sealed class BindingExpression : IDisposable
{
    readonly FrameworkElement target;
    readonly string property;
    readonly DependencyProperty? dependencyProperty;
    readonly Binding binding;
    readonly List<INotifyPropertyChanged> sources = new();
    bool busy, disposed, oneTimeDone;
    readonly BindingMode mode;
    readonly UpdateSourceTrigger trigger;
    public string? LastError { get; private set; }
    public static event Action<BindingExpression, string>? BindingFailed;
    internal BindingExpression(FrameworkElement target, string property, Binding binding, DependencyProperty? dp)
    {
        this.target = target; this.property = property; this.binding = binding; dependencyProperty = dp;
        mode = binding.Mode == BindingMode.Default ? (target is Controls.TextBox && property == "Text" || property is "IsChecked" or "SelectedItem" or "SelectedIndex" ? BindingMode.TwoWay : BindingMode.OneWay) : binding.Mode;
        trigger = binding.UpdateSourceTrigger == UpdateSourceTrigger.Default ? (target is Controls.TextBox && property == "Text" ? UpdateSourceTrigger.LostFocus : UpdateSourceTrigger.PropertyChanged) : binding.UpdateSourceTrigger;
        target.PropertyChanged += TargetChanged; target.LostFocus += LostFocus; UpdateTarget();
    }
    object? Root()
    {
        if (binding.HasSource) return binding.Source;
        if (binding.ElementName != null) return target.FindName(binding.ElementName);
        if (binding.RelativeSource is { } relative)
        {
            if (relative.Mode == RelativeSourceMode.Self) return target;
            int level = relative.AncestorLevel;
            for (var parent = target.Parent; parent != null; parent = parent.Parent)
                if ((relative.AncestorType == null || relative.AncestorType.IsInstanceOfType(parent)) && --level == 0) return parent;
            return null;
        }
        return target.DataContext;
    }
    void Watch(object? source)
    {
        if (mode != BindingMode.OneTime && source is INotifyPropertyChanged notify && !sources.Contains(notify)) { sources.Add(notify); notify.PropertyChanged += SourceChanged; }
    }
    void Unwatch() { foreach (var source in sources) source.PropertyChanged -= SourceChanged; sources.Clear(); }
    void SourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (busy || disposed) return;
        if (target.Dispatcher.CheckAccess()) UpdateTarget(); else _ = target.Dispatcher.InvokeAsync(UpdateTarget);
    }
    void TargetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (busy || disposed) return;
        if (e.PropertyName == "DataContext" && !binding.HasSource && binding.ElementName == null && binding.RelativeSource == null) { oneTimeDone = false; UpdateTarget(); }
        else if (e.PropertyName == property && trigger == UpdateSourceTrigger.PropertyChanged) UpdateSource();
    }
    void LostFocus(object sender, RoutedEventArgs e) { if (trigger == UpdateSourceTrigger.LostFocus) UpdateSource(); }
    static PropertyInfo Resolve(object owner, string name) => owner.GetType().GetProperty(name) ?? throw new MissingMemberException(owner.GetType().FullName, name);
    public void UpdateTarget()
    {
        if (disposed || busy || oneTimeDone) return;
        target.Dispatcher.VerifyAccess(); busy = true;
        try
        {
            Unwatch(); object? value = Root(); Watch(value);
            foreach (var part in binding.Path.Path.Split('.', StringSplitOptions.RemoveEmptyEntries)) { if (value == null) break; value = Resolve(value, part).GetValue(value); Watch(value); }
            var targetProperty = target.GetType().GetProperty(property);
            var targetType = targetProperty?.PropertyType ?? dependencyProperty?.PropertyType ?? throw new MissingMemberException(target.GetType().FullName, property);
            if (value == null && binding.TargetNullValue != DependencyProperty.UnsetValue) value = binding.TargetNullValue;
            if (binding.Converter != null) value = binding.Converter.Convert(value, targetType, binding.ConverterParameter, CultureInfo.CurrentCulture);
            if (ReferenceEquals(value, Binding.DoNothing)) return;
            if (ReferenceEquals(value, DependencyProperty.UnsetValue)) value = binding.FallbackValue;
            if (ReferenceEquals(value, DependencyProperty.UnsetValue)) throw new InvalidOperationException("Binding returned UnsetValue without a fallback.");
            if(binding.StringFormat!=null && targetType==typeof(string)) value=string.Format(CultureInfo.CurrentCulture,binding.StringFormat,value);
            value = Coerce(value, targetType);
            if (targetProperty != null) targetProperty.SetValue(target, value); else target.SetValue(dependencyProperty!, value);
            LastError = null; oneTimeDone = mode == BindingMode.OneTime && Root() != null;
        }
        catch (Exception error) { LastError = $"{target.Name}.{property}: {error.GetBaseException().Message}"; BindingFailed?.Invoke(this, LastError); }
        finally { busy = false; }
    }
    public void UpdateSource()
    {
        if (disposed || busy || mode != BindingMode.TwoWay) return;
        target.Dispatcher.VerifyAccess(); busy = true;
        try
        {
            object? owner = Root(); var parts = binding.Path.Path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || owner == null) return;
            foreach (var part in parts[..^1]) { owner = Resolve(owner, part).GetValue(owner); if (owner == null) return; }
            var destination = Resolve(owner, parts[^1]);
            var value = target.GetType().GetProperty(property)?.GetValue(target) ?? (dependencyProperty != null ? target.GetValue(dependencyProperty) : null);
            if (binding.Converter != null) value = binding.Converter.ConvertBack(value, destination.PropertyType, binding.ConverterParameter, CultureInfo.CurrentCulture);
            if (ReferenceEquals(value, Binding.DoNothing) || ReferenceEquals(value, DependencyProperty.UnsetValue)) return;
            destination.SetValue(owner, Coerce(value, destination.PropertyType)); LastError = null;
        }
        catch (Exception error) { LastError = error.GetBaseException().Message; BindingFailed?.Invoke(this, LastError); }
        finally { busy = false; }
        UpdateTarget();
    }
    internal static object? Coerce(object? value, Type type)
    {
        if (value == null && Nullable.GetUnderlyingType(type) != null) return null;
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (value == null) return type.IsValueType ? Activator.CreateInstance(type) : null;
        if (type.IsInstanceOfType(value)) return value;
        if (type == typeof(string)) return value.ToString();
        if(type==typeof(char)) return value.ToString() is {Length:1} character ? character[0] : throw new ArgumentException("A character value is required.");
        if (type == typeof(FontWeight)) return FontWeights.Parse(value.ToString()!);
        if (type == typeof(Media.FontFamily)) return new Media.FontFamily(value.ToString()!);
        if (typeof(Media.Brush).IsAssignableFrom(type)) return new Media.SolidColorBrush(Media.Color.FromNative(new global::ProjectZ.Shared.Drawing.Designer.SceneXamlParser(null!).ReadImportedColor(value.ToString()!)));
        if (type == typeof(Media.Color)) return Media.Color.FromNative(new global::ProjectZ.Shared.Drawing.Designer.SceneXamlParser(null!).ReadImportedColor(value.ToString()!));
        if (type == typeof(Uri)) return new Uri(value.ToString()!, UriKind.RelativeOrAbsolute);
        if (type == typeof(System.Drawing.Color)) { var color = new global::ProjectZ.Shared.Drawing.Designer.SceneXamlParser(null!).ReadImportedColor(value.ToString()!); return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B); }
        if (type == typeof(GridLength))
        {
            var text = value.ToString()!;
            return text == "Auto" ? GridLength.Auto : text.EndsWith('*') ? new GridLength(text.Length == 1 ? 1 : double.Parse(text[..^1], CultureInfo.InvariantCulture), GridUnitType.Star) : new GridLength(double.Parse(text, CultureInfo.InvariantCulture));
        }
        if (type == typeof(Input.Cursor)) return typeof(Input.Cursors).GetProperty(value.ToString()!)?.GetValue(null) ?? Input.Cursors.Arrow;
        if (type == typeof(Point)) { var points = value.ToString()!.Split(','); return new Point(double.Parse(points[0], CultureInfo.InvariantCulture), double.Parse(points[1], CultureInfo.InvariantCulture)); }
        if (type == typeof(Rect)) { var points = value.ToString()!.Split(',').Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray(); return new Rect(points[0], points[1], points[2], points[3]); }
        if (type == typeof(Thickness)) { var parts = value.ToString()!.Split(',').Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray(); return parts.Length == 1 ? new Thickness(parts[0]) : parts.Length == 2 ? new Thickness(parts[0], parts[1], parts[0], parts[1]) : new Thickness(parts[0], parts[1], parts[2], parts[3]); }
        if (type == typeof(CornerRadius)) { var parts = value.ToString()!.Split(',').Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray(); return parts.Length == 1 ? new CornerRadius(parts[0]) : new CornerRadius(parts[0], parts[1], parts[2], parts[3]); }
        if (type == typeof(TextDecorationCollection)) { var decorations = new TextDecorationCollection(); foreach (var name in value.ToString()!.Split(',')) decorations.Add(new TextDecoration { Location = Enum.Parse<TextDecorationLocation>(name.Trim(), true) }); return decorations; }
        if (type == typeof(Media.ImageSource)) return new Media.Imaging.BitmapImage(new Uri(value.ToString()!, UriKind.RelativeOrAbsolute));
        if (type.IsEnum) return Enum.Parse(type, value.ToString()!);
        return System.Convert.ChangeType(value, type, CultureInfo.CurrentCulture);
    }
    public void Dispose() { if (disposed) return; disposed = true; Unwatch(); target.PropertyChanged -= TargetChanged; target.LostFocus -= LostFocus; target.UntrackBinding(this); }
}
public static class BindingOperations
{
    static readonly ConditionalWeakTable<FrameworkElement, Dictionary<string, BindingExpression>> bindings = new();
    public static BindingExpression SetBinding(DependencyObject target, DependencyProperty property, Binding binding) => SetBinding((FrameworkElement)target, property.Name, binding, property);
    public static BindingExpression SetBinding(FrameworkElement target, string property, Binding binding, DependencyProperty? dp = null)
    {
        var map = bindings.GetOrCreateValue(target);
        if (map.Remove(property, out var old)) old.Dispose();
        var expression = new BindingExpression(target, property, binding, dp); map[property] = expression;
        target.TrackBinding(expression); return expression;
    }
    public static BindingExpression? GetBindingExpression(DependencyObject target, DependencyProperty property) => bindings.TryGetValue((FrameworkElement)target, out var map) && map.TryGetValue(property.Name, out var expression) ? expression : null;
    public static void ClearBinding(DependencyObject target, DependencyProperty property) { if (bindings.TryGetValue((FrameworkElement)target, out var map) && map.Remove(property.Name, out var expression)) expression.Dispose(); }
}
