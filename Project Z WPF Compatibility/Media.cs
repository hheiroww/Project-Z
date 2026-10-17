namespace ProjectZ.WpfCompatibility.Media;

public class ColorConverter
{
    public static object ConvertFromString(string value)=>Data.BindingExpression.Coerce(value,typeof(Color))!;
}

public sealed class FontFamily : IEquatable<FontFamily>
{
    public string Source { get; }
    public FontFamily(string familyName) { if (string.IsNullOrWhiteSpace(familyName)) throw new ArgumentException("Font family is required.", nameof(familyName)); Source = familyName; }
    public override string ToString() => Source;
    public bool Equals(FontFamily? other) => other != null && string.Equals(Source, other.Source, StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object? value) => value is FontFamily family && Equals(family);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Source);
}

public abstract class Brush : Animation.Animatable
{
    public Transform? RelativeTransform { get; set; }
    double opacity=1;
    public virtual double Opacity { get=>opacity;set {opacity=Math.Clamp(value,0,1);Changed(nameof(Opacity));} }
}
public sealed class SolidColorBrush(Color color) : Brush
{
    public SolidColorBrush() : this(Colors.Transparent) { }
    public static readonly DependencyProperty ColorProperty = DependencyProperty.RegisterNative(nameof(Color), typeof(Color), typeof(SolidColorBrush));
    Color current = color;
    public Color Color { get => current; set { current = value; Changed(nameof(Color)); } }
    internal Microsoft.Xna.Framework.Color RenderColor => Microsoft.Xna.Framework.Color.FromNonPremultiplied(Color.R,Color.G,Color.B,Color.A)*(float)Opacity;
}
public readonly record struct Color(byte A, byte R, byte G, byte B)
{
    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);
    public static Color FromRgb(byte r, byte g, byte b) => new(255, r, g, b);
    internal Microsoft.Xna.Framework.Color Native => new(R, G, B, A);
    internal static Color FromNative(Microsoft.Xna.Framework.Color c) => new(c.A, c.R, c.G, c.B);
    public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}
public static partial class Colors
{
    public static Color White => Color.FromRgb(255, 255, 255);
    public static Color Black => Color.FromRgb(0, 0, 0);
    public static Color Red => Color.FromRgb(255, 0, 0);
    public static Color Green => Color.FromRgb(0, 128, 0);
    public static Color Blue => Color.FromRgb(0, 0, 255);
    public static Color Transparent => new(0, 255, 255, 255);
}
public static partial class Brushes
{
    public static SolidColorBrush White => new(Colors.White); public static SolidColorBrush Black => new(Colors.Black);
    public static SolidColorBrush Red => new(Colors.Red); public static SolidColorBrush Green => new(Colors.Green);
    public static SolidColorBrush Blue => new(Colors.Blue); public static SolidColorBrush Transparent => new(Colors.Transparent);
}
