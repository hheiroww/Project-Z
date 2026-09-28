using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AudioPlugSharp;

namespace ProjectZ.Vst;

/// <summary>
/// Embeds MonoGame's native DirectX HWND into the WPF window owned by a DAW.
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
    private const int WmSize = 0x0005;
    private const int WmShowWindow = 0x0018;
    private const int WmCancelMode = 0x001F;
    private const int WmKillFocus = 0x0008;
    private const int WmGetDlgCode = 0x0087;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmMouseLeave = 0x02A3;
    private const int WmCaptureChanged = 0x0215;
    private const int SizeMinimized = 1;
    private const uint TmeLeave = 0x00000002;
    private const uint GaRoot = 2;

    private readonly ProjectZPlugin _plugin;
    private readonly WindowSubclassProc _windowSubclassProc;
    private ProjectZPluginGame? _game;
    private Exception? _gameCreationError;
    private System.Windows.Forms.Control? _nativeDropControl;
    private DispatcherTimer? _frameTimer;
    private IntPtr _renderWindow;
    private bool _fallbackWindow;
    private bool _inputSubclassAttached;
    private bool _trackingMouse;
    private long _frameCount;
    private long _handledInputMessageCount;
    private bool _lastInputWasInteractive;

    public ProjectZDirectXHost(ProjectZPlugin plugin)
    {
        _plugin = plugin;
        _windowSubclassProc = InputWindowSubclass;
        Focusable = true;

        // HwndHost suspends Dispatcher processing while BuildWindowCore runs.
        // MonoGame's native platform creates its SDL/Win32 window from the Game
        // constructor, which pumps creation messages and must happen before that
        // suspension begins.
        try
        {
            _game = new ProjectZPluginGame(_plugin, (int)_plugin.EditorWidth, (int)_plugin.EditorHeight);
        }
        catch (Exception ex)
        {
            _gameCreationError = ex;
        }
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        try
        {
            if (_game is null)
                throw new InvalidOperationException("MonoGame could not create the Project Z render window.", _gameCreationError);
            _renderWindow = ResolveRenderWindow(_game);

            SetParent(_renderWindow, hwndParent.Handle);

            var style = GetWindowLongPtr(_renderWindow, GwlStyle).ToInt64();
            style &= ~(WsPopup | WsCaption | WsThickFrame | WsSysMenu);
            style |= WsChild | WsVisible | WsClipSiblings | WsClipChildren;
            SetWindowLongPtr(_renderWindow, GwlStyle, new IntPtr(style));

            ResizeNativeWindow((int)_plugin.EditorWidth, (int)_plugin.EditorHeight);
            _game.RunOneFrame();

            // WindowsDX can recreate its WinForms handle while creating the
            // graphics device. HwndHost must return the HWND that owns the
            // initialized swap chain, not an earlier placeholder handle.
            var initializedRenderWindow = IsWindow(_game.Window.Handle)
                ? _game.Window.Handle
                : _renderWindow;
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

            AttachInputWindow();
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
        DetachInputWindow();
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

    private void AttachInputWindow()
    {
        if (_renderWindow == IntPtr.Zero)
            throw new InvalidOperationException("Project Z did not receive a DirectX child HWND.");

        _inputSubclassAttached = SetWindowSubclass(
            _renderWindow, _windowSubclassProc, new UIntPtr(1), IntPtr.Zero);
        if (!_inputSubclassAttached)
            Logger.Log("Project Z is using HwndHost message dispatch for the native DirectX child HWND.");
    }

    private void DetachInputWindow()
    {
        if (_inputSubclassAttached && _renderWindow != IntPtr.Zero)
            RemoveWindowSubclass(_renderWindow, _windowSubclassProc, new UIntPtr(1));
        _inputSubclassAttached = false;
    }

    private IntPtr InputWindowSubclass(
        IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, IntPtr referenceData)
    {
        switch ((int)message)
        {
            case WmGetDlgCode when IsInteractive(out _, out _):
                // Request arrows, tab, characters, and all other keys while the
                // embedded editor owns focus instead of letting the DAW consume them.
                return new IntPtr(0x0087);

            case WmMouseMove:
            case WmLButtonDown:
            case WmLButtonUp:
            case WmRButtonDown:
            case WmRButtonUp:
            case WmMouseLeave:
            case WmKeyDown:
            case WmKeyUp:
            case WmSysKeyDown:
            case WmSysKeyUp:
                var handled = false;
                var result = WndProc(window, (int)message, wParam, lParam, ref handled);
                if (handled)
                    return result;
                break;

            case WmKillFocus:
            case WmCancelMode:
            case WmCaptureChanged:
                _trackingMouse = false;
                _game?.CancelInput();
                break;

            case WmShowWindow when wParam == IntPtr.Zero:
            case WmSize when unchecked((int)wParam.ToInt64()) == SizeMinimized:
                _trackingMouse = false;
                _game?.CancelInput();
                break;
        }
        return DefSubclassProc(window, message, wParam, lParam);
    }

    internal Task ImportDroppedFilesAsync(IReadOnlyList<string> paths)
    {
        if (_game is null || paths.Count == 0)
            return Task.CompletedTask;
        return _game.ImportDroppedFilesAsync(paths);
    }

    internal IntPtr RenderWindowForTesting => _renderWindow;
    internal string PromptTextForTesting => _game?.PromptTextForTesting ?? string.Empty;
    internal bool PromptSelectedForTesting => _game?.PromptSelectedForTesting == true;
    internal bool PromptMouseOverForTesting => _game?.PromptMouseOverForTesting == true;
    internal Microsoft.Xna.Framework.Rectangle PromptBoundsForTesting =>
        _game?.PromptBoundsForTesting ?? Microsoft.Xna.Framework.Rectangle.Empty;
    internal long HandledInputMessageCountForTesting => _handledInputMessageCount;
    internal bool LastInputWasInteractiveForTesting => _lastInputWasInteractive;

    internal void ActivateInput()
    {
        Focus();
        if (_renderWindow != IntPtr.Zero && IsInteractive(out _, out _))
            SetFocus(_renderWindow);
    }

    internal void ForwardKeyDown(int virtualKey, bool isRepeat) =>
        _game?.InjectKeyDown(virtualKey, isRepeat);

    internal void ForwardKeyUp(int virtualKey) =>
        _game?.InjectKeyUp(virtualKey);

    protected override IntPtr WndProc(
        IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (message)
        {
            case WmMouseMove:
                _handledInputMessageCount++;
                _lastInputWasInteractive = TryGetInteractivePointer(
                    lParam, out var moveX, out var moveY, out var moveWidth, out var moveHeight);
                if (_lastInputWasInteractive)
                {
                    BeginMouseLeaveTracking(hwnd);
                    _game?.InjectMouseMove(moveX, moveY, moveWidth, moveHeight);
                }
                else
                {
                    _game?.CancelInput();
                }
                handled = true;
                return IntPtr.Zero;

            case WmLButtonDown:
                _handledInputMessageCount++;
                _lastInputWasInteractive = TryGetInteractivePointer(
                    lParam, out var downX, out var downY, out var downWidth, out var downHeight);
                if (_lastInputWasInteractive)
                {
                    SetFocus(hwnd);
                    SetCapture(hwnd);
                    _game?.InjectMouseLeftDown(downX, downY, downWidth, downHeight);
                }
                else
                {
                    _game?.CancelInput();
                }
                handled = true;
                return IntPtr.Zero;

            case WmLButtonUp:
                _handledInputMessageCount++;
                _lastInputWasInteractive = TryGetInteractivePointer(
                    lParam, out var upX, out var upY, out var upWidth, out var upHeight);
                if (_lastInputWasInteractive)
                    _game?.InjectMouseLeftUp(upX, upY, upWidth, upHeight);
                else
                    _game?.CancelInput();
                if (GetCapture() == hwnd)
                    ReleaseCapture();
                handled = true;
                return IntPtr.Zero;

            case WmRButtonDown:
                _handledInputMessageCount++;
                _lastInputWasInteractive = TryGetInteractivePointer(
                    lParam, out var rightX, out var rightY, out var rightWidth, out var rightHeight);
                if (_lastInputWasInteractive)
                {
                    SetFocus(hwnd);
                    _game?.InjectMouseRightDown(rightX, rightY, rightWidth, rightHeight);
                }
                handled = true;
                return IntPtr.Zero;

            case WmRButtonUp:
                _handledInputMessageCount++;
                _lastInputWasInteractive = TryGetInteractivePointer(
                    lParam, out var rightUpX, out var rightUpY, out var rightUpWidth, out var rightUpHeight);
                if (_lastInputWasInteractive)
                    _game?.InjectMouseRightUp(rightUpX, rightUpY, rightUpWidth, rightUpHeight);
                handled = true;
                return IntPtr.Zero;

            case WmMouseLeave:
                _trackingMouse = false;
                _game?.InjectMouseLeave();
                handled = true;
                return IntPtr.Zero;

            case WmKeyDown:
            case WmSysKeyDown:
                _handledInputMessageCount++;
                _lastInputWasInteractive = IsInteractive(out _, out _);
                if (_lastInputWasInteractive)
                    _game?.InjectKeyDown(unchecked((int)wParam.ToInt64()), (lParam.ToInt64() & (1L << 30)) != 0);
                else
                    _game?.CancelInput();
                handled = true;
                return IntPtr.Zero;

            case WmKeyUp:
            case WmSysKeyUp:
                _handledInputMessageCount++;
                _lastInputWasInteractive = IsInteractive(out _, out _);
                if (_lastInputWasInteractive)
                    _game?.InjectKeyUp(unchecked((int)wParam.ToInt64()));
                else
                    _game?.CancelInput();
                handled = true;
                return IntPtr.Zero;

            case WmKillFocus:
            case WmCancelMode:
            case WmCaptureChanged:
                _trackingMouse = false;
                _game?.CancelInput();
                break;

            case WmShowWindow when wParam == IntPtr.Zero:
            case WmSize when unchecked((int)wParam.ToInt64()) == SizeMinimized:
                _trackingMouse = false;
                _game?.CancelInput();
                break;
        }

        return base.WndProc(hwnd, message, wParam, lParam, ref handled);
    }

    private bool TryGetInteractivePointer(
        IntPtr lParam, out int x, out int y, out int width, out int height)
    {
        x = unchecked((short)(lParam.ToInt64() & 0xFFFF));
        y = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));
        return IsInteractive(out width, out height);
    }

    private bool IsInteractive(out int width, out int height)
    {
        width = 0;
        height = 0;
        if (_renderWindow == IntPtr.Zero || !IsWindowVisible(_renderWindow) ||
            !GetClientRect(_renderWindow, out var clientRect))
            return false;

        var root = GetAncestor(_renderWindow, GaRoot);
        if (root != IntPtr.Zero && IsIconic(root))
            return false;

        width = clientRect.Right - clientRect.Left;
        height = clientRect.Bottom - clientRect.Top;
        return width > 0 && height > 0;
    }

    private void BeginMouseLeaveTracking(IntPtr window)
    {
        if (_trackingMouse)
            return;
        var tracking = new TrackMouseEvent
        {
            Size = (uint)Marshal.SizeOf<TrackMouseEvent>(),
            Flags = TmeLeave,
            TrackWindow = window
        };
        _trackingMouse = TrackMouseEventNative(ref tracking);
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

    private static IntPtr ResolveRenderWindow(ProjectZPluginGame game)
    {
        // WindowsDX exposes the HWND directly. The 3.8.5.1 native backend
        // exposes its SDL_Window pointer instead, so locate the corresponding
        // top-level HWND using the unique title assigned by ProjectZPluginGame.
        var platformHandle = game.Window.Handle;
        if (platformHandle != IntPtr.Zero && IsWindow(platformHandle))
            return platformHandle;

        var resolved = IntPtr.Zero;
        var currentProcessId = (uint)Environment.ProcessId;
        EnumWindowsProc findWindow = (window, _) =>
        {
            GetWindowThreadProcessId(window, out var processId);
            if (processId != currentProcessId)
                return true;

            var titleLength = GetWindowTextLength(window);
            if (titleLength <= 0)
                return true;

            var title = new StringBuilder(titleLength + 1);
            GetWindowText(window, title, title.Capacity);
            if (!string.Equals(title.ToString(), game.RenderWindowTitle, StringComparison.Ordinal))
                return true;

            resolved = window;
            return false;
        };

        EnumThreadWindows(GetCurrentThreadId(), findWindow, IntPtr.Zero);
        if (resolved == IntPtr.Zero)
            EnumWindows(findWindow, IntPtr.Zero);
        if (resolved == IntPtr.Zero)
            throw new InvalidOperationException("Could not resolve MonoGame's native DirectX HWND.");
        return resolved;
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

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumThreadWindows(
        uint threadId, EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrackMouseEvent
    {
        public uint Size;
        public uint Flags;
        public IntPtr TrackWindow;
        public uint HoverTime;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCapture(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetCapture();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", EntryPoint = "TrackMouseEvent")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TrackMouseEventNative(ref TrackMouseEvent tracking);

    private delegate IntPtr WindowSubclassProc(
        IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, IntPtr referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        IntPtr window, WindowSubclassProc callback, UIntPtr subclassId, IntPtr referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        IntPtr window, WindowSubclassProc callback, UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
