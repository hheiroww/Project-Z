using ProjectZ.Windows.Composition;
using System.Runtime.InteropServices;
using System.Text.Json;

static class Program
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    [STAThread] static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        var settings = new WindowAppearanceSettings { BackgroundOpacity = -3 };
        Require(settings.BackgroundOpacity == 0, "opacity is clamped at zero");
        settings.BackgroundOpacity = 110;
        Require(settings.BackgroundOpacity == 100, "opacity is clamped at one hundred");
        settings.BackgroundOpacity = 37;
        var restored = JsonSerializer.Deserialize<WindowAppearanceSettings>(JsonSerializer.Serialize(settings))!;
        Require(restored.BackgroundOpacity == 37 && restored.DwmBlurEnabled, "framework settings persist without application-specific fields");
        using var first = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size(400, 300), ShowInTaskbar = false };
        using var second = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = new Size(400, 300), ShowInTaskbar = false };
        var appearance = new WindowAppearance();
        int firstOpacity = 0, secondOpacity = 0, callbacks = 0;
        using var a = appearance.Bind(first.Handle, s => { firstOpacity = s.BackgroundOpacity; callbacks++; });
        using (var b = appearance.Bind(second.Handle, s => secondOpacity = s.BackgroundOpacity))
        {
            first.Show(); second.Show(); Application.DoEvents();
            Require(first.ClientSize == new Size(400, 300), "borderless style changes preserve the full client pixel area");
            appearance.Update(settings);
            Require(firstOpacity == 37 && secondOpacity == 37, "one appearance change updates existing windows together");
            settings.BackgroundOpacity = 91;
            Require(appearance.Settings.BackgroundOpacity == 37, "the controller owns a snapshot of its settings");
            var point = first.PointToScreen(new Point(first.ClientSize.Width - 2, first.ClientSize.Height - 2));
            var packed = new IntPtr((point.Y << 16) | (point.X & 65535));
            Require(SendMessage(first.Handle, 0x84, IntPtr.Zero, packed).ToInt32() == 17, "a real bottom-right HWND hit test selects diagonal resizing");
            Require(SendMessage(first.Handle, 0x20, first.Handle, new IntPtr(17 | (0x200 << 16))).ToInt32() == 1, "the native resize cursor is handled");
            Require(BorderlessWindowChrome.CursorForHitTest(17) == Cursors.SizeNWSE, "bottom-right resizing uses the diagonal cursor");
        }
        var before = callbacks;
        appearance.Update(new WindowAppearanceSettings { BackgroundOpacity = 64, DwmBlurEnabled = false });
        Require(callbacks == before + 1 && secondOpacity == 37, "disposed windows are detached from shared changes");
        first.Hide(); second.Hide();

        foreach (var monitor in Screen.AllScreens)
        {
            var work = monitor.WorkingArea;
            using var window = new PassiveForm
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(work.Left + 30, work.Top + 40, 460, 300),
                MinimumSize = new Size(300, 200)
            };
            using var binding = appearance.Bind(window.Handle);
            window.Show();
            Application.DoEvents();
            var restoredBounds = window.Bounds;

            window.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            var client = window.RectangleToScreen(window.ClientRectangle);
            Require(window.WindowState == FormWindowState.Maximized && window.Bounds == work && client == work,
                $"borderless maximize uses the work area on {monitor.DeviceName}: window={window.Bounds}, client={client}, work={work}");
            Require(SetWindowPos(window.Handle, IntPtr.Zero, work.Left + 1, work.Top + 1, work.Width + 32, work.Height + 32, 0x14)
                && window.RectangleToScreen(window.ClientRectangle) == work,
                "an oversized native position request stays inside the work area while maximized");
            window.WindowState = FormWindowState.Normal;
            Application.DoEvents();
            Require(window.Bounds == restoredBounds && window.MinimumSize == new Size(300, 200),
                "restore returns to the original bounds and preserves the form's minimum size");
        }
    }

    private sealed class PassiveForm : Form
    {
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return parameters;
            }
        }
    }
}
