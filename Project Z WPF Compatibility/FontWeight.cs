using System.Globalization;

namespace ProjectZ.WpfCompatibility;

public readonly record struct FontWeight : IFormattable
{
    readonly int delta;
    FontWeight(int weight) { delta = weight - 400; }
    public int ToOpenTypeWeight() => delta + 400;
    public static FontWeight FromOpenTypeWeight(int weight) => weight is >= 1 and <= 999 ? new(weight) : throw new ArgumentOutOfRangeException(nameof(weight));
    public static int Compare(FontWeight left, FontWeight right) => left.delta.CompareTo(right.delta);
    public static bool operator <(FontWeight left, FontWeight right) => left.delta < right.delta;
    public static bool operator >(FontWeight left, FontWeight right) => left.delta > right.delta;
    public static bool operator <=(FontWeight left, FontWeight right) => left.delta <= right.delta;
    public static bool operator >=(FontWeight left, FontWeight right) => left.delta >= right.delta;
    public override string ToString() => FontWeights.Name(ToOpenTypeWeight());
    public string ToString(string? format, IFormatProvider? provider) => ToString();
}

public static class FontWeights
{
    public static FontWeight Thin => FontWeight.FromOpenTypeWeight(100);
    public static FontWeight ExtraLight => FontWeight.FromOpenTypeWeight(200);
    public static FontWeight UltraLight => ExtraLight;
    public static FontWeight Light => FontWeight.FromOpenTypeWeight(300);
    public static FontWeight Normal => FontWeight.FromOpenTypeWeight(400);
    public static FontWeight Regular => Normal;
    public static FontWeight Medium => FontWeight.FromOpenTypeWeight(500);
    public static FontWeight DemiBold => FontWeight.FromOpenTypeWeight(600);
    public static FontWeight SemiBold => DemiBold;
    public static FontWeight Bold => FontWeight.FromOpenTypeWeight(700);
    public static FontWeight ExtraBold => FontWeight.FromOpenTypeWeight(800);
    public static FontWeight UltraBold => ExtraBold;
    public static FontWeight Black => FontWeight.FromOpenTypeWeight(900);
    public static FontWeight Heavy => Black;
    public static FontWeight ExtraBlack => FontWeight.FromOpenTypeWeight(950);
    public static FontWeight UltraBlack => ExtraBlack;
    internal static FontWeight Parse(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight)) return FontWeight.FromOpenTypeWeight(weight);
        var property = typeof(FontWeights).GetProperties().FirstOrDefault(p => string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase));
        return property == null ? throw new FormatException("Invalid font weight " + value) : (FontWeight)property.GetValue(null)!;
    }
    internal static string Name(int weight) => weight switch
    {
        100 => "Thin", 200 => "ExtraLight", 300 => "Light", 400 => "Normal", 500 => "Medium", 600 => "SemiBold", 700 => "Bold", 800 => "ExtraBold", 900 => "Black", 950 => "ExtraBlack", _ => weight.ToString(CultureInfo.InvariantCulture)
    };
}
