using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

internal sealed class NativeBackdropFixture : IDisposable
{
#if PROJECTZ_WINDOWS_RUNTIME
    private sealed class BackgroundWindow : System.Windows.Forms.Form
    {
        protected override bool ShowWithoutActivation => true;
        protected override System.Windows.Forms.CreateParams CreateParams
        {
            get { var value = base.CreateParams; value.ExStyle |= 0x08000000; return value; }
        }
    }
    private readonly System.Windows.Forms.Form window;
    private readonly IntPtr host;
    private readonly bool wasTopmost;
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    internal NativeBackdropFixture(IntPtr host)
    {
        this.host = host;
        wasTopmost = (GetWindowLongPtr(host, -20).ToInt64() & 8) != 0;
        window = new BackgroundWindow { Text = "Project-Z controlled capture background", StartPosition = System.Windows.Forms.FormStartPosition.Manual,
            Bounds = new System.Drawing.Rectangle(350, 250, 900, 700), FormBorderStyle = System.Windows.Forms.FormBorderStyle.None, BackColor = System.Drawing.Color.Lime, ShowInTaskbar = false };
        window.Show(); System.Windows.Forms.Application.DoEvents();
        // Keep the controlled pair above unrelated active applications without
        // activating either window or changing the user's keyboard focus.
        SetWindowPos(window.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
        SetWindowPos(host, new IntPtr(-1), 0, 0, 0, 0, 0x13);
    }
    public void Dispose()
    {
        SetWindowPos(host, new IntPtr(wasTopmost ? -1 : -2), 0, 0, 0, 0, 0x13);
        window.Dispose();
    }
#else
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr CreateWindow([MarshalAs(UnmanagedType.LPUTF8Str)]string title, int x, int y, int width, int height, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr CreateRenderer(IntPtr window, int index, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetColor(IntPtr renderer, byte red, byte green, byte blue, byte alpha);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Clear(IntPtr renderer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Present(IntPtr renderer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void WindowAction(IntPtr window);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetContext();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int MakeCurrent(IntPtr window, IntPtr context);
    private readonly IntPtr library, window, renderer, host, context;
    private T Get<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
    internal NativeBackdropFixture(IntPtr host)
    {
        library = NativeLibrary.Load("libSDL2-2.0.so.0", typeof(Game).Assembly, null);
        this.host = host;
        context = Get<GetContext>("SDL_GL_GetCurrentContext")();
        window = Get<CreateWindow>("SDL_CreateWindow")("Project-Z controlled capture background", 350, 250, 900, 700, 4 | 16);
        renderer = Get<CreateRenderer>("SDL_CreateRenderer")(window, -1, 1);
        Program.Require(window != IntPtr.Zero && renderer != IntPtr.Zero, "Controlled X11 capture window could not be created.");
        Get<SetColor>("SDL_SetRenderDrawColor")(renderer, 0, 255, 0, 255);
        Get<Clear>("SDL_RenderClear")(renderer); Get<Present>("SDL_RenderPresent")(renderer);
        Get<WindowAction>("SDL_RaiseWindow")(host);
        Program.Require(Get<MakeCurrent>("SDL_GL_MakeCurrent")(host, context) == 0, "The host OpenGL context could not be restored after drawing the fixture.");
        Console.WriteLine("NATIVE_CAPTURE_FIXTURE_READY");
    }
    public void Dispose()
    {
        if (renderer != IntPtr.Zero) Get<WindowAction>("SDL_DestroyRenderer")(renderer);
        if (window != IntPtr.Zero) Get<WindowAction>("SDL_DestroyWindow")(window);
        Get<MakeCurrent>("SDL_GL_MakeCurrent")(host, context);
        NativeLibrary.Free(library);
    }
#endif
}
