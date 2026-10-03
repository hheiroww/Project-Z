using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using global::ProjectZ.Shared.Drawing.UI;
using Native = global::ProjectZ.Shared.Drawing.UI.Input;
using Primitives = global::ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.WpfCompatibility.Controls;

public class Control : FrameworkElement
{
    public Control() { }
    internal Control(SceneElement element) : base(element) { }
    public Media.Brush? Background { get => NativeElement is Primitives.RectangleElement r ? new Media.SolidColorBrush(Media.Color.FromNative(r.BackgroundColor)) : null; set { if (NativeElement is Primitives.RectangleElement r && value is Media.SolidColorBrush brush) r.BackgroundColor = brush.Color.Native; else throw new NotSupportedException("Only solid backgrounds are supported by this adapter."); } }
    public Media.Brush? Foreground
    {
        get => new Media.SolidColorBrush(Media.Color.FromNative((Microsoft.Xna.Framework.Color)(NativeElement.GetType().GetProperty("ForegroundColor")?.GetValue(NativeElement) ?? Microsoft.Xna.Framework.Color.White)));
        set { var p = NativeElement.GetType().GetProperty("ForegroundColor") ?? throw new NotSupportedException("This control has no foreground."); p.SetValue(NativeElement, ((Media.SolidColorBrush)value!).Color.Native); }
    }
    public Thickness Padding { get { var x = NativeElement.Padding; return new(x.Left, x.Top, x.Right, x.Bottom); } set { NativeElement.Padding = new((int)value.Left, (int)value.Top, (int)value.Right, (int)value.Bottom); } }
}
public class ContentControl : Control
{
    public static readonly DependencyProperty ContentProperty = DependencyProperty.RegisterNative(nameof(Content), typeof(object), typeof(ContentControl));
    object? content;
    public ContentControl() { }
    internal ContentControl(SceneElement native) : base(native) { }
    public virtual object? Content
    {
        get => content ?? LogicalChildren.FirstOrDefault();
        set
        {
            if (Content is FrameworkElement old) { NativeElement.Children.Remove(old.NativeElement); LogicalChildren.Remove(old); old.Reparent(null); }
            content = value;
            if (value is FrameworkElement child) { NativeElement.Children.Add(child.NativeElement); LogicalChildren.Add(child); child.Reparent(this); }
            else if (value != null) throw new NotSupportedException("This content control requires a FrameworkElement.");
            Changed(nameof(Content));
        }
    }
}
public class UserControl : ContentControl { public UserControl() { } internal UserControl(SceneElement native) : base(native) { } }
public class Page : UserControl { }
public class Border : ContentControl { public Border() { } internal Border(SceneElement native) : base(native) { } }
public sealed class UIElementCollection : Collection<FrameworkElement>
{
    readonly FrameworkElement owner;
    internal UIElementCollection(FrameworkElement owner) => this.owner = owner;
    protected override void InsertItem(int index, FrameworkElement item)
    {
        if (item.Parent != null) throw new InvalidOperationException("Remove the element from its existing parent first.");
        base.InsertItem(index, item); owner.LogicalChildren.Insert(index, item); owner.NativeElement.Children.Add(item.NativeElement); item.Reparent(owner);
        Application.Current.Scene.AddElement(item.NativeElement);
    }
    protected override void RemoveItem(int index) { var item = this[index]; owner.NativeElement.Children.Remove(item.NativeElement); owner.LogicalChildren.Remove(item); item.Reparent(null); Application.Current.Scene.RemoveElement(item.NativeElement); base.RemoveItem(index); }
    protected override void SetItem(int index, FrameworkElement item) { RemoveItem(index); InsertItem(index, item); }
    protected override void ClearItems() { while (Count != 0) RemoveAt(Count - 1); }
    internal void AddExisting(FrameworkElement item) { Items.Add(item); owner.LogicalChildren.Add(item); item.Reparent(owner); }
}
public class Panel : Control
{
    public UIElementCollection Children { get; }
    public Panel() { Children = new(this); }
    internal Panel(SceneElement native) : base(native) { Children = new(this); }
}
public class Grid : Panel
{
    public Grid() { } internal Grid(SceneElement native) : base(native) { }
    public static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached("Row", typeof(int), typeof(Grid), new(0, (d, e) => ((FrameworkElement)d).MarkupSetter?.Invoke("Grid.Row", e.NewValue)));
    public static readonly DependencyProperty ColumnProperty = DependencyProperty.RegisterAttached("Column", typeof(int), typeof(Grid), new(0, (d, e) => ((FrameworkElement)d).MarkupSetter?.Invoke("Grid.Column", e.NewValue)));
    public static void SetRow(FrameworkElement element, int row) { element.SetValue(RowProperty, row); element.MarkupSetter?.Invoke("Grid.Row", row); }
    public static int GetRow(FrameworkElement element) => (int)element.GetValue(RowProperty)!;
    public static void SetColumn(FrameworkElement element, int column) { element.SetValue(ColumnProperty, column); element.MarkupSetter?.Invoke("Grid.Column", column); }
    public static int GetColumn(FrameworkElement element) => (int)element.GetValue(ColumnProperty)!;
}
public enum Orientation { Horizontal, Vertical }
public class StackPanel : Panel
{
    public StackPanel() : base(new global::ProjectZ.Shared.Drawing.UI.Layout.StackPanel(Application.Current.Scene)) { }
    internal StackPanel(SceneElement native) : base(native) { }
    public Orientation Orientation { get => Enum.Parse<Orientation>(((global::ProjectZ.Shared.Drawing.UI.Layout.StackPanel)NativeElement).Orientation.ToString()); set => ((global::ProjectZ.Shared.Drawing.UI.Layout.StackPanel)NativeElement).Orientation = Enum.Parse<global::ProjectZ.Shared.Drawing.UI.Layout.Orientation>(value.ToString()); }
}
public class Canvas : Panel
{
    public Canvas() { } internal Canvas(SceneElement native) : base(native) { }
    public static readonly DependencyProperty LeftProperty = DependencyProperty.RegisterAttached("Left", typeof(double), typeof(Canvas), new(double.NaN, (d, e) => SetPosition((UIElement)d, (double)e.NewValue!, true)));
    public static readonly DependencyProperty TopProperty = DependencyProperty.RegisterAttached("Top", typeof(double), typeof(Canvas), new(double.NaN, (d, e) => SetPosition((UIElement)d, (double)e.NewValue!, false)));
    static void SetPosition(UIElement element, double value, bool left)
    {
        var p = element.NativeElement.Position;
        element.NativeElement.Position = left ? new((float)value, p.Y) : new(p.X, (float)value);
        if (element is FrameworkElement framework) framework.MarkupSetter?.Invoke(left ? "Canvas.Left" : "Canvas.Top", value);
    }
    public static void SetLeft(UIElement element, double value) => element.SetValue(LeftProperty, value);
    public static void SetTop(UIElement element, double value) => element.SetValue(TopProperty, value);
    public static double GetLeft(UIElement element) => (double)element.GetValue(LeftProperty)!;
    public static double GetTop(UIElement element) => (double)element.GetValue(TopProperty)!;
}
public class Button : ContentControl
{
    public event RoutedEventHandler? Click;
    ICommand? command;
    object? parameter;
    public Button() : this(new Native.Button(Application.Current.Scene)) { }
    internal Button(SceneElement native) : base(native)
    {
        Listen("MouseLeftClick", _ => { if (IsEnabled) { var e = Args(); Click?.Invoke(this, e); if (!e.Handled && command?.CanExecute(parameter) == true) command.Execute(parameter); } });
    }
    public override object? Content { get => ((Native.Button)NativeElement).Text; set { ((Native.Button)NativeElement).Text = value?.ToString() ?? ""; Changed(nameof(Content)); } }
    public ICommand? Command { get => command; set { if (command != null) command.CanExecuteChanged -= CanExecuteChanged; command = value; if (command != null) command.CanExecuteChanged += CanExecuteChanged; UpdateEnabled(); } }
    public object? CommandParameter { get => parameter; set { parameter = value; UpdateEnabled(); } }
    void CanExecuteChanged(object? sender, EventArgs e) { if (Dispatcher.CheckAccess()) UpdateEnabled(); else _ = Dispatcher.InvokeAsync(UpdateEnabled); }
    void UpdateEnabled() { if (!IsDisposed && command != null) IsEnabled = command.CanExecute(parameter); }
    public override void Dispose() { if (command != null) command.CanExecuteChanged -= CanExecuteChanged; base.Dispose(); }
}
public class TextChangedEventArgs : RoutedEventArgs { }
public class SelectionChangedEventArgs : RoutedEventArgs { }
public class TextBox : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterNative(nameof(Text), typeof(string), typeof(TextBox), new(""));
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public TextBox() : this(new Native.Textbox(Application.Current.Scene)) { }
    internal TextBox(SceneElement native) : base(native) { Listen("OnTextChanged", _ => { Changed(nameof(Text)); TextChanged?.Invoke(this, new() { Source = this, OriginalSource = this }); }); }
    public string Text { get => ((Native.Textbox)NativeElement).Text; set => ((Native.Textbox)NativeElement).Text = value; }
    public bool IsReadOnly { get => ((Native.Textbox)NativeElement).IsReadOnly; set => ((Native.Textbox)NativeElement).IsReadOnly = value; }
}
public class TextBlock : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterNative(nameof(Text), typeof(string), typeof(TextBlock), new(""));
    public TextBlock() : this(new Primitives.TextElement(Application.Current.Scene)) { }
    internal TextBlock(SceneElement native) : base(native) { }
    public string Text { get => ((Primitives.TextElement)NativeElement).Text; set { ((Primitives.TextElement)NativeElement).Text = value; Changed(nameof(Text)); } }
}
public class CheckBox : ContentControl
{
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.RegisterNative(nameof(IsChecked), typeof(bool?), typeof(CheckBox));
    public event RoutedEventHandler? Checked, Unchecked, Indeterminate;
    public CheckBox() : this(new Native.CheckBox(Application.Current.Scene)) { }
    internal CheckBox(SceneElement native) : base(native)
    {
        Listen("CheckedChanged", _ => Changed(nameof(IsChecked)));
        Listen("Checked", _ => Checked?.Invoke(this, Args())); Listen("Unchecked", _ => Unchecked?.Invoke(this, Args())); Listen("Indeterminate", _ => Indeterminate?.Invoke(this, Args()));
    }
    public bool? IsChecked { get => ((Native.CheckBox)NativeElement).IsChecked; set => ((Native.CheckBox)NativeElement).IsChecked = value; }
    public override object? Content { get => ((Native.CheckBox)NativeElement).Content; set => ((Native.CheckBox)NativeElement).Content = value?.ToString() ?? ""; }
}
public class ProgressBar : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterNative(nameof(Value), typeof(double), typeof(ProgressBar), new(0d));
    public ProgressBar() : this(new Native.ProgressBar(Application.Current.Scene)) { }
    internal ProgressBar(SceneElement native) : base(native) { Listen("ValueChanged", _ => Changed(nameof(Value))); }
    public double Value { get => ((Native.ProgressBar)NativeElement).Value; set => ((Native.ProgressBar)NativeElement).Value = value; }
    public double Minimum { get => ((Native.ProgressBar)NativeElement).Minimum; set => ((Native.ProgressBar)NativeElement).Minimum = value; }
    public double Maximum { get => ((Native.ProgressBar)NativeElement).Maximum; set => ((Native.ProgressBar)NativeElement).Maximum = value; }
}
public class ListBox : Control
{
    DataTemplate? template;
    readonly List<FrameworkElement> itemViews = new();
    public DataTemplate? ItemTemplate { get => template; set { if (this is ComboBox) throw new NotSupportedException("ComboBox item templates are not supported."); template = value; RefreshItems(); } }
    public IReadOnlyList<FrameworkElement> ItemViews => itemViews;
    IEnumerable? source;
    INotifyCollectionChanged? observable;
    protected readonly List<object?> items = new();
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.RegisterNative(nameof(ItemsSource), typeof(IEnumerable), typeof(ListBox));
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public ListBox() : this(new Native.ListBox(Application.Current.Scene)) { }
    internal ListBox(SceneElement native) : base(native) { Listen("SelectionChanged", _ => { Changed(nameof(SelectedIndex)); Changed(nameof(SelectedItem)); SelectionChanged?.Invoke(this, new() { Source = this, OriginalSource = this }); }); }
    public IEnumerable? ItemsSource
    {
        get => source;
        set { if (observable != null) observable.CollectionChanged -= CollectionChanged; source = value; observable = value as INotifyCollectionChanged; if (observable != null) observable.CollectionChanged += CollectionChanged; RefreshItems(); }
    }
    void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) { if (Dispatcher.CheckAccess()) RefreshItems(); else _ = Dispatcher.InvokeAsync(RefreshItems); }
    void RefreshItems()
    {
        if (IsDisposed) return;
        var selected = SelectedItem;
        foreach (var view in itemViews) { LogicalChildren.Remove(view); view.Dispose(); }
        itemViews.Clear();
        items.Clear(); if (source != null) foreach (var item in source) items.Add(item);
        if (NativeElement is Native.ListBox list) list.ItemVisualFactory = template == null ? null : index =>
        {
            var view = template.LoadContent(); view.Reparent(this); view.DataContext = items[index];
            view.IsHitTestVisible = false; LogicalChildren.Add(view); itemViews.Add(view); return view.NativeElement;
        };
        NativeElement.GetType().GetProperty("Items")!.SetValue(NativeElement, items.Select(x => x?.ToString() ?? "").ToList());
        SelectedIndex = selected == null ? -1 : items.IndexOf(selected);
        Changed(nameof(ItemsSource));
    }
    public int SelectedIndex { get => (int)NativeElement.GetType().GetProperty(nameof(SelectedIndex))!.GetValue(NativeElement)!; set => NativeElement.GetType().GetProperty(nameof(SelectedIndex))!.SetValue(NativeElement, value); }
    public object? SelectedItem { get => SelectedIndex >= 0 && SelectedIndex < items.Count ? items[SelectedIndex] : null; set => SelectedIndex = items.IndexOf(value); }
    public override void Dispose() { if (observable != null) observable.CollectionChanged -= CollectionChanged; base.Dispose(); }
}
public class ComboBox : ListBox { public ComboBox() : base(new Native.ComboBox(Application.Current.Scene)) { } internal ComboBox(SceneElement native) : base(native) { } }
internal static class ControlFactory
{
    internal static FrameworkElement Wrap(SceneElement native, string? name = null) => name switch
    {
        "Separator" => new Separator(native), "Slider" => new Slider(native), "PasswordBox" => new PasswordBox(native), "RadioButton" => new RadioButton(native), "Label" => new Label(native), "WrapPanel" => new WrapPanel(native), "ScrollViewer" => new ScrollViewer(native), "Expander" => new Expander(native), "GroupBox" => new GroupBox(native),
        "Grid" => new Grid(native), "StackPanel" => new StackPanel(native), "Canvas" => new Canvas(native), "Border" => new Border(native), "UserControl" or "Page" => new UserControl(native),
        _ => native switch { Native.Button => new Button(native), Native.Textbox => new TextBox(native), Primitives.TextElement => new TextBlock(native), Native.CheckBox => new CheckBox(native), Native.ProgressBar => new ProgressBar(native), Native.ComboBox => new ComboBox(native), Native.ListBox => new ListBox(native), _ => new Panel(native) }
    };
}
