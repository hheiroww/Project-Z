using System.Runtime.InteropServices;
namespace ProjectZ.WpfCompatibility
{
    public readonly record struct DpiScale(double DpiScaleX,double DpiScaleY) { public double PixelsPerDip=>DpiScaleY; }
    public abstract class PresentationSource
    {
        public abstract Media.CompositionTarget CompositionTarget { get; }
        public static PresentationSource? FromVisual(DependencyObject visual) => Window.GetWindow(visual) is { } window ? Interop.HwndSource.FromHwnd(window.NativeHandle) : null;
    }
    public partial class UIElement
    {
        public void UpdateLayout() => NativeElement.ValidationCheck();
        public Point TranslatePoint(Point point, UIElement relativeTo) => new(point.X+NativeElement.Rectangle.X-relativeTo.NativeElement.Rectangle.X,point.Y+NativeElement.Rectangle.Y-relativeTo.NativeElement.Rectangle.Y);
        public Point PointToScreen(Point point)
        {
            var position=new NativePoint { X=(int)(point.X+NativeElement.Rectangle.X),Y=(int)(point.Y+NativeElement.Rectangle.Y) };
            if(NativeElement.OwnerScene?.sender() is { } game) ClientToScreen(global::ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(game),ref position);
            return new(position.X,position.Y);
        }
        public Point PointFromScreen(Point point)
        {
            var position=new NativePoint { X=(int)point.X,Y=(int)point.Y };
            if(NativeElement.OwnerScene?.sender() is { } game) ScreenToClient(global::ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(game),ref position);
            return new(position.X-NativeElement.Rectangle.X,position.Y-NativeElement.Rectangle.Y);
        }
        [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X,Y; }
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref NativePoint point);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr window,ref NativePoint point);
    }
}
namespace ProjectZ.WpfCompatibility.Media
{
    public readonly record struct Matrix(double M11,double M12,double M21,double M22,double OffsetX,double OffsetY)
    {
        public static Matrix Identity=>new(1,0,0,1,0,0);
        public Point Transform(Point point)=>new(point.X*M11+point.Y*M21+OffsetX,point.X*M12+point.Y*M22+OffsetY);
    }
    public abstract class CompositionTarget
    {
        public static event EventHandler? Rendering;
        internal static void RenderFrame(TimeSpan time) => Rendering?.Invoke(null,new RenderingEventArgs(time));
        public virtual Matrix TransformToDevice=>Matrix.Identity;
        public virtual Matrix TransformFromDevice=>Matrix.Identity;
    }
    public class RenderingEventArgs(TimeSpan time) : EventArgs { public TimeSpan RenderingTime { get; }=time; }
    public static class VisualTreeHelper
    {
        public static DependencyObject? GetParent(DependencyObject visual) => visual is UIElement element && element.NativeElement.RoutedParent is { } parent ? UIElement.FromNative(parent) : null;
        public static int GetChildrenCount(DependencyObject visual)=>visual is UIElement element ? element.NativeElement.Children.Count : 0;
        public static DependencyObject GetChild(DependencyObject visual,int index) => visual is UIElement element ? UIElement.FromNative(element.NativeElement.Children[index]) ?? throw new InvalidOperationException("This native child has no imported adapter.") : throw new ArgumentException(nameof(visual));
        public static DpiScale GetDpi(DependencyObject visual)=>new(1,1);
    }
}
namespace ProjectZ.WpfCompatibility.Interop
{
    public delegate IntPtr HwndSourceHook(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled);
    public sealed class HwndTarget : Media.CompositionTarget { public Media.Color BackgroundColor { get; set; } }
    public sealed class HwndSource : PresentationSource, IDisposable
    {
        readonly HookWindow hook;
        readonly List<HwndSourceHook> hooks=new();
        public IntPtr Handle { get; }
        public override HwndTarget CompositionTarget { get; }=new();
        static readonly Dictionary<IntPtr,HwndSource> sources=new();
        HwndSource(IntPtr handle) { Handle=handle; hook=new(this); hook.AssignHandle(handle); }
        public static HwndSource? FromHwnd(IntPtr handle)
        {
            if(handle==IntPtr.Zero) return null;
            if(!sources.TryGetValue(handle,out var source)) sources[handle]=source=new(handle);
            return source;
        }
        public void AddHook(HwndSourceHook callback)=>hooks.Add(callback);
        public void RemoveHook(HwndSourceHook callback)=>hooks.Remove(callback);
        public void Dispose() { hook.ReleaseHandle(); hooks.Clear(); sources.Remove(Handle); }
        sealed class HookWindow(HwndSource source) : System.Windows.Forms.NativeWindow
        {
            protected override void WndProc(ref System.Windows.Forms.Message message)
            {
                bool handled=false;
                foreach(var callback in source.hooks.ToArray()) { var result=callback(message.HWnd,message.Msg,message.WParam,message.LParam,ref handled); if(handled) { message.Result=result; return; } }
                base.WndProc(ref message);
                if(message.Msg==0x82) { source.hooks.Clear(); sources.Remove(source.Handle); }
            }
        }
    }
}
