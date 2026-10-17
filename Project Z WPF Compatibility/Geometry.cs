using System.Collections.ObjectModel;
using Advanced = ProjectZ.Shared.Drawing.UI.Advanced;

namespace ProjectZ.WpfCompatibility
{
    public readonly record struct Rect(double X, double Y, double Width, double Height)
    {
        public Rect(Point location, Size size) : this(location.X, location.Y, size.Width, size.Height) { }
        public double Left => X; public double Top => Y; public double Right => X + Width; public double Bottom => Y + Height;
        public bool Contains(Point point) => point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
    }
    public enum TextDecorationLocation { Underline, OverLine, Strikethrough, Baseline }
    public class TextDecoration { public TextDecorationLocation Location { get; set; } }
    public class TextDecorationCollection : ObservableCollection<TextDecoration> { }
    public static class TextDecorations
    {
        static TextDecorationCollection Create(TextDecorationLocation location) => new() { new() { Location = location } };
        public static TextDecorationCollection Underline => Create(TextDecorationLocation.Underline);
        public static TextDecorationCollection Strikethrough => Create(TextDecorationLocation.Strikethrough);
        public static TextDecorationCollection OverLine => Create(TextDecorationLocation.OverLine);
        public static TextDecorationCollection Baseline => Create(TextDecorationLocation.Baseline);
    }
}
namespace ProjectZ.WpfCompatibility.Media
{
    public enum PenLineCap { Flat, Square, Round, Triangle }
    public enum PenLineJoin { Miter, Bevel, Round }
    public sealed class Pen : Freezable
    {
        public Pen() { }
        public Pen(Brush brush, double thickness) { Brush=brush; Thickness=thickness; }
        public Brush? Brush { get; set; }
        public double Thickness { get; set; }=1;
        public PenLineCap StartLineCap { get; set; }
        public PenLineCap EndLineCap { get; set; }
        public PenLineJoin LineJoin { get; set; }
    }
    public enum Stretch { None, Fill, Uniform, UniformToFill }
    public class PointCollection : ObservableCollection<Point> { public PointCollection() { } public PointCollection(IEnumerable<Point> points) { foreach (var point in points) Add(point); } }
    public abstract class Geometry : Animation.Animatable
    {
        public static Geometry Parse(string data) => new EmptyGeometry { Data = data };
        sealed class EmptyGeometry : Geometry { public string Data { get; init; } = ""; }
    }
    public class RectangleGeometry : Geometry
    {
        public RectangleGeometry() { } public RectangleGeometry(Rect rect) => Rect = rect;
        public Rect Rect { get => (Rect)GetValue(RectProperty)!; set => SetValue(RectProperty, value); }
        public static readonly DependencyProperty RectProperty = DependencyProperty.Register(nameof(Rect), typeof(Rect), typeof(RectangleGeometry), new(new Rect()));
        public double RadiusX { get; set; } public double RadiusY { get; set; }
    }
    public abstract class Transform : Animation.Animatable { }
    public class ScaleTransform : Transform
    {
        public ScaleTransform() { } public ScaleTransform(double scaleX, double scaleY) { ScaleX = scaleX; ScaleY = scaleY; }
        public static readonly DependencyProperty ScaleXProperty = DependencyProperty.Register(nameof(ScaleX), typeof(double), typeof(ScaleTransform), new(1d));
        public static readonly DependencyProperty ScaleYProperty = DependencyProperty.Register(nameof(ScaleY), typeof(double), typeof(ScaleTransform), new(1d));
        public static readonly DependencyProperty CenterXProperty = DependencyProperty.Register(nameof(CenterX), typeof(double), typeof(ScaleTransform), new(0d));
        public static readonly DependencyProperty CenterYProperty = DependencyProperty.Register(nameof(CenterY), typeof(double), typeof(ScaleTransform), new(0d));
        public double ScaleX { get => (double)GetValue(ScaleXProperty)!; set => SetValue(ScaleXProperty, value); }
        public double ScaleY { get => (double)GetValue(ScaleYProperty)!; set => SetValue(ScaleYProperty, value); }
        public double CenterX { get => (double)GetValue(CenterXProperty)!; set => SetValue(CenterXProperty, value); }
        public double CenterY { get => (double)GetValue(CenterYProperty)!; set => SetValue(CenterYProperty, value); }
    }
    public class TranslateTransform : Transform
    {
        public TranslateTransform() { } public TranslateTransform(double x, double y) { X = x; Y = y; }
        public static readonly DependencyProperty XProperty = DependencyProperty.Register(nameof(X), typeof(double), typeof(TranslateTransform), new(0d));
        public static readonly DependencyProperty YProperty = DependencyProperty.Register(nameof(Y), typeof(double), typeof(TranslateTransform), new(0d));
        public double X { get => (double)GetValue(XProperty)!; set => SetValue(XProperty, value); }
        public double Y { get => (double)GetValue(YProperty)!; set => SetValue(YProperty, value); }
    }
    public class TransformCollection : ObservableCollection<Transform> { }
    public class TransformGroup : Transform
    {
        TransformCollection children=new();
        public TransformCollection Children { get=>children;set {ArgumentNullException.ThrowIfNull(value);children.CollectionChanged-=ChildrenChanged;children=value;children.CollectionChanged+=ChildrenChanged;ChildrenChanged(null,null!);} }
        readonly HashSet<Transform> watched = new();
        public TransformGroup() => Children.CollectionChanged += ChildrenChanged;
        void ChildrenChanged(object? sender,System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
        {
            foreach (var old in watched.Where(n => !Children.Contains(n)).ToArray()) { old.PropertyChanged -= ChildChanged; watched.Remove(old); }
            foreach (var child in Children.Where(n => watched.Add(n))) child.PropertyChanged += ChildChanged;
            Changed(nameof(Children));
        }
        void ChildChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Changed(nameof(Children));
    }
    public class RotateTransform : Transform
    {
        public RotateTransform() { } public RotateTransform(double angle) => Angle = angle; public RotateTransform(double angle, double centerX, double centerY) { Angle = angle; CenterX = centerX; CenterY = centerY; }
        public static readonly DependencyProperty AngleProperty = DependencyProperty.Register(nameof(Angle), typeof(double), typeof(RotateTransform), new(0d));
        public static readonly DependencyProperty CenterXProperty = DependencyProperty.Register(nameof(CenterX), typeof(double), typeof(RotateTransform), new(0d));
        public static readonly DependencyProperty CenterYProperty = DependencyProperty.Register(nameof(CenterY), typeof(double), typeof(RotateTransform), new(0d));
        public double Angle { get => (double)GetValue(AngleProperty)!; set => SetValue(AngleProperty, value); }
        public double CenterX { get => (double)GetValue(CenterXProperty)!; set => SetValue(CenterXProperty, value); }
        public double CenterY { get => (double)GetValue(CenterYProperty)!; set => SetValue(CenterYProperty, value); }
    }
    public abstract class ImageSource : Animation.Animatable { internal abstract void Load(Advanced.ImageElement image); }
}
namespace ProjectZ.WpfCompatibility.Media.Imaging
{
    public enum BitmapCacheOption { Default, OnDemand, OnLoad, None }
    public enum BitmapCreateOptions { None = 0, PreservePixelFormat = 1, DelayCreation = 2, IgnoreColorProfile = 4, IgnoreImageCache = 8 }
    public class BitmapImage : BitmapSource
    {
        byte[]? bytes;
        public BitmapImage() { } public BitmapImage(Uri uri) { UriSource = uri; EndInit(); }
        public Uri? UriSource { get; set; }
        public Stream? StreamSource { get; set; }
        public BitmapCacheOption CacheOption { get; set; }
        public BitmapCreateOptions CreateOptions { get; set; }
        public int DecodePixelWidth { get; set; } public int DecodePixelHeight { get; set; }
        public void BeginInit() { }
        public void EndInit()
        {
            if (StreamSource != null) { using var buffer = new MemoryStream(); StreamSource.CopyTo(buffer); bytes = buffer.ToArray(); }
            if(bytes!=null) { using var stream=new MemoryStream(bytes,false); using var bitmap=new System.Drawing.Bitmap(stream); FromBitmap(bitmap); }
            else if(UriSource!=null) { using var stream=File.OpenRead(ResourceUris.FilePath(UriSource)); using var bitmap=new System.Drawing.Bitmap(stream);FromBitmap(bitmap); }
        }
        public override void Freeze() { if (PixelWidth==0) EndInit(); base.Freeze(); }
        internal override void Load(Advanced.ImageElement image)
        {
            if(PixelWidth==0) EndInit(); base.Load(image);
        }
    }
}
namespace ProjectZ.WpfCompatibility.Shapes
{
    public class Rectangle : Controls.Control
    {
        double radiusX, radiusY;
        public Rectangle() : this(new global::ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement(Application.Current.ControlScene)) { }
        internal Rectangle(global::ProjectZ.Shared.Drawing.UI.SceneElement native) : base(native) { }
        public Media.Brush? Fill { get => Background; set => Background = value; }
        public Media.Brush? Stroke { get => BorderBrush; set => BorderBrush = value; }
        public double StrokeThickness { get => BorderThickness.Left; set => BorderThickness = new(value); }
        public double RadiusX { get => radiusX; set { radiusX=value; ApplyRadius(); } }
        public double RadiusY { get => radiusY; set { radiusY=value; ApplyRadius(); } }
        void ApplyRadius() { var radius=(float)Math.Max(radiusX,radiusY); ((global::ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement)NativeElement).CornerRadii=new(radius,radius,radius,radius); }
    }
    public class Polygon : FrameworkElement
    {
        Media.PointCollection points = new();
        public static readonly DependencyProperty PointsProperty = DependencyProperty.RegisterNative(nameof(Points), typeof(Media.PointCollection), typeof(Polygon));
        public Polygon() : this(new Advanced.PolygonElement(Application.Current.ControlScene, [])) { }
        internal Polygon(global::ProjectZ.Shared.Drawing.UI.SceneElement native) : base(native) { points = new(((Advanced.PolygonElement)native).Vectors.Select(p => new Point(p.X, p.Y))); points.CollectionChanged += PointsChanged; }
        public Media.PointCollection Points { get => points; set { points.CollectionChanged -= PointsChanged; points = value ?? new(); points.CollectionChanged += PointsChanged; UpdatePoints(); } }
        void PointsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => UpdatePoints();
        void UpdatePoints() { ((Advanced.PolygonElement)NativeElement).Vectors = points.Select(p => new Microsoft.Xna.Framework.Vector2((float)p.X, (float)p.Y)).ToArray(); Changed(nameof(Points)); }
        public Media.Brush? Fill { get => new Media.SolidColorBrush(Media.Color.FromNative(((Advanced.PolygonElement)NativeElement).FillColor)); set { if (value is Media.SolidColorBrush brush) ((Advanced.PolygonElement)NativeElement).FillColor = brush.Color.Native; } }
        public Media.Brush? Stroke { get => new Media.SolidColorBrush(Media.Color.FromNative(((Advanced.PolygonElement)NativeElement).RenderProperties.WireFrameColor)); set { var native=(Advanced.PolygonElement)NativeElement; native.RenderProperties.WireFrame=value!=null; native.RenderProperties.WireFrameColor=(value as Media.SolidColorBrush)?.Color.Native ?? Microsoft.Xna.Framework.Color.Transparent; } }
        public double StrokeThickness { get; set; } = 1;
        public override void Dispose() { points.CollectionChanged -= PointsChanged; base.Dispose(); }
    }
}
