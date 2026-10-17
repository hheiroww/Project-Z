using System.Collections.ObjectModel;
using ProjectZ.Shared.Drawing.UI;
using Native = ProjectZ.Shared.Drawing.UI.Input;
using Layout = ProjectZ.Shared.Drawing.UI.Layout;
using Advanced = ProjectZ.Shared.Drawing.UI.Advanced;

namespace ProjectZ.WpfCompatibility.Controls;

public enum Dock { Left, Top, Right, Bottom }
public class DockPanel : Panel
{
    public static readonly DependencyProperty DockProperty = DependencyProperty.RegisterAttached("Dock", typeof(Dock), typeof(DockPanel), new(Dock.Left, (d, e) => { if (d is FrameworkElement child && child.Parent is DockPanel panel) panel.AttachChild(child); }));
    public DockPanel() : this(new Layout.DockPanel(Application.Current.ControlScene) { Padding = new(0) }) { }
    internal DockPanel(SceneElement native) : base(native) { }
    public bool LastChildFill { get => ((Layout.DockPanel)NativeElement).LastChildFill; set => ((Layout.DockPanel)NativeElement).LastChildFill = value; }
    public static void SetDock(UIElement element, Dock dock) { element.SetValue(DockProperty, dock); if (element is FrameworkElement child) child.MarkupSetter?.Invoke("DockPanel.Dock", dock); }
    public static Dock GetDock(UIElement element) => (Dock)element.GetValue(DockProperty)!;
    internal void AttachChild(FrameworkElement child) => ((Layout.DockPanel)NativeElement).SetDock(child.NativeElement, (Layout.Dock)GetDock(child));
}
public class Decorator : ContentControl
{
    public Decorator() { }
    internal Decorator(SceneElement native) : base(native) { }
    public UIElement? Child { get => Content as UIElement; set => Content = value; }
}
public class Image : FrameworkElement
{
    Media.ImageSource? source;
    public Image() : this(new Advanced.ImageElement(Application.Current.ControlScene)) { }
    internal Image(SceneElement native) : base(native) { }
    public Media.ImageSource? Source
    {
        get => source;
        set { Dispatcher.VerifyAccess(); if(source!=null) source.PropertyChanged-=SourceChanged; source = value; if(source!=null) source.PropertyChanged+=SourceChanged; ReloadSource(); Changed(nameof(Source)); }
    }
    void SourceChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs args) { if(Dispatcher.CheckAccess()) ReloadSource();else _=Dispatcher.InvokeAsync(ReloadSource); }
    void ReloadSource() { if(IsDisposed) return;var image=(Advanced.ImageElement)NativeElement;if(source==null) image.Texture=null;else source.Load(image); }
    public override void Dispose() { if(source!=null) source.PropertyChanged-=SourceChanged; base.Dispose(); }
    public Media.Stretch Stretch { get => (Media.Stretch)((Advanced.ImageElement)NativeElement).Stretch; set => ((Advanced.ImageElement)NativeElement).Stretch = (Advanced.ImageStretch)value; }
}
public class ListView : ListBox
{
    GridView? view;
    FrameworkElement? header;
    public ListView() { } internal ListView(SceneElement native) : base(native) { }
    public GridView? View
    {
        get => view;
        set
        {
            using var construction=Application.Current.ConstructIn(NativeElement.OwnerScene);
            view = value;
            header?.Dispose(); header = null;
            var list = (Native.ListBox)NativeElement;
            list.HeaderVisual = null;
            if (value != null)
            {
                value.ResourceOwner = this;
                ItemTemplate = value.CreateTemplate(value.SourceAssembly ?? GetType().Assembly);
                header = value.CreateTemplate(value.SourceAssembly ?? GetType().Assembly, true).LoadContent();
                list.HeaderVisual = header.NativeElement;
            }
            else ItemTemplate = null;
        }
    }
    public override void Dispose() { header?.Dispose(); base.Dispose(); }
}
public class ListViewItem : ListBoxItem { }
public class EmptyControl : ContentControl
{
    public EmptyControl(UIElement adornedElement) : this() { }
    protected override void OnRender(Media.DrawingContext drawingContext) { }
    public EmptyControl() { NativeElement.SuppressVisual = true; NativeElement.isMouseBypassEnabled = true; if (NativeElement is global::ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement rectangle) { rectangle.BackgroundColor = Microsoft.Xna.Framework.Color.Transparent; rectangle.BorderThickness = 0; } }
    internal EmptyControl(SceneElement native) : base(native) { native.SuppressVisual = true; native.isMouseBypassEnabled = true; }
    public override object? Content { get; set; }
    public Media.Brush? Fill { get; set; }
    public Media.Brush? Stroke { get; set; }
    public double StrokeThickness { get; set; }
    public Media.PenLineCap StrokeStartLineCap { get; set; }
    public Media.PenLineCap StrokeEndLineCap { get; set; }
    public Media.PenLineJoin StrokeLineJoin { get; set; }
    public Media.Stretch Stretch { get; set; }
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public int Columns { get; set; }
    public int Rows { get; set; }
    public UIElement? Child { get => Content as UIElement; set => Content=value; }
    public object? Header { get; set; }
    // Keep application metadata for controls deliberately emitted without a native visual.
    public bool IsSelected { get; set; }
    public bool? IsChecked { get; set; }
    public System.Collections.IEnumerable? ItemsSource { get; set; }
    public object? SelectedItem { get; set; }
    public object? SelectedValue { get; set; }
    public int SelectedIndex { get; set; } = -1;
    public string DisplayMemberPath { get; set; } = "";
    public string SelectedValuePath { get; set; } = "";
    public Media.Geometry? Data { get; set; }
    public Collection<object> Items { get; } = new();
    public override string GetCopyText() => "";
    public override void Copy() { }
}
public enum ScrollBarVisibility { Disabled, Auto, Hidden, Visible }
public class RichTextBox : TextBox
{
    Documents.FlowDocument document = new();
    public Documents.FlowDocument Document { get => document; set { document.Blocks.CollectionChanged -= DocumentChanged; document = value ?? new(); document.Blocks.CollectionChanged += DocumentChanged; RenderDocument(); } }
    public Documents.TextSelection Selection => new(this);
    public Documents.TextPointer CaretPosition { get => new(document, ((Native.RichTextBox)NativeElement).CaretPosition); set => ((Native.RichTextBox)NativeElement).CaretPosition = value.Offset; }
    void DocumentChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args) => RenderDocument();
    void RenderDocument()
    {
        Elements.Clear(); Text = string.Join("\n", document.Blocks.Where(block => block is not Documents.BlockUIContainer).Select(block => block.GetCopyText())); FontFamily = document.FontFamily; FontSize = document.FontSize; FontWeight = document.FontWeight;
        if (document.Foreground != null) Foreground = document.Foreground;
        foreach (var block in document.Blocks.OfType<Documents.BlockUIContainer>()) if (block.Child is FrameworkElement element) Elements.Add(element);
    }
    public UIElementCollection Elements { get; }
    public RichTextBox() : this(new Native.RichTextBox(Application.Current.ControlScene)) { }
    internal RichTextBox(SceneElement native) : base(native) { Elements = new(this); }
    public new bool IsReadOnlyCaretVisible { get => ((Native.RichTextBox)NativeElement).IsReadOnlyCaretVisible; set => ((Native.RichTextBox)NativeElement).IsReadOnlyCaretVisible = value; }
    public bool IsDocumentEnabled { get; set; } = true;
    public new ScrollBarVisibility HorizontalScrollBarVisibility { get; set; } = ScrollBarVisibility.Auto;
    public new ScrollBarVisibility VerticalScrollBarVisibility { get; set; } = ScrollBarVisibility.Auto;
    public new Media.Brush SelectionBrush { get => new Media.SolidColorBrush(Media.Color.FromNative(((Native.RichTextBox)NativeElement).SelectionColor)); set { if (value is Media.SolidColorBrush brush) ((Native.RichTextBox)NativeElement).SelectionColor = brush.Color.Native; } }
    public new double SelectionOpacity { get => ((Native.RichTextBox)NativeElement).SelectionColor.A / 255d; set { var input = (Native.RichTextBox)NativeElement; var color = input.SelectionColor; color.A = (byte)(Math.Clamp(value, 0, 1) * 255); input.SelectionColor = color; } }
}
public class MenuItem : Button
{
    public bool StaysOpenOnClick { get; set; }
    public bool IsHighlighted=>IsMouseOver || IsKeyboardFocused;
    public MenuItem() : this(new Native.MenuItem(Application.Current.ControlScene)) { }
    internal MenuItem(SceneElement native) : base(native) { menuItems = new(this); Click += (_, _) => { if (IsCheckable) IsChecked = !IsChecked; if (Items.Count > 0) { if (submenu == null) { submenu = new ContextMenu(); foreach (var item in menuItems.ToArray()) { menuItems.Remove(item); submenu.Items.Add(item); } } submenu.PlacementTarget = this; submenu.Placement = Primitives.PlacementMode.Right; submenu.IsOpen = true; } }; }
    ContextMenu? submenu;
    readonly MenuItemCollection menuItems;
    public object? Header { get => Content; set => Content = value; }
    public bool IsCheckable { get; set; }
    public bool IsChecked { get; set; }
    public UIElement? CommandTarget { get => CommandParameter as UIElement; set => CommandParameter = value; }
    public MenuItemCollection Items => submenu?.Items ?? menuItems;
    public override void Dispose() { submenu?.Dispose(); base.Dispose(); }
}
public sealed class MenuItemCollection : Collection<FrameworkElement>
{
    readonly FrameworkElement owner;
    internal MenuItemCollection(FrameworkElement owner) => this.owner = owner;
    public void Add(object item) => base.Add(item as FrameworkElement ?? throw new ArgumentException("A menu item must be a native element."));
    protected override void InsertItem(int index, FrameworkElement item) { if (item.Parent != null) throw new InvalidOperationException("The menu item already has a parent."); base.InsertItem(index, item); owner.LogicalChildren.Insert(index, item); item.Reparent(owner); if (owner is ContextMenu) { owner.NativeElement.Children.Add(item.NativeElement); Application.Current.ControlScene.AddElement(item.NativeElement); } }
    protected override void RemoveItem(int index) { var item = this[index]; owner.NativeElement.Children.Remove(item.NativeElement); owner.LogicalChildren.Remove(item); item.Reparent(null); Application.Current.ControlScene.RemoveElement(item.NativeElement); base.RemoveItem(index); }
    protected override void ClearItems() { while (Count > 0) RemoveAt(Count - 1); }
    protected override void SetItem(int index, FrameworkElement item) { RemoveItem(index); InsertItem(index, item); }
    internal void AddExisting(FrameworkElement item) { Items.Add(item); owner.LogicalChildren.Add(item); item.Reparent(owner); }
}
public class ContextMenu : Control
{
    public ContextMenu() : this(new Native.ContextMenu(Application.Current.ControlScene)) { }
    internal ContextMenu(SceneElement native) : base(native) { Items = new(this); Listen("VisibilityChanged", _ => Emit(IsOpen ? "Opened" : "Closed", Args())); }
    public event RoutedEventHandler? Opened
    {
        add { if (value != null) AddHandler(RoutedEvent.For("Opened"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("Opened"), value); }
    }
    public event RoutedEventHandler? Closed
    {
        add { if (value != null) AddHandler(RoutedEvent.For("Closed"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("Closed"), value); }
    }
    public MenuItemCollection Items { get; }
    public UIElement? PlacementTarget { get; set; }
    public Primitives.PlacementMode Placement { get; set; } = Primitives.PlacementMode.MousePoint;
    public bool IsOpen
    {
        get => ((Native.ContextMenu)NativeElement).IsOpen;
        set
        {
            Dispatcher.VerifyAccess();
            if (value && !IsOpen)
            {
                var at = PlacementTarget?.NativeElement.Position ?? Microsoft.Xna.Framework.Input.Mouse.GetState().Position.ToVector2();
                if (PlacementTarget != null && Placement == Primitives.PlacementMode.Bottom) at.Y += PlacementTarget.NativeElement.Size.Y;
                if (PlacementTarget != null && Placement == Primitives.PlacementMode.Right) at.X += PlacementTarget.NativeElement.Size.X;
                NativeElement.Position = at;
                NativeElement.Size = new((float)Math.Max(160, Items.Select(i => i.Width).DefaultIfEmpty(0).Max()), (float)(Items.Sum(i => Math.Max(24, i.Height)) + 8));
                Application.Current.ControlScene.AddElement(NativeElement);
            }
            ((Native.ContextMenu)NativeElement).IsOpen = value;
        }
    }
    public override void Dispose() { Application.Current.ControlScene.RemoveElement(NativeElement); base.Dispose(); }
}
