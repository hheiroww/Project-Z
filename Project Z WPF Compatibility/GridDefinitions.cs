using System.Collections.ObjectModel;
using Layout = ProjectZ.Shared.Drawing.UI.Layout;

namespace ProjectZ.WpfCompatibility.Controls;
public abstract class DefinitionBase : DependencyObject
{
    internal readonly Layout.GridDefinition Native;
    internal Grid? Owner;
    protected DefinitionBase() : this(Layout.GridDefinition.StarSize()) { }
    internal DefinitionBase(Layout.GridDefinition native) => Native = native;
    protected GridLength Length
    {
        get
        {
            var mode = Native.SizeMode ?? (Native.Size < 0 ? Layout.GridSizeMode.Auto : Native.Size == 0 ? Layout.GridSizeMode.Star : Layout.GridSizeMode.Pixel);
            return mode == Layout.GridSizeMode.Auto ? GridLength.Auto : mode == Layout.GridSizeMode.Star ? new(Native.Star, GridUnitType.Star) : new(Native.Size);
        }
        set { Native.SizeMode = value.IsAuto ? Layout.GridSizeMode.Auto : value.IsStar ? Layout.GridSizeMode.Star : Layout.GridSizeMode.Pixel; Native.Size = value.IsAuto ? -1 : value.IsStar ? 0 : (float)value.Value; Native.Star = value.IsStar ? (float)value.Value : 1; Owner?.InvalidateDefinitions(); }
    }
    protected double Minimum { get => Native.MinSize; set { if (!double.IsFinite(value) || value < 0 || value > float.MaxValue) throw new ArgumentOutOfRangeException(nameof(value)); Native.MinSize = (float)value; Owner?.InvalidateDefinitions(); } }
    protected double Maximum { get => Native.MaxSize == float.MaxValue ? double.PositiveInfinity : Native.MaxSize; set { if (double.IsNaN(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Native.MaxSize = (float)Math.Min(value, float.MaxValue); Owner?.InvalidateDefinitions(); } }
}
public sealed class RowDefinition : DefinitionBase
{
    public static readonly DependencyProperty HeightProperty = DependencyProperty.RegisterNative(nameof(Height), typeof(GridLength), typeof(RowDefinition));
    public GridLength Height { get => Length; set => Length = value; }
    public double MinHeight { get => Minimum; set => Minimum = value; }
    public double MaxHeight { get => Maximum; set => Maximum = value; }
    public double ActualHeight => Native.ActualSize;
    public RowDefinition() { }
    internal RowDefinition(Layout.GridDefinition native) : base(native) { }
}
public sealed class ColumnDefinition : DefinitionBase
{
    public static readonly DependencyProperty WidthProperty = DependencyProperty.RegisterNative(nameof(Width), typeof(GridLength), typeof(ColumnDefinition));
    public GridLength Width { get => Length; set => Length = value; }
    public double MinWidth { get => Minimum; set => Minimum = value; }
    public double MaxWidth { get => Maximum; set => Maximum = value; }
    public double ActualWidth => Native.ActualSize;
    public ColumnDefinition() { }
    internal ColumnDefinition(Layout.GridDefinition native) : base(native) { }
}
public abstract class DefinitionCollection<T> : Collection<T> where T : DefinitionBase
{
    readonly Grid owner;
    readonly List<Layout.GridDefinition> native;
    internal DefinitionCollection(Grid owner, List<Layout.GridDefinition> native, Func<Layout.GridDefinition, T> wrap)
    {
        this.owner = owner; this.native = native;
        foreach (var item in native) { var definition = wrap(item); definition.Owner = owner; Items.Add(definition); }
    }
    void Check(T item) { ArgumentNullException.ThrowIfNull(item); if (item.Owner != null) throw new InvalidOperationException("A grid definition already belongs to a grid."); }
    protected override void InsertItem(int index, T item) { Check(item); base.InsertItem(index, item); native.Insert(index, item.Native); item.Owner = owner; owner.InvalidateDefinitions(); }
    protected override void RemoveItem(int index) { this[index].Owner = null; base.RemoveItem(index); native.RemoveAt(index); owner.InvalidateDefinitions(); }
    protected override void SetItem(int index, T item) { if (ReferenceEquals(item, this[index])) return; Check(item); this[index].Owner = null; base.SetItem(index, item); native[index] = item.Native; item.Owner = owner; owner.InvalidateDefinitions(); }
    protected override void ClearItems() { foreach (var item in this) item.Owner = null; base.ClearItems(); native.Clear(); owner.InvalidateDefinitions(); }
    public void RemoveRange(int index, int count) { if (index < 0 || count < 0 || index > Count - count) throw new ArgumentOutOfRangeException(); for (int i = 0; i < count; i++) RemoveAt(index); }
}
public sealed class RowDefinitionCollection : DefinitionCollection<RowDefinition>
{
    internal RowDefinitionCollection(Grid owner, List<Layout.GridDefinition> native) : base(owner, native, n => new(n)) { }
}
public sealed class ColumnDefinitionCollection : DefinitionCollection<ColumnDefinition>
{
    internal ColumnDefinitionCollection(Grid owner, List<Layout.GridDefinition> native) : base(owner, native, n => new(n)) { }
}
