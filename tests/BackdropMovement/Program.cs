using System.Diagnostics;
using System.Runtime.InteropServices;
using ProjectZ.Windows.Composition;
using Color = Microsoft.Xna.Framework.Color;

static class Program
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hgt, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out Rect bounds);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint command);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder text, int count);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    static IntPtr NextVisible(IntPtr h) { do { h=GetWindow(h,2); } while(h!=IntPtr.Zero && !IsWindowVisible(h)); return h; }
    static bool Aligned(IntPtr owner, IntPtr backdrop) { GetWindowRect(owner, out var a); GetWindowRect(backdrop, out var b); return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom; }
    [STAThread] static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        using var owner = new Form { Text = "Project-Z framework movement check", FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Bounds = new(100,100,480,320) };
        owner.Show();
        using var host = new DwmBackdropHost(owner.Handle, new Color(2,8,6,92));
        int stale = 0, wrongOrder = 0; var timings = new List<double>();
        // Deliberately do not Tick/Pump the game loop. A native drag runs its own
        // message loop and must keep the two real HWNDs aligned without rendering.
        for(int i=0; i<120; i++) {
            var timer=Stopwatch.StartNew();
            SetWindowPos(owner.Handle, IntPtr.Zero, 100+i*2, 100+i, 480+i%40, 320+i%30, 0x14);
            timings.Add(timer.Elapsed.TotalMilliseconds);
            if(!Aligned(owner.Handle,host.Handle)) stale++;
            if(NextVisible(owner.Handle)!=host.Handle) wrongOrder++;
        }
        var beforeIdle=host.NativePositionUpdateCount;
        for(int i=0;i<120;i++) host.Tick();
        var idleUpdates=host.NativePositionUpdateCount-beforeIdle;
        owner.Hide(); bool hidden = !host.IsVisible;
        owner.Show(); bool shown = host.IsVisible && Aligned(owner.Handle,host.Handle);
        var text=$"Moves=120; staleBackdrop={stale}; incorrectZOrder={wrongOrder}; nativeMoveMeanMs={timings.Average():F3}; nativeMoveMaxMs={timings.Max():F3}; idleNativeMoves={idleUpdates}; hide={hidden}; restore={shown}";
        Console.WriteLine(text);
        if(args.Length>0) File.WriteAllText(args[0],text);
        return stale==0 && wrongOrder==0 && idleUpdates==0 && hidden && shown ? 0:1;
    }
}
