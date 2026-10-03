using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using global::ProjectZ.Shared.Drawing.UI;
using global::ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.WpfCompatibility;

public class UIElement : DependencyObject, IDisposable
{
    static readonly ConditionalWeakTable<SceneElement, UIElement> adapters = new();
    readonly List<Action> detach = new();
    protected readonly List<IDisposable> subscriptions = new();
    internal void TrackBinding(IDisposable binding) => subscriptions.Add(binding);
    internal void UntrackBinding(IDisposable binding) => subscriptions.Remove(binding);
    public SceneElement NativeElement { get; private set; }
    public bool IsDisposed { get; private set; }
    public event RoutedEventHandler? Loaded, Unloaded, GotFocus, LostFocus;
    public event SizeChangedEventHandler? SizeChanged;
    public event EventHandler<Input.MouseEventArgs>? MouseEnter, MouseLeave, MouseMove;
    public event EventHandler<Input.MouseButtonEventArgs>? PreviewMouseLeftButtonDown, MouseLeftButtonDown, PreviewMouseLeftButtonUp, MouseLeftButtonUp;
    public event EventHandler<Input.MouseWheelEventArgs>? PreviewMouseWheel, MouseWheel;
    public event EventHandler<Input.KeyEventArgs>? PreviewKeyDown, KeyDown, PreviewKeyUp, KeyUp;
    protected UIElement(SceneElement native) { NativeElement = native; AttachNative(); }
    public static UIElement FromNative(SceneElement native) => adapters.TryGetValue(native, out var value) ? value : Controls.ControlFactory.Wrap(native);
    internal void ReplaceNative(SceneElement native)
    {
        foreach (var action in detach) action(); detach.Clear(); adapters.Remove(NativeElement);
        NativeElement.Dispose(); NativeElement = native; AttachNative();
    }
    protected void Listen(string name, Action<object?[]> callback)
    {
        var info = NativeElement.GetType().GetEvent(name) ?? throw new NotSupportedException($"Native event {name}");
        var parameters = info.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
        var handler = Expression.Lambda(info.EventHandlerType, Expression.Invoke(Expression.Constant(callback), Expression.NewArrayInit(typeof(object), parameters.Select(p => Expression.Convert(p, typeof(object))))), parameters).Compile();
        var native = NativeElement;
        info.AddEventHandler(native, handler); detach.Add(() => info.RemoveEventHandler(native, handler));
    }
    void AttachNative()
    {
        adapters.Add(NativeElement, this);
        Listen("Loaded", _ => Loaded?.Invoke(this, Args()));
        Listen("Selected", _ => GotFocus?.Invoke(this, Args()));
        Listen("Deselected", _ => LostFocus?.Invoke(this, Args()));
        Listen("SizeChanged", a => { var x = (Vector2)a[0]!; var y = (Vector2)a[1]!; SizeChanged?.Invoke(this, new(new(x.X, x.Y), new(y.X, y.Y)) { Source = this, OriginalSource = this }); Changed("ActualWidth"); Changed("ActualHeight"); });
        Listen("MouseEnter", _ => MouseEnter?.Invoke(this, new() { Source = this, OriginalSource = this }));
        Listen("MouseLeave", _ => MouseLeave?.Invoke(this, new() { Source = this, OriginalSource = this }));
        Listen("MouseMove", a => MouseMove?.Invoke(this, new((Microsoft.Xna.Framework.Point)a[0]!) { Source = this, OriginalSource = this }));
        var native = NativeElement;
        native.ImportedInputFilter = (name, a) =>
        {
            Microsoft.Xna.Framework.Point GlobalPoint(object? point) { var p = (Microsoft.Xna.Framework.Point)point!; return new(p.X + (int)native.Position.X, p.Y + (int)native.Position.Y); }
            RoutedEventArgs args = name switch
            {
                "MouseLeftButtonDown" => new Input.MouseButtonEventArgs(GlobalPoint(a[0]), Input.MouseButtonState.Pressed),
                "MouseLeftButtonUp" => new Input.MouseButtonEventArgs(GlobalPoint(a[0]), Input.MouseButtonState.Released),
                "MouseWheel" => new Input.MouseWheelEventArgs((int)a[0]!, GlobalPoint(a[1])),
                _ => new Input.KeyEventArgs((Input.Key)(int)(Microsoft.Xna.Framework.Input.Keys)a[0]!)
            };
            Route(name, args); return args.Handled;
        };
        detach.Add(() => native.ImportedInputFilter = null);
    }
    protected RoutedEventArgs Args() => new() { Source = this, OriginalSource = this };
    internal void Route(string name, RoutedEventArgs args)
    {
        args.Source = args.OriginalSource = this;
        var route = new List<UIElement>();
        for (UIElement? node = this; node != null; node = (node as FrameworkElement)?.Parent) route.Add(node);
        foreach (var node in route.AsEnumerable().Reverse()) { if (args.Handled) break; node.Deliver("Preview" + name, args); }
        foreach (var node in route) { if (args.Handled) break; node.Deliver(name, args); }
    }
    void Deliver(string name, RoutedEventArgs args)
    {
        switch (name)
        {
            case "PreviewMouseLeftButtonDown": PreviewMouseLeftButtonDown?.Invoke(this, (Input.MouseButtonEventArgs)args); break;
            case "MouseLeftButtonDown": MouseLeftButtonDown?.Invoke(this, (Input.MouseButtonEventArgs)args); break;
            case "PreviewMouseLeftButtonUp": PreviewMouseLeftButtonUp?.Invoke(this, (Input.MouseButtonEventArgs)args); break;
            case "MouseLeftButtonUp": MouseLeftButtonUp?.Invoke(this, (Input.MouseButtonEventArgs)args); break;
            case "PreviewMouseWheel": PreviewMouseWheel?.Invoke(this, (Input.MouseWheelEventArgs)args); break;
            case "MouseWheel": MouseWheel?.Invoke(this, (Input.MouseWheelEventArgs)args); break;
            case "PreviewKeyDown": PreviewKeyDown?.Invoke(this, (Input.KeyEventArgs)args); break;
            case "KeyDown": KeyDown?.Invoke(this, (Input.KeyEventArgs)args); break;
            case "PreviewKeyUp": PreviewKeyUp?.Invoke(this, (Input.KeyEventArgs)args); break;
            case "KeyUp": KeyUp?.Invoke(this, (Input.KeyEventArgs)args); break;
        }
    }
    public bool IsEnabled { get => NativeElement.isEnabled; set { NativeElement.isEnabled = value; Changed(nameof(IsEnabled)); } }
    public bool IsHitTestVisible { get => !NativeElement.isMouseBypassEnabled; set => NativeElement.isMouseBypassEnabled = !value; }
    Visibility visibility;
    public Visibility Visibility { get => visibility; set { visibility = value; NativeElement.isVisible = value == Visibility.Visible; Changed(nameof(Visibility)); SyncMarkup(nameof(Visibility), value); } }
    public double Opacity { get => NativeElement.Opacity; set { NativeElement.Opacity = (float)value; Changed(nameof(Opacity)); } }
    public bool Focus() { Application.Current.Scene.FocusElement(NativeElement); return true; }
    protected virtual void SyncMarkup(string property, object? value) { }
    public virtual void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true; Unloaded?.Invoke(this, Args());
        foreach (var action in detach) action(); detach.Clear();
        foreach (var subscription in subscriptions.ToArray()) subscription.Dispose(); subscriptions.Clear();
        adapters.Remove(NativeElement);
    }
}

public class FrameworkElement : UIElement
{
    public static readonly DependencyProperty DataContextProperty = DependencyProperty.Register(nameof(DataContext), typeof(object), typeof(FrameworkElement));
    public string Name { get; set; } = "";
    public FrameworkElement? Parent { get; internal set; }
    public ResourceDictionary Resources { get; } = new();
    internal readonly List<FrameworkElement> LogicalChildren = new();
    internal Action<string, object?>? MarkupSetter;
    internal Dictionary<string, FrameworkElement>? NameScope;
    bool hasContext;
    public object? DataContext { get => hasContext ? GetValue(DataContextProperty) : Parent?.DataContext; set => SetValue(DataContextProperty, value); }
    public override void SetValue(DependencyProperty property, object? value) { if (property == DataContextProperty) hasContext = true; base.SetValue(property, value); if (property == DataContextProperty) ContextChanged(); }
    public override void ClearValue(DependencyProperty property) { base.ClearValue(property); if (property == DataContextProperty) { hasContext = false; ContextChanged(); } }
    void ContextChanged() { Changed(nameof(DataContext)); foreach (var child in LogicalChildren) if (!child.hasContext) child.ContextChanged(); }
    internal void Reparent(FrameworkElement? parent) { Parent = parent; ContextChanged(); }
    public FrameworkElement() : this(new RectangleElement(Application.Current.Scene)) { }
    internal FrameworkElement(SceneElement native) : base(native) { }
    protected override void SyncMarkup(string property, object? value) => MarkupSetter?.Invoke(property, value);
    public double Width { get => NativeElement.Size.X; set { NativeElement.Size = new((float)value, NativeElement.Size.Y); SyncMarkup(nameof(Width), value); Changed(nameof(Width)); } }
    public double Height { get => NativeElement.Size.Y; set { NativeElement.Size = new(NativeElement.Size.X, (float)value); SyncMarkup(nameof(Height), value); Changed(nameof(Height)); } }
    public double ActualWidth => NativeElement.Size.X;
    public double ActualHeight => NativeElement.Size.Y;
    public double MinWidth { get => NativeElement.MinSize.X; set => NativeElement.MinSize = new((float)value, NativeElement.MinSize.Y); }
    public double MinHeight { get => NativeElement.MinSize.Y; set => NativeElement.MinSize = new(NativeElement.MinSize.X, (float)value); }
    public double MaxWidth { get => NativeElement.MaxSize.X; set => NativeElement.MaxSize = new((float)value, NativeElement.MaxSize.Y); }
    public double MaxHeight { get => NativeElement.MaxSize.Y; set => NativeElement.MaxSize = new(NativeElement.MaxSize.X, (float)value); }
    public Thickness Margin { get { var x = NativeElement.Margin; return new(x.Left, x.Top, x.Right, x.Bottom); } set { NativeElement.Margin = new((int)value.Left, (int)value.Top, (int)value.Right, (int)value.Bottom); SyncMarkup(nameof(Margin), value); Changed(nameof(Margin)); } }
    public HorizontalAlignment HorizontalAlignment { get => Enum.Parse<HorizontalAlignment>(NativeElement.HorizontalAlign.ToString()); set { NativeElement.HorizontalAlign = Enum.Parse<global::ProjectZ.Shared.Drawing.UI.HorizontalAlignment>(value.ToString()); SyncMarkup(nameof(HorizontalAlignment), value); } }
    public VerticalAlignment VerticalAlignment { get => Enum.Parse<VerticalAlignment>(NativeElement.VerticalAlign.ToString()); set { NativeElement.VerticalAlign = Enum.Parse<global::ProjectZ.Shared.Drawing.UI.VerticalAlignment>(value.ToString()); SyncMarkup(nameof(VerticalAlignment), value); } }
    public object? FindName(string name)
    {
        for (var node = this; node != null; node = node.Parent) if (node.NameScope?.TryGetValue(name, out var found) == true) return found;
        return null;
    }
    public object FindResource(object key) => TryFindResource(key) ?? throw new KeyNotFoundException($"Resource {key}");
    public object? TryFindResource(object key) => Resources.TryGetValue(key, out var value) ? value : Parent?.TryFindResource(key) ?? (Application.Current.Resources.TryGetValue(key, out value) ? value : null);
    public Data.BindingExpression SetBinding(DependencyProperty property, Data.Binding binding) => Data.BindingOperations.SetBinding(this, property, binding);
    public Data.BindingExpression? GetBindingExpression(DependencyProperty property) => Data.BindingOperations.GetBindingExpression(this, property);
    public override void Dispose() { foreach (var child in LogicalChildren.ToArray()) child.Dispose(); base.Dispose(); }
}

public class Window : Controls.ContentControl
{
    public string Title { get; set; } = "";
    public event CancelEventHandler? Closing;
    public event EventHandler? Closed;
    bool shown;
    public void Show()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(Window));
        if (shown) { Visibility = Visibility.Visible; return; }
        shown = true; Application.Current.Windows.Add(this); Application.Current.MainWindow ??= this;
        Application.Current.Scene.AddElement(NativeElement);
    }
    public void Hide() => Visibility = Visibility.Hidden;
    public void Close()
    {
        if (IsDisposed) return;
        var args = new CancelEventArgs(); Closing?.Invoke(this, args); if (args.Cancel) return;
        Application.Current.Scene.RemoveElement(NativeElement); Application.Current.Windows.Remove(this);
        if (Application.Current.MainWindow == this) Application.Current.MainWindow = Application.Current.Windows.FirstOrDefault();
        Dispose(); NativeElement.Dispose(); Closed?.Invoke(this, EventArgs.Empty);
    }
}
