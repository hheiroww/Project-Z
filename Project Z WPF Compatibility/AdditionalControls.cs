using ProjectZ.Shared.Drawing.UI;
using Native = ProjectZ.Shared.Drawing.UI.Input;
using Layout = ProjectZ.Shared.Drawing.UI.Layout;

namespace ProjectZ.WpfCompatibility.Controls;

public class Separator : Control
{
    public Separator() : this(new Native.Separator(Application.Current.ControlScene)) { }
    internal Separator(SceneElement native) : base(native) { }
    public Orientation Orientation { get => (Orientation)((Native.Separator)NativeElement).Orientation; set => ((Native.Separator)NativeElement).Orientation = (Layout.Orientation)value; }
}

public class Label : ContentControl
{
    public Label() : this(new Native.Label(Application.Current.ControlScene)) { }
    internal Label(SceneElement native) : base(native) { }
    public override object? Content { get => ((Native.Label)NativeElement).Content; set { ((Native.Label)NativeElement).Content = value?.ToString() ?? ""; Changed(nameof(Content)); } }
}

public class PasswordBox : Control
{
    public event RoutedEventHandler? PasswordChanged
    {
        add { if (value != null) AddHandler(RoutedEvent.For("PasswordChanged"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("PasswordChanged"), value); }
    }
    public PasswordBox() : this(new Native.PasswordBox(Application.Current.ControlScene)) { }
    internal PasswordBox(SceneElement native) : base(native) { Listen("OnTextChanged", _ => Emit("PasswordChanged", Args())); }
    public string Password { get => ((Native.PasswordBox)NativeElement).Password; set => ((Native.PasswordBox)NativeElement).Password = value; }
    public void Clear() => Password = "";
    public char PasswordChar { get=>((Native.PasswordBox)NativeElement).MaskCharacter??'\u2022'; set=>((Native.PasswordBox)NativeElement).MaskCharacter=value; }
    public void SelectAll() => ((Native.PasswordBox)NativeElement).Select(0,Password.Length);
}

public class RadioButton : ButtonBase
{
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.RegisterNative(nameof(IsChecked), typeof(bool?), typeof(RadioButton), new(false));
    public event RoutedEventHandler? Checked
    {
        add { if (value != null) AddHandler(RoutedEvent.For("Checked"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("Checked"), value); }
    }
    public event RoutedEventHandler? Unchecked
    {
        add { if (value != null) AddHandler(RoutedEvent.For("Unchecked"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("Unchecked"), value); }
    }
    public RadioButton() : this(new Native.RadioButton(Application.Current.ControlScene)) { }
    internal RadioButton(SceneElement native) : base(native)
    {
        Listen("CheckedChanged", _ => Changed(nameof(IsChecked)));
        Listen("Checked", _ => Emit("Checked", Args()));
        Listen("Unchecked", _ => Emit("Unchecked", Args()));
    }
    public bool? IsChecked { get => ((Native.RadioButton)NativeElement).IsChecked; set => ((Native.RadioButton)NativeElement).IsChecked = value == true; }
    public string GroupName { get => ((Native.RadioButton)NativeElement).GroupName; set => ((Native.RadioButton)NativeElement).GroupName = value; }
    public override object? Content { get => ((Native.RadioButton)NativeElement).Content; set => ((Native.RadioButton)NativeElement).Content = value?.ToString() ?? ""; }
}

public class Slider : Control
{
    public double LargeChange { get; set; } = 10;
    public double SmallChange { get; set; } = 1;
    public double TickFrequency {get=>((Native.Trackbar)NativeElement).TickFrequency;set {if(!double.IsFinite(value)||value<0)throw new ArgumentOutOfRangeException(nameof(value));((Native.Trackbar)NativeElement).TickFrequency=value;}}
    public bool IsSnapToTickEnabled {get=>((Native.Trackbar)NativeElement).IsSnapToTickEnabled;set=>((Native.Trackbar)NativeElement).IsSnapToTickEnabled=value;}
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterNative(nameof(Value), typeof(double), typeof(Slider), new(0d));
    public event RoutedPropertyChangedEventHandler<double>? ValueChanged
    {
        add { if (value != null) AddHandler(RoutedEvent.For("ValueChanged"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("ValueChanged"), value); }
    }
    double previous;
    public Slider() : this(new Native.Trackbar(Application.Current.ControlScene) { FixedInterval = false, ShowValue = false }) { }
    internal Slider(SceneElement native) : base(native)
    {
        previous = Value;
        Listen("ValueChanged", _ => { var old = previous; previous = Value; Changed(nameof(Value)); Emit("ValueChanged", new RoutedPropertyChangedEventArgs<double>(old, Value)); });
        KeyDown += (_, args) =>
        {
            double change = args.Key switch { Input.Key.Left or Input.Key.Down => -SmallChange, Input.Key.Right or Input.Key.Up => SmallChange, Input.Key.PageDown => -LargeChange, Input.Key.PageUp => LargeChange, _ => 0 };
            if (change == 0) return;
            Value = Math.Clamp(Value + change, Minimum, Maximum); args.Handled = true;
        };
    }
    public double Value { get => ((Native.Trackbar)NativeElement).Value; set => ((Native.Trackbar)NativeElement).Value = value; }
    public double Minimum { get => ((Native.Trackbar)NativeElement).MinimumValue; set => ((Native.Trackbar)NativeElement).MinimumValue = value; }
    public double Maximum { get => ((Native.Trackbar)NativeElement).MaximumValue; set => ((Native.Trackbar)NativeElement).MaximumValue = value; }
}

public class UniformGrid : Panel
{
    public UniformGrid() : this(new Layout.UniformGrid(Application.Current.ControlScene)) { }
    internal UniformGrid(SceneElement native) : base(native) { }
    public int Rows { get => ((Layout.UniformGrid)NativeElement).Rows; set { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); ((Layout.UniformGrid)NativeElement).Rows = value; MarkupSetter?.Invoke(nameof(Rows), value); NativeElement.Invalidate(); Changed(nameof(Rows)); } }
    public int Columns { get => ((Layout.UniformGrid)NativeElement).Columns; set { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); ((Layout.UniformGrid)NativeElement).Columns = value; MarkupSetter?.Invoke(nameof(Columns), value); NativeElement.Invalidate(); Changed(nameof(Columns)); } }
}
public class WrapPanel : Panel
{
    public WrapPanel() : this(new Layout.WrapPanel(Application.Current.ControlScene) { HorizontalSpacing = 0, VerticalSpacing = 0 }) { }
    internal WrapPanel(SceneElement native) : base(native) { }
    public Orientation Orientation { get => (Orientation)((Layout.WrapPanel)NativeElement).Orientation; set => ((Layout.WrapPanel)NativeElement).Orientation = (Layout.Orientation)value; }
    public double ItemWidth { get => ((Layout.WrapPanel)NativeElement).ItemWidth; set => ((Layout.WrapPanel)NativeElement).ItemWidth = (float)value; }
    public double ItemHeight { get => ((Layout.WrapPanel)NativeElement).ItemHeight; set => ((Layout.WrapPanel)NativeElement).ItemHeight = (float)value; }
}

// These native controls own an internal viewport/content host. Replacing Content
// must update that host as well as the adapter's logical tree.
public abstract class HostedContentControl : ContentControl
{
    internal HostedContentControl(SceneElement native) : base(native) { }
    protected abstract SceneElement? NativeContent { get; set; }
    public override object? Content
    {
        get => LogicalChildren.FirstOrDefault();
        set
        {
            if (value is not null and not FrameworkElement) throw new NotSupportedException("Content must be a FrameworkElement.");
            if (ReferenceEquals(Content, value)) return;
            if (value is FrameworkElement next && next.Parent != null) throw new InvalidOperationException("Content already has a parent.");
            if (Content is FrameworkElement old) { LogicalChildren.Remove(old); old.Reparent(null); }
            NativeContent = (value as FrameworkElement)?.NativeElement;
            if (value is FrameworkElement child) { LogicalChildren.Add(child); child.Reparent(this); }
            Changed(nameof(Content));
        }
    }
}

public class ScrollViewer : HostedContentControl
{
    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty = DependencyProperty.RegisterAttached(nameof(HorizontalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer), new(ScrollBarVisibility.Disabled, ApplyScrollPolicy));
    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = DependencyProperty.RegisterAttached(nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer), new(ScrollBarVisibility.Auto, ApplyScrollPolicy));
    public static readonly DependencyProperty CanContentScrollProperty = DependencyProperty.RegisterAttached(nameof(CanContentScroll), typeof(bool), typeof(ScrollViewer), new(false));
    static void ApplyScrollPolicy(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is ScrollViewer viewer)
        {
            var native = (Native.ScrollViewer)viewer.NativeElement;
            if (args.Property == HorizontalScrollBarVisibilityProperty) native.HorizontalScrollBarVisibility = (int)(ScrollBarVisibility)args.NewValue!;
            else native.VerticalScrollBarVisibility = (int)(ScrollBarVisibility)args.NewValue!;
        }
    }
    public static void SetHorizontalScrollBarVisibility(DependencyObject target, ScrollBarVisibility value) => target.SetValue(HorizontalScrollBarVisibilityProperty, value);
    public static ScrollBarVisibility GetHorizontalScrollBarVisibility(DependencyObject target) => (ScrollBarVisibility)target.GetValue(HorizontalScrollBarVisibilityProperty)!;
    public static void SetVerticalScrollBarVisibility(DependencyObject target, ScrollBarVisibility value) => target.SetValue(VerticalScrollBarVisibilityProperty, value);
    public static ScrollBarVisibility GetVerticalScrollBarVisibility(DependencyObject target) => (ScrollBarVisibility)target.GetValue(VerticalScrollBarVisibilityProperty)!;
    public static void SetCanContentScroll(DependencyObject target, bool value) => target.SetValue(CanContentScrollProperty, value);
    public static bool GetCanContentScroll(DependencyObject target) => (bool)target.GetValue(CanContentScrollProperty)!;
    public ScrollBarVisibility HorizontalScrollBarVisibility { get => GetHorizontalScrollBarVisibility(this); set => SetHorizontalScrollBarVisibility(this, value); }
    public ScrollBarVisibility VerticalScrollBarVisibility { get => GetVerticalScrollBarVisibility(this); set => SetVerticalScrollBarVisibility(this, value); }
    public bool CanContentScroll { get => GetCanContentScroll(this); set => SetCanContentScroll(this, value); }
    public ScrollViewer() : this(new Native.ScrollViewer(Application.Current.ControlScene)) { }
    internal ScrollViewer(SceneElement native) : base(native) { }
    protected override SceneElement? NativeContent { get => ((Native.ScrollViewer)NativeElement).Content; set => ((Native.ScrollViewer)NativeElement).Content = value; }
    public double HorizontalOffset => ((Native.ScrollViewer)NativeElement).ScrollOffset.X;
    public double VerticalOffset => ((Native.ScrollViewer)NativeElement).ScrollOffset.Y;
    public double ScrollableHeight => ((Native.ScrollViewer)NativeElement).ScrollableSize.Y;
    public double ScrollableWidth => ((Native.ScrollViewer)NativeElement).ScrollableSize.X;
    public void ScrollToHorizontalOffset(double offset) => ((Native.ScrollViewer)NativeElement).ScrollTo(new((float)offset, (float)VerticalOffset));
    public void ScrollToVerticalOffset(double offset) => ((Native.ScrollViewer)NativeElement).ScrollTo(new((float)HorizontalOffset, (float)offset));
}

public class GroupBox : HostedContentControl
{
    public GroupBox() : this(new Native.GroupBox(Application.Current.ControlScene)) { }
    internal GroupBox(SceneElement native) : base(native) { }
    protected override SceneElement? NativeContent { get => ((Native.GroupBox)NativeElement).Content; set => ((Native.GroupBox)NativeElement).Content = value; }
    public object? Header { get => ((Native.GroupBox)NativeElement).Header; set => ((Native.GroupBox)NativeElement).Header = value?.ToString() ?? ""; }
}

public class Expander : HostedContentControl
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.RegisterNative(nameof(IsExpanded), typeof(bool), typeof(Expander), new(false));
    public event RoutedEventHandler? Expanded
    {
        add { if (value != null) AddHandler(RoutedEvent.For("Expanded"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("Expanded"), value); }
    }
    public event RoutedEventHandler? Collapsed
    {
        add { if (value != null) AddHandler(RoutedEvent.For("Collapsed"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("Collapsed"), value); }
    }
    public Expander() : this(new Native.Expander(Application.Current.ControlScene) { IsExpanded = false }) { }
    internal Expander(SceneElement native) : base(native)
    {
        Listen("ExpandedChanged", _ => { Changed(nameof(IsExpanded)); Emit(IsExpanded ? "Expanded" : "Collapsed", Args()); });
    }
    protected override SceneElement? NativeContent { get => ((Native.Expander)NativeElement).Content; set => ((Native.Expander)NativeElement).Content = value; }
    public bool IsExpanded { get => ((Native.Expander)NativeElement).IsExpanded; set => ((Native.Expander)NativeElement).IsExpanded = value; }
    public object? Header { get => ((Native.Expander)NativeElement).Header; set => ((Native.Expander)NativeElement).Header = value?.ToString() ?? ""; }
}
