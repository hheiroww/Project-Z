using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Keys = Microsoft.Xna.Framework.Input.Keys;
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
sealed class NativeItem(string id, string name) : System.ComponentModel.INotifyPropertyChanged
{
    string caption = name;
    public string Id { get; } = id;
    public string Name { get => caption; set { caption = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
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
        application = new(scene) { ShutdownMode = ShutdownMode.OnExplicitShutdown }; csharp = new(); csharp.Show();
    }
    void Require(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); checks.Add("PASS " + description); }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static WeakReference EvictRenderedFont(ContentContainer resources, GraphicsDevice device)
    {
        resources.TryGetVectorFont(ContentContainer.SystemFontName("Arial", 600, 128), out var first);
        first.Draw(device, "Cache", Vector2.Zero, Color.White, Vector2.Zero, 0.01f);
        var reference = new WeakReference(first);
        for (int points = 1; points <= 72; points++)
            resources.TryGetVectorFont(ContentContainer.SystemFontName("Arial", 400, points), out _);
        return reference;
    }
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
            csharp.Hide();
            using (var nativeView = new NativeCodegenFixture.View())
            {
                nativeView.Show(); manager.Tick(time); scene.Draw(time);
                var surface = (Controls.Border)nativeView.FindName("Surface")!;
                var blurred = (Controls.Border)nativeView.FindName("Blurred")!;
                var combo = (Controls.ComboBox)nativeView.FindName("Models")!;
                var history = (Controls.ListBox)nativeView.FindName("History")!;
                var second = (Controls.ComboBoxItem)nativeView.FindName("Second")!;
                Require(combo.Items.Count == 3 && ReferenceEquals(combo.SelectedItem, second) && (string?)second.Tag == "large", "Windows codegen materializes named object items and metadata without source edits");
                Require(history.Items.Count == 2 && ReferenceEquals(history.SelectedItem, nativeView.FindName("Entry")), "property-element list items preserve object selection");
                var gradient = (Compat.Media.LinearGradientBrush)surface.Background!;
                var nativeSurface = (ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement)surface.NativeElement;
                Require(gradient.GradientStops.Count == 2 && surface.CornerRadius.TopLeft == 12 && surface.ActualWidth == 260 && surface.ActualHeight == 55 && surface.Effect is Compat.Media.Effects.DropShadowEffect,
                    "Windows codegen reuses native inherited styles gradients rounded surfaces and shadows");
                Click((Controls.Button)nativeView.FindName("Change")!); manager.Tick(time); scene.Draw(time);
                Require(nativeView.Clicks == 1 && combo.Items.Count == 4 && (string?)((Controls.ComboBoxItem)combo.SelectedItem!).Tag == "generated" && ReferenceEquals(nativeView.AddedItem, combo.SelectedItem), "translated original code adds object items and receives actual selection event items");
                Require(nativeSurface.BackgroundBrush.Stops[0].Color == Color.Red && nativeSurface.VisualEffect.ShadowDepth == 9 && blurred.NativeElement.VisualEffect.Radius == 5,
                    "translated original code mutates native gradient and effect objects");
                using var codegenPreview = new RenderTarget2D(GraphicsDevice, 720, 480); GraphicsDevice.SetRenderTarget(codegenPreview); scene.Draw(time); GraphicsDevice.SetRenderTarget(null);
                var appearance = new Color[720 * 480]; codegenPreview.GetData(appearance);
                using (var file = File.Create(Path.Combine(output, "native-codegen.png"))) codegenPreview.SaveAsPng(file, 720, 480);
                Require(appearance.Distinct().Count() > 100, "Windows codegen gradients and effects render actual native GPU detail");
                int gradientY = (int)nativeSurface.Position.Y + 32;
                var gradientLeft = appearance[gradientY * 720 + (int)nativeSurface.Position.X + 20];
                var gradientRight = appearance[gradientY * 720 + (int)nativeSurface.Position.X + 230];
                Require(gradientLeft.R > gradientLeft.G + 60 && gradientRight.G > gradientRight.R + 50, "native GPU pixels reflect the mutated gradient across the imported surface");
                Require(ReferenceEquals(surface.Background, nativeView.FindResource("Gradient")), "native resource brushes retain the same object used by code-behind");
                combo.Items.Clear();
                var firstRecord = new NativeItem("a", "Alpha"); var secondRecord = new NativeItem("b", "Beta");
                var records = new System.Collections.ObjectModel.ObservableCollection<NativeItem> { firstRecord, secondRecord };
                combo.DisplayMemberPath = nameof(NativeItem.Name); combo.SelectedValuePath = nameof(NativeItem.Id); combo.ItemsSource = records; combo.SelectedValue = "b";
                Require(combo.Items.IsReadOnly && ReferenceEquals(combo.SelectedItem, secondRecord) && (string?)combo.SelectedValue == "b", "native ItemsSource exposes original object items and SelectedValuePath");
                records.Insert(0, new("z", "Zero"));
                Require(combo.SelectedIndex == 2 && ReferenceEquals(combo.SelectedItem, secondRecord), "observable insertion retains selected object identity");
                secondRecord.Name = "Updated Beta";
                Require(((ProjectZ.Shared.Drawing.UI.Input.ComboBox)combo.NativeElement).Items[2] == "Updated Beta", "item property notification updates native display member");
                records.Remove(secondRecord);
                Require(combo.SelectedIndex == -1 && combo.SelectedItem == null, "removing a selected item clears native selection");
                bool mutationRejected = false; try { combo.Items.Add(firstRecord); } catch (InvalidOperationException) { mutationRejected = true; }
                Require(mutationRejected, "bound item collections reject direct mutation");
                nativeView.Close();
                secondRecord.Name = "After disposal";
                Require(combo.IsDisposed, "closing generated view detaches observable item subscriptions");
            }
            var alternateXaml = "<UserControl xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:identity='http://schemas.microsoft.com/winfx/2006/xaml'><UserControl.Resources><Style identity:Key='Rounded' TargetType='Border'><Setter Property='CornerRadius' Value='8'/></Style></UserControl.Resources><Border identity:Name='AlternateName' Style='{StaticResource Rounded}' Width='40' Height='30'/></UserControl>";
            string alternatePath = Path.Combine(output, "alternate-prefix.xaml"); File.WriteAllText(alternatePath, alternateXaml);
            using (var alternate = new Controls.UserControl())
            {
                XamlLoader.Initialize(alternate, alternatePath);
                Require(alternate.FindName("AlternateName") is Controls.Border { CornerRadius.TopLeft: 8 }, "native XAML identity uses namespace URIs for names and resource keys");
            }
            var unknownStyle = System.Xml.Linq.XDocument.Parse("<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Window.Resources><Style TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate><Border/></ControlTemplate></Setter.Value></Setter></Style></Window.Resources></Window>");
            Require(XamlSupport.Validate(unknownStyle).Any(d => d.Message.Contains("Template")), "codegen still rejects control templates without a complete native contract");
            using (var sharedWindow = new Window { Width = 220, Height = 120 })
            {
                var sharedEffect = new Compat.Media.Effects.BlurEffect { Radius = 2 };
                var outer = new Controls.Border { Width = 200, Height = 100, Background = Compat.Media.Brushes.Blue, Effect = sharedEffect };
                var inner = new Controls.Border { Width = 70, Height = 35, Background = Compat.Media.Brushes.Red, Effect = sharedEffect };
                outer.Content = inner; sharedWindow.Content = outer; sharedWindow.Show(); manager.Tick(time); scene.Draw(time);
                using var sharedTarget = new RenderTarget2D(GraphicsDevice, 720, 480); GraphicsDevice.SetRenderTarget(sharedTarget); scene.Draw(time); GraphicsDevice.SetRenderTarget(null);
                var sharedPixels = new Color[720 * 480]; sharedTarget.GetData(sharedPixels);
                Require(sharedPixels.Count(p => p.R > 180 && p.B < 50) > 300 && sharedPixels.Count(p => p.B > 180 && p.R < 50) > 300, "nested controls can share an effect resource while rendering independent native GPU surfaces");
                sharedWindow.Close();
            }
            foreach (var linked in new Window[] { new LinkedCSharp.View(), new LinkedVisualBasic.View() })
            {
                linked.Show(); manager.Tick(time); scene.Draw(time);
                var action = (Controls.Button)linked.FindName("Action")!;
                Click(action);
                int calls = (int)linked.GetType().GetProperty("Calls")!.GetValue(linked)!;
                string designerLabel = (string)linked.GetType().GetProperty("DesignerLabel")!.GetValue(linked)!;
                Require(calls == 1 && ((Controls.TextBlock)linked.FindName("Status")!).Text == designerLabel + "1", linked.GetType().FullName + " linked XAML, code-behind and designer execute once through native input");
                linked.Close();
            }
            csharp.Show(); manager.Tick(time); scene.Draw(time);
            using (var textWindow = new TextLayout.ProbeWindow())
            {
                csharp.Hide(); textWindow.Show(); manager.Tick(time); scene.Draw(time);
                var wrapped = (Controls.TextBlock)textWindow.FindName("Wrapped")!; var overflow = (Controls.TextBlock)textWindow.FindName("Overflow")!;
                var trimmed = (Controls.TextBlock)textWindow.FindName("Trimmed")!;
                Require(wrapped.ActualHeight > overflow.ActualHeight && Math.Abs(wrapped.FontSize - 12) < 0.1,
                    $"native Wrap breaks long words while WrapWithOverflow keeps an unbreakable word ({wrapped.ActualHeight}, {overflow.ActualHeight}, {wrapped.FontSize})");
                Require(trimmed.TextTrimming == TextTrimming.WordEllipsis && ((ProjectZ.Shared.Drawing.UI.Primitives.TextElement)trimmed.NativeElement).Clip,
                    "imported text trimming enables native clipping");
                var typeface = (Controls.TextBlock)textWindow.FindName("Typeface")!;
                var nativeText = (ProjectZ.Shared.Drawing.UI.Primitives.TextElement)typeface.NativeElement;
                Require(typeface.FontFamily.Source == "Consolas" && typeface.FontWeight == FontWeights.Bold && nativeText.Text == "WWW ✕ ▾ …",
                    "imported text retains the requested font weight family and Unicode glyphs");
                var fontResources = (ContentContainer)Services.GetService(typeof(ContentContainer));
                Require(fontResources.TryGetVectorFont(nativeText.Font, out var boldFont) && boldFont.FilePath != null && boldFont.FilePath.EndsWith("consolab.ttf", StringComparison.OrdinalIgnoreCase),
                    "native renderer resolves the actual requested bold font face");
                var boldWidth = nativeText.MeasureXamlText(300).X;
                textWindow.ChangeTypeface(); manager.Tick(time); scene.Draw(time);
                Require(typeface.FontFamily.Source == "Segoe UI" && typeface.FontWeight == FontWeights.Normal && Math.Abs(typeface.FontSize - 16) < 0.1 &&
                    Math.Abs(nativeText.MeasureXamlText(300).X - boldWidth) > 1,
                    "imported code changes font family and weight while preserving size and updating native measurement");
                var evictedFont = EvictRenderedFont(fontResources, GraphicsDevice);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                Require(!evictedFont.IsAlive, "bounded system font cache releases evicted GPU font resources and device subscriptions");
                textWindow.Change(); manager.Tick(time); scene.Draw(time);
                Require(wrapped.TextWrapping == TextWrapping.NoWrap && Math.Abs(wrapped.FontSize - 18) < 0.1 && trimmed.TextTrimming == TextTrimming.CharacterEllipsis,
                    "unchanged imported code updates native font size wrapping and trimming");
                textWindow.Close();
            }
            csharp.Show(); manager.Tick(time); scene.Draw(time);
            using (var stackWindow = new StackLayout.ProbeWindow())
            {
                csharp.Hide(); stackWindow.Show(); manager.Tick(time); scene.Draw(time);
                var first = (Controls.Border)stackWindow.FindName("First")!;
                var second = (Controls.Border)stackWindow.FindName("Second")!;
                Require(Math.Abs(second.NativeElement.Position.X - first.NativeElement.Position.X - 45) < 0.1 &&
                    Math.Abs(second.NativeElement.Position.Y - first.NativeElement.Position.Y - 1) < 0.1 &&
                    Math.Abs(first.ActualWidth - 40) < 0.1 && Math.Abs(second.ActualHeight - 30) < 0.1,
                    "horizontal imported stack arranges widths heights and margins on the correct axes");
                stackWindow.Vertical(); manager.Tick(time); scene.Draw(time);
                Require(Math.Abs(second.NativeElement.Position.X - first.NativeElement.Position.X - 1) < 0.1 &&
                    Math.Abs(second.NativeElement.Position.Y - first.NativeElement.Position.Y - 25) < 0.1,
                    "imported code changes stack orientation and invalidates measured layout");
                stackWindow.Close();
            }
            csharp.Show(); manager.Tick(time); scene.Draw(time);
            using (var gridWindow = new GridImport.ProbeWindow())
            {
                csharp.Hide(); gridWindow.Show(); manager.Tick(time); scene.Draw(time);
                var grid = (Controls.Grid)gridWindow.FindName("Layout")!;
                var first = (Controls.Border)gridWindow.FindName("First")!; var second = (Controls.Border)gridWindow.FindName("Second")!;
                Require(Math.Abs(first.ActualWidth - 100) < 0.1 && Math.Abs(second.ActualWidth - 200) < 0.1 && Math.Abs(grid.RowDefinitions[0].ActualHeight - 20) < 0.1,
                    "imported grid respects fractional star weights and measured Auto rows");
                Require(Controls.Grid.GetColumn(second) == 1 && Controls.Grid.GetRow(second) == 1, "imported grid attached getters retain XAML values");
                gridWindow.Constrain(); manager.Tick(time); scene.Draw(time);
                Require(Math.Abs(first.ActualWidth - 200) < 0.1 && Math.Abs(second.ActualWidth - 100) < 0.1, "grid minimum redistributes remaining star space after code change");
                gridWindow.Limit(); manager.Tick(time); scene.Draw(time);
                Require(Math.Abs(first.ActualWidth - 50) < 0.1 && Math.Abs(second.ActualWidth - 250) < 0.1, "grid maximum redistributes remaining star space after code change");
                gridWindow.Zero(); manager.Tick(time); scene.Draw(time);
                Require(first.ActualWidth == 0 && Math.Abs(second.ActualWidth - 300) < 0.1, "fixed zero column remains distinct from star size");
                Controls.Grid.SetColumnSpan(first, 2); manager.Tick(time); scene.Draw(time);
                Require(Math.Abs(first.ActualWidth - 300) < 0.1, "attached column span change invalidates imported layout");
                gridWindow.Close();
            }
            using (var createdGrid = new Controls.Grid { Width = 300, Height = 90 })
            {
                createdGrid.ColumnDefinitions.Add(new() { Width = new GridLength(0.5, GridUnitType.Star) });
                createdGrid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
                var child = new Controls.Border { Height = 20 };
                Controls.Grid.SetColumn(child, 1); createdGrid.Children.Add(child);
                Require(createdGrid.NativeElement is ProjectZ.Shared.Drawing.UI.Layout.Grid && Math.Abs(child.ActualWidth - 200) < 0.1 && child.ActualHeight == 20,
                    $"code created grid uses native layout and retains preattached position and explicit height ({child.ActualWidth}, {child.ActualHeight})");
                Controls.Grid.SetColumn(child, 0);
                Require(Math.Abs(child.ActualWidth - 100) < 0.1, "code created grid reacts to attached position changes");
            }
            csharp.Show(); manager.Tick(time); scene.Draw(time);
            using (var dispatched = new DispatcherImport.ProbeWindow())
            {
                csharp.Hide(); dispatched.Show();
                manager.Tick(time); scene.Draw(time);
                var operation = dispatched.GetValue();
                manager.Tick(time); scene.Draw(time);
                Click((Controls.Button)dispatched.FindName("Action")!);
                Require(dispatched.Ticks > 0 && ((Controls.TextBlock)dispatched.FindName("Status")!).Text.StartsWith("tick ") && dispatched.Clicks == 1,
                    "unchanged imported dispatcher timer updates native text while pointer input remains responsive");
                Require(operation.Task.GetAwaiter().GetResult() == 42, "imported generic dispatcher result survives semantic translation");
                dispatched.Close(); int beforeTicks = dispatched.Ticks;
                manager.Tick(time); scene.Draw(time);
                Require(dispatched.Ticks == beforeTicks, "imported window closure stops dispatcher timer");
            }
            csharp.Show(); manager.Tick(time); scene.Draw(time);
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
            Compat.Input.MouseButtonEventHandler preview = (_, e) => { route.Add("root-preview"); e.Handled = true; };
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
{
    // Hide the main sample Window
    var originalWindow = csharp;
    originalWindow.Hide();

    // Create and show the AppearanceSamples.Demo
    var demo = new AppearanceSamples.Demo();
    demo.Show();

    // Advance native layout
    manager.Tick(time);

    // Check that the named Curve adapter wraps the native Input.Border class
    var curveAdapter = (Controls.Border)demo.FindName("Curve");
    Require(curveAdapter.NativeElement is global::ProjectZ.Shared.Drawing.UI.Input.Border, "Curve adapter correctly wraps the native Input.Border class.");

    // Cast the NativeElement to the native type for further checks
    var nativeBorder = (global::ProjectZ.Shared.Drawing.UI.Input.Border)curveAdapter.NativeElement;

    // Check initial native values
    Require(nativeBorder.CornerRadii.TopLeft == 4 && nativeBorder.CornerRadii.TopRight == 12 &&
            nativeBorder.CornerRadii.BottomRight == 20 && nativeBorder.CornerRadii.BottomLeft == 28,
            "Initial corner radii match expected values.");
    Require(nativeBorder.BorderColor.R == 224 && nativeBorder.BorderColor.G == 48 && nativeBorder.BorderColor.B == 80 &&
            nativeBorder.BorderColor.A == 255, "Initial border color matches expected value.");
    Require(nativeBorder.BorderThickness == 3, "Initial border thickness matches expected value.");

    // Call UpdateCorners method and tick again
var beforePixels = CaptureAndSaveScreenshot("native-appearance-before.png");
Require(CountSpecificColors(beforePixels, 32, 64, 96) > 100, "Native fill is rendered with RGB32,64,96 as expected.");
Require(CountSpecificColors(beforePixels, 224, 48, 80) > 100, "Native stroke is rendered with RGB224,48,80 as expected.");
    demo.UpdateCorners();
    manager.Tick(time);

    // Check that native values have changed
    Require(nativeBorder.CornerRadii.TopLeft == 30 && nativeBorder.CornerRadii.TopRight == 20 &&
            nativeBorder.CornerRadii.BottomRight == 10 && nativeBorder.CornerRadii.BottomLeft == 0,
            "Updated corner radii match expected values.");

    // Check that native values remain unchanged after layout
    manager.Tick(time);
    Require(nativeBorder.CornerRadii.TopLeft == 30 && nativeBorder.CornerRadii.TopRight == 20 &&
            nativeBorder.CornerRadii.BottomRight == 10 && nativeBorder.CornerRadii.BottomLeft == 0,
            "Corner radii remain unchanged after additional layout.");

    // Close/dispose the demo
var afterPixels = CaptureAndSaveScreenshot("native-appearance-after.png");
Require(CountChangedPixels(beforePixels, afterPixels) > 20, "The native renderer regression passed because the number of changed pixel positions between two corner-radius renders exceeded 20, meeting the specified condition for passing the test.");
    demo.Close();
    demo.Dispose();

    // Show the main sample Window again
    originalWindow.Show();

// Local function to capture and save screenshot
Color[] CaptureAndSaveScreenshot(string filename)
{
    using (var controlPreview = new RenderTarget2D(GraphicsDevice, 720, 480))
    {
        GraphicsDevice.SetRenderTarget(controlPreview);
        scene.Draw(time);
        GraphicsDevice.SetRenderTarget(null);

        Color[] pixels = new Color[720 * 480];
        controlPreview.GetData(pixels);

        using var png = File.Create(Path.Combine(output, filename));
        controlPreview.SaveAsPng(png, 720, 480);

        return pixels;
    }
}

// Local function to count changed pixels
int CountChangedPixels(Color[] before, Color[] after)
{
    int changedCount = 0;
    for (int i = 0; i < before.Length; i++)
    {
        if (before[i] != after[i])
        {
            changedCount++;
        }
    }
    return changedCount;
}

// Local function to count specific colors
int CountSpecificColors(Color[] pixels, int r, int g, int b)
{
    int count = 0;
    foreach (var pixel in pixels)
    {
        if (pixel.R == r && pixel.G == g && pixel.B == b)
        {
            count++;
        }
    }
    return count;
}
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
            CheckRequestedControls(time);
            Require(ProjectZ.Shared.Drawing.UI.SceneElement.RenderExceptionCount == 0, "native renderer reports no swallowed errors");
        }
        catch (Exception error) { checks.Add("FAIL " + error); Environment.ExitCode = 1; }
        finally { File.WriteAllLines(Path.Combine(output, "result.txt"), checks); foreach (var line in checks) Console.WriteLine(line); application.Dispose(); Exit(); }
    }
    void CheckRequestedControls(GameTime time)
    {
        foreach (var window in application.Windows.ToArray()) window.Hide();
        using var view = new ControlsCodegen.View(); scene.AddElement(view.NativeElement);
        manager.Tick(time); scene.Draw(time);
        var dock = (Controls.DockPanel)view.FindName("DockRoot")!;
        var caption = (Controls.TextBlock)view.FindName("Caption")!;
        var files = (Controls.ListView)view.FindName("Files")!;
        var action = (Controls.Button)view.FindName("Action")!;
        var editor = (Controls.RichTextBox)view.FindName("Editor")!;
        var rotated = (Controls.Border)view.FindName("Rotated")!;
        var clipped = (Controls.Border)view.FindName("Clipped")!;
        var polygon = (Compat.Shapes.Polygon)view.FindName("Triangle")!;
        Require(polygon.Points.Count == 3 && polygon.Points[2].Y == 60, "generated polygon exposes its parsed native points");
        polygon.Points[0] = new Compat.Point(5, 0); Require(((ProjectZ.Shared.Drawing.UI.Advanced.PolygonElement)polygon.NativeElement).Vectors[0].X == 5, "polygon point collection edits update native geometry"); polygon.Points[0] = new Compat.Point(0, 0);
        var savedPoints = polygon.Points.ToArray(); polygon.Points.Clear(); scene.Draw(time); Require(((ProjectZ.Shared.Drawing.UI.Advanced.PolygonElement)polygon.NativeElement).MeshData == null, "clearing polygon points removes its rendered mesh"); polygon.Points = new Compat.Media.PointCollection(savedPoints);
        Require(files.Items.Count == 2 && view.FindName("First") is Controls.ListViewItem, "Windows codegen creates native list view items");
        Require(files.NativeElement.Position.Y >= caption.NativeElement.Position.Y + caption.ActualHeight && files.ActualWidth == 150, "generated docking consumes the top strip then the left strip");
        Require(caption.TextDecorations.Count == 1 && ((ProjectZ.Shared.Drawing.UI.Primitives.TextElement)caption.NativeElement).Underline && caption.FontFamily.Source == "Arial", "generated typography and decorations use native vector text");
        Require(caption.TextAlignment == Compat.TextAlignment.Center && ((ProjectZ.Shared.Drawing.UI.Primitives.TextElement)caption.NativeElement).TextAlignment == ProjectZ.Shared.Drawing.UI.HorizontalAlignment.Center, "generated text alignment uses the native vector renderer");
        using (var decorated = new Controls.TextBlock()) { decorated.TextDecorations.Add(new Compat.TextDecoration { Location = Compat.TextDecorationLocation.Strikethrough }); Require(((ProjectZ.Shared.Drawing.UI.Primitives.TextElement)decorated.NativeElement).Strikethrough, "new decoration collections update native text when mutated"); decorated.TextDecorations = new(); Require(!((ProjectZ.Shared.Drawing.UI.Primitives.TextElement)decorated.NativeElement).Strikethrough, "replacing decoration collections clears native decorations"); }
        Require(action.IsDefault && action.BorderThickness.Left == 2 && ((Compat.Media.SolidColorBrush)action.BorderBrush!).Color.R == 255, "control appearance properties translate to native surfaces");
        Click(action); Require(view.Actions == 1 && action.FontSize == 21, "translated original codebehind changes the vector font and receives native input");
        var count = view.Actions; action.RaiseEvent(new RoutedEventArgs(Controls.Button.ClickEvent)); Require(view.Actions == count + 1, "translated ClickEvent dispatches original handler");
        editor.AcceptsReturn = false; editor.Focus(); count = view.Actions; manager.InjectKeyDown(Keys.Enter); manager.InjectKeyUp(Keys.Enter); Require(view.Actions == count + 1, "translated default button activates through native keyboard routing");
        editor.AcceptsReturn = true; count = view.Actions; manager.InjectKeyDown(Keys.Enter); manager.InjectKeyUp(Keys.Enter); Require(view.Actions == count, "rich input accepting returns retains Enter instead of activating the default button");
        Require(view.FindName("Unsupported") is Controls.EmptyControl empty && empty.GetCopyText() == "" && empty.NativeElement.SuppressVisual, "unsupported control becomes an empty named element");
        Require(view.FindName("BlankTabs") is Controls.TabControl tabs && tabs.Items.Count == 2 && tabs.Items[1].IsSelected && tabs.SelectedIndex == 1 && !tabs.NativeElement.SuppressVisual && view.FindName("BlankChild") is Controls.TextBlock, "native tabs preserve XAML content and codebehind item selection");
        var rotate = (Compat.Media.RotateTransform)rotated.RenderTransform!; Require(rotate.Angle == 90, "generated RotateTransform is applied");
        var imagePath = Path.Combine(output, "generated-image.png"); using (var texture = new Texture2D(GraphicsDevice, 8, 8)) { texture.SetData(Enumerable.Repeat(Color.Red, 64).ToArray()); using var file = File.Create(imagePath); texture.SaveAsPng(file, 8, 8); }
        view.LoadImage(imagePath); Require(((ProjectZ.Shared.Drawing.UI.Advanced.ImageElement)((Controls.Image)view.FindName("Picture")!).NativeElement).Texture.Width == 8, "translated Image and BitmapImage load into a native GPU texture");
        rotate.Angle = 0; Require(rotated.NativeElement.RenderRotation == 0, "mutating rotation updates the native element"); rotate.Angle = 90;
        Require(clipped.Clip is Compat.Media.RectangleGeometry && clipped.NativeElement.ClipBounds!.Value.Width == 40, "rectangle geometry clips the native surface");
        using (var target = new RenderTarget2D(GraphicsDevice, 720, 480))
        {
            GraphicsDevice.SetRenderTarget(target); scene.Draw(time); GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[720 * 480]; target.GetData(pixels);
            var magenta = pixels.Select((p, i) => (p, i)).Where(v => v.p.R > 240 && v.p.B > 240 && v.p.G < 10).Select(v => (X: v.i % 720, Y: v.i / 720)).ToArray();
            Require(magenta.Length > 500 && magenta.Max(p => p.X) - magenta.Min(p => p.X) < magenta.Max(p => p.Y) - magenta.Min(p => p.Y), "GPU rotation turns a horizontal rectangle into a vertical rectangle");
            Require(pixels.Count(p => p.R > 240 && p.G > 240 && p.B < 10) > 500, "generated polygon renders native geometry");
            Require(pixels.Count(p => p.G > 200 && p.R < 30 && p.B < 30) > 100, "vector glyphs and decorations render visible green detail");
            using var file = File.Create(Path.Combine(output, "requested-controls.png")); target.SaveAsPng(file, 720, 480);
        }
        view.SetDocument(); manager.Tick(time); scene.Draw(time);
        Require(editor.Text == "Document text" && editor.Elements.Count == 1 && editor.GetCopyText().Contains("Embedded vector element"), "rich input hosts Project-Z elements and exposes their copy text");
        editor.Copy(); Require(Compat.Clipboard.GetText().Contains("Embedded vector element"), "rich input copies text through the native desktop clipboard");
        Require(view.ClipboardText() == Compat.Clipboard.GetText(), "codegen maps typed WPF clipboard data interfaces to the desktop service");
        Compat.Clipboard.SetText("native Unicode \u2713", Compat.TextDataFormat.UnicodeText);
        Require(Compat.Clipboard.ContainsText(Compat.TextDataFormat.UnicodeText) && Compat.Clipboard.GetText(Compat.TextDataFormat.UnicodeText)=="native Unicode \u2713", "imported Unicode clipboard formats use the actual desktop clipboard");
        var beforePaste=editor.Text; int routedPaste=0;
        Compat.DataObjectPastingEventHandler cancelPaste=(_,args)=> { Require(Equals(args.SourceDataObject.GetData(System.Windows.Forms.DataFormats.UnicodeText),"native Unicode \u2713"), "native paste handlers receive actual clipboard source data"); args.CancelCommand(); };
        Compat.DataObjectPastingEventHandler parentPaste=(_,args)=> { routedPaste++; Require(ReferenceEquals(args.OriginalSource,editor), "native paste events preserve their original input source while bubbling"); };
        Compat.DataObject.AddPastingHandler(editor,cancelPaste);Compat.DataObject.AddPastingHandler(view,parentPaste);
        Require(!((global::ProjectZ.Shared.Drawing.UI.Input.Textbox)editor.NativeElement).PasteClipboard() && editor.Text==beforePaste && routedPaste==1, "cancelled routed paste commands preserve text and reach the native parent route");
        Compat.DataObject.RemovePastingHandler(editor,cancelPaste);Compat.DataObject.RemovePastingHandler(view,parentPaste);
        Require(((global::ProjectZ.Shared.Drawing.UI.Input.Textbox)editor.NativeElement).PasteClipboard() && editor.Text.Contains("native Unicode \u2713"), "removing paste handlers restores native clipboard insertion");
        var clipboardImage=Compat.Media.Imaging.BitmapSource.Create(2,1,96,96,Compat.Media.PixelFormats.Bgra32,null,new byte[]{0,255,0,255,0,0,255,255},8);
        var imageData=new Compat.DataObject(); imageData.SetData(System.Windows.Forms.DataFormats.Bitmap,clipboardImage); Compat.Clipboard.SetDataObject(imageData,true);
        var nativeClipboardImage=Compat.Clipboard.GetDataObject()!.GetData(System.Windows.Forms.DataFormats.Bitmap,true) as Compat.Media.Imaging.BitmapSource;
        var clipboardPixels=new byte[8];nativeClipboardImage?.CopyPixels(clipboardPixels,8,0);
        Require(nativeClipboardImage?.PixelWidth==2 && clipboardPixels.SequenceEqual(new byte[]{0,255,0,255,0,0,255,255}), "desktop clipboard bitmap data round trips through imported bitmap source pixels");
        Compat.DataObjectPastingEventHandler imagePaste=(_,args)=> { Require(args.SourceDataObject.GetData(System.Windows.Forms.DataFormats.Bitmap,true) is Compat.Media.Imaging.BitmapSource, "native image paste events deliver bitmap sources expected by imported attachment handlers");args.CancelCommand(); };
        Compat.DataObject.AddPastingHandler(editor,imagePaste);Require(!((global::ProjectZ.Shared.Drawing.UI.Input.Textbox)editor.NativeElement).PasteClipboard(),"image attachment paste handlers can cancel native text insertion");Compat.DataObject.RemovePastingHandler(editor,imagePaste);
        Compat.Clipboard.SetText("retain"); ((UIElement)view.FindName("Unsupported")!).Copy(); Require(Compat.Clipboard.GetText() == "retain", "copy on a nontext element is empty");
        var open = (Compat.Dialogs.OpenFileDialog)view.OpenPicker(); var save = (Compat.Dialogs.SaveFileDialog)view.SavePicker(); var folder = (Compat.Dialogs.OpenFolderDialog)view.FolderPicker();
        var directory = Path.Combine(output, "picker-data"); Directory.CreateDirectory(directory); Directory.CreateDirectory(Path.Combine(directory, "child")); File.WriteAllText(Path.Combine(directory, "one.txt"), "one"); File.WriteAllText(Path.Combine(directory, "two.txt"), "two"); File.WriteAllText(Path.Combine(directory, "hidden.bin"), "hidden");
        open.InitialDirectory = directory;
        using (var picker = open.CreateWindow())
        {
            picker.Surface.RenderFrame(); Require(picker.EntryPaths.Count == 3 && picker.EntryPaths.All(p => !p.EndsWith("hidden.bin")), "Project-Z open dialog filters files while retaining folders");
            var preview = picker.Surface.CaptureFrame(); picker.Surface.SaveFrame(Path.Combine(output, "custom-open-dialog.png")); Require(preview.Count(p => p.R > 220 && p.G > 220 && p.B > 220) > 500 && picker.Surface.FrameCount >= 2, "custom file picker renders vector text and native controls");
            picker.NameBox.Text = "\"one.txt\" \"two.txt\""; picker.Surface.RenderFrame(); picker.Surface.DispatchClick((int)picker.AcceptButton.Position.X + 10, (int)picker.AcceptButton.Position.Y + 10); Require(picker.Result == true && open.FileNames.Length == 2 && open.FileNames.All(File.Exists), "native picker returns multiple selected files through its actual button input path");
        }
        save.InitialDirectory = directory;
        using (var picker = save.CreateWindow()) { picker.NameBox.Text = "new-name"; picker.Accept(); Require(picker.Result == true && save.FileName.EndsWith("new-name.txt"), "native save dialog supplies the default extension"); }
        save.FileName = Path.Combine(directory, "one.txt");
        using (var picker = save.CreateWindow()) { picker.NameBox.Text = "one.txt"; picker.Accept(); Require(picker.Result == null && picker.Status.Contains("Confirm"), "native save dialog requests overwrite confirmation"); picker.ConfirmOverwrite(); Require(picker.Result == true, "native overwrite confirmation returns the existing path"); }
        folder.InitialDirectory = directory;
        using (var picker = folder.CreateWindow()) { picker.NameBox.Text = "child"; picker.Accept(); Require(picker.Result == true && Directory.Exists(folder.FolderName), "native folder dialog selects a directory"); }
        using (var picker = open.CreateWindow()) { picker.Cancel(); Require(picker.Result == false, "native file dialog cancellation returns false"); }
        scene.RemoveElement(view.NativeElement);
    }
}
