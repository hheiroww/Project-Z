namespace ProjectZ.WpfCompatibility
{
    /// <summary>An explicit empty object for optional WPF rendering metadata.</summary>
    public class NotSupportedNull : Freezable
    {
        public string GetCopyText() => "";
        public void Copy() { }
    }
}
namespace ProjectZ.WpfCompatibility.Interop
{
    public enum RenderMode { Default, SoftwareOnly }
}
namespace ProjectZ.WpfCompatibility.Media
{
    public enum TextFormattingMode { Ideal, Display }
    public enum TextRenderingMode { Auto, Aliased, Grayscale, ClearType }
    public enum BitmapScalingMode { Unspecified, LowQuality, HighQuality, Linear, Fant, NearestNeighbor }
    public static class TextOptions
    {
        public static readonly DependencyProperty TextFormattingModeProperty = DependencyProperty.Register("TextFormattingMode", typeof(TextFormattingMode), typeof(TextOptions), new(TextFormattingMode.Display));
        public static readonly DependencyProperty TextRenderingModeProperty = DependencyProperty.Register("TextRenderingMode", typeof(TextRenderingMode), typeof(TextOptions), new(TextRenderingMode.Grayscale));
    }
    public static class RenderOptions
    {
        public static Interop.RenderMode ProcessRenderMode { get; set; }
        public static void SetBitmapScalingMode(DependencyObject target, BitmapScalingMode mode) { }
    }
    public class StreamGeometry : Geometry
    {
        internal readonly List<Point> Points = new();
        public StreamGeometryContext Open() { Points.Clear(); return new(this); }
    }
    public sealed class StreamGeometryContext(StreamGeometry geometry) : IDisposable
    {
        public void BeginFigure(Point point, bool isFilled, bool isClosed) => geometry.Points.Add(point);
        public void LineTo(Point point, bool isStroked, bool isSmoothJoin) => geometry.Points.Add(point);
        public void Dispose() { }
    }
}
namespace ProjectZ.WpfCompatibility.Documents
{
    // Drag adorners are optional overlays. Their absence must not block native
    // drag/drop handlers or the application's normal content.
    public class AdornerLayer : Controls.EmptyControl
    {
        public static AdornerLayer? GetAdornerLayer(UIElement element) => null;
        public void Add(UIElement adorner) { }
        public void Remove(UIElement adorner) { }
    }
}
