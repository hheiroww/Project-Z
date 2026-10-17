using System.Globalization;

namespace ProjectZ.WpfCompatibility;
public enum GridUnitType { Auto, Pixel, Star }
public readonly struct GridLength : IEquatable<GridLength>
{
    public double Value { get; }
    public GridUnitType GridUnitType { get; }
    public bool IsAbsolute => GridUnitType == GridUnitType.Pixel;
    public bool IsAuto => GridUnitType == GridUnitType.Auto;
    public bool IsStar => GridUnitType == GridUnitType.Star;
    public static GridLength Auto => new(1, GridUnitType.Auto);
    public GridLength(double pixels) : this(pixels, GridUnitType.Pixel) { }
    public GridLength(double value, GridUnitType type)
    {
        if (!double.IsFinite(value) || value < 0 || value > float.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
        if (type < GridUnitType.Auto || type > GridUnitType.Star) throw new ArgumentOutOfRangeException(nameof(type));
        Value = type == GridUnitType.Auto ? 1 : value; GridUnitType = type;
    }
    public bool Equals(GridLength other) => Value == other.Value && GridUnitType == other.GridUnitType;
    public override bool Equals(object? value) => value is GridLength length && Equals(length);
    public override int GetHashCode() => HashCode.Combine(Value, GridUnitType);
    public static bool operator ==(GridLength left, GridLength right) => left.Equals(right);
    public static bool operator !=(GridLength left, GridLength right) => !left.Equals(right);
    public override string ToString() => IsAuto ? "Auto" : Value.ToString(CultureInfo.InvariantCulture) + (IsStar ? "*" : "");
}
