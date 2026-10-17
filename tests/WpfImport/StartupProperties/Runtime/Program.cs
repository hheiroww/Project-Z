using ProjectZ.WpfCompatibility;
using ProjectZ.WpfCompatibility.Threading;
using ProjectZ.Shared.Drawing;
using System.Reflection;
static class Program
{
    static void Require(bool value, string label) { if (!value) throw new InvalidOperationException(label); Console.WriteLine("PASS " + label); }
    [STAThread] static void Main(string[] args)
    {
        try
        {
            var selected = args.Contains("selected");
            var options = ProjectZGenerated.Application.Startup;
            Require(options.FramesPerSecond == (selected ? 45 : 60), "properties generate the selected frame rate");
            Require(options.AntiAliasingSamples == (selected ? 2 : 4) && options.TextureFiltering == (selected ? TextureFiltering.Anisotropic8x : TextureFiltering.Anisotropic16x), "properties generate AA and AF settings");
            DispatcherTimer? timer = null;
            Exception? failure = null;
            ProjectZGenerated.Application.Run(() =>
            {
                var app = new StartupProperties.App();
                var ticks = 0;
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                timer.Tick += (_, _) =>
                {
                    if (++ticks < 2) return; // Allow startup construction and one rendered frame to complete.
                    timer.Stop();
                    try
                    {
                        var window = app.MainWindow!;
                        Require(window.GetType() == (selected ? typeof(StartupProperties.AlternateWindow) : typeof(StartupProperties.DefaultWindow)), "main window selection reaches the native host");
                        Require(window.NativeHandle != IntPtr.Zero, "startup creates a real native window");
                        Require(IsWindowVisible(window.NativeHandle), "the shown application window stays visible while its coordinator stays hidden");
                        Require(!IsWindowVisible(ProjectZ.Windows.Composition.WindowsComposition.ResolveHwnd(app.Scene.sender())), "native application coordinator stays hidden after startup");
                        var scene = window.NativeElement.OwnerScene;
                        var manager = (SceneManager)typeof(Scene).GetField("ownerManager", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(scene)!;
                        Require(manager.LimitFPS == options.FramesPerSecond && manager.MinimumViewportSize.X == options.MinimumWidth && manager.AutoResizeViewport == options.AutoResizeViewport, "initial scene manager settings reach the window scene");
                        Require(scene.Quality.AntiAliasingSamples == options.AntiAliasingSamples && scene.Quality.Filtering == options.TextureFiltering, "AA and AF settings reach the native window device");
                        Require(window.Width == options.Width && window.Height == options.Height, "initial viewport dimensions reach an unsized XAML window");
                        Require(scene.BackgroundColor == options.BackgroundColor && app.ShutdownMode == (options.ShutdownMode ?? ShutdownMode.OnLastWindowClose), "native background and application lifetime settings reach startup");
                        Require(window.CaptureFrame().Any(pixel => pixel != options.BackgroundColor), "startup renders real XAML content pixels");
                        window.SaveFrame(Path.GetFullPath("artifacts/wpf-import/startup-" + (selected ? "selected" : "default") + ".png"));
                        if (!selected)
                        {
                            var intrinsicButton = (FrameworkElement)window.FindName("IntrinsicButton")!;
                            var intrinsicCheckBox = (FrameworkElement)window.FindName("IntrinsicCheckBox")!;
                            var intrinsicGrid = (FrameworkElement)window.FindName("IntrinsicGrid")!;
                            Require(new[] { intrinsicButton, intrinsicCheckBox, intrinsicGrid }.All(control => float.IsFinite(control.NativeElement.Size.X) && control.NativeElement.Size.X > 20 && control.NativeElement.Size.X < 400), "unconstrained horizontal stacks measure native buttons, checkboxes and auto grids intrinsically");
                            Require(intrinsicCheckBox.NativeElement.Position.X >= intrinsicButton.NativeElement.Position.X + intrinsicButton.NativeElement.Size.X && intrinsicGrid.NativeElement.Position.X >= intrinsicCheckBox.NativeElement.Position.X + intrinsicCheckBox.NativeElement.Size.X, "intrinsic horizontal controls render in successive finite pixel bounds");
                            var mixedText = (FrameworkElement)window.FindName("MixedColumnText")!;
                            var autoButton = (FrameworkElement)window.FindName("AutoColumnButton")!;
                            Require(mixedText.NativeElement.Size.X > 600 && autoButton.NativeElement.Size.X > 20 && autoButton.NativeElement.Size.X < 200 && autoButton.NativeElement.Position.X >= mixedText.NativeElement.Position.X + mixedText.NativeElement.Size.X, "Auto columns measure intrinsic content and preserve neighboring star-column width");
                            var spanLabel = (FrameworkElement)window.FindName("SpanLabel")!;
                            var spanCombo = (FrameworkElement)window.FindName("SpanCombo")!;
                            Require(spanLabel.NativeElement.Size.X < 100 && spanCombo.NativeElement.Size.X > 800 && spanCombo.NativeElement.Size.Y >= 30, "spanning text preserves Auto/star columns and ComboBox intrinsic row height");
                            var wrapped = (FrameworkElement)window.FindName("MeasuredWrap")!;
                            var wrapFirst = (FrameworkElement)window.FindName("WrapFirst")!;
                            var wrapSecond = (FrameworkElement)window.FindName("WrapSecond")!;
                            Require(wrapped.NativeElement.Size.Y >= 56 && wrapSecond.NativeElement.Position.Y >= wrapFirst.NativeElement.Position.Y + wrapFirst.NativeElement.Size.Y, "WrapPanel measures wrapped native controls without overlapping the next row");
                            Require(((FrameworkElement)window.FindName("InitiallyCollapsed")!).Visibility == Visibility.Collapsed && ((FrameworkElement)window.FindName("CollapsedTab")!).Visibility == Visibility.Collapsed, "codebehind sees the actual initial collapsed XAML visibility");
                            var measuredGroup = (ProjectZ.WpfCompatibility.Controls.GroupBox)window.FindName("MeasuredGroup")!;
                            var afterGroup = (FrameworkElement)window.FindName("AfterMeasuredGroup")!;
                            Require(measuredGroup.NativeElement.Size.Y >= 70 && afterGroup.NativeElement.Position.Y >= measuredGroup.NativeElement.Position.Y + measuredGroup.NativeElement.Size.Y, "Auto rows measure hosted GroupBox content and leave room for following controls");
                            var uniformGrid = (ProjectZ.WpfCompatibility.Controls.UniformGrid)window.FindName("NativeUniformGrid")!;
                            var firstCell = (FrameworkElement)window.FindName("UniformFirst")!;
                            var secondCell = (FrameworkElement)window.FindName("UniformSecond")!;
                            var thirdCell = (FrameworkElement)window.FindName("UniformThird")!;
                            Require(uniformGrid.NativeElement is ProjectZ.Shared.Drawing.UI.Layout.UniformGrid && uniformGrid.NativeElement.Size.Y >= 64 && secondCell.NativeElement.Position.X > firstCell.NativeElement.Position.X && thirdCell.NativeElement.Position.Y > firstCell.NativeElement.Position.Y, "UniformGrid imports to native equal-sized cells with measured rows and margins");
                            var logo = (ProjectZ.WpfCompatibility.Controls.Image)window.FindName("ResourceLogo")!;
                            Require(logo.Source is ProjectZ.WpfCompatibility.Media.Imaging.BitmapSource { PixelWidth: 2, PixelHeight: 2 }, "native XAML loads deployed WPF Resource images");
                            var packedLogo = new ProjectZ.WpfCompatibility.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/logo.png", UriKind.Absolute));
                            Require(packedLogo.PixelWidth == 2 && packedLogo.PixelHeight == 2, "application pack image URIs resolve to native deployed resources");
                            var spriteSurface = (ProjectZ.WpfCompatibility.Shapes.Rectangle)window.FindName("SpriteSurface")!;
                            var spriteBrush = (ProjectZ.WpfCompatibility.Media.ImageBrush)window.FindName("SpriteBrush")!;
                            var spriteImage = spriteSurface.NativeElement.Children.OfType<ProjectZ.Shared.Drawing.UI.Advanced.ImageElement>().Single();
                            var spriteFrame = window.CaptureFrame();
                            var spriteX = (int)(spriteSurface.NativeElement.Position.X + spriteSurface.NativeElement.Size.X / 2);
                            var spriteY = (int)(spriteSurface.NativeElement.Position.Y + spriteSurface.NativeElement.Size.Y / 2);
                            var spritePixel = spriteFrame[spriteY * (int)window.Width + spriteX];
                            // Linear filtering of a one-pixel cell can sample a
                            // neighboring texel slightly at the destination center.
                            Require(spritePixel.R >= 240 && spritePixel.G >= 240 && spritePixel.B <= 16, $"ImageBrush renders its cropped source pixels through the native image element ({spritePixel})");
                            var spriteTexture = spriteImage.Texture;
                            spriteBrush.Viewbox = new Rect(1, 0, 1, 1);
                            Require(spriteImage.SourceRectangle == new Microsoft.Xna.Framework.Rectangle(1, 0, 1, 1) && ReferenceEquals(spriteImage.Texture, spriteTexture), "sprite viewbox animation updates the crop without reuploading its texture");
                            var stateCheckBox = (ProjectZ.WpfCompatibility.Controls.CheckBox)window.FindName("StateCheckBox")!;
                            stateCheckBox.NativeElement.isMouseOver = true;
                            Require(stateCheckBox.Background is ProjectZ.WpfCompatibility.Media.SolidColorBrush { Color: var hoverColor } && hoverColor == ProjectZ.WpfCompatibility.Media.Colors.Yellow, "CheckBox style hover triggers route through native Project-Z events");
                            stateCheckBox.NativeElement.isMouseOver = false;
                            Require(stateCheckBox.Background is ProjectZ.WpfCompatibility.Media.SolidColorBrush { Color: var normalColor } && normalColor == ProjectZ.WpfCompatibility.Media.Colors.Red, "CheckBox hover restores the native base background");
                            var tabs = (ProjectZ.WpfCompatibility.Controls.TabControl)window.FindName("Tabs")!;
                            Require(tabs.SelectedIndex == 1 && tabs.Items.Count == 3 && !((ProjectZ.Shared.Drawing.UI.Input.TabControl)tabs.NativeElement).Tabs[2].IsVisible, "native tabs preserve XAML selection and hide collapsed headers");
                            var widget = (StartupProperties.Widget)window.FindName("CustomWidget")!;
                            var contentHost = (ProjectZ.WpfCompatibility.Controls.ContentControl)window.FindName("NativeContentHost")!;
                            var hostedText = (FrameworkElement)window.FindName("HostedText")!;
                            Require(ReferenceEquals(contentHost.Content, hostedText) && hostedText.NativeElement.Size.X > 0 && hostedText.NativeElement.Size.Y > 0, "ContentControl retains inline native content and exposes it to source codebehind");
                            contentHost.Content = hostedText;
                            Require(contentHost.NativeElement.Children.Count(child => ReferenceEquals(child, hostedText.NativeElement)) == 1, "assigning existing native content preserves a single visual child");
                            Require(widget.Value == "Custom native content", "source custom controls preserve type, constructor and APIs");
                            var tooltipLabel = (ProjectZ.WpfCompatibility.Controls.TextBlock)widget.FindName("Label")!;
                            Require(tooltipLabel.ToolTip is ProjectZ.WpfCompatibility.Controls.ToolTip tip && tip.Content?.ToString() == "First tooltip line" + Environment.NewLine + "Second tooltip line", "declarative tooltips become owned native text popups instead of inline visuals");
                            var popupTarget = (ProjectZ.WpfCompatibility.Controls.Button)widget.FindName("PopupTarget")!;
                            Require(popupTarget.ContextMenu is { } menu && ReferenceEquals(menu, widget.FindName("OwnedMenu")) && menu.Items.Count == 1, "declarative context menus retain native items and the owning name scope");
                            Require(popupTarget.ContextMenu!.CornerRadius.TopLeft == 7 && popupTarget.ContextMenu.NativeElement.Padding.Left == 8, "context menu border templates preserve native rounding and padding");
                            var scopedRows = (ProjectZ.WpfCompatibility.Controls.ItemsControl)widget.FindName("Rows")!;
                            var scopedText = (ProjectZ.WpfCompatibility.Controls.TextBlock)scopedRows.ItemViews[0].FindName("RowText")!;
                            Require(ReferenceEquals(scopedText.Foreground, widget.FindResource("ScopedBrush")) && scopedText.FontSize == 21 && scopedText.Text == "Queued during construction", "item templates inherit defining control resources and native styles before attachment");
                            Require(((ProjectZ.Shared.Drawing.UI.Input.ListBox)scopedRows.NativeElement).WrapItems, "ItemsPanelTemplate configures native item layout without creating a declaration as a live control");
                            scopedRows.Items.Clear();
                            scopedRows.DataContext = new { Rows = new[] { "Bound native item" } };
                            Require(((ProjectZ.WpfCompatibility.Controls.TextBlock)scopedRows.ItemViews[0].FindName("RowText")!).Text == "Bound native item", "ItemsControl ItemsSource binding populates the native template");
                            var mutableRow = new MutableRow { Value = "First metric" };
                            var mutableRows = new System.Collections.ObjectModel.ObservableCollection<MutableRow> { mutableRow }; scopedRows.ItemsSource = mutableRows;
                            var retainedView = scopedRows.ItemViews[0]; mutableRow.Value = "Updated metric";
                            Require(ReferenceEquals(retainedView, scopedRows.ItemViews[0]) && ((ProjectZ.WpfCompatibility.Controls.TextBlock)retainedView.FindName("RowText")!).Text == "Updated metric", "item property updates refresh native bindings without reconstructing template controls");
                            mutableRows.Add(new MutableRow { Value = "Appended metric" });
                            Require(scopedRows.ItemViews.Count == 2 && ReferenceEquals(retainedView, scopedRows.ItemViews[0]) && ((ProjectZ.WpfCompatibility.Controls.TextBlock)scopedRows.ItemViews[1].FindName("RowText")!).Text == "Appended metric", "observable template appends preserve existing native controls");
                            mutableRows.RemoveAt(0);
                            Require(scopedRows.ItemViews.Count == 1 && retainedView.IsDisposed && ((ProjectZ.WpfCompatibility.Controls.TextBlock)scopedRows.ItemViews[0].FindName("RowText")!).Text == "Appended metric", "observable template removal rebuilds the remaining rows and releases removed controls");
                            using var styledList = new ProjectZ.WpfCompatibility.Controls.ListView { ItemContainerStyle = new Style(typeof(ProjectZ.WpfCompatibility.Controls.ListViewItem)) };
                            styledList.Items.Add("Styled item");
                            Require(styledList.Items.Count == 1, "ListView styles use native ListViewItem containers");
                            using var changingPanel = new ProjectZ.WpfCompatibility.Controls.StackPanel();
                            var firstFontChild = new ProjectZ.WpfCompatibility.Controls.Button();
                            using var removedFontChild = new ProjectZ.WpfCompatibility.Controls.Button();
                            changingPanel.Children.Add(firstFontChild); changingPanel.Children.Add(removedFontChild);
                            firstFontChild.PropertyChanged += (_, change) => { if (change.PropertyName == "FontFamily" && removedFontChild.Parent == changingPanel) changingPanel.Children.Remove(removedFontChild); };
                            changingPanel.FontSize = 17;
                            Require(changingPanel.Children.Count == 1 && firstFontChild.FontSize == 17, "native font inheritance tolerates child collection changes during notifications");
                            changingPanel.Children.Add(removedFontChild);
                            firstFontChild.PropertyChanged += (_, change) => { if (change.PropertyName == "DataContext" && removedFontChild.Parent == changingPanel) changingPanel.Children.Remove(removedFontChild); };
                            var inheritedContext = new object(); changingPanel.DataContext = inheritedContext;
                            Require(changingPanel.Children.Count == 1 && ReferenceEquals(firstFontChild.DataContext, inheritedContext), "data context inheritance tolerates binding-driven child replacement");
                            changingPanel.Children.Add(removedFontChild);
                            firstFontChild.IsVisibleChanged += (_, _) => { if (removedFontChild.Parent == changingPanel) changingPanel.Children.Remove(removedFontChild); };
                            changingPanel.Visibility = Visibility.Collapsed;
                            Require(changingPanel.Children.Count == 1 && !firstFontChild.IsVisible, "native visibility propagation tolerates event-driven child replacement");
                            using var programmaticBorder = new ProjectZ.WpfCompatibility.Controls.Border { BorderThickness = new(0, 0, 0, 1) };
                            Require(programmaticBorder.BorderThickness.Top == 0 && programmaticBorder.BorderThickness.Bottom == 1 && ((ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement)programmaticBorder.NativeElement).BorderThickness == 1, "codebehind asymmetric border decoration uses the same native stroke projection as XAML");
                            Require(ProjectZ.WpfCompatibility.Automation.AutomationProperties.GetName(widget) == "Native widget" && ProjectZ.WpfCompatibility.Automation.AutomationProperties.GetHelpText(widget) == "Native help", "WPF accessibility metadata routes to Project-Z while desktop automation remains native");
                            Require(widget.NativeElement.Size.Y > 0 && ((ProjectZ.WpfCompatibility.Controls.TextBlock)widget.FindName("Label")!).NativeElement.Size.Y > 0, "nested source controls retain their native measure and layout");
                            Require(((StartupProperties.DefaultWindow)window).Screens > 0 && ((StartupProperties.DefaultWindow)window).WindowProcessId(window.NativeHandle) == Environment.ProcessId, "Windows Forms and desktop accessibility services retain their real operating-system behavior");
                            var rectangle = (ProjectZ.WpfCompatibility.Shapes.Rectangle)window.FindName("GradientSurface")!;
                            Require(ReferenceEquals(rectangle.Fill,window.FindName("NamedGradient")) && ((ProjectZ.WpfCompatibility.Media.GradientStop)window.FindName("NamedStop")!).Color == ProjectZ.WpfCompatibility.Media.Colors.Blue && ((ProjectZ.WpfCompatibility.Media.TranslateTransform)window.FindName("NamedMotion")!).X == 10, "named XAML brush, gradient stop and transform fields drive native objects");
                            Require(((ProjectZ.WpfCompatibility.Media.TranslateTransform)window.FindName("BlankMotion")!).X == 4 && window.FindName("BlankClip") is ProjectZ.WpfCompatibility.Media.RectangleGeometry, "blank optional visuals preserve named transform and geometry references");
                            var header = (ProjectZ.WpfCompatibility.Controls.Button)window.FindName("HeaderButton")!;
                            header.RaiseEvent(new RoutedEventArgs(ProjectZ.WpfCompatibility.Controls.Button.ClickEvent));
                            Require(((StartupProperties.DefaultWindow)window).ReloadCount == 1 && ((ProjectZ.WpfCompatibility.Controls.TextBlock)window.FindName("HeaderText")!).Text == "Reloaded", "custom tab header buttons route their imported events");
                            tabs.SelectedIndex = 0; tabs.SelectedIndex = 1;
                            Require(!header.IsDisposed && widget.Value == "Custom native content", "tab changes preserve header and application controls");
                            widget.Dispose();
                            Require(widget.DisposeCalls == 1 && !widget.IsDisposed, "source-owned IDisposable methods retain their WPF behavior without hiding warnings");
                        }
                    }
                    catch (Exception error) { failure = error; }
                    app.Shutdown(failure == null ? 0 : 1);
                };
                timer.Start();
                return app;
            }, []);
            if (failure != null) throw failure;
            Console.WriteLine("STARTUP_PROPERTIES_RUNTIME_OK");
        }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
    sealed class MutableRow : System.ComponentModel.INotifyPropertyChanged
    {
        string value = "";
        public string Value { get => value; set { this.value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public override string ToString() => Value;
    }
}
