using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using global::ProjectZ.Shared.Drawing.UI;
using global::ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.WpfCompatibility;

public partial class UIElement : DependencyObject, IInputElement, IDisposable
{
    static readonly ConditionalWeakTable<SceneElement, UIElement> adapters = new();
    static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<Action<object?[]>, Delegate>> nativeEventFactories = new();
    readonly List<Action> detach = new();
    protected readonly List<IDisposable> subscriptions = new();
    internal void TrackBinding(IDisposable binding) => subscriptions.Add(binding);
    internal void UntrackBinding(IDisposable binding) => subscriptions.Remove(binding);
    public SceneElement NativeElement { get; private set; }
    public bool IsDisposed { get; private set; }
    public virtual void Copy() => NativeElement.Copy();
    public virtual string GetCopyText() => NativeElement.GetCopyText();
    public static readonly DependencyProperty OpacityProperty = DependencyProperty.RegisterNative(nameof(Opacity), typeof(double), typeof(UIElement), new(1d));
    public static readonly DependencyProperty VisibilityProperty = DependencyProperty.RegisterNative(nameof(Visibility), typeof(Visibility), typeof(UIElement), new(Visibility.Visible));
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterNative(nameof(IsEnabled), typeof(bool), typeof(UIElement), new(true));
    public static readonly DependencyProperty IsHitTestVisibleProperty = DependencyProperty.RegisterNative(nameof(IsHitTestVisible), typeof(bool), typeof(UIElement), new(true));
    public static readonly DependencyProperty ClipToBoundsProperty = DependencyProperty.RegisterNative(nameof(ClipToBounds), typeof(bool), typeof(UIElement), new(false));
    public void RaiseEvent(RoutedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        args.Source ??= this; args.OriginalSource ??= this;
        if (args.RoutedEvent == null) throw new ArgumentException("A routed event identity is required.", nameof(args));
        var native = new SceneRoutedEventArgs(args.RoutedEvent.Native, NativeElement, args) { Handled = args.Handled };
        SceneEvents.RaiseEvent(NativeElement, native); args.Handled = native.Handled;
        AfterRoutedEvent(args);
    }
    protected virtual void AfterRoutedEvent(RoutedEventArgs args)
    {
        if (!args.Handled && args.RoutedEvent?.Native == TextInputEvent.Native && args is Input.TextCompositionEventArgs text && NativeElement is global::ProjectZ.Shared.Drawing.UI.Input.Textbox { IsReadOnly: false } input)
        {
            input.ReplaceSelection(text.Text); args.Handled = true;
        }
    }
    bool lastVisible;
    public static readonly DependencyProperty IsVisibleProperty=DependencyProperty.RegisterNative(nameof(IsVisible),typeof(bool),typeof(UIElement));
    public bool IsVisible => NativeElement.isVisible && (this is not FrameworkElement element || element.Parent?.IsVisible != false);
    public event DependencyPropertyChangedEventHandler? IsVisibleChanged;
    internal void NotifyVisibility()
    {
        var visible=IsVisible;
        if(lastVisible!=visible) {var old=lastVisible;lastVisible=visible;Changed(nameof(IsVisible));IsVisibleChanged?.Invoke(this,new(IsVisibleProperty,old,visible));}
        if(this is FrameworkElement element)
            foreach(var child in element.LogicalChildren.ToArray())
                if (!child.IsDisposed && child.Parent == element) child.NotifyVisibility();
    }
    // Native bounds use the renderer's integer pixel grid for every control.
    public bool SnapsToDevicePixels { get; set; } = true;
    public bool IsTabStop { get => Focusable; set => Focusable = value; }
    public bool IsMouseOver => NativeElement.isMouseOver;
    public bool IsPressed => NativeElement.isMouseDown;
    Media.Transform? renderTransform;
    Media.Geometry? clipGeometry;
    public static readonly DependencyProperty RenderTransformProperty = DependencyProperty.RegisterNative(nameof(RenderTransform), typeof(Media.Transform), typeof(UIElement));
    public Media.Transform? RenderTransform
    {
        get => renderTransform;
        set { if (renderTransform != null) renderTransform.PropertyChanged -= TransformChanged; renderTransform = value; if (value != null) value.PropertyChanged += TransformChanged; TransformChanged(null, null!); }
    }
    void TransformChanged(object? sender, PropertyChangedEventArgs e)
    {
        NativeElement.RenderRotation = 0; NativeElement.RenderRotationCenter = Vector2.Zero;
        NativeElement.RenderScaleOrigin = NativeElement.RenderRotationOrigin; NativeElement.RenderScaleCenter = Vector2.Zero;
        NativeElement.RenderScale = Vector2.One; NativeElement.RenderTranslation = Vector2.Zero;
        Apply(renderTransform);
        void Apply(Media.Transform? transform)
        {
            if (transform != null) transform.AnimationScene = NativeElement.OwnerScene;
            switch (transform)
            {
                case Media.RotateTransform rotate: NativeElement.RenderRotation += (float)rotate.Angle; NativeElement.RenderRotationCenter = new((float)rotate.CenterX, (float)rotate.CenterY); break;
                case Media.ScaleTransform scale: NativeElement.RenderScale *= new Vector2((float)scale.ScaleX, (float)scale.ScaleY); NativeElement.RenderScaleCenter = new((float)scale.CenterX, (float)scale.CenterY); break;
                case Media.TranslateTransform translate: NativeElement.RenderTranslation += new Vector2((float)translate.X, (float)translate.Y); break;
                case Media.TransformGroup group: foreach (var child in group.Children) Apply(child); break;
            }
        }
        Changed(nameof(RenderTransform));
    }
    public Point RenderTransformOrigin { get => new(NativeElement.RenderRotationOrigin.X, NativeElement.RenderRotationOrigin.Y); set { NativeElement.RenderRotationOrigin = new((float)value.X, (float)value.Y); NativeElement.RenderScaleOrigin = new((float)value.X, (float)value.Y); } }
    public Media.Geometry? Clip
    {
        get => clipGeometry;
        set { if (clipGeometry != null) clipGeometry.PropertyChanged -= ClipChanged; clipGeometry = value; if (value != null) value.PropertyChanged += ClipChanged; ClipChanged(null, null!); }
    }
    bool clipToBounds;
    public bool ClipToBounds { get => clipToBounds; set { clipToBounds = value; SyncMarkup(nameof(ClipToBounds), value); ClipChanged(null, null!); Changed(nameof(ClipToBounds)); } }
    void ClipChanged(object? sender, PropertyChangedEventArgs e) { NativeElement.Clip = clipToBounds || clipGeometry != null; NativeElement.ClipBounds = clipGeometry is Media.RectangleGeometry rect ? new Microsoft.Xna.Framework.Rectangle((int)rect.Rect.X, (int)rect.Rect.Y, (int)rect.Rect.Width, (int)rect.Rect.Height) : null; }
    public Media.Effects.Effect? Effect { get => NativeElement.VisualEffect == null ? null : Media.Effects.Effect.Wrap(NativeElement.VisualEffect); set { Dispatcher.VerifyAccess(); NativeElement.VisualEffect = value?.Native; Changed(nameof(Effect)); } }
    public event RoutedEventHandler? Loaded
    {
        add { if (value != null) AddHandler(RoutedEvent.For("Loaded"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("Loaded"), value); }
    }
    public event RoutedEventHandler? Unloaded
    {
        add { if (value != null) AddHandler(RoutedEvent.For("Unloaded"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("Unloaded"), value); }
    }
    public event RoutedEventHandler? GotFocus
    {
        add { if (value != null) AddHandler(RoutedEvent.For("GotFocus"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("GotFocus"), value); }
    }
    public event RoutedEventHandler? LostFocus
    {
        add { if (value != null) AddHandler(RoutedEvent.For("LostFocus"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("LostFocus"), value); }
    }
    public event SizeChangedEventHandler? SizeChanged
    {
        add { if (value != null) AddHandler(RoutedEvent.For("SizeChanged"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("SizeChanged"), value); }
    }
    public event Input.MouseEventHandler? MouseEnter
    {
        add { if (value != null) AddHandler(RoutedEvent.For("MouseEnter"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("MouseEnter"), value); }
    }
    public event Input.MouseEventHandler? MouseLeave
    {
        add { if (value != null) AddHandler(RoutedEvent.For("MouseLeave"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("MouseLeave"), value); }
    }
    public event Input.MouseEventHandler? MouseMove
    {
        add { if (value != null) AddHandler(RoutedEvent.For("MouseMove"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("MouseMove"), value); }
    }
    public event Input.MouseEventHandler? PreviewMouseMove
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PreviewMouseMove"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PreviewMouseMove"), value); }
    }
    public event Input.MouseButtonEventHandler? PreviewMouseLeftButtonDown
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PreviewMouseLeftButtonDown"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PreviewMouseLeftButtonDown"), value); }
    }
    public event Input.MouseButtonEventHandler? MouseLeftButtonDown
    {
        add { if (value != null) AddHandler(RoutedEvent.For("MouseLeftButtonDown"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("MouseLeftButtonDown"), value); }
    }
    public event Input.MouseButtonEventHandler? PreviewMouseLeftButtonUp
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PreviewMouseLeftButtonUp"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PreviewMouseLeftButtonUp"), value); }
    }
    public event Input.MouseButtonEventHandler? MouseLeftButtonUp
    {
        add { if (value != null) AddHandler(RoutedEvent.For("MouseLeftButtonUp"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("MouseLeftButtonUp"), value); }
    }
    public event Input.MouseButtonEventHandler? PreviewMouseRightButtonDown
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PreviewMouseRightButtonDown"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PreviewMouseRightButtonDown"), value); }
    }
    public event Input.MouseButtonEventHandler? MouseRightButtonDown
    {
        add { if (value != null) AddHandler(RoutedEvent.For("MouseRightButtonDown"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("MouseRightButtonDown"), value); }
    }
    public event Input.MouseButtonEventHandler? PreviewMouseRightButtonUp
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PreviewMouseRightButtonUp"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PreviewMouseRightButtonUp"), value); }
    }
    public event Input.MouseButtonEventHandler? MouseRightButtonUp
    {
        add { if (value != null) AddHandler(RoutedEvent.For("MouseRightButtonUp"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("MouseRightButtonUp"), value); }
    }
    public event Input.MouseButtonEventHandler? PreviewMouseDown
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PreviewMouseDown"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PreviewMouseDown"), value); }
    }
    public event Input.MouseButtonEventHandler? MouseDown
    {
        add { if (value != null) AddHandler(RoutedEvent.For("MouseDown"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("MouseDown"), value); }
    }
    public event Input.MouseButtonEventHandler? PreviewMouseUp
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PreviewMouseUp"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PreviewMouseUp"), value); }
    }
    public event Input.MouseButtonEventHandler? MouseUp
    {
        add { if (value != null) AddHandler(RoutedEvent.For("MouseUp"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("MouseUp"), value); }
    }
    public event Input.MouseWheelEventHandler? PreviewMouseWheel
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PreviewMouseWheel"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PreviewMouseWheel"), value); }
    }
    public event Input.MouseWheelEventHandler? MouseWheel
    {
        add { if (value != null) AddHandler(RoutedEvent.For("MouseWheel"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("MouseWheel"), value); }
    }
    public event Input.KeyEventHandler? PreviewKeyDown
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PreviewKeyDown"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PreviewKeyDown"), value); }
    }
    public event Input.KeyEventHandler? KeyDown
    {
        add { if (value != null) AddHandler(RoutedEvent.For("KeyDown"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("KeyDown"), value); }
    }
    public event Input.KeyEventHandler? PreviewKeyUp
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PreviewKeyUp"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PreviewKeyUp"), value); }
    }
    public event Input.KeyEventHandler? KeyUp
    {
        add { if (value != null) AddHandler(RoutedEvent.For("KeyUp"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("KeyUp"), value); }
    }
    public event Input.TextCompositionEventHandler? PreviewTextInput
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PreviewTextInput"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PreviewTextInput"), value); }
    }
    public event Input.TextCompositionEventHandler? TextInput
    {
        add { if (value != null) AddHandler(RoutedEvent.For("TextInput"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("TextInput"), value); }
    }
    public event Input.MouseEventHandler? GotMouseCapture
    {
        add { if (value != null) AddHandler(RoutedEvent.For("GotMouseCapture"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("GotMouseCapture"), value); }
    }
    public event Input.MouseEventHandler? LostMouseCapture
    {
        add { if (value != null) AddHandler(RoutedEvent.For("LostMouseCapture"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("LostMouseCapture"), value); }
    }
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
        var factory = nativeEventFactories.GetOrAdd(info.EventHandlerType!, handlerType =>
        {
            var callbackParameter = Expression.Parameter(typeof(Action<object?[]>), "callback");
            var parameters = handlerType.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
            var handlerExpression = Expression.Lambda(handlerType, Expression.Invoke(callbackParameter, Expression.NewArrayInit(typeof(object), parameters.Select(p => Expression.Convert(p, typeof(object))))), parameters);
            return Expression.Lambda<Func<Action<object?[]>, Delegate>>(Expression.Convert(handlerExpression, typeof(Delegate)), callbackParameter).Compile();
        });
        var handler = factory(callback);
        var native = NativeElement;
        native.ImportedInputDetached = false;
        info.AddEventHandler(native, handler); detach.Add(() => info.RemoveEventHandler(native, handler));
    }
    void AttachNative()
    {
        adapters.Add(NativeElement, this);
        lastVisible=IsVisible;Listen("VisibilityChanged",_=>NotifyVisibility());
        AttachRoutedHandlers();
        Listen("Loaded", _ => { IsLoaded = true; Emit("Loaded", Args()); });
        Listen("Selected", _ => { NotifyFocusState(); Emit("GotFocus", Args()); });
        Listen("Deselected", _ => { NotifyFocusState(); Emit("LostFocus", Args()); });
        Listen("SizeChanged", a => { var x = (Vector2)a[0]!; var y = (Vector2)a[1]!; Emit("SizeChanged", new SizeChangedEventArgs(new(x.X, x.Y), new(y.X, y.Y))); Changed("ActualWidth"); Changed("ActualHeight"); });
        Listen("MouseEnter", _ => { Changed(nameof(IsMouseOver)); Emit("MouseEnter", new Input.MouseEventArgs(NativeElement.OwnerScene?.MousePosition ?? default)); });
        Listen("MouseLeave", _ => { Changed(nameof(IsMouseOver)); Emit("MouseLeave", new Input.MouseEventArgs(NativeElement.OwnerScene?.MousePosition ?? default)); });
        Listen("MouseLeftDown", _ => Changed(nameof(IsPressed)));
        Listen("MouseLeftUp", _ => Changed(nameof(IsPressed)));
        Listen("MouseRightClick", _ => { if (this is FrameworkElement { ContextMenu: { } menu } element) { menu.PlacementTarget = element; menu.IsOpen = true; } });
        var native = NativeElement;
        long previousClick = 0;
        Microsoft.Xna.Framework.Point previousClickPosition = default;
        native.ImportedInputFilter = (name, a) =>
        {
            Microsoft.Xna.Framework.Point GlobalPoint(object? point) { var p = (Microsoft.Xna.Framework.Point)point!; return new(p.X + (int)native.Position.X, p.Y + (int)native.Position.Y); }
            RoutedEventArgs args = name switch
            {
                "MouseLeftButtonDown" => new Input.MouseButtonEventArgs(GlobalPoint(a[0]), Input.MouseButtonState.Pressed),
                "MouseLeftButtonUp" => new Input.MouseButtonEventArgs(GlobalPoint(a[0]), Input.MouseButtonState.Released),
                "MouseRightButtonDown" => new Input.MouseButtonEventArgs(GlobalPoint(a[0]), Input.MouseButtonState.Pressed, Input.MouseButton.Right),
                "MouseRightButtonUp" => new Input.MouseButtonEventArgs(GlobalPoint(a[0]), Input.MouseButtonState.Released, Input.MouseButton.Right),
                "MouseMove" => new Input.MouseEventArgs(GlobalPoint(a[0])),
                "MouseWheel" => new Input.MouseWheelEventArgs((int)a[0]!, GlobalPoint(a[1])),
                "TextInput" => new Input.TextCompositionEventArgs(Input.Keyboard.PrimaryDevice, new Input.TextComposition(Input.InputManager.Current, this, (string)a[0]!)),
                _ => new Input.KeyEventArgs((Input.Key)(int)(Microsoft.Xna.Framework.Input.Keys)a[0]!)
            };
            Route(name, args);
            if (name == "MouseLeftButtonDown" && args is Input.MouseButtonEventArgs click)
            {
                var now = Environment.TickCount64; var point = GlobalPoint(a[0]);
                if (previousClick != 0 && now - previousClick <= System.Windows.Forms.SystemInformation.DoubleClickTime && Math.Abs(point.X - previousClickPosition.X) <= 4 && Math.Abs(point.Y - previousClickPosition.Y) <= 4)
                {
                    var twice = new Input.MouseButtonEventArgs(point, Input.MouseButtonState.Pressed) { ClickCount = 2 };
                    Route("MouseDoubleClick", twice); args.Handled |= twice.Handled; previousClick = 0;
                }
                else { previousClick = now; previousClickPosition = point; }
            }
            return args.Handled;
        };
        detach.Add(() => { native.ImportedInputFilter = null; native.ImportedInputDetached = true; });
    }
    protected RoutedEventArgs Args() => new() { Source = this, OriginalSource = this };
    void NotifyFocusState() { Changed(nameof(IsKeyboardFocused)); for(var node=this as FrameworkElement;node!=null;node=node.Parent) node.Changed(nameof(IsKeyboardFocusWithin)); }
    internal void Route(string name, RoutedEventArgs args)
    {
        args.Source ??= this; args.OriginalSource ??= this;
        string? generic = name is "MouseLeftButtonDown" or "MouseRightButtonDown" ? "MouseDown" : name is "MouseLeftButtonUp" or "MouseRightButtonUp" ? "MouseUp" : null;
        if (generic != null) Emit("Preview" + generic, args);
        Emit("Preview" + name, args);
        if (generic != null) Emit(generic, args);
        Emit(name, args);
    }
    protected internal void Emit(string name, RoutedEventArgs args)
    {
        args.RoutedEvent = RoutedEvent.For(name); RaiseEvent(args);
    }
    public bool IsEnabled { get => NativeElement.isEnabled; set { NativeElement.isEnabled = value; Changed(nameof(IsEnabled)); } }
    public bool IsHitTestVisible { get => !NativeElement.isMouseBypassEnabled; set { NativeElement.isMouseBypassEnabled = !value;Changed(nameof(IsHitTestVisible)); } }
    Visibility visibility;
    public Visibility Visibility { get => visibility; set { visibility = value; NativeElement.isVisible = value == Visibility.Visible; Changed(nameof(Visibility)); SyncMarkup(nameof(Visibility), value); } }
    public double Opacity { get => NativeElement.Opacity; set { NativeElement.Opacity = (float)value; Changed(nameof(Opacity)); } }
    public bool Focus() { if (!Focusable || !IsEnabled || !IsVisible) return false; (NativeElement.OwnerScene ?? Application.Current.ControlScene).FocusElement(NativeElement); return NativeElement.isSelected; }
    protected virtual void SyncMarkup(string property, object? value) { }
    public virtual void Dispose()
    {
        if (IsDisposed) return;
        if (IsLoaded) { IsLoaded = false; Emit("Unloaded", Args()); }
        ReleaseMouseCapture(); ClearAnimations(); IsDisposed = true;
        if (renderTransform != null) renderTransform.PropertyChanged -= TransformChanged;
        if (clipGeometry != null) clipGeometry.PropertyChanged -= ClipChanged;
        foreach (var action in detach) action(); detach.Clear();
        foreach (var subscription in subscriptions.ToArray()) subscription.Dispose(); subscriptions.Clear();
        ClearRoutedHandlers();
        adapters.Remove(NativeElement);
    }
}

public class FrameworkElement : UIElement
{
    internal object? XamlEventOwner;
    Style? style;
    public Style? Style { get=>style; set { style=value;value?.Apply(this);Changed(nameof(Style)); } }
    public static readonly DependencyProperty DataContextProperty = DependencyProperty.Register(nameof(DataContext), typeof(object), typeof(FrameworkElement));
    public static readonly DependencyProperty TagProperty = DependencyProperty.Register(nameof(Tag), typeof(object), typeof(FrameworkElement));
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }
    public string Name { get; set; } = "";
    public FrameworkElement? Parent { get; internal set; }
    public ResourceDictionary Resources { get; } = new();
    internal readonly List<FrameworkElement> LogicalChildren = new();
    internal Action<string, object?>? MarkupSetter;
    internal Dictionary<string, FrameworkElement>? NameScope;
    internal Dictionary<string, object>? ObjectNameScope;
    internal string? XamlSourceDirectory;
    internal FrameworkElement? ResourceOwner;
    internal readonly Dictionary<string, System.Xml.Linq.XElement> ResourceMarkup = new(StringComparer.Ordinal);
    object? toolTip;
    Controls.ToolTip? visibleToolTip;
    Threading.DispatcherTimer? toolTipTimer;
    public object? ToolTip { get => toolTip; set => toolTip = value; }
    public Controls.ContextMenu? ContextMenu { get; set; }
    public Input.Cursor? Cursor { get; set; }
    bool hasContext;
    internal bool WidthSpecified, HeightSpecified, HorizontalAlignmentSpecified, VerticalAlignmentSpecified;
    public object? DataContext { get => hasContext ? GetValue(DataContextProperty) : Parent?.DataContext; set => SetValue(DataContextProperty, value); }
    public override void SetValue(DependencyProperty property, object? value) { if (property == DataContextProperty) hasContext = true; base.SetValue(property, value); if (property == DataContextProperty) ContextChanged(); }
    public override void ClearValue(DependencyProperty property) { base.ClearValue(property); if (property == DataContextProperty) { hasContext = false; ContextChanged(); } }
    void ContextChanged()
    {
        Changed(nameof(DataContext));
        foreach (var child in LogicalChildren.ToArray())
            if (!child.hasContext && !child.IsDisposed && child.Parent == this) child.ContextChanged();
    }
    internal void Reparent(FrameworkElement? parent) { Parent = parent; ContextChanged(); NotifyVisibility(); }
    public FrameworkElement() : this(new RectangleElement(Application.Current.ControlScene)) { }
    // Optional WPF drawing overlays have an empty native projection.
    protected virtual void OnRender(Media.DrawingContext drawingContext) { }
    protected internal FrameworkElement(SceneElement native) : base(native)
    {
        MouseEnter += (_, _) => { if (Cursor != null) System.Windows.Forms.Cursor.Current = Cursor.Native; if (ToolTip != null) { toolTipTimer ??= new() { Interval = TimeSpan.FromMilliseconds(500) }; toolTipTimer.Tick -= ShowToolTip; toolTipTimer.Tick += ShowToolTip; toolTipTimer.Start(); } };
        MouseLeave += (_, _) => { toolTipTimer?.Stop(); if (visibleToolTip != null) visibleToolTip.IsOpen = false; System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default; };
    }
    void ShowToolTip(object? sender, EventArgs args) { toolTipTimer?.Stop(); if (ToolTip == null || IsDisposed) return; visibleToolTip = ToolTip as Controls.ToolTip ?? new Controls.ToolTip { Content = ToolTip }; visibleToolTip.Show(new(NativeElement.Position.X, NativeElement.Position.Y + NativeElement.Size.Y + 4)); }
    protected override void SyncMarkup(string property, object? value) => MarkupSetter?.Invoke(property, value);
    public double Width { get => NativeElement.Size.X; set { WidthSpecified = !double.IsNaN(value); if (WidthSpecified) NativeElement.Size = new((float)value, NativeElement.Size.Y); SyncMarkup(nameof(Width), value); Changed(nameof(Width)); } }
    public double Height { get => NativeElement.Size.Y; set { HeightSpecified = !double.IsNaN(value); if (HeightSpecified) NativeElement.Size = new(NativeElement.Size.X, (float)value); SyncMarkup(nameof(Height), value); Changed(nameof(Height)); } }
    public double ActualWidth => NativeElement.Size.X;
    public double ActualHeight => NativeElement.Size.Y;
    public double MinWidth { get => NativeElement.MinSize.X; set => NativeElement.MinSize = new((float)value, NativeElement.MinSize.Y); }
    public double MinHeight { get => NativeElement.MinSize.Y; set => NativeElement.MinSize = new(NativeElement.MinSize.X, (float)value); }
    public double MaxWidth { get => NativeElement.MaxSize.X; set => NativeElement.MaxSize = new((float)value, NativeElement.MaxSize.Y); }
    public double MaxHeight { get => NativeElement.MaxSize.Y; set => NativeElement.MaxSize = new(NativeElement.MaxSize.X, (float)value); }
    public Thickness Margin { get { var x = NativeElement.Margin; return new(x.Left, x.Top, x.Right, x.Bottom); } set { NativeElement.Margin = new((int)value.Left, (int)value.Top, (int)value.Right, (int)value.Bottom); SyncMarkup(nameof(Margin), value); Changed(nameof(Margin)); } }
    public HorizontalAlignment HorizontalAlignment { get => Enum.Parse<HorizontalAlignment>(NativeElement.HorizontalAlign.ToString()); set { HorizontalAlignmentSpecified = true; NativeElement.HorizontalAlign = Enum.Parse<global::ProjectZ.Shared.Drawing.UI.HorizontalAlignment>(value.ToString()); SyncMarkup(nameof(HorizontalAlignment), value); } }
    public VerticalAlignment VerticalAlignment { get => Enum.Parse<VerticalAlignment>(NativeElement.VerticalAlign.ToString()); set { VerticalAlignmentSpecified = true; NativeElement.VerticalAlign = Enum.Parse<global::ProjectZ.Shared.Drawing.UI.VerticalAlignment>(value.ToString()); SyncMarkup(nameof(VerticalAlignment), value); } }
    public object? FindName(string name)
    {
        for (var node = this; node != null; node = node.Parent)
        {
            if (node.NameScope?.TryGetValue(name, out var found) == true) return found;
            if (node.ObjectNameScope?.TryGetValue(name, out var value) == true) return value;
        }
        return null;
    }
    public object FindResource(object key) => TryFindResource(key) ?? throw new KeyNotFoundException($"Resource {key}");
    public object? TryFindResource(object key) => Resources.TryGetValue(key, out var value) ? value : ResourceOwner?.TryFindResource(key) ?? Parent?.TryFindResource(key) ?? (Application.Current.Resources.TryGetValue(key, out value) ? value : null);
    public Data.BindingExpression SetBinding(DependencyProperty property, Data.Binding binding) => Data.BindingOperations.SetBinding(this, property, binding);
    public Data.BindingExpression? GetBindingExpression(DependencyProperty property) => Data.BindingOperations.GetBindingExpression(this, property);
    public override void Dispose() { toolTipTimer?.Stop(); visibleToolTip?.Dispose(); foreach (var child in LogicalChildren.ToArray()) child.Dispose(); base.Dispose(); }
}

public partial class Window : Controls.ContentControl
{
    public event CancelEventHandler? Closing;
    public event EventHandler? Closed;
    bool shown;
    public void Show()
    {
        Dispatcher.VerifyAccess();
        if (IsDisposed) throw new ObjectDisposedException(nameof(Window));
        if (Application.Current.ShutdownRequested) throw new InvalidOperationException("The application is shutting down.");
        if (shown) { Visibility = Visibility.Visible; surface?.SetVisible(true); return; }
        shown = true; Application.Current.Windows.Add(this); Application.Current.MainWindow ??= this;
        NativeElement.OwnerScene.AddElement(NativeElement);
        ShowNativeWindow();
    }
    public void Hide() { Dispatcher.VerifyAccess(); Visibility = Visibility.Hidden; surface?.SetVisible(false); }
    public void Close() => CloseCore(false);
    internal void CloseForShutdown() => CloseCore(true);
    void CloseCore(bool shutdown)
    {
        Dispatcher.VerifyAccess();
        if (IsDisposed) return;
        var args = new CancelEventArgs(); Closing?.Invoke(this, args); if (args.Cancel && !shutdown) return;
        bool wasMain = Application.Current.MainWindow == this;
        NativeElement.OwnerScene.RemoveElement(NativeElement); Application.Current.Windows.Remove(this);
        if (Application.Current.MainWindow == this) Application.Current.MainWindow = Application.Current.Windows.FirstOrDefault();
        Dispose(); NativeElement.Dispose(); Closed?.Invoke(this, EventArgs.Empty);
        Application.Current.WindowClosed(wasMain);
    }
}
