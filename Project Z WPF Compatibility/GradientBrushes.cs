using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using Native = ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.WpfCompatibility.Media;

public enum BrushMappingMode { Absolute, RelativeToBoundingBox }
public enum GradientSpreadMethod { Pad, Reflect, Repeat }
public sealed class GradientStop : Animation.Animatable
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.RegisterNative(nameof(Color),typeof(Color),typeof(GradientStop));
    internal Native.XamlGradientStop Native { get; }
    public GradientStop() : this(new Native.XamlGradientStop()) { }
    public GradientStop(Color color, double offset) : this() { Color = color; Offset = offset; }
    internal GradientStop(Native.XamlGradientStop native) => Native = native;
    public Color Color { get => Color.FromNative(Native.Color); set => Native.Color = value.Native; }
    public double Offset { get => Native.Offset; set => Native.Offset = (float)value; }
}
public sealed class GradientStopCollection : Collection<GradientStop>
{
    readonly Native.XamlGradientBrush native;
    internal GradientStopCollection(Native.XamlGradientBrush brush) { native = brush; foreach (var stop in brush.Stops) Items.Add(new(stop)); }
    protected override void InsertItem(int index, GradientStop item) { ArgumentNullException.ThrowIfNull(item); native.Stops.Insert(index, item.Native); base.InsertItem(index, item); }
    protected override void SetItem(int index, GradientStop item) { ArgumentNullException.ThrowIfNull(item); native.Stops[index] = item.Native; base.SetItem(index, item); }
    protected override void RemoveItem(int index) { native.Stops.RemoveAt(index); base.RemoveItem(index); }
    protected override void ClearItems() { native.Stops.Clear(); base.ClearItems(); }
}
public abstract class GradientBrush : Brush
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Native.XamlGradientBrush, GradientBrush> adapters = new();
    internal Native.XamlGradientBrush Native { get; }
    protected GradientBrush(Native.XamlGradientBrush native) { Native = native; GradientStops = new(native); adapters.Add(native, this); }
    public GradientStopCollection GradientStops { get; }
    public override double Opacity { get => Native.Opacity; set { Native.Opacity = (float)value;Changed(nameof(Opacity)); } }
    public BrushMappingMode MappingMode { get => Native.AbsoluteMapping ? BrushMappingMode.Absolute : BrushMappingMode.RelativeToBoundingBox; set => Native.AbsoluteMapping = value == BrushMappingMode.Absolute; }
    public GradientSpreadMethod SpreadMethod { get => Enum.Parse<GradientSpreadMethod>(Native.SpreadMethod); set => Native.SpreadMethod = value.ToString(); }
    internal static GradientBrush Wrap(Native.XamlGradientBrush native) => adapters.TryGetValue(native, out var brush) ? brush : native.IsRadial ? new RadialGradientBrush(native) : new LinearGradientBrush(native);
    protected static Vector2 Vector(Point value) => new((float)value.X, (float)value.Y);
    protected static Point Point(Vector2 value) => new(value.X, value.Y);
}
public sealed class LinearGradientBrush : GradientBrush
{
    public LinearGradientBrush() : base(new Native.XamlGradientBrush()) { }
    internal LinearGradientBrush(Native.XamlGradientBrush native) : base(native) { }
    public LinearGradientBrush(Color startColor, Color endColor, Point startPoint, Point endPoint) : this() { StartPoint = startPoint; EndPoint = endPoint; GradientStops.Add(new(startColor, 0)); GradientStops.Add(new(endColor, 1)); }
    public LinearGradientBrush(Color startColor, Color endColor, double angle) : this()
    {
        double radians = angle * Math.PI / 180;
        double dx = Math.Cos(radians), dy = Math.Sin(radians);
        double scale = Math.Max(Math.Abs(dx), Math.Abs(dy));
        StartPoint = new(0.5 - dx / scale / 2, 0.5 - dy / scale / 2); EndPoint = new(1 - StartPoint.X, 1 - StartPoint.Y);
        GradientStops.Add(new(startColor, 0)); GradientStops.Add(new(endColor, 1));
    }
    public Point StartPoint { get => Point(Native.StartPoint); set => Native.StartPoint = Vector(value); }
    public Point EndPoint { get => Point(Native.EndPoint); set => Native.EndPoint = Vector(value); }
}
public sealed class RadialGradientBrush : GradientBrush
{
    public RadialGradientBrush() : base(new Native.XamlGradientBrush { IsRadial = true }) { }
    internal RadialGradientBrush(Native.XamlGradientBrush native) : base(native) { }
    public Point Center { get => Point(Native.Center); set => Native.Center = Vector(value); }
    public Point GradientOrigin { get => Point(Native.GradientOrigin); set => Native.GradientOrigin = Vector(value); }
    public double RadiusX { get => Native.Radius.X; set => Native.Radius = new((float)value, Native.Radius.Y); }
    public double RadiusY { get => Native.Radius.Y; set => Native.Radius = new(Native.Radius.X, (float)value); }
}
