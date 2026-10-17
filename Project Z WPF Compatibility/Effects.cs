using Native = ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.WpfCompatibility.Media.Effects;

public abstract class Effect : DependencyObject
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Native.XamlNativeEffect, Effect> adapters = new();
    internal Native.XamlNativeEffect Native { get; }
    protected Effect(Native.XamlNativeEffect native) { Native = native; adapters.Add(native, this); }
    internal static Effect Wrap(Native.XamlNativeEffect native) => adapters.TryGetValue(native, out var effect) ? effect : native.Kind switch
    {
        global::ProjectZ.Shared.Drawing.UI.Primitives.XamlEffectKind.Blur => new BlurEffect(native),
        global::ProjectZ.Shared.Drawing.UI.Primitives.XamlEffectKind.DropShadow => new DropShadowEffect(native),
        _ => throw new NotSupportedException("No code-behind adapter exists for " + native.Kind)
    };
}
public sealed class BlurEffect : Effect
{
    public BlurEffect() : this(new Native.XamlNativeEffect { Kind = global::ProjectZ.Shared.Drawing.UI.Primitives.XamlEffectKind.Blur, Radius = 5 }) { }
    internal BlurEffect(Native.XamlNativeEffect native) : base(native) { }
    public double Radius { get => Native.Radius; set => Native.Radius = (float)value; }
}
public sealed class DropShadowEffect : Effect
{
    public static readonly DependencyProperty OpacityProperty = DependencyProperty.RegisterNative(nameof(Opacity),typeof(double),typeof(DropShadowEffect),new(1d));
    public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.RegisterNative(nameof(BlurRadius),typeof(double),typeof(DropShadowEffect),new(5d));
    public static readonly DependencyProperty ColorProperty = DependencyProperty.RegisterNative(nameof(Color),typeof(Color),typeof(DropShadowEffect),new(Colors.Black));
    public DropShadowEffect() : this(new Native.XamlNativeEffect { Kind = global::ProjectZ.Shared.Drawing.UI.Primitives.XamlEffectKind.DropShadow, Radius = 5, ShadowDepth = 5, Direction = 315, Opacity = 1, Color = Microsoft.Xna.Framework.Color.Black }) { }
    internal DropShadowEffect(Native.XamlNativeEffect native) : base(native) { }
    public double BlurRadius { get => Native.Radius; set => Native.Radius = (float)value; }
    public double ShadowDepth { get => Native.ShadowDepth; set => Native.ShadowDepth = (float)value; }
    public double Direction { get => Native.Direction; set => Native.Direction = (float)value; }
    public double Opacity { get => Native.Opacity; set => Native.Opacity = (float)value; }
    public Color Color { get => Color.FromNative(Native.Color); set => Native.Color = value.Native; }
}
