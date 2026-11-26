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
        var outputDirectory = Environment.GetEnvironmentVariable("PROJECTZ_DIAGNOSTICS_TEST_DIRECTORY");
        var options = new DiagnosticOptions { DirectoryPath = outputDirectory, ShowErrorDialogs = false };
        if (args.Length == 2 && args[0] == "--wait-for-exit-signal")
        {
            ApplicationDiagnostics.Initialize(options);
            if (!SpinWait.SpinUntil(() => File.Exists(args[1]), TimeSpan.FromSeconds(30)))
                throw new TimeoutException("The diagnostics test did not signal the application to exit.");
            return;
        }
        if (args.Contains("--handle-churn"))
        {
            ApplicationDiagnostics.Initialize(options);
            // Short-lived native threads force debugger handle reuse. Keep
            // independent process/event handles alive across their exit events.
            for (int round = 0; round < 120; round++)
            {
                using var ownProcess = System.Diagnostics.Process.GetCurrentProcess();
                _ = ownProcess.Handle;
                using var signal = new ManualResetEvent(false);
                var thread = new Thread(() => signal.Set());
                thread.Start();
                if (!signal.WaitOne(5000)) throw new Exception("Thread signal was lost");
                thread.Join();
                Thread.Sleep(15);
                if (ownProcess.HasExited || !signal.WaitOne(0)) throw new Exception("Independent handle was invalidated");
            }
            Thread.Sleep(1300); // Exercise the observer's timeout/HasExited path.
            return;
        }
        if (args.Contains("--native-graphics-origin"))
        {
            ApplicationDiagnostics.Initialize(options);
            try { ThrowGraphicsError(); }
            catch (SEHException error) { ApplicationDiagnostics.Report("Handled graphics failure", error); }
            return;
        }
        if (args.Contains("--managed"))
        {
            ApplicationDiagnostics.Initialize(options);
            throw new InvalidOperationException("Project-Z managed diagnostic test", new IOException("Project-Z inner diagnostic test"));
        }
        if (args.Contains("--force-exit"))
        {
            ApplicationDiagnostics.Initialize(options);
            Thread.Sleep(30000);
            return;
        }
        if (args.Contains("--native"))
        {
            ApplicationDiagnostics.Initialize(options);
            RaiseFailFastException(IntPtr.Zero, IntPtr.Zero, 0);
            return;
        }
        if (outputDirectory != null)
            ApplicationDiagnostics.Initialize(options);
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
            // The default invocation starts diagnostics through the framework.
            using var scenes = new SceneManager(this, 60);
            if (!File.Exists(ApplicationDiagnostics.SessionPath)) throw new Exception("Automatic diagnostics did not start");
            var observerLog = Path.ChangeExtension(ApplicationDiagnostics.SessionPath, ".observer.log");
            if (!File.Exists(observerLog) || !File.ReadAllText(observerLog).Contains("Native observer attached")) throw new Exception("Packaged observer did not attach");
            File.WriteAllText(result, "PASS: independent Project-Z consumer has diagnostics; " + ApplicationDiagnostics.SessionPath);
            Exit();
        }
    }
}
