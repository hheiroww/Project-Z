using System.ComponentModel;
using System.Runtime.InteropServices;
using ProjectZ.Shared.Drawing;
using Native = ProjectZ.Shared.Drawing.UI.Input;

namespace ProjectZ.WpfCompatibility
{
    public enum WindowState { Normal, Minimized, Maximized }
    public enum WindowStyle { None, SingleBorderWindow, ThreeDBorderWindow, ToolWindow }
    public enum WindowStartupLocation { Manual, CenterScreen, CenterOwner }
    public enum ResizeMode { NoResize, CanMinimize, CanResize, CanResizeWithGrip }
    public enum SizeToContent { Manual, Width, Height, WidthAndHeight }
    public static class SystemParameters
    {
        static Rect Rect(System.Drawing.Rectangle r) => new(r.X,r.Y,r.Width,r.Height);
        public static Rect WorkArea => Rect(System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea);
        public static double PrimaryScreenWidth => System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width;
        public static double PrimaryScreenHeight => System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height;
        public static double VirtualScreenLeft => System.Windows.Forms.SystemInformation.VirtualScreen.Left;
        public static double VirtualScreenTop => System.Windows.Forms.SystemInformation.VirtualScreen.Top;
        public static double VirtualScreenWidth => System.Windows.Forms.SystemInformation.VirtualScreen.Width;
        public static double VirtualScreenHeight => System.Windows.Forms.SystemInformation.VirtualScreen.Height;
        public static bool ClientAreaAnimation => DesktopNative.SystemParametersInfo(0x1042,0,out var enabled,0) && enabled;
    }
    public static class SystemCommands
    {
        public static void MinimizeWindow(Window window) => window.WindowState=WindowState.Minimized;
        public static void MaximizeWindow(Window window) => window.WindowState=WindowState.Maximized;
        public static void RestoreWindow(Window window) => window.WindowState=WindowState.Normal;
        public static void CloseWindow(Window window) => window.Close();
    }
    public partial class Window
    {
        internal NativeWindowSurface? surface;
        readonly Scene? previousConstructionScene;
        bool rendering, pendingSurfaceDispose;
        Interop.HwndSource? messageSource;
        IntPtr WindowMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
        {
            if(message==0x10) { handled=true;Close(); }
            else if (message == 0x5) // WM_SIZE, including taskbar/system-menu state changes
            {
                var nativeState = wParam.ToInt32() == 1 ? WindowState.Minimized : wParam.ToInt32() == 2 ? WindowState.Maximized : WindowState.Normal;
                if (state != nativeState)
                {
                    if (state == WindowState.Normal && lastBounds is {} bounds) RestoreBounds = bounds;
                    state = nativeState;
                    StateChanged?.Invoke(this, EventArgs.Empty);
                    Changed(nameof(WindowState));
                }
            }
            else if (message == 0x83 && HasCustomFrame) // WM_NCCALCSIZE
            {
                // Retain the resize style for Windows sizing/snap behavior, but
                // allocate the entire window to the application's own UI.
                handled = true;
            }
            else if (message == 0x84 && HasCustomFrame && !DesktopNative.IsZoomed(hwnd) && resize is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip)
            {
                var bounds = ReadBounds();
                int x = unchecked((short)lParam.ToInt64()), y = unchecked((short)(lParam.ToInt64() >> 16));
                var edges = Shell.WindowChrome.GetWindowChrome(this)?.ResizeBorderThickness ?? new Thickness(8);
                bool leftEdge = x < bounds.Left + edges.Left, rightEdge = x >= bounds.Right - edges.Right;
                bool topEdge = y < bounds.Top + edges.Top, bottomEdge = y >= bounds.Bottom - edges.Bottom;
                int hit = topEdge ? (leftEdge ? 13 : rightEdge ? 14 : 12) : bottomEdge ? (leftEdge ? 16 : rightEdge ? 17 : 15) : leftEdge ? 10 : rightEdge ? 11 : 0;
                if (hit != 0) { handled = true; return new IntPtr(hit); }
            }
            else if (message == 0x24) // WM_GETMINMAXINFO
            {
                var sizing = Marshal.PtrToStructure<DesktopNative.MinMaxInfo>(lParam);
                sizing.MinTrack.X = (int)Math.Ceiling(Math.Max(1, MinWidth));
                sizing.MinTrack.Y = (int)Math.Ceiling(Math.Max(1, MinHeight));
                if (double.IsFinite(MaxWidth) && MaxWidth < int.MaxValue) sizing.MaxTrack.X = (int)Math.Max(sizing.MinTrack.X, MaxWidth);
                if (double.IsFinite(MaxHeight) && MaxHeight < int.MaxValue) sizing.MaxTrack.Y = (int)Math.Max(sizing.MinTrack.Y, MaxHeight);
                if (HasCustomFrame && DesktopNative.GetMonitorInfo(DesktopNative.MonitorFromWindow(hwnd, 2), out var monitor))
                {
                    sizing.MaxPosition = new() { X = monitor.Work.Left - monitor.Monitor.Left, Y = monitor.Work.Top - monitor.Monitor.Top };
                    sizing.MaxSize = new() { X = monitor.Work.Right - monitor.Work.Left, Y = monitor.Work.Bottom - monitor.Work.Top };
                }
                Marshal.StructureToPtr(sizing, lParam, false);
                handled = true;
            }
            return IntPtr.Zero;
        }
        bool HasCustomFrame => style == WindowStyle.None || Shell.WindowChrome.GetWindowChrome(this) != null;
        readonly record struct Root(Native.Border Element, NativeWindowSurface? Surface, Scene? Previous);
        static Root CreateRoot()
        {
            var app=Application.Current; var previous=app.ConstructionScene;
            var surface=app.UseNativeWindows ? new NativeWindowSurface("Project-Z",app.StartupOptions.Width,app.StartupOptions.Height) : null;
            var scene=surface?.Scene ?? app.ControlScene;
            app.ConstructionScene=scene;
            return new(new Native.Border(scene) { BackgroundColor=Microsoft.Xna.Framework.Color.Transparent, BorderThickness=0 },surface,previous);
        }
        public Window() : this(CreateRoot()) { }
        Window(Root root) : base(root.Element) { surface=root.Surface; previousConstructionScene=root.Previous; Width=Application.Current.StartupOptions.Width; Height=Application.Current.StartupOptions.Height; }
        public IntPtr NativeHandle => surface?.Handle ?? (NativeElement.OwnerScene?.sender() is { } game ? global::ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(game) : IntPtr.Zero);
        public Microsoft.Xna.Framework.Color[] CaptureFrame() => surface?.CaptureFrame() ?? throw new InvalidOperationException("This window has no native surface.");
        public void SaveFrame(string path) { if(surface==null) throw new InvalidOperationException("This window has no native surface."); surface.SaveFrame(path); }
        string title="";
        public string Title { get => title; set { title=value; if(surface!=null) surface.Title=value; Changed(nameof(Title)); } }
        double left=double.NaN,top=double.NaN;
        public static readonly DependencyProperty LeftProperty=DependencyProperty.RegisterNative(nameof(Left),typeof(double),typeof(Window),new(double.NaN));
        public static readonly DependencyProperty TopProperty=DependencyProperty.RegisterNative(nameof(Top),typeof(double),typeof(Window),new(double.NaN));
        public double Left { get => ReadBounds().Left; set { left=value; Place(); Changed(nameof(Left)); } }
        public double Top { get => ReadBounds().Top; set { top=value; Place(); Changed(nameof(Top)); } }
        public new double Width { get => base.Width; set { base.Width=value; if(shown) ResizeSurface(); } }
        public new double Height { get => base.Height; set { base.Height=value; if(shown) ResizeSurface(); } }
        public Rect RestoreBounds { get; private set; }
        public WindowStartupLocation WindowStartupLocation { get; set; }
        WindowState state;
        public WindowState WindowState
        {
            get => NativeHandle==IntPtr.Zero ? state : DesktopNative.IsIconic(NativeHandle) ? WindowState.Minimized : DesktopNative.IsZoomed(NativeHandle) ? WindowState.Maximized : WindowState.Normal;
            set { if(value==WindowState) return; if(WindowState==WindowState.Normal) RestoreBounds=ReadBounds(); state=value; if(shown) DesktopNative.ShowWindow(NativeHandle,value==WindowState.Minimized?6:value==WindowState.Maximized?3:9); StateChanged?.Invoke(this,EventArgs.Empty); Changed(nameof(WindowState)); }
        }
        WindowStyle style=WindowStyle.SingleBorderWindow;
        ResizeMode resize=ResizeMode.CanResize;
        bool taskbar=true,topmost;
        public WindowStyle WindowStyle { get => style; set { style=value; ApplyStyles(); } }
        public ResizeMode ResizeMode { get => resize; set { resize=value; ApplyStyles(); } }
        public bool ShowInTaskbar { get => taskbar; set { taskbar=value; ApplyStyles(); } }
        public bool Topmost { get => topmost; set { topmost=value; if(NativeHandle!=IntPtr.Zero) DesktopNative.SetWindowPos(NativeHandle,value?new IntPtr(-1):new IntPtr(-2),0,0,0,0,3|16); } }
        public bool ShowActivated { get; set; }=true;
        public bool AllowsTransparency { get; set; }
        public SizeToContent SizeToContent { get; set; }
        public Media.ImageSource? Icon { get; set; }
        Window? owner;
        public Window? Owner { get => owner; set { if(value==this) throw new ArgumentException("A window cannot own itself."); owner=value; surface?.SetOwner(value?.NativeHandle ?? IntPtr.Zero); } }
        bool? dialogResult;
        bool modal;
        public bool? DialogResult { get => dialogResult; set { dialogResult=value; if(modal && value.HasValue) Close(); } }
        public event EventHandler? SourceInitialized;
        public event EventHandler? StateChanged;
        public event EventHandler? Activated;
        public event EventHandler? Deactivated;
        public event EventHandler? LocationChanged;
        bool active;
        long nextNativeFrame;
        Rect lastBounds;
        internal void ShowNativeWindow()
        {
            Application.Current.ConstructionScene=previousConstructionScene;
            Visibility=Visibility.Visible; if(surface==null) return;
            if (messageSource == null) { messageSource = Interop.HwndSource.FromHwnd(NativeHandle); messageSource?.AddHook(WindowMessage); }
            ApplyStyles(); ResizeSurface();
            if(WindowStartupLocation!=WindowStartupLocation.Manual)
            {
                var area=WindowStartupLocation==WindowStartupLocation.CenterOwner && Owner!=null ? Owner.ReadBounds() : SystemParameters.WorkArea;
                left=area.Left+(area.Width-Width)/2; top=area.Top+(area.Height-Height)/2;
            }
            Place(); surface.SetVisible(true);
            DesktopNative.ShowWindow(NativeHandle,state==WindowState.Minimized?6:state==WindowState.Maximized?3:ShowActivated?5:4);
            SourceInitialized?.Invoke(this,EventArgs.Empty);
            if(ShowActivated) Activate();
        }
        void ResizeSurface() { if(surface!=null) surface.Resize((int)Math.Clamp(Width,1,16384),(int)Math.Clamp(Height,1,16384)); }
        Rect ReadBounds() { if(NativeHandle!=IntPtr.Zero && DesktopNative.GetWindowRect(NativeHandle,out var r)) return new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top); return new(double.IsNaN(left)?0:left,double.IsNaN(top)?0:top,Width,Height); }
        void Place() { if(NativeHandle==IntPtr.Zero || double.IsNaN(left)&&double.IsNaN(top)) return; var r=ReadBounds(); DesktopNative.SetWindowPos(NativeHandle,IntPtr.Zero,(int)(double.IsNaN(left)?r.X:left),(int)(double.IsNaN(top)?r.Y:top),0,0,1|4|16); }
        internal void ApplyStyles()
        {
            if(NativeHandle==IntPtr.Zero) return;
            long flags=DesktopNative.GetWindowLongPtr(NativeHandle,-16).ToInt64(); flags &= ~0x00cf0000L;
            if(!HasCustomFrame) flags|=0x00c80000L;
            if(resize!=ResizeMode.NoResize) flags|=0x00020000;
            if(resize is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip) flags|=0x00050000;
            DesktopNative.SetWindowLongPtr(NativeHandle,-16,new IntPtr(flags));
            long ex=DesktopNative.GetWindowLongPtr(NativeHandle,-20).ToInt64(); ex &= ~(0x00040080L);
            ex|=taskbar && style!=WindowStyle.ToolWindow?0x00040000L:0x80L;
            DesktopNative.SetWindowLongPtr(NativeHandle,-20,new IntPtr(ex));
            DesktopNative.SetWindowPos(NativeHandle,IntPtr.Zero,0,0,0,0,1|2|4|16|32);
        }
        public bool Activate() { if(NativeHandle==IntPtr.Zero) return false; return DesktopNative.SetForegroundWindow(NativeHandle); }
        public void DragMove() { if(NativeHandle==IntPtr.Zero) return; DesktopNative.ReleaseCapture(); DesktopNative.SendMessage(NativeHandle,0xA1,new IntPtr(2),IntPtr.Zero); }
        public static Window? GetWindow(DependencyObject element)
        {
            if(element is Window window) return window;
            if(element is FrameworkElement fe) for(var parent=fe.Parent;parent!=null;parent=parent.Parent) if(parent is Window found) return found;
            if(element is UIElement visual) return Application.Current.Windows.FirstOrDefault(w=>ReferenceEquals(w.NativeElement.OwnerScene,visual.NativeElement.OwnerScene));
            return null;
        }
        public bool? ShowDialog()
        {
            if(surface==null) throw new InvalidOperationException("A native host is required for a modal window.");
            modal=true; Show(); var ownerHandle=Owner?.NativeHandle ?? IntPtr.Zero;
            if(ownerHandle!=IntPtr.Zero) DesktopNative.EnableWindow(ownerHandle,false);
            try { while(!IsDisposed && !Application.Current.ShutdownRequested) { Application.Current.Dispatcher.Pump(); Application.Current.PumpWindows(); System.Windows.Forms.Application.DoEvents(); Thread.Sleep(1); } }
            finally { modal=false; if(ownerHandle!=IntPtr.Zero) { DesktopNative.EnableWindow(ownerHandle,true); Owner?.Activate(); } }
            return dialogResult ?? false;
        }
        internal void RenderNativeFrame()
        {
            if(surface==null || IsDisposed || Visibility!=Visibility.Visible) return;
            if(surface.IsClosed) { Close(); return; }
            var current=DesktopNative.GetForegroundWindow()==NativeHandle;
            if(current!=active) { active=current; nextNativeFrame=0; if(current) Activated?.Invoke(this,EventArgs.Empty); else Deactivated?.Invoke(this,EventArgs.Empty); }
            if (DesktopNative.IsIconic(NativeHandle)) return;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            int frameLimit = current ? Application.Current.StartupOptions.FramesPerSecond : Math.Min(15, Application.Current.StartupOptions.FramesPerSecond);
            long period = System.Diagnostics.Stopwatch.Frequency / frameLimit;
            if (now < nextNativeFrame) return;
            nextNativeFrame = nextNativeFrame == 0 || now - nextNativeFrame >= period ? now + period : nextNativeFrame + period;
            var bounds=ReadBounds(); if(bounds!=lastBounds) { lastBounds=bounds; LocationChanged?.Invoke(this,EventArgs.Empty); }
            rendering=true;
            try { surface.RenderFrame(false); }
            finally { rendering=false; if(pendingSurfaceDispose) { surface.Dispose(); surface=null; } }
        }
        public override void Dispose()
        {
            if(IsDisposed) return; base.Dispose();
            messageSource?.RemoveHook(WindowMessage); messageSource?.Dispose();messageSource=null;
            if(Application.Current.ConstructionScene==NativeElement.OwnerScene) Application.Current.ConstructionScene=previousConstructionScene;
            if(rendering) pendingSurfaceDispose=true; else { surface?.Dispose(); surface=null; }
        }
    }
    internal static class DesktopNative
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Bounds { public int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct NativePoint { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct MinMaxInfo { public NativePoint Reserved,MaxSize,MaxPosition,MinTrack,MaxTrack; }
        [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Bounds Monitor,Work; public uint Flags; }
        [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
        [DllImport("user32.dll",EntryPoint="GetMonitorInfoW")] static extern bool ReadMonitorInfo(IntPtr monitor,ref MonitorInfo info);
        internal static bool GetMonitorInfo(IntPtr monitor,out MonitorInfo info) { info=new() { Size=Marshal.SizeOf<MonitorInfo>() }; return ReadMonitorInfo(monitor,ref info); }
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window,out Bounds bounds);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window,int command);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr value);
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool EnableWindow(IntPtr window,bool enabled);
        [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
        [DllImport("user32.dll")] internal static extern bool SystemParametersInfo(uint action,uint parameter,[MarshalAs(UnmanagedType.Bool)] out bool value,uint flags);
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    }
}
namespace ProjectZ.WpfCompatibility.Interop
{
    public class WindowInteropHelper(Window window)
    {
        public IntPtr Handle=>window.NativeHandle;
        public IntPtr EnsureHandle()=>Handle;
        public IntPtr Owner { get=>DesktopNative.GetWindowLongPtr(Handle,-8); set=>DesktopNative.SetWindowLongPtr(Handle,-8,value); }
    }
}
