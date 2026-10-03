using Microsoft.Xna.Framework;
using Point = Microsoft.Xna.Framework.Point;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI.Primitives;
using ProjectZ.WpfCompatibility;
using Controls = ProjectZ.WpfCompatibility.Controls;
using Application = ProjectZ.WpfCompatibility.Application;

class Entry {
    [STAThread] static void Main(string[] args) {
        using var game = new Probe(args.Contains("--smoke-test"));
        game.Run();
    }
}
class TestScene(SceneManager manager) : Scene(manager);
class Probe : Game {
    readonly GraphicsDeviceManager graphics;
    readonly bool smokeTest;
    SceneManager manager;
    TestScene scene;
    Application app;
    Window activeView;
    PointerMessages pointer;
    bool smokeCompleted;
    int reportedCalls;

    public Probe(bool smokeTest) {
        this.smokeTest = smokeTest;
        graphics = new(this) { PreferredBackBufferWidth = 500, PreferredBackBufferHeight = 250 };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Project-Z build-time XAML";
    }
    protected override void LoadContent() {
        var resources = new ContentContainer(Content, GraphicsDevice);
        resources.LoadAllContent();
        Services.AddService(typeof(SpriteBatch), new SpriteBatch(GraphicsDevice));
        Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
        Services.AddService(typeof(ContentContainer), resources);
        manager = new(this) { UseExternalInput = smokeTest, UseHardwareInput = !smokeTest, AutoResizeViewport = false, ExternalViewportSize = new Point(500, 250) };
        scene = new(manager) { EnableDesignTimeControls = false };
        manager.AddScene("probe", scene);
        manager.ActiveScene = scene;
        app = new(scene);
        if (!smokeTest) {
            pointer = new PointerMessages(ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(this));
            scene.OnKeyDown += (key, state) => {
                if (key == Keys.F1) ShowView(false);
                if (key == Keys.F2) ShowView(true);
            };
            scene.AddElement(new TextElement(scene) {
                Text = "Click Run to test the original event handler.\nF1: C# view   F2: VB view   Close the window to exit.",
                Position = new Vector2(10, 180), MaxWidth = 480, Font = "SegoeUI_12"
            });
            ShowView(false);
        }
    }
    void ShowView(bool visualBasic) {
        activeView?.Close();
        activeView = visualBasic ? new LinkedVisualBasic.View() : new LinkedCSharp.View();
        activeView.Show();
        reportedCalls = 0;
        Window.Title = $"Project-Z generated {(visualBasic ? "VB" : "C#")} view | F1: C# | F2: VB";
    }
    protected override void Update(GameTime time) {
        if (!smokeTest) {
            while (pointer.Messages.TryDequeue(out var input)) {
                int x = (short)(input.Position & 0xffff), y = (short)((input.Position >> 16) & 0xffff);
                int width = Window.ClientBounds.Width, height = Window.ClientBounds.Height;
                switch (input.Id) {
                    case 0x200: manager.InjectMouseMove(x, y, width, height); break;
                    case 0x201: manager.InjectMouseLeftDown(x, y, width, height); break;
                    case 0x202: manager.InjectMouseLeftUp(x, y, width, height); break;
                    case 0x204: manager.InjectMouseRightDown(x, y, width, height); break;
                    case 0x205: manager.InjectMouseRightUp(x, y, width, height); break;
                    case 0x215: manager.CancelInjectedPointer(); break;
                }
            }
            manager.Tick(time);
            int calls = (int)activeView.GetType().GetProperty("Calls").GetValue(activeView);
            if (calls != reportedCalls) {
                reportedCalls = calls;
                Console.WriteLine($"Interactive {activeView.GetType()}: {((Controls.TextBlock)activeView.FindName("Status")).Text}");
            }
        }
        base.Update(time);
    }
    protected override void Draw(GameTime time) {
        if (!smokeTest) {
            scene.Draw(time);
            base.Draw(time);
            return;
        }
        if (smokeCompleted) return;
        smokeCompleted = true;
        try {
            foreach (var view in new Window[] { new LinkedCSharp.View(), new LinkedVisualBasic.View(), new DesignerOnlyCSharp.View(), new DesignerOnlyVisualBasic.View() }) {
                view.Show(); manager.Tick(time); scene.Draw(time);
                var button = (Controls.Button)view.FindName("Action");
                button.NativeElement.ValidationCheck();
                var p = button.NativeElement.Position;
                manager.InjectMouseLeftDown((int)p.X + 10, (int)p.Y + 10, 500, 250);
                manager.InjectMouseLeftUp((int)p.X + 10, (int)p.Y + 10, 500, 250);
                int calls = (int)view.GetType().GetProperty("Calls").GetValue(view);
                string label = (string)view.GetType().GetProperty("DesignerLabel").GetValue(view);
                if (calls != 1 || ((Controls.TextBlock)view.FindName("Status")).Text != label + "1")
                    throw new Exception("Generated view event failed: " + view.GetType());
                Console.WriteLine("PASS compile-time designer and original code-behind: " + view.GetType());
                view.Close();
            }
        } catch (Exception e) {
            Console.Error.WriteLine(e);
            Environment.ExitCode = 1;
        } finally {
            Exit();
        }
    }
    protected override void UnloadContent() {
        pointer?.ReleaseHandle();
        manager?.Dispose();
        app?.Dispose();
        base.UnloadContent();
    }
}

// Queue native messages so scene callbacks run on Update, outside the window procedure.
sealed class PointerMessages : System.Windows.Forms.NativeWindow {
    public readonly System.Collections.Concurrent.ConcurrentQueue<(int Id, long Position)> Messages = new();
    public PointerMessages(IntPtr handle) { AssignHandle(handle); }
    protected override void WndProc(ref System.Windows.Forms.Message message) {
        if (message.Msg is 0x200 or 0x201 or 0x202 or 0x204 or 0x205 or 0x215)
            Messages.Enqueue((message.Msg, message.LParam.ToInt64()));
        base.WndProc(ref message);
    }
}
