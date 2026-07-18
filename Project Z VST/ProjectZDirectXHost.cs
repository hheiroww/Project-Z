using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AudioPlugSharp;

namespace ProjectZ.Vst;

/// <summary>
/// Embeds KNI's WinForms DirectX 11 HWND into the WPF window owned by a DAW.
/// The audio thread never enters this class.
/// </summary>
internal sealed class ProjectZDirectXHost : HwndHost
{
    private const int GwlStyle = -16;
    private const long WsChild = 0x40000000L;
    private const long WsVisible = 0x10000000L;
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsPopup = unchecked((long)0x80000000);
    private const long WsClipSiblings = 0x04000000L;
    private const long WsClipChildren = 0x02000000L;

    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;

    private readonly ProjectZPlugin _plugin;
    private ProjectZPluginGame? _game;
    private System.Windows.Forms.Control? _nativeDropControl;
    private DispatcherTimer? _frameTimer;
    private IntPtr _renderWindow;
    private bool _fallbackWindow;
    private long _frameCount;

    public ProjectZDirectXHost(ProjectZPlugin plugin)
    {
        _plugin = plugin;
        Focusable = true;
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        try
        {
            _game = new ProjectZPluginGame(_plugin, (int)_plugin.EditorWidth, (int)_plugin.EditorHeight);
            _renderWindow = _game.Window.Handle;

            SetParent(_renderWindow, hwndParent.Handle);

            var style = GetWindowLongPtr(_renderWindow, GwlStyle).ToInt64();
            style &= ~(WsPopup | WsCaption | WsThickFrame | WsSysMenu);
            style |= WsChild | WsVisible | WsClipSiblings | WsClipChildren;
            SetWindowLongPtr(_renderWindow, GwlStyle, new IntPtr(style));

            ResizeNativeWindow((int)_plugin.EditorWidth, (int)_plugin.EditorHeight);
            _game.RunOneFrame();

            // WinForms is allowed to recreate a control handle while KNI
            // creates the graphics device. HwndHost must return the HWND that
            // actually owns the initialized DX11 swap chain, not the earlier
            // placeholder handle.
            var initializedRenderWindow = _game.Window.Handle;
            if (initializedRenderWindow != _renderWindow)
            {
                Logger.Log($"Project Z DirectX HWND changed during initialization: " +
                           $"0x{_renderWindow.ToInt64():X} -> 0x{initializedRenderWindow.ToInt64():X}.");
                _renderWindow = initializedRenderWindow;
                SetParent(_renderWindow, hwndParent.Handle);
                style = GetWindowLongPtr(_renderWindow, GwlStyle).ToInt64();
                style &= ~(WsPopup | WsCaption | WsThickFrame | WsSysMenu);
                style |= WsChild | WsVisible | WsClipSiblings | WsClipChildren;
                SetWindowLongPtr(_renderWindow, GwlStyle, new IntPtr(style));
                ResizeNativeWindow((int)_plugin.EditorWidth, (int)_plugin.EditorHeight);
                _game.Tick();
            }

            AttachNativeDropTarget();

            _frameCount = 1;
            Logger.Log($"Project Z DirectX editor initialized. Parent=0x{hwndParent.Handle.ToInt64():X}, " +
                       $"child=0x{_renderWindow.ToInt64():X}, size={_plugin.EditorWidth}x{_plugin.EditorHeight}.");

            _frameTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
            };
            _frameTimer.Tick += RenderFrame;
            _frameTimer.Start();
        }
        catch (Exception ex)
        {
            Logger.Log("Project Z DirectX editor initialization failed: " + ex);
            _game?.Dispose();
            _game = null;
            _renderWindow = CreateWindowEx(
                0, "STATIC", "Project Z editor could not initialize. See the AudioPlugSharp log.",
                (uint)(WsChild | WsVisible), 0, 0, (int)_plugin.EditorWidth, (int)_plugin.EditorHeight,
                hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (_renderWindow == IntPtr.Zero)
                throw;
            _fallbackWindow = true;
        }

        return new HandleRef(this, _renderWindow);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        DetachNativeDropTarget();
        if (_frameTimer is not null)
        {
            _frameTimer.Stop();
            _frameTimer.Tick -= RenderFrame;
            _frameTimer = null;
        }

        _game?.Dispose();
        _game = null;
        if (_fallbackWindow && _renderWindow != IntPtr.Zero)
            DestroyWindow(_renderWindow);
        _fallbackWindow = false;
        _renderWindow = IntPtr.Zero;
    }

    internal Task ImportDroppedFilesAsync(IReadOnlyList<string> paths)
    {
        if (_game is null || paths.Count == 0)
            return Task.CompletedTask;
        return _game.ImportDroppedFilesAsync(paths);
    }

    private void AttachNativeDropTarget()
    {
        DetachNativeDropTarget();
        _nativeDropControl = System.Windows.Forms.Control.FromHandle(_renderWindow);
        if (_nativeDropControl is null)
        {
            Logger.Log($"Project Z could not resolve DirectX HWND 0x{_renderWindow.ToInt64():X} as a WinForms drop target.");
            return;
        }

        _nativeDropControl.AllowDrop = true;
        _nativeDropControl.DragEnter += NativeDragEnter;
        _nativeDropControl.DragOver += NativeDragEnter;
        _nativeDropControl.DragDrop += NativeDragDrop;
        Logger.Log("Project Z DirectX file-drop target enabled.");
    }

    private void DetachNativeDropTarget()
    {
        if (_nativeDropControl is null)
            return;
        _nativeDropControl.DragEnter -= NativeDragEnter;
        _nativeDropControl.DragOver -= NativeDragEnter;
        _nativeDropControl.DragDrop -= NativeDragDrop;
        _nativeDropControl.AllowDrop = false;
        _nativeDropControl = null;
    }

    private static void NativeDragEnter(object? sender, System.Windows.Forms.DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(System.Windows.Forms.DataFormats.FileDrop) == true
            ? System.Windows.Forms.DragDropEffects.Copy
            : System.Windows.Forms.DragDropEffects.None;
    }

    private async void NativeDragDrop(object? sender, System.Windows.Forms.DragEventArgs e)
    {
        try
        {
            if (e.Data?.GetData(System.Windows.Forms.DataFormats.FileDrop) is string[] paths)
                await ImportDroppedFilesAsync(paths);
        }
        catch (Exception ex)
        {
            Logger.Log("Project Z DirectX file drop failed: " + ex);
        }
    }

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        ResizeNativeWindow(
            Math.Max(1, (int)Math.Ceiling(rcBoundingBox.Width)),
            Math.Max(1, (int)Math.Ceiling(rcBoundingBox.Height)));
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        // AudioPlugSharp embeds this HwndHost through a native VST parent. WPF
        // can report IsVisible=false even while the native child is on-screen,
        // which previously stopped the DirectX frame pump permanently.
        if (_game is null)
            return;

        try
        {
            _game.Tick();
            _frameCount++;
            if (_frameCount == 2 || _frameCount == 60 || _frameCount == 300)
                Logger.Log($"Project Z DirectX editor presented frame {_frameCount}.");
        }
        catch (ObjectDisposedException)
        {
            _frameTimer?.Stop();
        }
        catch (Exception ex)
        {
            Logger.Log("Project Z DirectX editor frame failed: " + ex);
            _frameTimer?.Stop();
        }
    }

    private void ResizeNativeWindow(int width, int height)
    {
        if (_renderWindow == IntPtr.Zero)
            return;

        SetWindowPos(
            _renderWindow,
            IntPtr.Zero,
            0,
            0,
            Math.Max(width, 1),
            Math.Max(height, 1),
            SwpNoActivate | SwpFrameChanged | SwpShowWindow);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int extendedStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu,
        IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr newValue);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
