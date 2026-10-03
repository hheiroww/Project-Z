using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.WpfCompatibility;
using ProjectZ.WpfCompatibility.Data;
using System.Windows.Input;
using Compat = ProjectZ.WpfCompatibility;
using Controls = ProjectZ.WpfCompatibility.Controls;
using Color = Microsoft.Xna.Framework.Color;
using Point = Microsoft.Xna.Framework.Point;
using Binding = ProjectZ.WpfCompatibility.Data.Binding;

internal static class Program
{
    [STAThread] static void Main(string[] args) { using var game = new Probe(args.FirstOrDefault() ?? "artifacts/wpf-import/runtime"); game.Run(); }
}
sealed class TestScene(SceneManager manager) : Scene(manager);
sealed class Command : ICommand
{
    public bool Allowed = true; public int Count;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => Allowed;
    public void Execute(object? parameter) => Count++;
    public void Change(bool value) { Allowed = value; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
}
sealed class Probe : Game
{
    readonly string output;
    readonly GraphicsDeviceManager graphics;
    readonly List<string> checks = new();
    SceneManager manager = null!;
    TestScene scene = null!;
    Compat.Application application = null!;
    ImportSamples.Demo csharp = null!;
    bool tested;
    public Probe(string directory)
    {
        output = Path.GetFullPath(directory); Directory.CreateDirectory(output);
        graphics = new(this) { PreferredBackBufferWidth = 720, PreferredBackBufferHeight = 480, GraphicsProfile = GraphicsProfile.HiDef };
        Content.RootDirectory = "Content"; Window.Title = "Project-Z imported C# / VB WPF compatibility";
    }
    protected override void LoadContent()
    {
        var batch = new SpriteBatch(GraphicsDevice); var resources = new ContentContainer(Content, GraphicsDevice); resources.LoadAllContent();
        Services.AddService(typeof(SpriteBatch), batch); Services.AddService(typeof(GraphicsDevice), GraphicsDevice); Services.AddService(typeof(ContentContainer), resources);
        manager = new(this) { UseExternalInput = true, AutoResizeViewport = false, ExternalViewportSize = new Point(720, 480) };
        scene = new(manager) { EnableDesignTimeControls = false, BackgroundColor = new Color(10, 24, 32) };
        manager.AddScene("import", scene); manager.ActiveScene = scene;
        application = new(scene); csharp = new(); csharp.Show();
    }
    void Require(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); checks.Add("PASS " + description); }
    void Click(FrameworkElement element)
    {
        element.NativeElement.ValidationCheck();
        var p = element.NativeElement.Position; int x = (int)p.X + 10, y = (int)p.Y + 10;
        manager.InjectMouseLeftDown(x, y, 720, 480); manager.InjectMouseLeftUp(x, y, 720, 480);
    }
    protected override void Draw(GameTime time)
    {
        if (tested) return; tested = true;
        try
        {
            manager.Tick(time); scene.Draw(time);
            var editor = (Controls.TextBox)csharp.FindName("Editor")!; var label = (Controls.TextBlock)csharp.FindName("Label")!; var save = (Controls.Button)csharp.FindName("Save")!;
            Require(ReferenceEquals(UIElement.FromNative(editor.NativeElement), editor), "stable adapter identity");
            Require(editor.Text == "Ada" && label.Text == "Ada", "inherited DataContext initializes native text");
            Require(((Controls.TextBlock)csharp.FindName("Converted")!).Text == "Converted: Ada", "XAML converter resource runs converted project code");
            editor.SetValue(Controls.TextBox.TextProperty, "DP"); Require(editor.Text == "DP" && (string?)editor.GetValue(Controls.TextBox.TextProperty) == "DP", "built-in dependency property bridges native control");
            editor.Text = "Grace"; Require(csharp.Model.Person.Name == "Grace" && label.Text == "Grace", "two-way text updates model and sibling");
            var old = csharp.Model.Person; csharp.Model.Person = new() { Name = "replacement" }; old.Name = "detached";
            Require(editor.Text == "replacement", "nested path replacement detaches old notifications");
            Click(save); Require(csharp.Clicks == 1 && label.Text == "saved 1", "native pointer click invokes converted C# handler once");
            var route = new List<string>();
            EventHandler<Compat.Input.MouseButtonEventArgs> preview = (_, e) => { route.Add("root-preview"); e.Handled = true; };
            csharp.PreviewMouseLeftButtonDown += preview;
            save.MouseLeftButtonDown += (_, _) => route.Add("target"); Click(save);
            Require(csharp.Clicks == 1 && route.SequenceEqual(new[] { "root-preview" }), "preview Handled suppresses native click and bubble");
            csharp.PreviewMouseLeftButtonDown -= preview;
            var command = new Command(); save.Command = command; Click(save); Require(command.Count == 1, "ICommand executes through native click");
            command.Change(false); Require(!save.IsEnabled, "CanExecute updates native enabled state"); command.Change(true);
            csharp.Model.Items.Add("third"); var items = (Controls.ListBox)csharp.FindName("Items")!;
            Require(((ProjectZ.Shared.Drawing.UI.Input.ListBox)items.NativeElement).Items.Count == 3, "observable items update native list");
            Require(items.ItemViews.Count == 3 && ((Controls.TextBlock)items.ItemViews[2].FindName("ItemLabel")!).Text == "third", "item templates have separate namescopes and live contexts");
            var oneTime = new Controls.TextBlock(); oneTime.SetBinding(Controls.TextBlock.TextProperty, new Binding("Person.Name") { Source = csharp.Model, Mode = BindingMode.OneTime });
            var once = oneTime.Text; csharp.Model.Person.Name = "changed"; Require(oneTime.Text == once, "OneTime binding detaches source updates"); oneTime.Dispose();
            var byName = new Controls.TextBlock(); ((Controls.Panel)save.Parent!).Children.Add(byName);
            byName.SetBinding(Controls.TextBlock.TextProperty, new Binding("Text") { ElementName = "Editor" });
            Require(byName.Text == editor.Text, "dynamic child resolves ElementName through parent namescope");
            byName.SetBinding(Controls.TextBlock.TextProperty, new Binding("DataContext.Person.Name") { RelativeSource = new(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Compat.Window) } });
            Require(byName.Text == csharp.Model.Person.Name, "relative ancestor binding resolves inherited model");
            ((Controls.Panel)save.Parent!).Children.Remove(byName); byName.Dispose();
            var toggle = (Controls.CheckBox)csharp.FindName("Toggle")!; toggle.IsChecked = true; Require(csharp.Model.Enabled, "checkbox two-way binding");
            var dispatcherThread = 0; var task = Task.Run(() => application.Dispatcher.Invoke(() => dispatcherThread = Environment.CurrentManagedThreadId));
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < timeout) { application.Dispatcher.Pump(); Thread.Yield(); }
            Require(task.IsCompletedSuccessfully && dispatcherThread == Environment.CurrentManagedThreadId, "background dispatch executes on scene thread");
            var dp = DependencyProperty.RegisterAttached("Tag", typeof(string), typeof(Probe), new PropertyMetadata("default"));
            editor.SetValue(dp, "attached"); Require((string?)editor.GetValue(dp) == "attached", "attached dependency property storage");
            var controlPath = Path.Combine(output, "additional-controls.xaml");
            File.WriteAllText(controlPath, """
                <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="600" Height="400">
                  <StackPanel>
                    <Separator x:Name="Divider" Height="2" Background="#44AA88" />
                    <Label x:Name="Caption" Content="Native controls" Height="30" />
                    <PasswordBox x:Name="Secret" Width="150" Height="30" />
                    <Slider x:Name="Range" Minimum="1" Maximum="120" Value="30" Height="35" />
                    <RadioButton x:Name="First" Content="First" GroupName="probe" Height="30" />
                    <RadioButton x:Name="Second" Content="Second" GroupName="probe" Height="30" />
                    <Expander x:Name="Expand" Header="Details" IsExpanded="True" Height="90"><GroupBox x:Name="Group" Header="Inside"><TextBlock x:Name="Inside" Text="Content" /></GroupBox></Expander>
                    <ScrollViewer x:Name="Scroll" Height="30"><WrapPanel x:Name="Wrap" Width="300" Height="100"><TextBlock Text="Scrollable" /></WrapPanel></ScrollViewer>
                  </StackPanel>
                </Window>
                """);
            using (var controls = new Compat.Window())
            {
                XamlLoader.Initialize(controls, controlPath);
                var divider = (Controls.Separator)controls.FindName("Divider")!;
                Require(divider.NativeElement is ProjectZ.Shared.Drawing.UI.Input.Separator, "imported separator uses native renderer");
                var secret = (Controls.PasswordBox)controls.FindName("Secret")!;
                int passwordEvents = 0; secret.PasswordChanged += (_, _) => passwordEvents++;
                secret.Password = "private"; secret.Clear();
                Require(passwordEvents == 2 && ((ProjectZ.Shared.Drawing.UI.Input.PasswordBox)secret.NativeElement).MaskCharacter.HasValue, "password changes notify while native glyphs remain masked");
                var range = (Controls.Slider)controls.FindName("Range")!;
                int rangeEvents = 0; range.ValueChanged += (_, _) => rangeEvents++;
                range.Maximum = 100; Require(range.Value == 30, "slider range changes preserve valid value");
                range.Value = 150; Require(range.Value == 100 && rangeEvents == 1, "slider clamps and emits value event");
                var first = (Controls.RadioButton)controls.FindName("First")!; var second = (Controls.RadioButton)controls.FindName("Second")!;
                first.IsChecked = true; second.IsChecked = true;
                Require(first.IsChecked == false && second.IsChecked == true, "radio group preserves exclusive selection");
                var expand = (Controls.Expander)controls.FindName("Expand")!; var group = (Controls.GroupBox)controls.FindName("Group")!;
                Require(ReferenceEquals(((ProjectZ.Shared.Drawing.UI.Input.Expander)expand.NativeElement).Content, group.NativeElement) && ReferenceEquals(((ProjectZ.Shared.Drawing.UI.Input.GroupBox)group.NativeElement).Content, ((FrameworkElement)controls.FindName("Inside")!).NativeElement), "imported expander and group content use internal native hosts");
                int collapsed = 0; expand.Collapsed += (_, _) => collapsed++; expand.IsExpanded = false;
                Require(collapsed == 1 && !expand.IsExpanded, "expander collapse dispatches converted event");
                var replacement = new Controls.TextBlock { Text = "Replacement" }; group.Content = replacement;
                Require(ReferenceEquals(((ProjectZ.Shared.Drawing.UI.Input.GroupBox)group.NativeElement).Content, replacement.NativeElement), "content replacement updates native host");
                var scroll = (Controls.ScrollViewer)controls.FindName("Scroll")!;
                Require(scroll.Content is Controls.WrapPanel, "scroll viewer preserves wrapping content adapter");
                expand.IsExpanded = true;
                Require(expand.NativeElement.Size.Y == 90, "expander restores height on reopening");
                csharp.Hide(); controls.Show(); manager.Tick(time);
                using (var controlPreview = new RenderTarget2D(GraphicsDevice, 720, 480))
                {
                    GraphicsDevice.SetRenderTarget(controlPreview); scene.Draw(time); GraphicsDevice.SetRenderTarget(null);
                    using var png = File.Create(Path.Combine(output, "additional-controls.png")); controlPreview.SaveAsPng(png, 720, 480);
                }
                controls.Close(); csharp.Show();
            }
            csharp.Close(); var frozen = editor.Text; csharp.Model.Person.Name = "after close"; Require(editor.Text == frozen && editor.IsDisposed, "closing detaches bindings");
            var vb = new ImportSamplesVB.Demo(); vb.Show(); manager.Tick(time); scene.Draw(time);
            var vbEditor = (Controls.TextBox)vb.FindName("Editor")!; var vbSave = (Controls.Button)vb.FindName("Save")!;
            vbEditor.Focus(); vbEditor.Text = "pending"; Require(vb.Model.Person.Name == "Ada", "default text trigger waits for focus loss");
            vbSave.Focus(); Require(vb.Model.Person.Name == "pending", "focus loss commits two-way text");
            Click(vbSave); Require(vb.Clicks == 1, "VB WithEvents Handles executes once");
            vb.Close(); csharp = new(); csharp.Show(); manager.Tick(time); scene.Draw(time);
            Click((FrameworkElement)csharp.FindName("Save")!); Require(csharp.Clicks == 1, "fresh view has no stale subscriptions");
            using var target = new RenderTarget2D(GraphicsDevice, 720, 480); GraphicsDevice.SetRenderTarget(target); scene.Draw(time); GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[720 * 480]; target.GetData(pixels); Console.WriteLine("Rendered colors: " + pixels.Distinct().Count());
            using (var file = File.Create(Path.Combine(output, "imported.png"))) target.SaveAsPng(file, 720, 480);
            Require(pixels.Count(p => p.R > 220 && p.G > 220 && p.B > 220) > 100, "imported native controls render visible detail");
            Require(ProjectZ.Shared.Drawing.UI.SceneElement.RenderExceptionCount == 0, "native renderer reports no swallowed errors");
        }
        catch (Exception error) { checks.Add("FAIL " + error); Environment.ExitCode = 1; }
        finally { File.WriteAllLines(Path.Combine(output, "result.txt"), checks); foreach (var line in checks) Console.WriteLine(line); application.Dispose(); Exit(); }
    }
}
