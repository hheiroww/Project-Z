using System.Globalization;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.WpfCompatibility.Controls;

public partial class Control
{
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.RegisterNative(nameof(Background), typeof(Media.Brush), typeof(Control));
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.RegisterNative(nameof(Foreground), typeof(Media.Brush), typeof(Control));
    public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.RegisterNative(nameof(BorderBrush), typeof(Media.Brush), typeof(Control));
    public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.RegisterNative(nameof(BorderThickness), typeof(Thickness), typeof(Control), new(new Thickness(0)));
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterNative(nameof(CornerRadius), typeof(CornerRadius), typeof(Control), new(new CornerRadius(0)));
    public CornerRadius CornerRadius
    {
        get => NativeElement is RectangleElement rectangle ? new(rectangle.CornerRadii.TopLeft, rectangle.CornerRadii.TopRight, rectangle.CornerRadii.BottomRight, rectangle.CornerRadii.BottomLeft) : new(0);
        set
        {
            if (NativeElement is RectangleElement rectangle) rectangle.CornerRadii = new((float)value.TopLeft, (float)value.TopRight, (float)value.BottomRight, (float)value.BottomLeft);
            MarkupSetter?.Invoke(nameof(CornerRadius), value); Changed(nameof(CornerRadius));
        }
    }
    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.RegisterNative(nameof(FontFamily), typeof(Media.FontFamily), typeof(Control), new(new Media.FontFamily("Segoe UI")));
    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.RegisterNative(nameof(FontSize), typeof(double), typeof(Control), new(12d));
    public static readonly DependencyProperty FontWeightProperty = DependencyProperty.RegisterNative(nameof(FontWeight), typeof(FontWeight), typeof(Control), new(FontWeights.Normal));
    public static readonly DependencyProperty HorizontalContentAlignmentProperty = DependencyProperty.RegisterNative(nameof(HorizontalContentAlignment), typeof(HorizontalAlignment), typeof(Control), new(HorizontalAlignment.Left));
    public static readonly DependencyProperty VerticalContentAlignmentProperty = DependencyProperty.RegisterNative(nameof(VerticalContentAlignment), typeof(VerticalAlignment), typeof(Control), new(VerticalAlignment.Top));
    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.RegisterNative(nameof(TextAlignment), typeof(TextAlignment), typeof(Control), new(TextAlignment.Left));
    Media.FontFamily family = new("Segoe UI");
    double fontSize = 12;
    FontWeight weight = FontWeights.Normal;
    public Media.FontFamily FontFamily { get => family; set { ArgumentNullException.ThrowIfNull(value); family = value; ApplyFont(); } }
    public double FontSize { get => fontSize; set { if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); fontSize = value; ApplyFont(); } }
    public FontWeight FontWeight { get => weight; set { weight = value; ApplyFont(); } }
    void ApplyFont()
    {
        Dispatcher.VerifyAccess();
        var name = ContentContainer.SystemFontName(family.Source, weight.ToOpenTypeWeight(), (float)(fontSize * .75));
        Apply(NativeElement);
        void Apply(SceneElement element)
        {
            if (element.IsDisposed) return;
            element.GetType().GetProperty("Font")?.SetValue(element, name);
            if (element is TextElement text) { text.UseXamlTextLayout = true; text.FontScale = 1; text.Text = text.Text; }
            foreach (var child in element.Children.ToArray()) Apply(child);
        }
        foreach (var child in LogicalChildren.OfType<Control>().ToArray())
        {
            if (child.IsDisposed || child.Parent != this) continue;
            child.family = family; child.fontSize = fontSize; child.weight = weight; child.ApplyFont();
        }
        foreach (var property in new[] { nameof(FontFamily), nameof(FontSize), nameof(FontWeight) }) Changed(property);
    }
    public Media.Brush? BorderBrush
    {
        get => NativeElement is RectangleElement rectangle ? new Media.SolidColorBrush(Media.Color.FromNative(rectangle.BorderColor)) : null;
        set { if (NativeElement is RectangleElement rectangle) rectangle.BorderColor = value is Media.SolidColorBrush solid ? solid.Color.Native : Microsoft.Xna.Framework.Color.Transparent; Changed(nameof(BorderBrush)); }
    }
    public Thickness BorderThickness
    {
        get => NativeElement is RectangleElement rectangle ? new(rectangle.BorderThickness) : new(0);
        set { if (NativeElement is RectangleElement rectangle) rectangle.BorderThickness = (float)Math.Max(Math.Max(value.Left, value.Right), Math.Max(value.Top, value.Bottom)); Changed(nameof(BorderThickness)); }
    }
    HorizontalAlignment horizontalContent;
    VerticalAlignment verticalContent;
    public HorizontalAlignment HorizontalContentAlignment
    {
        get => horizontalContent;
        set { horizontalContent = value; NativeElement.GetType().GetProperty("HorizontalTextAlignment")?.SetValue(NativeElement, Enum.Parse<global::ProjectZ.Shared.Drawing.UI.HorizontalAlignment>(value.ToString())); foreach (var text in NativeElement.Children.OfType<TextElement>()) text.TextAlignment = Enum.Parse<global::ProjectZ.Shared.Drawing.UI.HorizontalAlignment>(value.ToString()); Changed(nameof(HorizontalContentAlignment)); }
    }
    public VerticalAlignment VerticalContentAlignment
    {
        get => verticalContent;
        set { verticalContent = value; NativeElement.GetType().GetProperty("VerticalTextAlignment")?.SetValue(NativeElement, Enum.Parse<global::ProjectZ.Shared.Drawing.UI.VerticalAlignment>(value.ToString())); Changed(nameof(VerticalContentAlignment)); }
    }
    TextAlignment textAlignment;
    public TextAlignment TextAlignment
    {
        get => textAlignment;
        set
        {
            textAlignment = value;
            var nativeAlignment = value switch { TextAlignment.Center => global::ProjectZ.Shared.Drawing.UI.HorizontalAlignment.Center, TextAlignment.Right => global::ProjectZ.Shared.Drawing.UI.HorizontalAlignment.Right, _ => global::ProjectZ.Shared.Drawing.UI.HorizontalAlignment.Left };
            if (NativeElement is TextElement text) text.TextAlignment = nativeAlignment;
            NativeElement.GetType().GetProperty("HorizontalTextAlignment")?.SetValue(NativeElement, nativeAlignment);
            Changed(nameof(TextAlignment));
        }
    }
}
