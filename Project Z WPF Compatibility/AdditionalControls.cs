using ProjectZ.Shared.Drawing.UI;
using Native = ProjectZ.Shared.Drawing.UI.Input;
using Layout = ProjectZ.Shared.Drawing.UI.Layout;

namespace ProjectZ.WpfCompatibility.Controls;

public class Separator : Control
{
    public Separator() : this(new Native.Separator(Application.Current.Scene)) { }
    internal Separator(SceneElement native) : base(native) { }
    public Orientation Orientation { get => (Orientation)((Native.Separator)NativeElement).Orientation; set => ((Native.Separator)NativeElement).Orientation = (Layout.Orientation)value; }
}

public class Label : ContentControl
{
    public Label() : this(new Native.Label(Application.Current.Scene)) { }
    internal Label(SceneElement native) : base(native) { }
    public override object? Content { get => ((Native.Label)NativeElement).Content; set { ((Native.Label)NativeElement).Content = value?.ToString() ?? ""; Changed(nameof(Content)); } }
}

public class PasswordBox : Control
{
    public event RoutedEventHandler? PasswordChanged;
    public PasswordBox() : this(new Native.PasswordBox(Application.Current.Scene)) { }
    internal PasswordBox(SceneElement native) : base(native) { Listen("OnTextChanged", _ => PasswordChanged?.Invoke(this, Args())); }
    public string Password { get => ((Native.PasswordBox)NativeElement).Password; set => ((Native.PasswordBox)NativeElement).Password = value; }
    public void Clear() => Password = "";
}

public class RadioButton : ContentControl
{
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.RegisterNative(nameof(IsChecked), typeof(bool?), typeof(RadioButton), new(false));
    public event RoutedEventHandler? Checked, Unchecked;
    public RadioButton() : this(new Native.RadioButton(Application.Current.Scene)) { }
    internal RadioButton(SceneElement native) : base(native)
    {
        Listen("CheckedChanged", _ => Changed(nameof(IsChecked)));
        Listen("Checked", _ => Checked?.Invoke(this, Args()));
        Listen("Unchecked", _ => Unchecked?.Invoke(this, Args()));
    }
    public bool? IsChecked { get => ((Native.RadioButton)NativeElement).IsChecked; set => ((Native.RadioButton)NativeElement).IsChecked = value == true; }
    public string GroupName { get => ((Native.RadioButton)NativeElement).GroupName; set => ((Native.RadioButton)NativeElement).GroupName = value; }
    public override object? Content { get => ((Native.RadioButton)NativeElement).Content; set => ((Native.RadioButton)NativeElement).Content = value?.ToString() ?? ""; }
}

public class Slider : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterNative(nameof(Value), typeof(double), typeof(Slider), new(0d));
    public event RoutedPropertyChangedEventHandler<double>? ValueChanged;
    double previous;
    public Slider() : this(new Native.Trackbar(Application.Current.Scene) { FixedInterval = false, ShowValue = false }) { }
    internal Slider(SceneElement native) : base(native)
    {
        previous = Value;
        Listen("ValueChanged", _ => { var old = previous; previous = Value; Changed(nameof(Value)); ValueChanged?.Invoke(this, new(old, Value) { Source = this, OriginalSource = this }); });
    }
    public double Value { get => ((Native.Trackbar)NativeElement).Value; set => ((Native.Trackbar)NativeElement).Value = value; }
    public double Minimum { get => ((Native.Trackbar)NativeElement).MinimumValue; set => ((Native.Trackbar)NativeElement).MinimumValue = value; }
    public double Maximum { get => ((Native.Trackbar)NativeElement).MaximumValue; set => ((Native.Trackbar)NativeElement).MaximumValue = value; }
}

public class WrapPanel : Panel
{
    public WrapPanel() : this(new Layout.WrapPanel(Application.Current.Scene) { HorizontalSpacing = 0, VerticalSpacing = 0 }) { }
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
    public ScrollViewer() : this(new Native.ScrollViewer(Application.Current.Scene)) { }
    internal ScrollViewer(SceneElement native) : base(native) { }
    protected override SceneElement? NativeContent { get => ((Native.ScrollViewer)NativeElement).Content; set => ((Native.ScrollViewer)NativeElement).Content = value; }
    public double HorizontalOffset => ((Native.ScrollViewer)NativeElement).ScrollOffset.X;
    public double VerticalOffset => ((Native.ScrollViewer)NativeElement).ScrollOffset.Y;
    public void ScrollToHorizontalOffset(double offset) => ((Native.ScrollViewer)NativeElement).ScrollTo(new((float)offset, (float)VerticalOffset));
    public void ScrollToVerticalOffset(double offset) => ((Native.ScrollViewer)NativeElement).ScrollTo(new((float)HorizontalOffset, (float)offset));
}

public class GroupBox : HostedContentControl
{
    public GroupBox() : this(new Native.GroupBox(Application.Current.Scene)) { }
    internal GroupBox(SceneElement native) : base(native) { }
    protected override SceneElement? NativeContent { get => ((Native.GroupBox)NativeElement).Content; set => ((Native.GroupBox)NativeElement).Content = value; }
    public object? Header { get => ((Native.GroupBox)NativeElement).Header; set => ((Native.GroupBox)NativeElement).Header = value?.ToString() ?? ""; }
}

public class Expander : HostedContentControl
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.RegisterNative(nameof(IsExpanded), typeof(bool), typeof(Expander), new(false));
    public event RoutedEventHandler? Expanded, Collapsed;
    public Expander() : this(new Native.Expander(Application.Current.Scene) { IsExpanded = false }) { }
    internal Expander(SceneElement native) : base(native)
    {
        Listen("ExpandedChanged", _ => { Changed(nameof(IsExpanded)); if (IsExpanded) Expanded?.Invoke(this, Args()); else Collapsed?.Invoke(this, Args()); });
    }
    protected override SceneElement? NativeContent { get => ((Native.Expander)NativeElement).Content; set => ((Native.Expander)NativeElement).Content = value; }
    public bool IsExpanded { get => ((Native.Expander)NativeElement).IsExpanded; set => ((Native.Expander)NativeElement).IsExpanded = value; }
    public object? Header { get => ((Native.Expander)NativeElement).Header; set => ((Native.Expander)NativeElement).Header = value?.ToString() ?? ""; }
}
