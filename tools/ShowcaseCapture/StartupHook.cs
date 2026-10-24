using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

// Capture the installed application in a separate offline process. No application
// source is embedded in this helper, and no OS input is sent to other windows.
public static class StartupHook
{
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object? Read(object target, string name) => target.GetType().GetProperty(name, Members)?.GetValue(target)
        ?? target.GetType().GetField(name, Members)?.GetValue(target);
    static void Write(object target, string name, object value)
    {
        var property = target.GetType().GetProperty(name, Members);
        if (property != null) property.SetValue(target, value);
        else (target.GetType().GetField(name, Members) ?? throw new MissingFieldException(name)).SetValue(target, value);
    }
    static object? Call(object target, string name, params object[] args) =>
        (target.GetType().GetMethods(Members).Single(method => method.Name == name && method.GetParameters().Length == args.Length)).Invoke(target, args);

    [DllImport("user32.dll")] static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    public static void Initialize()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROJECTZ_SHOWCASE_OUTPUT"))) return;
        var captureThread = new Thread(Capture);
        captureThread.SetApartmentState(ApartmentState.STA);
        captureThread.Start();
        captureThread.Join();
    }

    static void Capture()
    {
        string? output = Environment.GetEnvironmentVariable("PROJECTZ_SHOWCASE_OUTPUT");
        if (string.IsNullOrEmpty(output)) return;
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            string profile = Path.Combine(output, "Profile");
            Directory.CreateDirectory(profile);
            Environment.SetEnvironmentVariable("HEIROWSNAP_FACEBOOK_TEST_ROOT", profile);
            Environment.SetEnvironmentVariable("PROJECTZ_DESKTOP_PROBE_DIR", null);
            Environment.SetEnvironmentVariable("PROJECTZ_TARGET_FPS", "60");
            var app = Assembly.Load("heirowSnap.ProjectZ");
            Type settingsType = app.GetTypes().Single(type => type.Name == "HeirowSnapSettings");
            var settings = Activator.CreateInstance(settingsType)!;
            Write(settings, "AntiAliasingSamples", 16);
            Write(settings, "DwmBlurPercent", 68);
            Write(settings, "DwmBlurEnabled", false);
            Write(settings, "MainBackgroundOpacity", 30);
            Write(settings, "AutomaticPresence", false);
            Write(settings, "WindowWidth", 876);
            Write(settings, "WindowHeight", 711);
            Call(settings, "Save");

            var shared = Assembly.Load("SharedClasses");
            Type legacy = shared.GetTypes().Single(type => type.Name == "wSettingsFile");
            legacy.GetProperty("ConfigFile")!.SetValue(null, Path.Combine(profile, "heirowSnapConfig.json"));
            CreateMedia(Path.Combine(profile, "My Files"));

            using var backdrop = new BackdropProcess(output);
            var game = Activator.CreateInstance(app.GetTypes().Single(type => type.Name == "HeirowSnapGame"))!;
            int frame = 0;
            bool initialized = false;
            Exception? failure = null;
            var clock = Stopwatch.StartNew();
            // Actions are dispatched on the actual game's owning scene thread.
            var driver = Task.Run(async () =>
            {
                await Task.Delay(1800);
                while (frame < 150 && failure == null)
                {
                    Call(game, "Enqueue", (Action)(() =>
                    {
                        try
                        {
                            if (Read(game, "MainScene") is not object scene) return;
                            if (!initialized)
                            {
                                Call(game, "ShowMainForAppearanceProbe");
                                nint handle = (nint)Read(Read(game, "composition")!, "Handle")!;
                                SetWindowPos(handle, new nint(-1), 100, 110, 876, 711, 0x50);
                                SetWindowPos(backdrop.Handle, handle, 40, 40, 1440, 900, 0x50);
                                Call(scene, "LoadGalleryForProbe");
                                initialized = true;
                            }
                            backdrop.Phase = frame / 150f;
                            backdrop.Refresh();
                            Write(Read(scene, "quotaText")!, "Text", "Showcase gallery - 12 staged images");
                            Call(scene, "SetStatus", "Native DX12 / 16x SSAA / anisotropic 16x", false);
                            var surface = Read(game, "alphaSurface")!;
                            var mainBackdrop = Read(game, "backdropHost")!;
                            nint mainHandle = (nint)Read(Read(game, "composition")!, "Handle")!;
                            SetWindowPos(mainHandle, new nint(-1), frame >= 60 ? 70 : 100, 110, frame >= 60 ? 750 : 876, 711, 0x50);
                            SetWindowPos(backdrop.Handle, mainHandle, 40, 40, 1440, 900, 0x50);
                            Call(mainBackdrop, "Tick");
                            Call(surface, "Synchronize");
                            if (frame == 8)
                            {
                                Write(settings, "DwmBlurEnabled", true);
                                Call(settings, "Save");
                                Call(game, "ApplyGraphicsSettings", settings);
                            }
                            if (frame == 12)
                            {
                                var native = app.GetTypes().Single(type => type.Name == "ApplicationBackdrop");
                                var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                                var error = native.GetProperty("LastError", flags)!.GetValue(null)?.ToString();
                                var count = native.GetProperty("NativeWindowCount", flags)!.GetValue(null);
                                File.WriteAllText(Path.Combine(output, "native-blur.txt"), $"STA={Thread.CurrentThread.GetApartmentState()}; windows={count}; error={error}");
                                if (!string.IsNullOrEmpty(error) || Convert.ToInt32(count) == 0) throw new InvalidOperationException("Native DWM blur is unavailable: " + error);
                            }
                            if (frame >= 22 && frame < 58)
                            {
                                var scroll = Read(scene, "galleryScroll")!;
                                var extent = Read(scroll, "ScrollableSize")!;
                                float height = Convert.ToSingle(Read(extent, "Y"));
                                var vector = Activator.CreateInstance(extent.GetType(), 0f, height * (frame - 22) / 36f)!;
                                Call(scroll, "ScrollTo", vector);
                            }
                            if (frame == 60)
                            {
                                var scroll = Read(scene, "galleryScroll")!;
                                Call(scroll, "ScrollTo", Activator.CreateInstance(Read(scroll, "ScrollableSize")!.GetType(), 0f, 0f)!);
                                SetWindowPos((nint)Read(Read(game, "composition")!, "Handle")!, new nint(-1), 70, 110, 750, 711, 0x50);
                                Call(game, "ShowSettingsWindow");
                                var desktop = Read(game, "desktopWindows")!;
                                Write(desktop, "settingsPage", "Graphics");
                                Call(desktop, "BuildSettings");
                                var tool = Read(desktop, "settingsWindow")!;
                                var form = (Form)Read(tool, "Window")!;
                                form.TopMost = true;
                                form.Location = new Point(850, 150);
                                form.BringToFront();
                            }
                            if (frame >= 60)
                            {
                                var tool = Read(Read(game, "desktopWindows")!, "settingsWindow")!;
                                ((Form)Read(tool, "Window")!).BringToFront();
                                var surface2 = Read(tool, "alphaPresenter");
                                if (surface2 != null) Call(surface2, "Synchronize");
                            }
                            using var image = new Bitmap(1440, 900, PixelFormat.Format24bppRgb);
                            using (var painter = Graphics.FromImage(image)) painter.CopyFromScreen(40, 40, 0, 0, image.Size);
                            image.Save(Path.Combine(output, $"frame-{frame:D4}.png"));
                            if (frame == 18) image.Save(Path.Combine(output, "gallery.png"));
                            if (frame == 92) image.Save(Path.Combine(output, "glass-settings.png"));
                            if (frame == 145) image.Save(Path.Combine(output, "accent.png"));
                            frame++;
                            if (frame == 150) Call(game, "Exit");
                        }
                        catch (Exception error) { failure = error; Call(game, "Exit"); }
                    }));
                    await Task.Delay(100);
                    if (clock.Elapsed.TotalSeconds > 55) { failure = new TimeoutException("Showcase capture exceeded 55 seconds."); Call(game, "Exit"); }
                }
            });
            Call(game, "Run");
            driver.Wait(TimeSpan.FromSeconds(2));
            (game as IDisposable)?.Dispose();
            if (failure != null) throw failure;
            if (frame != 150) throw new InvalidOperationException($"Expected 150 frames; captured {frame}.");
            File.WriteAllText(Path.Combine(output, "capture.txt"),
                $"PASS {frame} live frames; {FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion}; AA16; anisotropic16; native DWM blur68; background opacity30; isolated offline profile; staged abstract media; gallery scroll and Graphics panel.\n");
            backdrop.Dispose();
            Environment.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "capture-error.txt"), error.ToString());
            Environment.Exit(1);
        }
    }

    static void CreateMedia(string folder)
    {
        Directory.CreateDirectory(folder);
        string[] names = ["Aurora", "Prism", "Lagoon", "Afterglow", "Orbit", "Bloom", "Spectrum", "Tide", "Flux", "Meadow", "Solstice", "Dusk"];
        Color[] palette = [Color.FromArgb(74, 232, 177), Color.FromArgb(119, 102, 249), Color.FromArgb(38, 186, 228), Color.FromArgb(247, 135, 122)];
        for (int i = 0; i < names.Length; i++)
        {
            using var bitmap = new Bitmap(640, 480);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new LinearGradientBrush(new Rectangle(0, 0, 640, 480), palette[i % 4], Color.FromArgb(11, 28, 54), 55f);
            g.FillRectangle(fill, 0, 0, 640, 480);
            using var sun = new SolidBrush(Color.FromArgb(220, 245, 231, 178));
            g.FillEllipse(sun, 390 - (i % 3) * 45, 70, 100, 100);
            using var ridge = new SolidBrush(Color.FromArgb(135, 12, 35, 58));
            g.FillPolygon(ridge, [new(0, 355), new(120, 160 + i * 7), new(290, 330), new(430, 185), new(640, 340), new(640, 480), new(0, 480)]);
            using var foreground = new SolidBrush(Color.FromArgb(210, 8, 29, 44));
            g.FillEllipse(foreground, -150, 345, 1000, 450);
            bitmap.Save(Path.Combine(folder, $"{i + 1:D2}-{names[i]}.png"));
        }
    }

    public sealed class Backdrop : Form
    {
        public float Phase;
        public Backdrop()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(40, 40, 1440, 900);
            DoubleBuffered = true;
        }
        protected override bool ShowWithoutActivation => true;
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new LinearGradientBrush(ClientRectangle, Color.FromArgb(17, 62, 102), Color.FromArgb(72, 23, 114), 20 + Phase * 35);
            fill.InterpolationColors = new ColorBlend { Positions = [0, .28f, .58f, .82f, 1], Colors = [Color.FromArgb(21, 156, 187), Color.FromArgb(27, 182, 139), Color.FromArgb(82, 77, 187), Color.FromArgb(212, 70, 137), Color.FromArgb(241, 160, 75)] };
            g.FillRectangle(fill, ClientRectangle);
            for (int i = 0; i < 5; i++)
            {
                using var path = new GraphicsPath();
                path.AddEllipse(-220 + i * 380 + (int)(Math.Sin(Phase * 6 + i) * 85), -180 + i % 2 * 500, 950, 950);
                using var glow = new PathGradientBrush(path) { CenterColor = Color.FromArgb(95, i % 2 == 0 ? Color.Aquamarine : Color.HotPink), SurroundColors = [Color.Transparent] };
                g.FillPath(glow, path);
            }
            using var ring = new Pen(Color.FromArgb(175, 255, 214, 110), 62);
            g.DrawEllipse(ring, 120 + (int)(Phase * 80), -110, 590, 590);
            using var ribbon = new Pen(Color.FromArgb(135, 103, 244, 228), 90);
            g.DrawBezier(ribbon, new Point(730, 900), new Point(520, 480), new Point(1310, 620), new Point(1450, 50));
            using var orbit = new Pen(Color.FromArgb(160, 255, 156, 207), 34);
            g.DrawEllipse(orbit, 1070 - (int)(Phase * 65), 220, 420, 420);
            using var dot = new SolidBrush(Color.FromArgb(135, 230, 255, 250));
            for (int y = 290; y < 720; y += 70)
                for (int x = 900; x < 1440; x += 70)
                    g.FillEllipse(dot, x + (int)(Phase * 25), y, 12, 12);
        }
    }

    sealed class BackdropProcess : IDisposable
    {
        readonly Process process;
        readonly string folder;
        public nint Handle { get; }
        public float Phase { set => File.WriteAllText(Path.Combine(folder, "backdrop.phase"), value.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        public void Refresh() { }
        public BackdropProcess(string output)
        {
            folder = output;
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add(typeof(StartupHook).Assembly.Location);
            start.ArgumentList.Add("--backdrop");
            start.ArgumentList.Add(output);
            start.Environment.Remove("DOTNET_STARTUP_HOOKS");
            start.Environment.Remove("PROJECTZ_SHOWCASE_OUTPUT");
            process = Process.Start(start)!;
            string ready = Path.Combine(output, "backdrop.ready");
            var timer = Stopwatch.StartNew();
            while (!File.Exists(ready) && !process.HasExited && timer.Elapsed.TotalSeconds < 10) Thread.Sleep(20);
            if (!File.Exists(ready)) throw new InvalidOperationException("The separate desktop backdrop did not become ready.");
            Handle = new nint(long.Parse(File.ReadAllText(ready)));
        }
        public void Dispose()
        {
            if (!process.HasExited) { process.CloseMainWindow(); if (!process.WaitForExit(2000)) process.Kill(); }
            process.Dispose();
        }
    }
}

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args is not ["--backdrop", var folder]) throw new ArgumentException("This executable is a showcase startup hook and backdrop fixture.");
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        using var form = new StartupHook.Backdrop();
        form.Show(); form.Hide(); form.Show();
        File.WriteAllText(Path.Combine(folder, "backdrop.ready"), form.Handle.ToInt64().ToString());
        using var timer = new System.Windows.Forms.Timer { Interval = 60 };
        timer.Tick += (_, _) =>
        {
            string phase = Path.Combine(folder, "backdrop.phase");
            if (File.Exists(phase) && float.TryParse(File.ReadAllText(phase), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
            { form.Phase = value; form.Refresh(); }
        };
        timer.Start();
        Application.Run(form);
    }
}
