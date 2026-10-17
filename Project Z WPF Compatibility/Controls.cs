using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using global::ProjectZ.Shared.Drawing.UI;
using Native = global::ProjectZ.Shared.Drawing.UI.Input;
using NativePrimitives = global::ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.WpfCompatibility.Controls;

public partial class Control : FrameworkElement
{
    Media.Brush? background, foreground;
    global::ProjectZ.Shared.Drawing.UI.Advanced.ImageElement? backgroundImage;
    Media.ImageSource? loadedBackgroundImage;
    bool backgroundImageLayoutHooked;
    public Control() { }
    internal Control(SceneElement element) : base(element) { }
    public Media.Brush? Background
    {
        get
        {
            if(background==null && NativeElement is NativePrimitives.RectangleElement r)
                Background=r.BackgroundBrush!=null ? Media.GradientBrush.Wrap(r.BackgroundBrush) : new Media.SolidColorBrush(Media.Color.FromNative(r.BackgroundColor));
            return background;
        }
        set
        {
            Dispatcher.VerifyAccess();
            if (NativeElement is not NativePrimitives.RectangleElement rectangle) throw new NotSupportedException("This control has no background surface.");
            if(background!=null) background.PropertyChanged-=BackgroundChanged;
            background=value;
            if(value!=null) {value.AnimationScene=NativeElement.OwnerScene;value.PropertyChanged+=BackgroundChanged;}
            ApplyBackground(rectangle);
            Changed(nameof(Background));
        }
    }
    void BackgroundChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) { if (args.PropertyName == nameof(Media.ImageBrush.ImageSource)) loadedBackgroundImage = null; ApplyBackground((NativePrimitives.RectangleElement)NativeElement); }
    void ApplyBackground(NativePrimitives.RectangleElement rectangle)
    {
            if (background is not Media.ImageBrush) ClearBackgroundImage();
            switch (background)
            {
                case Media.ImageBrush image: rectangle.BackgroundBrush = null; rectangle.BackgroundColor = Microsoft.Xna.Framework.Color.Transparent; ApplyBackgroundImage(rectangle, image); break;
                case Media.GradientBrush gradient: rectangle.BackgroundBrush = gradient.Native; break;
                case Media.SolidColorBrush solid: rectangle.BackgroundBrush = null; rectangle.BackgroundColor = solid.RenderColor; break;
                case null: rectangle.BackgroundBrush = null; rectangle.BackgroundColor = Microsoft.Xna.Framework.Color.Transparent; break;
                default: throw new NotSupportedException("Unsupported background brush " + background.GetType().Name);
            }
    }
    void ApplyBackgroundImage(NativePrimitives.RectangleElement rectangle, Media.ImageBrush brush)
    {
        if (backgroundImage == null)
        {
            backgroundImage = new(rectangle.OwnerScene) { isMouseBypassEnabled = true, HorizontalAlign = global::ProjectZ.Shared.Drawing.UI.HorizontalAlignment.Stretch, VerticalAlign = global::ProjectZ.Shared.Drawing.UI.VerticalAlignment.Stretch };
            rectangle.Children.Insert(0, backgroundImage);
            if (!backgroundImageLayoutHooked) { Listen("SizeChanged", _ => ArrangeBackgroundImage()); Listen("PositionChanged", _ => ArrangeBackgroundImage()); backgroundImageLayoutHooked = true; }
        }
        if (brush.ImageSource == null) { backgroundImage.Texture = null; loadedBackgroundImage = null; }
        else if (!ReferenceEquals(loadedBackgroundImage, brush.ImageSource)) { brush.ImageSource.Load(backgroundImage); loadedBackgroundImage = brush.ImageSource; }
        backgroundImage.Stretch = (global::ProjectZ.Shared.Drawing.UI.Advanced.ImageStretch)brush.Stretch;
        backgroundImage.Opacity = (float)brush.Opacity;
        if (brush.ImageSource is Media.Imaging.BitmapSource bitmap)
        {
            var scaleX = brush.ViewboxUnits == Media.BrushMappingMode.Absolute ? 1 : bitmap.PixelWidth;
            var scaleY = brush.ViewboxUnits == Media.BrushMappingMode.Absolute ? 1 : bitmap.PixelHeight;
            var box = brush.Viewbox;
            backgroundImage.SourceRectangle = new((int)Math.Round(box.X * scaleX), (int)Math.Round(box.Y * scaleY), (int)Math.Round(box.Width * scaleX), (int)Math.Round(box.Height * scaleY));
        }
        ArrangeBackgroundImage();
    }
    void ArrangeBackgroundImage() { if (backgroundImage != null) { backgroundImage.Position = NativeElement.Position; backgroundImage.Size = NativeElement.Size; } }
    void ClearBackgroundImage()
    {
        if (backgroundImage == null) return;
        NativeElement.Children.Remove(backgroundImage); NativeElement.OwnerScene.RemoveElement(backgroundImage); backgroundImage.Dispose(); backgroundImage = null; loadedBackgroundImage = null;
    }
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public Media.Brush Foreground
    {
        get { if(foreground==null) Foreground=new Media.SolidColorBrush(Media.Color.FromNative((Microsoft.Xna.Framework.Color)(NativeElement.GetType().GetProperty("ForegroundColor")?.GetValue(NativeElement) ?? Microsoft.Xna.Framework.Color.White))); return foreground!; }
        set
        {
            if(value is not null and not Media.SolidColorBrush) throw new NotSupportedException("Vector text requires a solid foreground brush.");
            if(foreground!=null) foreground.PropertyChanged-=ForegroundChanged;
            foreground=value;
            if(value!=null) {value.AnimationScene=NativeElement.OwnerScene;value.PropertyChanged+=ForegroundChanged;}
            ApplyForeground();Changed(nameof(Foreground));
        }
    }
    void ForegroundChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs args)=>ApplyForeground();
    void ApplyForeground()
    {
        var color=(foreground as Media.SolidColorBrush)?.RenderColor??Microsoft.Xna.Framework.Color.Transparent;
        var property=NativeElement.GetType().GetProperty("ForegroundColor");
        if(property!=null) property.SetValue(NativeElement,color);
        else
        {
            void Apply(SceneElement element) { if(element is NativePrimitives.TextElement text) text.ForegroundColor=color; foreach(var child in element.Children) Apply(child); }
            Apply(NativeElement);
        }
    }
    public override void Dispose()
    {
        ClearBackgroundImage();
        if(background!=null) background.PropertyChanged-=BackgroundChanged;
        if(foreground!=null) foreground.PropertyChanged-=ForegroundChanged;
        base.Dispose();
    }
    public Thickness Padding { get { var x = NativeElement.Padding; return new(x.Left, x.Top, x.Right, x.Bottom); } set { NativeElement.Padding = new((int)value.Left, (int)value.Top, (int)value.Right, (int)value.Bottom); if (NativeElement is Native.Textbox input) input.TextPadding = new((float)value.Left, (float)value.Top); } }
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
            if (ReferenceEquals(Content, value)) return;
            if (Content is FrameworkElement old) { NativeElement.Children.Remove(old.NativeElement); LogicalChildren.Remove(old); old.Reparent(null); }
            content = value;
            if (value is FrameworkElement child) { PrepareContentChild(child); NativeElement.Children.Add(child.NativeElement); LogicalChildren.Add(child); child.Reparent(this); }
            else if (value != null) throw new NotSupportedException("This content control requires a FrameworkElement.");
            Changed(nameof(Content));
        }
    }
    protected static void PrepareContentChild(FrameworkElement child)
    {
        if (!child.WidthSpecified && !child.HorizontalAlignmentSpecified) child.NativeElement.HorizontalAlign = global::ProjectZ.Shared.Drawing.UI.HorizontalAlignment.Stretch;
        if (!child.HeightSpecified && !child.VerticalAlignmentSpecified) child.NativeElement.VerticalAlign = global::ProjectZ.Shared.Drawing.UI.VerticalAlignment.Stretch;
    }
}
public class UserControl : ContentControl { public UserControl() { } internal UserControl(SceneElement native) : base(native) { } }
public class Page : UserControl { }
public class Border : Decorator {
    public new static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterNative(nameof(CornerRadius), typeof(global::ProjectZ.WpfCompatibility.CornerRadius), typeof(Border));

    public new global::ProjectZ.WpfCompatibility.CornerRadius CornerRadius
    {
        get
        {
            var rectangleElement = (NativePrimitives.RectangleElement)NativeElement;
            var cornerRadii = rectangleElement.CornerRadii;
            return new global::ProjectZ.WpfCompatibility.CornerRadius(cornerRadii.TopLeft, cornerRadii.TopRight, cornerRadii.BottomRight, cornerRadii.BottomLeft);
        }
        set
        {
            var rectangleElement = (NativePrimitives.RectangleElement)NativeElement;
            rectangleElement.CornerRadii = new NativePrimitives.CornerRadii((float)value.TopLeft, (float)value.TopRight, (float)value.BottomRight, (float)value.BottomLeft);
            MarkupSetter?.Invoke(nameof(CornerRadius), value);
            Changed(nameof(CornerRadius));
        }
    }

    public new Media.Brush? BorderBrush
{
    get
    {
        var nativeColor = ((NativePrimitives.RectangleElement)NativeElement).BorderColor;
        return new Media.SolidColorBrush(Media.Color.FromNative(nativeColor));
    }
    set
    {
        if (value == null)
        {
            ((NativePrimitives.RectangleElement)NativeElement).BorderColor = global::Microsoft.Xna.Framework.Color.Transparent;
        }
        else if (value is Media.SolidColorBrush brush)
        {
            ((NativePrimitives.RectangleElement)NativeElement).BorderColor = brush.Color.Native;
        }
        else
        {
            throw new NotSupportedException();
        }

        var nativeColor = ((NativePrimitives.RectangleElement)NativeElement).BorderColor;
        MarkupSetter?.Invoke(nameof(BorderBrush), $"#{nativeColor.A:X2}{nativeColor.R:X2}{nativeColor.G:X2}{nativeColor.B:X2}");
        Changed(nameof(BorderBrush));
    }
}

    Thickness? requestedBorderThickness;
    public new Thickness BorderThickness
{
    get
    {
        var nativeStroke = ((NativePrimitives.RectangleElement)NativeElement).BorderThickness;
        return requestedBorderThickness ?? new Thickness(nativeStroke);
    }
    set
    {
        if (!double.IsFinite(value.Left) || !double.IsFinite(value.Top) || !double.IsFinite(value.Right) || !double.IsFinite(value.Bottom))
        {
            throw new ArgumentOutOfRangeException(nameof(BorderThickness), "Nonfinite values are not allowed.");
        }

        if (value.Left < 0 || value.Top < 0 || value.Right < 0 || value.Bottom < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BorderThickness), "Negative values are not allowed.");
        }

        var nativeStroke = Math.Max(Math.Max(value.Left, value.Top), Math.Max(value.Right, value.Bottom));
        if (nativeStroke > float.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(BorderThickness), "Left value exceeds the maximum allowed value for a float.");
        }

        // Match the importer's decorative stroke projection for borders created
        // in codebehind too, while retaining their requested logical thickness.
        requestedBorderThickness = value;
        ((NativePrimitives.RectangleElement)NativeElement).BorderThickness = (float)nativeStroke;
        MarkupSetter?.Invoke(nameof(BorderThickness), (float)nativeStroke);
        Changed(nameof(BorderThickness));
    }
}

public new static readonly DependencyProperty BorderBrushProperty =
    DependencyProperty.RegisterNative(nameof(BorderBrush), typeof(Media.Brush), typeof(Border));

public new static readonly DependencyProperty BorderThicknessProperty =
    DependencyProperty.RegisterNative(nameof(BorderThickness), typeof(Thickness), typeof(Border));

public Border() : this(new Native.Border(Application.Current.ControlScene)
{
    BackgroundColor = global::Microsoft.Xna.Framework.Color.Transparent,
    BorderColor = global::Microsoft.Xna.Framework.Color.Transparent,
    BorderThickness = 0,
    Padding = new(0)
})
{
}

    internal Border(SceneElement native) : base(native) { }
}
public sealed class UIElementCollection : Collection<FrameworkElement>
{
    readonly FrameworkElement owner;
    internal UIElementCollection(FrameworkElement owner) => this.owner = owner;
    public void Add(UIElement element) => base.Add(element as FrameworkElement ?? throw new ArgumentException("The native visual must have a FrameworkElement adapter."));
    public void Add(object element) => Add(element as UIElement ?? throw new ArgumentException("A visual item must be a native UIElement."));
    protected override void InsertItem(int index, FrameworkElement item)
    {
        if (item.Parent != null) throw new InvalidOperationException("Remove the element from its existing parent first.");
        base.InsertItem(index, item); owner.LogicalChildren.Insert(index, item); item.Reparent(owner);
        if (owner is Grid grid) grid.AttachChild(item);
        if (owner is DockPanel dock) dock.AttachChild(item);
        if (owner is Canvas canvas) canvas.AttachChild(item);
        if (owner is RichTextBox) ((Native.RichTextBox)owner.NativeElement).Elements.Insert(index, item.NativeElement);
        owner.NativeElement.Children.Add(item.NativeElement);
        owner.NativeElement.OwnerScene.AddElement(item.NativeElement);
    }
    protected override void RemoveItem(int index) { var item = this[index]; owner.NativeElement.Children.Remove(item.NativeElement); if (owner is RichTextBox) ((Native.RichTextBox)owner.NativeElement).Elements.Remove(item.NativeElement); owner.LogicalChildren.Remove(item); item.Reparent(null); owner.NativeElement.OwnerScene.RemoveElement(item.NativeElement); base.RemoveItem(index); }
    protected override void SetItem(int index, FrameworkElement item) { RemoveItem(index); InsertItem(index, item); }
    protected override void ClearItems() { while (Count != 0) RemoveAt(Count - 1); }
    internal void AddExisting(FrameworkElement item) { Items.Add(item); owner.LogicalChildren.Add(item); item.Reparent(owner); }
}
public class Panel : Control
{
    public static readonly DependencyProperty ZIndexProperty=DependencyProperty.RegisterAttached("ZIndex",typeof(int),typeof(Panel),new(0,(d,e)=>((UIElement)d).NativeElement.zIndex=(int)e.NewValue!));
    public static void SetZIndex(UIElement element,int value)=>element.SetValue(ZIndexProperty,value);
    public static int GetZIndex(UIElement element)=>(int)element.GetValue(ZIndexProperty)!;
    public UIElementCollection Children { get; }
    public Panel() { Children = new(this); }
    internal Panel(SceneElement native) : base(native) { Children = new(this); }
}
public class Grid : Panel
{
    public RowDefinitionCollection RowDefinitions { get; }
    public ColumnDefinitionCollection ColumnDefinitions { get; }
    internal Action? DefinitionsChanged;
    public Grid() : this(new global::ProjectZ.Shared.Drawing.UI.Layout.Grid(Application.Current.ControlScene) { Padding = new(0) }) { }
    internal Grid(SceneElement native) : base(native)
    {
        var grid = (global::ProjectZ.Shared.Drawing.UI.Layout.Grid)native;
        RowDefinitions = new(this, grid.RowDefinitions); ColumnDefinitions = new(this, grid.ColumnDefinitions);
    }
    internal void InvalidateDefinitions() { Dispatcher.VerifyAccess(); DefinitionsChanged?.Invoke(); ((global::ProjectZ.Shared.Drawing.UI.Layout.Grid)NativeElement).ArrangeChildren(); }
    public static readonly DependencyProperty RowProperty = DependencyProperty.RegisterAttached("Row", typeof(int), typeof(Grid), new(0, (d, e) => SetIndex((FrameworkElement)d, (int)e.NewValue!, true)));
    public static readonly DependencyProperty ColumnProperty = DependencyProperty.RegisterAttached("Column", typeof(int), typeof(Grid), new(0, (d, e) => SetIndex((FrameworkElement)d, (int)e.NewValue!, false)));
    public static readonly DependencyProperty RowSpanProperty = DependencyProperty.RegisterAttached("RowSpan", typeof(int), typeof(Grid), new(1, (d, e) => SetSpan((FrameworkElement)d, (int)e.NewValue!, true)));
    public static readonly DependencyProperty ColumnSpanProperty = DependencyProperty.RegisterAttached("ColumnSpan", typeof(int), typeof(Grid), new(1, (d, e) => SetSpan((FrameworkElement)d, (int)e.NewValue!, false)));
#nullable disable
    public static void SetRow(UIElement element, int row) { ArgumentNullException.ThrowIfNull(element); if (row < 0) throw new ArgumentOutOfRangeException(nameof(row)); element.SetValue(RowProperty, row); }
#nullable restore
    public static int GetRow(FrameworkElement element) => (int)element.GetValue(RowProperty)!;
#nullable disable
    public static void SetColumn(UIElement element, int column) { ArgumentNullException.ThrowIfNull(element); if (column < 0) throw new ArgumentOutOfRangeException(nameof(column)); element.SetValue(ColumnProperty, column); }
#nullable restore
    public static int GetColumn(FrameworkElement element) => (int)element.GetValue(ColumnProperty)!;
    static void SetIndex(FrameworkElement element, int index, bool row)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index)); element.MarkupSetter?.Invoke(row ? "Grid.Row" : "Grid.Column", index);
        if (element.Parent is Grid grid) { var native = (global::ProjectZ.Shared.Drawing.UI.Layout.Grid)grid.NativeElement; if (row) native.SetRow(element.NativeElement, index); else native.SetColumn(element.NativeElement, index); }
    }
    internal void AttachChild(FrameworkElement element)
    {
        ((global::ProjectZ.Shared.Drawing.UI.Layout.Grid)NativeElement).ConfigureChild(element.NativeElement,
            GetRow(element), GetColumn(element), GetRowSpan(element), GetColumnSpan(element), element.WidthSpecified, element.HeightSpecified);
    }
    static void SetSpan(FrameworkElement element, int span, bool row)
    {
        if (span < 1) throw new ArgumentOutOfRangeException(nameof(span)); element.MarkupSetter?.Invoke(row ? "Grid.RowSpan" : "Grid.ColumnSpan", span);
        if (element.Parent is Grid grid) { var native = (global::ProjectZ.Shared.Drawing.UI.Layout.Grid)grid.NativeElement; if (row) native.SetRowSpan(element.NativeElement, span); else native.SetColumnSpan(element.NativeElement, span); }
    }
#nullable disable
    public static void SetRowSpan(UIElement element, int span) { ArgumentNullException.ThrowIfNull(element); element.SetValue(RowSpanProperty, span); }
#nullable restore
    public static int GetRowSpan(FrameworkElement element) => (int)element.GetValue(RowSpanProperty)!;
#nullable disable
    public static void SetColumnSpan(UIElement element, int span) { ArgumentNullException.ThrowIfNull(element); element.SetValue(ColumnSpanProperty, span); }
#nullable restore
    public static int GetColumnSpan(FrameworkElement element) => (int)element.GetValue(ColumnSpanProperty)!;
}
public enum Orientation { Horizontal, Vertical }
public class StackPanel : Panel
{
    public StackPanel() : base(new global::ProjectZ.Shared.Drawing.UI.Layout.StackPanel(Application.Current.ControlScene)) { }
    internal StackPanel(SceneElement native) : base(native) { }
    public Orientation Orientation
    {
        get => Enum.Parse<Orientation>(((global::ProjectZ.Shared.Drawing.UI.Layout.StackPanel)NativeElement).Orientation.ToString());
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            ((global::ProjectZ.Shared.Drawing.UI.Layout.StackPanel)NativeElement).Orientation = Enum.Parse<global::ProjectZ.Shared.Drawing.UI.Layout.Orientation>(value.ToString());
            MarkupSetter?.Invoke(nameof(Orientation), value); Changed(nameof(Orientation));
        }
    }
}
public class Canvas : Panel
{
    public Canvas() : this(new global::ProjectZ.Shared.Drawing.UI.Layout.CanvasPanel(Application.Current.ControlScene)) { } internal Canvas(SceneElement native) : base(native) { }
    public static readonly DependencyProperty LeftProperty = DependencyProperty.RegisterAttached("Left", typeof(double), typeof(Canvas), new(double.NaN, (d, e) => SetPosition((UIElement)d, (double)e.NewValue!, true)));
    public static readonly DependencyProperty TopProperty = DependencyProperty.RegisterAttached("Top", typeof(double), typeof(Canvas), new(double.NaN, (d, e) => SetPosition((UIElement)d, (double)e.NewValue!, false)));
    static void SetPosition(UIElement element, double value, bool left)
    {
        var p = element.NativeElement.Position;
        element.NativeElement.Position = left ? new((float)value, p.Y) : new(p.X, (float)value);
        if (element is FrameworkElement child && child.Parent is Canvas canvas) canvas.AttachChild(child);
        if (element is FrameworkElement framework) framework.MarkupSetter?.Invoke(left ? "Canvas.Left" : "Canvas.Top", value);
    }
    public static void SetLeft(UIElement element, double value) => element.SetValue(LeftProperty, value);
    public static void SetTop(UIElement element, double value) => element.SetValue(TopProperty, value);
    public static double GetLeft(UIElement element) => (double)element.GetValue(LeftProperty)!;
    public static double GetTop(UIElement element) => (double)element.GetValue(TopProperty)!;
    internal void AttachChild(FrameworkElement child)
    {
        var left=GetLeft(child); var top=GetTop(child);
        ((global::ProjectZ.Shared.Drawing.UI.Layout.CanvasPanel)NativeElement).SetChildPosition(child.NativeElement,new(double.IsNaN(left)?0:(float)left,double.IsNaN(top)?0:(float)top));
    }
}
public class ButtonBase : ContentControl
{
    public static readonly RoutedEvent ClickEvent = new("Click");
    protected override void AfterRoutedEvent(RoutedEventArgs args)
    {
        if (args.RoutedEvent?.Native == ClickEvent.Native && IsEnabled && !args.Handled && command?.CanExecute(parameter) == true) command.Execute(parameter);
        base.AfterRoutedEvent(args);
    }
    public event RoutedEventHandler? Click
    {
        add { if (value != null) AddHandler(RoutedEvent.For("Click"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("Click"), value); }
    }
    ICommand? command;
    object? parameter;
    public ButtonBase() : this(new Native.Button(Application.Current.ControlScene)) { }
    protected internal ButtonBase(SceneElement native) : base(native)
    {
        Listen("MouseLeftClick", _ => { if (IsEnabled) Emit("Click", Args()); });
    }
    public ICommand? Command { get => command; set { if (command != null) command.CanExecuteChanged -= CanExecuteChanged; command = value; if (command != null) command.CanExecuteChanged += CanExecuteChanged; UpdateEnabled(); } }
    public object? CommandParameter { get => parameter; set { parameter = value; UpdateEnabled(); } }
    void CanExecuteChanged(object? sender, EventArgs e) { if (Dispatcher.CheckAccess()) UpdateEnabled(); else _ = Dispatcher.InvokeAsync(UpdateEnabled); }
    void UpdateEnabled() { if (!IsDisposed && command != null) IsEnabled = command.CanExecute(parameter); }
    public override void Dispose() { if (command != null) command.CanExecuteChanged -= CanExecuteChanged; base.Dispose(); }
}
public class Button : ButtonBase
{
    public Button() : this(new Native.Button(Application.Current.ControlScene)) { }
    internal Button(SceneElement native) : base(native) { }
    public bool IsDefault { get => ((Native.Button)NativeElement).IsDefault; set => ((Native.Button)NativeElement).IsDefault = value; }
    public bool IsCancel { get => ((Native.Button)NativeElement).IsCancel; set => ((Native.Button)NativeElement).IsCancel = value; }
    public override object? Content { get => ((Native.Button)NativeElement).Text; set { ((Native.Button)NativeElement).Text = value?.ToString() ?? ""; Changed(nameof(Content)); } }
}
public class TextChangedEventArgs : RoutedEventArgs { }
public class SelectionChangedEventArgs : RoutedEventArgs
{
    public IList RemovedItems { get; internal set; } = Array.Empty<object>();
    public IList AddedItems { get; internal set; } = Array.Empty<object>();
}
public class TextBox : Control
{
    public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; } = ScrollBarVisibility.Hidden;
    public ScrollBarVisibility VerticalScrollBarVisibility { get; set; } = ScrollBarVisibility.Hidden;
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterNative(nameof(Text), typeof(string), typeof(TextBox), new(""));
    public event EventHandler<TextChangedEventArgs>? TextChanged
    {
        add { if (value != null) AddHandler(RoutedEvent.For("TextChanged"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("TextChanged"), value); }
    }
    public TextBox() : this(new Native.Textbox(Application.Current.ControlScene)) { }
    internal TextBox(SceneElement native) : base(native) { Listen("OnTextChanged", _ => { Changed(nameof(Text)); Emit("TextChanged", new TextChangedEventArgs()); }); }
    public string Text { get => ((Native.Textbox)NativeElement).Text; set => ((Native.Textbox)NativeElement).Text = value; }
    public bool IsReadOnly { get => ((Native.Textbox)NativeElement).IsReadOnly; set => ((Native.Textbox)NativeElement).IsReadOnly = value; }
    public bool AcceptsReturn { get => ((Native.Textbox)NativeElement).AcceptsReturn; set => ((Native.Textbox)NativeElement).AcceptsReturn = value; }
    public int SelectionStart { get => ((Native.Textbox)NativeElement).SelectionStart; set => ((Native.Textbox)NativeElement).SelectionStart = value; }
    public int SelectionLength { get => ((Native.Textbox)NativeElement).SelectionLength; set => ((Native.Textbox)NativeElement).SelectionLength = value; }
    public string SelectedText { get => ((Native.Textbox)NativeElement).SelectedText; set => ((Native.Textbox)NativeElement).ReplaceSelection(value); }
    public bool IsReadOnlyCaretVisible {get=>((Native.Textbox)NativeElement).IsReadOnlyCaretVisible;set=>((Native.Textbox)NativeElement).IsReadOnlyCaretVisible=value;}
    public void SelectAll() => ((Native.Textbox)NativeElement).Select(0, Text.Length);
    public void Select(int start, int length) => ((Native.Textbox)NativeElement).Select(start, length);
    public void Clear() => Text = "";
    public void AppendText(string text) => Text += text;
    public int CaretIndex { get => ((Native.Textbox)NativeElement).CaretPosition; set => ((Native.Textbox)NativeElement).CaretPosition=value; }
    public TextWrapping TextWrapping { get=>((Native.Textbox)NativeElement).WrapText?TextWrapping.Wrap:TextWrapping.NoWrap; set=>((Native.Textbox)NativeElement).WrapText=value!=TextWrapping.NoWrap; }
    public int GetCharacterIndexFromPoint(Point point,bool snapToText) => ((Native.Textbox)NativeElement).GetCharacterIndexFromPoint(new((float)point.X,(float)point.Y),snapToText);
    public Media.Brush CaretBrush { get=>new Media.SolidColorBrush(Media.Color.FromNative(((Native.Textbox)NativeElement).CaretColor)); set { if(value is Media.SolidColorBrush brush) ((Native.Textbox)NativeElement).CaretColor=brush.Color.Native; } }
    public void ScrollToHome() => CaretIndex=0;
    public void ScrollToEnd() => CaretIndex=Text.Length;
    public Media.Brush SelectionBrush { get=>new Media.SolidColorBrush(Media.Color.FromNative(((Native.Textbox)NativeElement).SelectionColor)); set { if(value is Media.SolidColorBrush brush) ((Native.Textbox)NativeElement).SelectionColor=brush.Color.Native; } }
    public double SelectionOpacity { get=>((Native.Textbox)NativeElement).SelectionColor.A/255d; set { var input=(Native.Textbox)NativeElement;var color=input.SelectionColor;color.A=(byte)(Math.Clamp(value,0,1)*255);input.SelectionColor=color; } }
}
public class TextBlock : Control
{
    public double LineHeight {get=>((NativePrimitives.TextElement)NativeElement).LineHeight;set {var text=(NativePrimitives.TextElement)NativeElement;text.LineHeight=(float)value;text.BlockLineHeight=double.IsFinite(value)&&value>0;text.Text=text.Text;}}
    TextDecorationCollection decorations = new();
    public static readonly DependencyProperty TextDecorationsProperty = DependencyProperty.RegisterNative(nameof(TextDecorations), typeof(TextDecorationCollection), typeof(TextBlock));
    public TextDecorationCollection TextDecorations
    {
        get => decorations;
        set { decorations.CollectionChanged -= DecorationsChanged; decorations = value ?? new(); decorations.CollectionChanged += DecorationsChanged; UpdateDecorations(); }
    }
    void DecorationsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => UpdateDecorations();
    void UpdateDecorations() { var text = (NativePrimitives.TextElement)NativeElement; text.Underline = decorations.Any(d => d.Location == TextDecorationLocation.Underline); text.Strikethrough = decorations.Any(d => d.Location == TextDecorationLocation.Strikethrough); text.Overline = decorations.Any(d => d.Location == TextDecorationLocation.OverLine); text.BaselineDecoration = decorations.Any(d => d.Location == TextDecorationLocation.Baseline); }
    public new static readonly DependencyProperty FontFamilyProperty = DependencyProperty.RegisterNative(nameof(FontFamily), typeof(Media.FontFamily), typeof(TextBlock), new(new Media.FontFamily("Segoe UI")));
    public new static readonly DependencyProperty FontWeightProperty = DependencyProperty.RegisterNative(nameof(FontWeight), typeof(FontWeight), typeof(TextBlock), new(FontWeights.Normal));
    public new Media.FontFamily FontFamily
    {
        get => new(((NativePrimitives.TextElement)NativeElement).FontFamily);
        set { ArgumentNullException.ThrowIfNull(value); var size = FontSize; ((NativePrimitives.TextElement)NativeElement).FontFamily = value.Source; FontSize = size; MarkupSetter?.Invoke(nameof(FontFamily), value.Source); Changed(nameof(FontFamily)); }
    }
    public new FontWeight FontWeight
    {
        get => FontWeight.FromOpenTypeWeight(((NativePrimitives.TextElement)NativeElement).FontWeight);
        set { var size = FontSize; ((NativePrimitives.TextElement)NativeElement).FontWeight = value.ToOpenTypeWeight(); FontSize = size; MarkupSetter?.Invoke(nameof(FontWeight), value); Changed(nameof(FontWeight)); }
    }
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterNative(nameof(Text), typeof(string), typeof(TextBlock), new(""));
    public new static readonly DependencyProperty FontSizeProperty = DependencyProperty.RegisterNative(nameof(FontSize), typeof(double), typeof(TextBlock), new(12d));
    public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.RegisterNative(nameof(TextWrapping), typeof(TextWrapping), typeof(TextBlock), new(TextWrapping.NoWrap));
    public static readonly DependencyProperty TextTrimmingProperty = DependencyProperty.RegisterNative(nameof(TextTrimming), typeof(TextTrimming), typeof(TextBlock), new(TextTrimming.None));
    public TextBlock() : this(new NativePrimitives.TextElement(Application.Current.ControlScene) { FontScale = 0.75f, Padding = new(0), UseXamlTextLayout = true, TextWrapping = NativePrimitives.TextWrapping.NoWrap }) { }
    internal TextBlock(SceneElement native) : base(native) { decorations.CollectionChanged += DecorationsChanged; }
    public override void Dispose() { decorations.CollectionChanged -= DecorationsChanged; base.Dispose(); }
    public string Text { get => ((NativePrimitives.TextElement)NativeElement).Text; set { ((NativePrimitives.TextElement)NativeElement).Text = value; Changed(nameof(Text)); } }
    double FontPoints
    {
        get { var font = ((NativePrimitives.TextElement)NativeElement).Font; return double.TryParse(font[(Math.Max(font.LastIndexOf('_'), font.LastIndexOf(':')) + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var points) && points > 0 ? points : 12; }
    }
    public new double FontSize
    {
        get => ((NativePrimitives.TextElement)NativeElement).FontScale * FontPoints / 0.75;
        set
        {
            if (!double.IsFinite(value) || value <= 0 || value > 512) throw new ArgumentOutOfRangeException(nameof(value));
            var text = (NativePrimitives.TextElement)NativeElement; text.FontScale = (float)(value * 0.75 / FontPoints); text.Text = text.Text;
            MarkupSetter?.Invoke(nameof(FontSize), value); Changed(nameof(FontSize));
        }
    }
    public TextWrapping TextWrapping
    {
        get => Enum.Parse<TextWrapping>(((NativePrimitives.TextElement)NativeElement).TextWrapping.ToString());
        set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); ((NativePrimitives.TextElement)NativeElement).TextWrapping = Enum.Parse<NativePrimitives.TextWrapping>(value.ToString()); MarkupSetter?.Invoke(nameof(TextWrapping), value); Changed(nameof(TextWrapping)); }
    }
    public TextTrimming TextTrimming
    {
        get => Enum.Parse<TextTrimming>(((NativePrimitives.TextElement)NativeElement).TextTrimming.ToString());
        set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); var text = (NativePrimitives.TextElement)NativeElement; text.TextTrimming = Enum.Parse<NativePrimitives.TextTrimming>(value.ToString()); text.Clip = value != TextTrimming.None || text.MaxSize.Y < float.MaxValue; MarkupSetter?.Invoke(nameof(TextTrimming), value); Changed(nameof(TextTrimming)); }
    }
}
public class CheckBox : ButtonBase
{
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.RegisterNative(nameof(IsChecked), typeof(bool?), typeof(CheckBox));
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
    public event RoutedEventHandler? Indeterminate
    {
        add { if (value != null) AddHandler(RoutedEvent.For("Indeterminate"), value); }
        remove { if (value != null) RemoveHandler(RoutedEvent.For("Indeterminate"), value); }
    }
    public CheckBox() : this(new Native.CheckBox(Application.Current.ControlScene)) { }
    internal CheckBox(SceneElement native) : base(native)
    {
        Listen("CheckedChanged", _ => Changed(nameof(IsChecked)));
        Listen("Checked", _ => Emit("Checked", Args())); Listen("Unchecked", _ => Emit("Unchecked", Args())); Listen("Indeterminate", _ => Emit("Indeterminate", Args()));
    }
    public bool? IsChecked { get => ((Native.CheckBox)NativeElement).IsChecked; set => ((Native.CheckBox)NativeElement).IsChecked = value; }
    public bool IsThreeState { get => ((Native.CheckBox)NativeElement).IsThreeState; set => ((Native.CheckBox)NativeElement).IsThreeState = value; }
    public override object? Content { get => ((Native.CheckBox)NativeElement).Content; set => ((Native.CheckBox)NativeElement).Content = value?.ToString() ?? ""; }
}
public class ProgressBar : Control
{
    public Orientation Orientation { get => (Orientation)((Native.ProgressBar)NativeElement).Orientation; set => ((Native.ProgressBar)NativeElement).Orientation = (global::ProjectZ.Shared.Drawing.UI.Layout.Orientation)value; }
    public bool IsIndeterminate { get=>((Native.ProgressBar)NativeElement).IsIndeterminate;set=>((Native.ProgressBar)NativeElement).IsIndeterminate=value; }
    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterNative(nameof(Value), typeof(double), typeof(ProgressBar), new(0d));
    public ProgressBar() : this(new Native.ProgressBar(Application.Current.ControlScene)) { }
    internal ProgressBar(SceneElement native) : base(native) { Listen("ValueChanged", _ => Changed(nameof(Value))); }
    public double Value { get => ((Native.ProgressBar)NativeElement).Value; set => ((Native.ProgressBar)NativeElement).Value = value; }
    public double Minimum { get => ((Native.ProgressBar)NativeElement).Minimum; set => ((Native.ProgressBar)NativeElement).Minimum = value; }
    public double Maximum { get => ((Native.ProgressBar)NativeElement).Maximum; set => ((Native.ProgressBar)NativeElement).Maximum = value; }
}
public enum SelectionMode { Single, Multiple, Extended }
public class ListBox : ItemsControl
{
    public ListBox() : this(new Native.ListBox(Application.Current.ControlScene)) { }
    internal ListBox(SceneElement native) : base(native) { SelectedItems=new SelectedItemCollection(this); }
    public IList SelectedItems { get; }
    public SelectionMode SelectionMode
    {
        get=>(SelectionMode)((Native.ListBox)NativeElement).SelectionMode;
        set {if(!Enum.IsDefined(value))throw new ArgumentOutOfRangeException(nameof(value));((Native.ListBox)NativeElement).SelectionMode=(int)value;if(value==SelectionMode.Single && SelectedItems.Count>1)SelectedIndex=SelectedIndex;Changed(nameof(SelectionMode));}
    }
    public void ScrollIntoView(object? item)
    {
        Dispatcher.VerifyAccess();
        int index = Items.IndexOf(item);
        if (index >= 0) ((Native.ListBox)NativeElement).ScrollIntoView(index);
    }
}
public class ComboBox : ItemsControl
{
    public bool IsEditable { get => ((Native.ComboBox)NativeElement).IsEditable; set => ((Native.ComboBox)NativeElement).IsEditable = value; }
    public bool IsTextSearchEnabled { get; set; } = true;
    public double MaxDropDownHeight { get; set; } = 300;
    public ComboBox() : this(new Native.ComboBox(Application.Current.ControlScene)) { }
    internal ComboBox(SceneElement native) : base(native) { Listen("TextChanged", _ => Changed(nameof(Text))); }
    public bool IsDropDownOpen { get => ((Native.ComboBox)NativeElement).IsDropDownOpen; set { Dispatcher.VerifyAccess(); ((Native.ComboBox)NativeElement).IsDropDownOpen = value; } }
    public string Text
    {
        get => IsEditable ? ((Native.ComboBox)NativeElement).EditableText : SelectedItem == null ? "" : DisplayText(SelectedItem);
        set { Dispatcher.VerifyAccess(); if (IsEditable) ((Native.ComboBox)NativeElement).EditableText = value; else SelectedIndex = Items.Cast<object?>().ToList().FindIndex(item => DisplayText(item) == value); Changed(nameof(Text)); }
    }
}
internal static class ControlFactory
{
    internal static FrameworkElement Wrap(SceneElement native, string? name = null) => name switch
    {
        "ItemsControl" => new ItemsControl(native), "TabControl" => new TabControl(native), "TabItem" => new TabItem(native), "Menu" => new Menu(native), "ToggleButton" => new ToggleButton(native),
        "DockPanel" => new DockPanel(native), "Image" => new Image(native), "RichTextBox" => new RichTextBox(native), "ListView" => new ListView(native), "ContextMenu" => new ContextMenu(native), "MenuItem" => new MenuItem(native), "ToolTip" => new ToolTip(native), "Decorator" => new Decorator(native), "EmptyControl" => new EmptyControl(native), "Polygon" => new Shapes.Polygon(native), "Rectangle" => new Shapes.Rectangle(native),
        "Separator" => new Separator(native), "Slider" => new Slider(native), "PasswordBox" => new PasswordBox(native), "RadioButton" => new RadioButton(native), "Label" => new Label(native), "WrapPanel" => new WrapPanel(native), "ScrollViewer" => new ScrollViewer(native), "Expander" => new Expander(native), "GroupBox" => new GroupBox(native),
        "ContentControl" => new ContentControl(native), "UniformGrid" => new UniformGrid(native), "Grid" => new Grid(native), "StackPanel" => new StackPanel(native), "Canvas" => new Canvas(native), "Border" => new Border(native), "UserControl" or "Page" => new UserControl(native),
        _ => native switch { Native.Button => new Button(native), Native.Textbox => new TextBox(native), NativePrimitives.TextElement => new TextBlock(native), Native.CheckBox => new CheckBox(native), Native.ProgressBar => new ProgressBar(native), Native.ComboBox => new ComboBox(native), Native.ListBox => new ListBox(native), _ => new Panel(native) }
    };
}
