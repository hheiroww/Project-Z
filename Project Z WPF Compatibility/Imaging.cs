using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using NativeImage=ProjectZ.Shared.Drawing.UI.Advanced.ImageElement;

namespace ProjectZ.WpfCompatibility
{
    public readonly record struct Int32Rect(int X,int Y,int Width,int Height) { public static Int32Rect Empty=>default; }
}
namespace ProjectZ.WpfCompatibility.Media
{
    public readonly record struct PixelFormat(string Name,int BitsPerPixel);
    public static class PixelFormats
    {
        public static PixelFormat Default=>Bgra32;
        public static PixelFormat Bgra32=>new("Bgra32",32);
        public static PixelFormat Pbgra32=>new("Pbgra32",32);
        public static PixelFormat Bgr32=>new("Bgr32",32);
    }
    public abstract class TileBrush : Brush
    {
        Rect viewbox = new(0, 0, 1, 1);
        BrushMappingMode units = BrushMappingMode.RelativeToBoundingBox;
        Stretch stretch = Stretch.Fill;
        public Rect Viewbox { get => viewbox; set { viewbox = value; Changed(nameof(Viewbox)); } }
        public BrushMappingMode ViewboxUnits { get => units; set { units = value; Changed(nameof(ViewboxUnits)); } }
        public Stretch Stretch { get => stretch; set { stretch = value; Changed(nameof(Stretch)); } }
    }
    public class VisualBrush(UIElement visual) : TileBrush { public UIElement Visual { get; set; }=visual; }
    public class ImageBrush : TileBrush
    {
        ImageSource? source;
        public ImageSource? ImageSource
        {
            get => source;
            set { if (source != null) source.PropertyChanged -= SourceChanged; source = value; if (source != null) source.PropertyChanged += SourceChanged; Changed(nameof(ImageSource)); }
        }
        void SourceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Changed(nameof(ImageSource));
        public ImageBrush() { }
        public ImageBrush(ImageSource source) => ImageSource = source;
    }
    public class DrawingVisual : DependencyObject
    {
        internal List<(Brush Brush,Rect Bounds)> Drawings { get; }=new();
        public DrawingContext RenderOpen() { Drawings.Clear(); return new(this); }
    }
    public class DrawingContext(DrawingVisual target) : IDisposable
    {
        public void DrawGeometry(Brush? brush, Pen? pen, Geometry geometry) { }
        public void DrawRectangle(Brush brush,object? pen,Rect bounds)=>target.Drawings.Add((brush,bounds));
        public void Dispose() { }
    }
}
namespace ProjectZ.WpfCompatibility.Media.Imaging
{
    public class BitmapSource : ImageSource
    {
        protected byte[] Pixels=[];
        public int PixelWidth { get; protected set; }
        public int PixelHeight { get; protected set; }
        public double DpiX { get; protected set; }=96;
        public double DpiY { get; protected set; }=96;
        public double Width=>PixelWidth*96/DpiX;
        public double Height=>PixelHeight*96/DpiY;
        public PixelFormat Format { get; protected set; }=PixelFormats.Bgra32;
        protected void Initialize(int width,int height,double dpiX,double dpiY,PixelFormat format)
        {
            if(width<=0||height<=0||width*(long)height>64*1024*1024) throw new ArgumentOutOfRangeException(nameof(width));
            if(format.BitsPerPixel!=32) throw new NotSupportedException("Native bitmap imports require a 32 bit pixel format.");
            PixelWidth=width; PixelHeight=height; DpiX=dpiX>0?dpiX:96;DpiY=dpiY>0?dpiY:96;Format=format;Pixels=new byte[checked(width*height*4)];
        }
        protected void FromBitmap(Bitmap bitmap)
        {
            Initialize(bitmap.Width,bitmap.Height,bitmap.HorizontalResolution,bitmap.VerticalResolution,PixelFormats.Bgra32);
            using var converted=new Bitmap(bitmap.Width,bitmap.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using(var graphics=Graphics.FromImage(converted)) graphics.DrawImage(bitmap,0,0,bitmap.Width,bitmap.Height);
            var data=converted.LockBits(new Rectangle(0,0,PixelWidth,PixelHeight),ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try { for(int row=0;row<PixelHeight;row++) Marshal.Copy(data.Scan0+row*data.Stride,Pixels,row*PixelWidth*4,PixelWidth*4); }
            finally { converted.UnlockBits(data); }
        }
        internal static BitmapSource FromDesktopBitmap(Bitmap bitmap) { var source=new BitmapSource(); source.FromBitmap(bitmap); return source; }
        internal Bitmap ToBitmap()
        {
            var bitmap=new Bitmap(PixelWidth,PixelHeight,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var data=bitmap.LockBits(new Rectangle(0,0,PixelWidth,PixelHeight),ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var raw=Pixels.ToArray();
            if(Format==PixelFormats.Pbgra32) for(int i=0;i<raw.Length;i+=4) if(raw[i+3]>0) for(int c=0;c<3;c++) raw[i+c]=(byte)Math.Min(255,raw[i+c]*255/raw[i+3]);
            try { for(int row=0;row<PixelHeight;row++) Marshal.Copy(raw,row*PixelWidth*4,data.Scan0+row*data.Stride,PixelWidth*4); }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }
        public void CopyPixels(Array buffer,int stride,int offset)=>CopyPixels(new(0,0,PixelWidth,PixelHeight),buffer,stride,offset);
        public void CopyPixels(Int32Rect sourceRect,Array buffer,int stride,int offset)
        {
            if(sourceRect.Width==0&&sourceRect.Height==0) sourceRect=new(0,0,PixelWidth,PixelHeight);
            ValidateRect(sourceRect);
            if(stride<sourceRect.Width*4||offset<0||offset+(long)(sourceRect.Height-1)*stride+sourceRect.Width*4>Buffer.ByteLength(buffer)) throw new ArgumentException("The destination buffer or stride is too small.");
            for(int row=0;row<sourceRect.Height;row++) Buffer.BlockCopy(Pixels,((sourceRect.Y+row)*PixelWidth+sourceRect.X)*4,buffer,offset+row*stride,sourceRect.Width*4);
        }
        protected void ValidateRect(Int32Rect rect) { if(rect.X<0||rect.Y<0||rect.Width<0||rect.Height<0||rect.X+(long)rect.Width>PixelWidth||rect.Y+(long)rect.Height>PixelHeight) throw new ArgumentOutOfRangeException(nameof(rect)); }
        public static BitmapSource Create(int width,int height,double dpiX,double dpiY,PixelFormat format,object? palette,Array pixels,int stride)
        {
            var source=new WriteableBitmap(width,height,dpiX,dpiY,format,palette); source.WritePixels(new(0,0,width,height),pixels,stride,0);return source;
        }
        internal override void Load(NativeImage image)
        {
            var colors=new Microsoft.Xna.Framework.Color[PixelWidth*PixelHeight];
            var raw=Pixels;
            for(int i=0;i<colors.Length;i++) { int p=i*4;var alpha=raw[p+3]; if(Format==PixelFormats.Pbgra32 || alpha==255) colors[i]=new(raw[p+2],raw[p+1],raw[p],alpha); else colors[i]=new((byte)(raw[p+2]*alpha/255),(byte)(raw[p+1]*alpha/255),(byte)(raw[p]*alpha/255),alpha); }
            image.LoadPixels(PixelWidth,PixelHeight,colors);
        }
    }
    public class WriteableBitmap : BitmapSource
    {
        public WriteableBitmap(int width,int height,double dpiX,double dpiY,PixelFormat format,object? palette)=>Initialize(width,height,dpiX,dpiY,format);
        public WriteableBitmap(BitmapSource source) { Initialize(source.PixelWidth,source.PixelHeight,source.DpiX,source.DpiY,source.Format);source.CopyPixels(Pixels,PixelWidth*4,0); }
        public void WritePixels(Int32Rect rect,Array buffer,int stride,int offset)
        {
            ValidateRect(rect);
            if(stride<rect.Width*4||offset<0||offset+(long)(rect.Height-1)*stride+rect.Width*4>Buffer.ByteLength(buffer)) throw new ArgumentException("The source buffer or stride is too small.");
            for(int row=0;row<rect.Height;row++) Buffer.BlockCopy(buffer,offset+row*stride,Pixels,((rect.Y+row)*PixelWidth+rect.X)*4,rect.Width*4);
            Changed(nameof(Pixels));
        }
    }
    public class CroppedBitmap : BitmapSource
    {
        public CroppedBitmap(BitmapSource source,Int32Rect rect) { Initialize(rect.Width,rect.Height,source.DpiX,source.DpiY,source.Format);source.CopyPixels(rect,Pixels,rect.Width*4,0); }
    }
    public class FormatConvertedBitmap : BitmapSource
    {
        public BitmapSource? Source { get; set; }
        public PixelFormat DestinationFormat { get; set; }=PixelFormats.Bgra32;
        public FormatConvertedBitmap() { }
        public FormatConvertedBitmap(BitmapSource source,PixelFormat format,object? palette,double alphaThreshold) { Source=source;DestinationFormat=format;EndInit(); }
        public void BeginInit() { }
        public void EndInit()
        {
            var source=Source??throw new InvalidOperationException("A bitmap source is required.");Initialize(source.PixelWidth,source.PixelHeight,source.DpiX,source.DpiY,DestinationFormat);source.CopyPixels(Pixels,PixelWidth*4,0);
            if(source.Format==DestinationFormat) return;
            for(int i=0;i<Pixels.Length;i+=4) { int a=Pixels[i+3]; for(int c=0;c<3;c++) { if(DestinationFormat==PixelFormats.Pbgra32) Pixels[i+c]=(byte)(Pixels[i+c]*a/255); else if(source.Format==PixelFormats.Pbgra32&&a>0) Pixels[i+c]=(byte)Math.Min(255,Pixels[i+c]*255/a); } if(DestinationFormat==PixelFormats.Bgr32) Pixels[i+3]=255; }
        }
    }
    public class BitmapFrame : BitmapSource
    {
        public static BitmapFrame Create(BitmapSource source) { var frame=new BitmapFrame();frame.Initialize(source.PixelWidth,source.PixelHeight,source.DpiX,source.DpiY,source.Format);source.CopyPixels(frame.Pixels,source.PixelWidth*4,0);return frame; }
    }
    public abstract class BitmapEncoder
    {
        public Collection<BitmapFrame> Frames { get; }=new();
        public abstract void Save(Stream stream);
        protected Bitmap Image()=>Frames.Count==1 ? Frames[0].ToBitmap() : throw new InvalidOperationException("Native bitmap encoders require one frame.");
    }
    public class PngBitmapEncoder : BitmapEncoder { public override void Save(Stream stream) { using var image=Image();image.Save(stream,ImageFormat.Png); } }
    public class JpegBitmapEncoder : BitmapEncoder
    {
        public int QualityLevel { get; set; }=75;
        public override void Save(Stream stream) { using var image=Image();using var parameters=new EncoderParameters(1); parameters.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,Math.Clamp(QualityLevel,1,100));image.Save(stream,ImageCodecInfo.GetImageEncoders().Single(c=>c.FormatID==ImageFormat.Jpeg.Guid),parameters); }
    }
    public class RenderTargetBitmap : BitmapSource
    {
        public RenderTargetBitmap(int width,int height,double dpiX,double dpiY,PixelFormat format)=>Initialize(width,height,dpiX,dpiY,format);
        public void Render(DependencyObject visual)
        {
            if(visual is UIElement element) Capture(element,new Rect(0,0,element.NativeElement.Size.X,element.NativeElement.Size.Y),new Rect(0,0,Width,Height));
            else if(visual is DrawingVisual drawing) foreach(var (brush,bounds) in drawing.Drawings) if(brush is VisualBrush native) Capture(native.Visual,native.Viewbox,bounds); else throw new NotSupportedException("Drawing capture requires a native VisualBrush.");
            else throw new NotSupportedException("Capture requires a Project-Z visual.");
        }
        void Capture(UIElement element,Rect source,Rect destination)
        {
            var window=Window.GetWindow(element)??throw new InvalidOperationException("The visual is not attached to a native window.");
            var pixels=window.CaptureFrame(); var width=(int)window.ActualWidth; var height=pixels.Length/width;
            for(int y=0;y<PixelHeight;y++) for(int x=0;x<PixelWidth;x++)
            {
                var logical=new Point(x*96/DpiX,y*96/DpiY); if(!destination.Contains(logical)) continue;
                var sx=(int)(element.NativeElement.Rectangle.X+source.X+(logical.X-destination.X)*source.Width/destination.Width);
                var sy=(int)(element.NativeElement.Rectangle.Y+source.Y+(logical.Y-destination.Y)*source.Height/destination.Height);
                if(sx<0||sy<0||sx>=width||sy>=height) continue; var c=pixels[sy*width+sx];int p=(y*PixelWidth+x)*4;Pixels[p]=c.B;Pixels[p+1]=c.G;Pixels[p+2]=c.R;Pixels[p+3]=c.A;
            }
        }
    }
}
