namespace ProjectZ.WpfCompatibility.Media;

public abstract class Brush { }
public sealed class SolidColorBrush(Color color) : Brush { public Color Color { get; set; } = color; }
public readonly record struct Color(byte A, byte R, byte G, byte B)
{
    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);
    public static Color FromRgb(byte r, byte g, byte b) => new(255, r, g, b);
    internal Microsoft.Xna.Framework.Color Native => new(R, G, B, A);
    internal static Color FromNative(Microsoft.Xna.Framework.Color c) => new(c.A, c.R, c.G, c.B);
}
public static class Colors
{
    public static Color White => Color.FromRgb(255, 255, 255);
    public static Color Black => Color.FromRgb(0, 0, 0);
    public static Color Red => Color.FromRgb(255, 0, 0);
    public static Color Green => Color.FromRgb(0, 128, 0);
    public static Color Blue => Color.FromRgb(0, 0, 255);
    public static Color Transparent => new(0, 0, 0, 0);
}
public static class Brushes
{
    public static SolidColorBrush White => new(Colors.White); public static SolidColorBrush Black => new(Colors.Black);
    public static SolidColorBrush Red => new(Colors.Red); public static SolidColorBrush Green => new(Colors.Green);
    public static SolidColorBrush Blue => new(Colors.Blue); public static SolidColorBrush Transparent => new(Colors.Transparent);
}
