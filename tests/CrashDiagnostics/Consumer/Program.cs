using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using ProjectZ.RuntimeDiagnostics;
using ProjectZ.Shared.Drawing;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern void RaiseFailFastException(IntPtr record, IntPtr context, uint flags);
    [DllImport("ProjectZ.NativeExceptionFixture", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ThrowGraphicsError();

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--native-graphics-origin"))
        {
            ApplicationDiagnostics.Initialize(new DiagnosticOptions { ShowErrorDialogs = false });
            try { ThrowGraphicsError(); }
            catch (SEHException error) { ApplicationDiagnostics.Report("Handled graphics failure", error); }
            return;
        }
        if (args.Contains("--managed"))
        {
            ApplicationDiagnostics.Initialize(new DiagnosticOptions { ShowErrorDialogs = false });
            throw new InvalidOperationException("Project-Z managed diagnostic test", new IOException("Project-Z inner diagnostic test"));
        }
        if (args.Contains("--force-exit"))
        {
            ApplicationDiagnostics.Initialize(new DiagnosticOptions { ShowErrorDialogs = false });
            Thread.Sleep(30000);
            return;
        }
        if (args.Contains("--native"))
        {
            ApplicationDiagnostics.Initialize(new DiagnosticOptions { ShowErrorDialogs = false });
            RaiseFailFastException(IntPtr.Zero, IntPtr.Zero, 0);
            return;
        }
        using var game = new ConsumerGame(args[0]);
        game.Run();
    }

    private sealed class ConsumerGame : Game
    {
        private readonly GraphicsDeviceManager graphics;
        private readonly string result;
        public ConsumerGame(string result)
        {
            this.result = result;
            graphics = new GraphicsDeviceManager(this);
        }
        protected override void LoadContent()
        {
            // No diagnostics bootstrap, helper arguments or heirowSnap references.
            using var scenes = new SceneManager(this, 60);
            if (!File.Exists(ApplicationDiagnostics.SessionPath)) throw new Exception("Automatic diagnostics did not start");
            var observerLog = Path.ChangeExtension(ApplicationDiagnostics.SessionPath, ".observer.log");
            if (!File.Exists(observerLog) || !File.ReadAllText(observerLog).Contains("Native observer attached")) throw new Exception("Packaged observer did not attach");
            File.WriteAllText(result, "PASS: independent Project-Z consumer automatically starts diagnostics; " + ApplicationDiagnostics.SessionPath);
            Exit();
        }
    }
}
