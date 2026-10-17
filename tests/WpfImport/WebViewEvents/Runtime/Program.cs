using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using ProjectZ.WpfCompatibility;
using ProjectZ.WpfCompatibility.Controls;
using ProjectZ.Shared.Drawing.UI;
using Forms = System.Windows.Forms;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using Button = ProjectZ.WpfCompatibility.Controls.Button;

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        try
        {
            var output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/wpf-import/webview-events-runtime");
            Directory.CreateDirectory(output);
            using var surface = new NativeWindowSurface("Project-Z WebView2 and routed events", 520, 380);
            surface.SetVisible(true);
            using var app = new ProjectZ.WpfCompatibility.Application(surface.Scene);
            using var view = new BrowserEvents.View();
            var browser = (ProjectZ.WpfCompatibility.Controls.WebView2)view.FindName("Browser")!;
            var editor = (ProjectZ.WpfCompatibility.Controls.TextBox)view.FindName("Editor")!;
            var rows = (ProjectZ.WpfCompatibility.Controls.ListBox)view.FindName("Rows")!;
            var crop = (ProjectZ.WpfCompatibility.Controls.Border)view.FindName("Crop")!;
            var action = (Button)view.FindName("Action")!;
            surface.Scene.AddElement(view.NativeElement);
            void Frame() { surface.RenderFrame(); app.Dispatcher.Pump(); }
            void Until(Func<bool> ready, string step)
            {
                var timer = Stopwatch.StartNew();
                while (!ready()) { if (timer.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException(step); Frame(); Thread.Sleep(8); }
            }
            T Complete<T>(Task<T> task, string step) { Until(() => task.IsCompleted, step); return task.GetAwaiter().GetResult(); }
            for (int i = 0; i < 6; i++) Frame();
            var initialize = browser.EnsureCoreWebView2Async();
            Until(() => initialize.IsCompleted, "WebView2 initialization"); initialize.GetAwaiter().GetResult();
            Require(browser.CoreWebView2 != null, "imported WebView2 initializes the real installed browser runtime");
            var handle = browser.NativeHostHandle;
            Require(IsWindow(handle) && GetParent(handle) == surface.Handle, "browser host is a child HWND of the Project-Z window");
            var core = browser.CoreWebView2!;
            bool navigated = false, message = false, popup = false;
            browser.NavigationCompleted += (_, e) => { if (e.IsSuccess) navigated = true; };
            browser.WebMessageReceived += (_, e) => { if (e.TryGetWebMessageAsString() == "native-probe") message = true; };
            core.NewWindowRequested += (_, e) => { e.Handled = true; popup = true; };
            browser.NavigateToString("<html><body style='margin:0;background:#123456;color:white'><h3>Project-Z native WebView2</h3><button id='probe' onclick=\"chrome.webview.postMessage('native-probe')\">Browser event probe</button></body></html>");
            Until(() => navigated, "native browser navigation");
            Require(view.NavigationCount > 0 && ReferenceEquals(view.LastSource, browser), "WebView2 XAML navigation handlers receive the native adapter as sender");
            var title = Complete(browser.ExecuteScriptAsync("document.querySelector('h3').textContent"), "DOM script");
            Require(title.Contains("Project-Z native WebView2"), "translated browser executes scripts against its loaded page");
            Complete(browser.ExecuteScriptAsync("document.getElementById('probe').click();window.open('about:blank');'ok'"), "browser input script");
            Until(() => message && popup, "SDK message and popup handlers");
            Require(message && popup, "WebView2 SDK messages and cancellable popup events stay functional");
            for (int i = 0; i < 4; i++) Frame();
            using (var previewFile = File.Create(Path.Combine(output, "browser-preview.png")))
            {
                var preview = core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, previewFile);
                Until(() => preview.IsCompleted, "browser capture"); preview.GetAwaiter().GetResult();
            }
            if (!IsWindowVisible(handle))
                for (var node = browser.NativeElement; node != null; node = node.RoutedParent) Console.WriteLine($"BROWSER_LAYOUT {node.GetType().Name} bounds={node.Rectangle} visible={node.isVisible} suppressed={node.SuppressVisual} clip={node.Clip}:{node.ClippingRectangle} registered={surface.Scene.ContainsElement(node)} parentVisible={IsWindowVisible(surface.Handle)}");
            Require(IsWindowVisible(handle), "native browser child is visible after layout");
            Require(GetWindowRect(handle, out var hostBounds) && hostBounds.Right - hostBounds.Left == 300, "browser HWND follows native element bounds");
            var clip = CreateRectRgn(0, 0, 0, 0);
            try { Require(GetWindowRgn(handle, clip) != 0 && GetRgnBox(clip, out var clipped) != 0 && clipped.Right - clipped.Left == 280 && clipped.Bottom - clipped.Top == 160, "ancestor clipping limits the child HWND to its Project-Z viewport"); }
            finally { DeleteObject(clip); }
            crop.Visibility = Visibility.Hidden; Frame(); Require(!IsWindowVisible(handle), "hiding a native ancestor hides the browser child");
            crop.Visibility = Visibility.Visible; Frame(); Require(IsWindowVisible(handle), "showing the ancestor restores the browser child");
            void Click(FrameworkElement element, bool right = false) { var r = element.NativeElement.Rectangle; if (right) surface.DispatchRightClick(r.Center.X, r.Center.Y); else surface.DispatchClick(r.Center.X, r.Center.Y); }
            view.Events.Clear(); Click(action);
            Require(view.Events.SequenceEqual(new[] { "preview-root", "bubble-action", "bubble-root", "click" }) && ReferenceEquals(view.LastSource, action), "actual native pointer input invokes preview bubble and click handlers exactly once");
            view.BlockPointer = true; view.Events.Clear(); Click(action);
            Require(view.Events.SequenceEqual(new[] { "preview-root" }), "handled XAML preview prevents native click behavior");
            view.BlockPointer = false; view.Events.Clear(); Click(action, true);
            Require(view.Events.Contains("right-preview"), "native right pointer input reaches the missing preview event");
            IEnumerable<SceneElement> Descendants(SceneElement node) { yield return node; foreach (var child in node.Children) foreach (var descendant in Descendants(child)) yield return descendant; }
            var row = Descendants(rows.NativeElement).Where(element => element is ProjectZ.Shared.Drawing.UI.Input.Button).Select(UIElement.FromNative).OfType<Button>().First(element => element.Name == "RowAction");
            Click(row);
            Require(view.RowClickCount == 1 && ReferenceEquals(view.LastSource, row), "native data-template buttons invoke the original codebehind handler");
            Require(((ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement)editor.NativeElement).CornerRadius==4 && editor.Background is ProjectZ.WpfCompatibility.Media.SolidColorBrush {Color:{R:32,G:64,B:96}},"input content-host templates translate into the native input surface");
            editor.IsEnabled=false;Require(Math.Abs(editor.Opacity-0.45)<0.000001,"projected template triggers route named border setters through native control state");editor.IsEnabled=true;Require(editor.Opacity==1,"native input template trigger exit restores the original surface opacity");
            Require(editor.Focus(), "the native input element acquires keyboard focus");
            view.Events.Clear(); surface.DispatchKey(Keys.F5);
            Require(view.Events.Contains("preview-key"), "native keyboard input reaches the routed XAML preview handler");
            view.Events.Clear(); var changes = view.TextChangeCount; surface.DispatchTextInput("λ🙂");
            Require(editor.Text == "λ🙂" && view.Events.SequenceEqual(new[] { "preview-text", "text" }) && view.TextChangeCount == changes + 1, "Unicode composition edits native input once and bubbles the attached text-change event");
            view.BlockText = true; view.Events.Clear(); surface.DispatchTextInput("blocked");
            Require(editor.Text == "λ🙂" && view.Events.SequenceEqual(new[] { "preview-text" }), "handled text composition leaves the native input unchanged");
            int captures = 0, releases = 0;
            action.GotMouseCapture += (_, _) => captures++; action.LostMouseCapture += (_, _) => releases++;
            Require(action.CaptureMouse() && ReferenceEquals(ProjectZ.WpfCompatibility.Input.Mouse.Captured, action) && captures == 1, "imported mouse capture and its routed event are owned by the Project-Z scene");
            action.ReleaseMouseCapture(); Require(ProjectZ.WpfCompatibility.Input.Mouse.Captured == null && releases == 1, "mouse capture and its lost event are released through the native scene");
            var references = typeof(BrowserEvents.View).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Require(!references.Any(name => name is "PresentationFramework" or "PresentationCore" or "WindowsBase" or "Microsoft.Web.WebView2.Wpf"), "generated fixture references native compatibility and Core SDK assemblies without WPF");
            browser.Dispose(); Require(!IsWindow(handle), "disposing the translated browser destroys its native child HWND");
            view.StartMotion();
            var motionDeadline=DateTime.UtcNow.AddSeconds(5);
            while(!view.MotionCompleted && DateTime.UtcNow<motionDeadline) { surface.RenderFrame();app.Dispatcher.Pump();Thread.Sleep(10); }
            Require(view.MotionCompleted && view.Motion.X==16 && action.NativeElement.RenderTranslation.X==16,"original codebehind animations are generated into native Project-Z frame clocks");
            Console.WriteLine("WEBVIEW_EVENTS_RUNTIME_OK"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr window, IntPtr region);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] static extern int GetRgnBox(IntPtr region, out Rect rect);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
}
