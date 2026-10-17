using System.Xml.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.Designer;
using ProjectZ.WpfCompatibility;
using ProjectZ.WpfCompatibility.Media;

static class Program
{
    static int checks;
    static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks++; Console.WriteLine("PASS " + description);
    }
    static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    }
    static void Run(string[] args)
    {
        if (args.Length == 3 && args[0] == "--validate-report") { ValidateReport(args[1], args[2]); return; }
        const string presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var document = XDocument.Parse($"""
            <UserControl xmlns="{presentation}" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <UserControl.Resources>
                <DataTemplate x:Key="Row"><ContentPresenter x:Name="Blank" Content="Hidden" Width="42"/></DataTemplate>
                <SkewTransform x:Key="Skew"/>
              </UserControl.Resources>
              <Border ClipToBounds="True" AutomationProperties.Name="Region" AutomationProperties.HelpText="Clipped native content"/>
            </UserControl>
            """);
        var fallbacks = MarkupTranslation.Translate(document);
        var blank = document.Descendants().Single(n => n.Name.LocalName == "EmptyControl");
        Require(fallbacks.Count == 1 && blank.Attribute("Content") == null && (string?)blank.Attribute("Width") == "42", "unsupported template visuals in resources become blank and retain layout");
        Require((string?)blank.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "Blank", "template fallback preserves its local name");
        Require(document.Descendants().Any(n => n.Name.LocalName == "SkewTransform"), "nonvisual resource declarations are preserved for diagnostics");
        var diagnostics = XamlSupport.Validate(document).ToArray();
        Require(diagnostics.Any(d => d.Message.Contains("SkewTransform")) && !diagnostics.Any(d => d.Message.Contains("ClipToBounds") || d.Message.Contains("AutomationProperties")), "native clipping and accessibility markup validate while unsupported contracts remain gated");
        Require(MarkupTranslation.Translate(document).Count == 0, "blank-control normalization is idempotent");
        var nonvisual = XDocument.Parse($"<DataTemplate xmlns='{presentation}'><Border><Border.Tag><BitmapImage/><DiscreteColorKeyFrame/><CustomValueConverter/></Border.Tag></Border></DataTemplate>");
        Require(MarkupTranslation.Translate(nonvisual).Count == 0 && nonvisual.Descendants().Any(n => n.Name.LocalName == "DiscreteColorKeyFrame"), "image resources keyframes and converters inside templates retain their nonvisual contracts");
        var templates = XDocument.Parse($$"""
            <UserControl xmlns="{{presentation}}" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <UserControl.Resources>
                <Style x:Key="NativeButton" TargetType="Button">
                  <Setter Property="Background" Value="Blue"/><Setter Property="BorderThickness" Value="1"/>
                  <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                    <Border x:Name="Chrome" Background="{TemplateBinding Background}" BorderThickness="2" Padding="8" CornerRadius="6"><ContentPresenter HorizontalAlignment="Right" VerticalAlignment="Center"/></Border>
                    <ControlTemplate.Triggers>
                      <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Background" Value="Lime"/></Trigger>
                      <Trigger Property="IsPressed" Value="True"><Setter TargetName="Chrome" Property="Background" Value="Yellow"/></Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate></Setter.Value></Setter>
                </Style>
              </UserControl.Resources>
              <Button x:Name="Action" Style="{StaticResource NativeButton}" Background="Red" Content="Native text"/>
            </UserControl>
            """);
        MarkupTranslation.Translate(templates);
        Require(!XamlSupport.Validate(templates).Any(), "Border content templates and native button state colors pass codegen validation");
        var parser = new SceneXamlParser(null!);
        var expanded = XDocument.Parse(parser.ExpandImportedStyles(templates.ToString()));
        var button = expanded.Descendants().Single(n => n.Name.LocalName == "Button");
        Require((string?)button.Attribute("Background") == "Red" && (string?)button.Attribute("CornerRadius") == "6" && (string?)button.Attribute("BorderThickness") == "2" && (string?)button.Attribute("Padding") == "8", "native template expansion preserves control bindings and projects literal surface geometry");
        Require((string?)button.Attribute("MouseOverBackground") == "Lime" && (string?)button.Attribute("MouseDownBackground") == "Yellow", "template state colors use the native hover and pressed properties");
        Require((string?)button.Attribute("HorizontalContentAlignment") == "Right" && (string?)button.Attribute("VerticalContentAlignment") == "Center", "template content alignment maps to native vector text alignment");
        button.SetAttributeValue("Style", null); button.Add(new XElement(XName.Get("Button.Template", presentation), new XElement(templates.Descendants().Single(n => n.Name.LocalName == "ControlTemplate"))));
        button.Attribute("MouseOverBackground")!.Remove(); button.Attribute("MouseDownBackground")!.Remove();
        var inline = XDocument.Parse(parser.ExpandImportedStyles(expanded.ToString()));
        // Parse applies inline templates; shared style expansion must do so too.
        Require((string?)inline.Descendants().Single(n => n.Name.LocalName == "Button").Attribute("MouseOverBackground") == "Lime", "inline templates project the same native state colors");
        var complex = XDocument.Parse($"<ControlTemplate xmlns='{presentation}' TargetType='Button'><Grid/></ControlTemplate>");
        Require(!XamlSupport.Validate(complex).Any(), "complex decorative templates project onto their functional native control");
        var hints = XDocument.Parse($"<TextBox xmlns='{presentation}' Text='Preserved' VirtualizingPanel.IsVirtualizing='True' BorderThickness='0,0,0,2' ToolTipService.ShowDuration='20000'/>");
        Require(MarkupTranslation.Translate(hints).Count == 3 && !XamlSupport.Validate(hints).Any() && (string?)hints.Root!.Attribute("Text") == "Preserved" && (string?)hints.Root.Attribute("BorderThickness") == "2", "optional WPF rendering hints and asymmetric strokes use recorded native equivalents without losing input content");
        Require(MarkupTranslation.Translate(hints).Count == 0, "optional rendering fallback translation is idempotent");
        var missingParts = XDocument.Parse($"<ControlTemplate xmlns='{presentation}' TargetType='ComboBox'><Grid/><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Arrow' Property='Background' Value='Red'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        Require(!XamlSupport.Validate(missingParts).Any(), "optional template part decoration cannot block native dropdown codegen");
        templates.Descendants().First(n => n.Name.LocalName == "Trigger").SetAttributeValue("Property", "IsChecked");
        Require(XamlSupport.Validate(templates).Any(d => d.Message.Contains("Unsupported native trigger state")), "state changes without native equivalents remain diagnosed");
        using var element = new HeadlessAdapter();
        element.ClipToBounds = true;
        Require(element.NativeElement.Clip && element.ClipToBounds, "ClipToBounds enables native bounds clipping without a graphics device");
        var geometry = new RectangleGeometry();
        element.Clip = geometry; element.ClipToBounds = false;
        Require(element.NativeElement.Clip && element.NativeElement.ClipBounds == new Rectangle(0, 0, 0, 0), "explicit geometry remains clipped when ClipToBounds is disabled");
        element.Clip = null;
        Require(!element.NativeElement.Clip && element.NativeElement.ClipBounds == null, "removing both clipping sources clears native clipping");
        element.ClipToBounds = true; element.Clip = geometry; element.Clip = null;
        Require(element.NativeElement.Clip && element.NativeElement.ClipBounds == null, "removing geometry preserves independently requested bounds clipping");
        var objects = XDocument.Parse($$"""
            <UserControl xmlns="{{presentation}}" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework">
              <UserControl.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceDictionary><SolidColorBrush x:Key="Tone" Color="Red"/></ResourceDictionary></ResourceDictionary.MergedDictionaries></ResourceDictionary></UserControl.Resources>
              <Grid>
                <ListView><ListView.View><GridView><GridViewColumn Header="Name" Width="120" DisplayMemberBinding="{Binding Name}"/><GridViewColumn Header="Action"><GridViewColumn.CellTemplate><DataTemplate><Button Content="Copy"/></DataTemplate></GridViewColumn.CellTemplate></GridViewColumn></GridView></ListView.View><ListView.ItemsPanel><ItemsPanelTemplate><StackPanel Orientation="Horizontal"/></ItemsPanelTemplate></ListView.ItemsPanel></ListView>
                <RichTextBox><FlowDocument PagePadding="0"><Paragraph><Run Text="Native "/><Bold>text</Bold><LineBreak/><Run Text="input"/></Paragraph></FlowDocument></RichTextBox>
                <Border x:Name="Pulse"><Border.RenderTransform><TransformGroup><ScaleTransform ScaleX="2"/><TranslateTransform X="5"/></TransformGroup></Border.RenderTransform><Border.Triggers><EventTrigger RoutedEvent="FrameworkElement.Loaded"><BeginStoryboard><Storyboard AutoReverse="True"><DoubleAnimation Storyboard.TargetProperty="Opacity" To="0.5" Duration="0:0:1"/></Storyboard></BeginStoryboard></EventTrigger></Border.Triggers></Border>
              </Grid>
            </UserControl>
            """);
        Require(!XamlSupport.Validate(objects).Any(), "list-view columns documents items panels inline storyboards and transform groups validate together");
        var textDocument = (ProjectZ.WpfCompatibility.Documents.FlowDocument)DocumentMarkup.Load(objects.Descendants().Single(n => n.Name.LocalName == "FlowDocument"));
        Require(textDocument.GetCopyText() == "Native text\ninput" && textDocument.PagePadding.Left == 0, "document markup projects nested inline text and line breaks into copyable input text");
        var columns = new ProjectZ.WpfCompatibility.Controls.GridView();
        columns.Columns.Add(new() { Header = "Name", Width = 120, DisplayMemberBinding = new ProjectZ.WpfCompatibility.Data.Binding("Name") });
        columns.Columns.Add(new() { Header = "Size", Width = 80, DisplayMemberBinding = new ProjectZ.WpfCompatibility.Data.Binding("Size") });
        var row = columns.CreateRowMarkup();
        Require(!XamlSupport.Validate(new(row)).Any() && row.Descendants().Count(n => n.Name.LocalName == "ColumnDefinition") == 2 && (string?)row.Descendants().First(n => n.Name.LocalName == "TextBlock").Attribute("Text") == "{Binding Name}", "grid-view columns produce native grid cells with live text bindings and declared widths");
        Require(columns.CreateRowMarkup(true).Descendants().Where(n => n.Name.LocalName == "TextBlock").Select(n => (string?)n.Attribute("Text")).SequenceEqual(new[] { "Name", "Size" }), "list-view headers use native vector text cells");
        var chrome = XDocument.Parse($"<Window xmlns='{presentation}' xmlns:shell='clr-namespace:System.Windows.Shell;assembly=PresentationFramework'><shell:WindowChrome.WindowChrome><shell:WindowChrome CaptionHeight='44' CornerRadius='6' ResizeBorderThickness='7' GlassFrameThickness='0' UseAeroCaptionButtons='False'/></shell:WindowChrome.WindowChrome></Window>");
        Require(!XamlSupport.Validate(chrome).Any(), "window chrome declarations translate to Project-Z-owned decoration metadata");
        var data = XDocument.Parse($"<UserControl xmlns='{presentation}'><UserControl.Triggers><DataTrigger Binding='{{Binding Enabled}}' Value='True'><Setter Property='Opacity' Value='0.5'/></DataTrigger></UserControl.Triggers></UserControl>");
        Require(!XamlSupport.Validate(data).Any(), "data-trigger declarations validate without dropping their condition or setter");
        CheckBitmapBuffers();
        CheckStateTrigger();
        CheckCollectionViews();
        CheckAnimationClock();
        CheckEvents(presentation);
        using (var app = new Application(new HeadlessScene()))
        {
            var facade = (FrameworkElement)Activator.CreateInstance(typeof(FrameworkElement), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new object[] { new HeadlessNative() }, null)!;
            using (facade)
            {
                var scale = new ScaleTransform(2, 3); facade.RenderTransform = scale;
                Require(facade.NativeElement.RenderScale == new Vector2(2, 3) && facade.NativeElement.RenderScaleOrigin == Vector2.Zero, "scale transforms update native rendering with WPF's default origin");
                scale.ScaleX = 4;
                Require(facade.NativeElement.RenderScale.X == 4, "scale dependency-property changes update the native transform");
                var group = new TransformGroup(); group.Children.Add(scale); group.Children.Add(new TranslateTransform(5, 6)); facade.RenderTransform = group;
                Require(facade.NativeElement.RenderTranslation == new Vector2(5, 6) && facade.NativeElement.RenderScale.Y == 3, "transform groups carry native scale and translation values");
                group.Children.Clear(); Require(facade.NativeElement.RenderScale == Vector2.One && facade.NativeElement.RenderTranslation == Vector2.Zero, "clearing a transform group restores the identity transform");
                var model = new TriggerModel(); facade.DataContext = model;
                using var trigger = new NativeDataTrigger(facade, data.Descendants().Single(n => n.Name.LocalName == "DataTrigger"), parser);
                model.Enabled = true; Require(facade.Opacity == 0.5, "data-trigger model changes apply native scalar setters");
                model.Enabled = false; Require(facade.Opacity == 1, "data-trigger exit restores the original native value");
                trigger.Dispose(); model.Enabled = true; Require(facade.Opacity == 1, "disposed data triggers release their model subscription");
            }
        }
        var animationParser = new SceneXamlParser(null!);
        animationParser.RegisterFactory("Border", _ => new ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement());
        animationParser.Parse($"<Border xmlns='{presentation}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' x:Name='Pulse' Width='50' Height='40'/>");
        var tracks = animationParser.BeginImportedStoryboard($"<Storyboard xmlns='{presentation}' AutoReverse='True'><DoubleAnimation To='0.5' Duration='0:0:1' Storyboard.TargetProperty='Opacity' BeginTime='0:0:0.2'/></Storyboard>", "Pulse");
        Require(tracks.Count == 1 && tracks[0].AutoReverse && tracks[0].BeginDelay == TimeSpan.FromSeconds(0.2), "inline storyboards retain their implicit target delay and reverse timing");
        var clock = new GameTime(); tracks[0].gameTime = clock; tracks[0].Value();
        clock.TotalGameTime = TimeSpan.FromSeconds(0.7); Require(Math.Abs((double)tracks[0].Value() - 0.75) < 0.001, "native animation interpolates after its begin delay");
        clock.TotalGameTime = TimeSpan.FromSeconds(2.2); Require(Math.Abs((double)tracks[0].Value() - 1) < 0.001 && !tracks[0].Running, "native auto-reverse animation returns to its initial value and completes");
        var animated = new HeadlessNative();
        var timeline = new ProjectZ.Shared.Animations.Timeline(new GameTime());
        var firstClock = animationParser.BeginImportedStoryboard($"<Storyboard xmlns='{presentation}'><DoubleAnimation From='1' To='0.5' Duration='0:0:0.1' Storyboard.TargetProperty='Opacity'/></Storyboard>", "Pulse")[0];
        var delayedClock = animationParser.BeginImportedStoryboard($"<Storyboard xmlns='{presentation}'><DoubleAnimation To='1' BeginTime='0:0:0.5' Duration='0:0:0.5' Storyboard.TargetProperty='Opacity'/></Storyboard>", "Pulse")[0];
        timeline.AddChild(firstClock, new ProjectZ.Shared.Animations.Properties.OpacityProperty(animated));
        timeline.AddChild(delayedClock, new ProjectZ.Shared.Animations.Properties.OpacityProperty(animated));
        timeline.Tick(new GameTime());
        timeline.Tick(new GameTime(TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1)));
        Require(Math.Abs(animated.Opacity - 0.5) < 0.001, "a delayed storyboard track leaves the earlier track active until its own start");
        timeline.Tick(new GameTime(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0.4)));
        Require(Math.Abs(animated.Opacity - 0.5) < 0.001, "a delayed animation captures the current value when its clock starts");
        timeline.Tick(new GameTime(TimeSpan.FromSeconds(0.75), TimeSpan.FromSeconds(0.25)));
        Require(Math.Abs(animated.Opacity - 0.75) < 0.001, "successive native storyboard tracks interpolate without replacing an earlier clock prematurely");
        Console.WriteLine($"HEADLESS_MARKUP_OK {checks} checks; no game, graphics device, or window was created.");
    }
    static void ValidateReport(string reportPath, string outputPath)
    {
        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
        var saved = report.RootElement.GetProperty("Issues").EnumerateArray().Where(n => n.GetProperty("Code").GetString() == "PZI301").Select(n => new MarkupIssue(n.GetProperty("File").GetString()!, n.GetProperty("Line").GetInt32(), n.GetProperty("Column").GetInt32(), n.GetProperty("Message").GetString()!)).ToHashSet();
        var current = new HashSet<MarkupIssue>(); var fallbacks = new HashSet<string>();
        var files = saved.Select(n => n.File).Concat(report.RootElement.GetProperty("EmptyControls").EnumerateArray().Select(n => n.GetString()!.Split(": {", 2)[0])).Distinct().ToArray();
        foreach (var file in files)
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var issue in XamlSupport.Validate(document)) current.Add(new(file, issue.Line, issue.Column, issue.Message));
            foreach (var fallback in MarkupTranslation.Translate(document)) fallbacks.Add(file + ": " + fallback);
        }
        var savedFallbacks = report.RootElement.GetProperty("EmptyControls").EnumerateArray().Select(n => n.GetString()!).ToHashSet();
        var result = new { Files = files.Length, SavedDiagnostics = saved.Count, CurrentDiagnostics = current.Count, Added = current.Except(saved).ToArray(), Removed = saved.Except(current).ToArray(), AddedFallbacks = fallbacks.Except(savedFallbacks).ToArray(), RemovedFallbacks = savedFallbacks.Except(fallbacks).ToArray() };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"MARKUP_REPORT_CHECK files={result.Files} saved={result.SavedDiagnostics} current={result.CurrentDiagnostics} added={result.Added.Length} removed={result.Removed.Length} fallbackChanges={result.AddedFallbacks.Length + result.RemovedFallbacks.Length}");
        if (result.Added.Length != 0 || result.Removed.Length != 0 || result.AddedFallbacks.Length != 0 || result.RemovedFallbacks.Length != 0) Environment.ExitCode = 1;
    }
    static void CheckEvents(string presentation)
    {
        var browserMarkup = XDocument.Parse($$"""
            <UserControl xmlns="{{presentation}}" xmlns:web="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf" TextBox.TextChanged="Changed">
              <web:WebView2 Source="about:blank" NavigationCompleted="Navigated"><web:WebView2.CreationProperties><web:CoreWebView2CreationProperties UserDataFolder="profile"/></web:WebView2.CreationProperties></web:WebView2>
              <ListBox><ListBox.ItemTemplate><DataTemplate><Button Click="ItemClicked" PreviewMouseRightButtonDown="Preview"/></DataTemplate></ListBox.ItemTemplate></ListBox>
            </UserControl>
            """);
        Require(MarkupTranslation.Translate(browserMarkup).Count == 0 && !XamlSupport.Validate(browserMarkup).Any(), "WebView2 XAML attached events and template handlers retain their native mappings");
        var scene = new HeadlessScene();
        using var app = new Application(scene);
        HeadlessNative Native()
        {
            var native = new HeadlessNative();
            typeof(SceneElement).GetField("Scene", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(native, scene);
            return native;
        }
        using var root = new HeadlessAdapter(Native());
        using var child = new HeadlessAdapter(Native());
        root.NativeElement.Children.Add(child.NativeElement);
        var calls = new List<string>();
        bool block = false;
        root.PreviewMouseDown += (_, e) => { calls.Add("root-preview"); Require(ReferenceEquals(e.Source, child) && ReferenceEquals(e.OriginalSource, child), "routing preserves the original Project-Z input source"); if (block) e.Handled = true; };
        child.PreviewMouseDown += (_, _) => calls.Add("child-preview");
        child.MouseDown += (_, _) => calls.Add("child-bubble");
        root.MouseDown += (_, _) => calls.Add("root-bubble");
        var consumed = child.NativeElement.FilterImportedInput("MouseLeftButtonDown", new Microsoft.Xna.Framework.Point(4, 5));
        Require(!consumed && calls.SequenceEqual(new[] { "root-preview", "child-preview", "child-bubble", "root-bubble" }), "native pointer events tunnel and bubble in tree order exactly once");
        block = true; calls.Clear();
        RoutedEventHandler observer = (_, e) => calls.Add(e.Handled ? "handled-observer" : "unexpected");
        root.AddHandler(UIElement.MouseDownEvent, observer, true);
        Require(child.NativeElement.FilterImportedInput("MouseLeftButtonDown", new Microsoft.Xna.Framework.Point(4, 5)) && calls.SequenceEqual(new[] { "root-preview", "handled-observer" }), "handled preview events suppress defaults while opted-in native handlers still receive them");
        root.RemoveHandler(UIElement.MouseDownEvent, observer); calls.Clear();
        child.NativeElement.FilterImportedInput("MouseLeftButtonDown", new Microsoft.Xna.Framework.Point(4, 5));
        Require(calls.SequenceEqual(new[] { "root-preview" }), "removing a routed handler releases its native subscription");
        block = false; calls.Clear();
        child.PreviewMouseRightButtonDown += (_, e) => { calls.Add("right-preview"); Require(e.ChangedButton == ProjectZ.WpfCompatibility.Input.MouseButton.Right, "right pointer events keep their actual button"); };
        child.NativeElement.FilterImportedInput("MouseRightButtonDown", new Microsoft.Xna.Framework.Point(4, 5));
        Require(calls.Contains("right-preview"), "native right-button events reach imported handlers");
        calls.Clear(); child.PreviewMouseMove += (_, _) => calls.Add("move-preview"); child.MouseMove += (_, _) => calls.Add("move");
        child.NativeElement.FilterImportedInput("MouseMove", new Microsoft.Xna.Framework.Point(4, 5));
        Require(calls.SequenceEqual(new[] { "move-preview", "move" }), "native mouse move is routed once without duplicate delivery");
        calls.Clear(); child.PreviewTextInput += (_, e) => { calls.Add(e.Text); e.Handled = true; }; child.TextInput += (_, _) => calls.Add("unexpected-text");
        Require(child.NativeElement.FilterImportedInput("TextInput", "λ🙂") && calls.SequenceEqual(new[] { "λ🙂" }), "Unicode text composition uses the native preview route and supports cancellation");
        calls.Clear(); child.MouseDown += (_, _) => calls.Add("explicit");
        child.RaiseEvent(new ProjectZ.WpfCompatibility.Input.MouseButtonEventArgs(ProjectZ.WpfCompatibility.Input.Mouse.PrimaryDevice, 1, ProjectZ.WpfCompatibility.Input.MouseButton.Left) { RoutedEvent = UIElement.MouseDownEvent });
        Require(!calls.Contains("root-preview") && calls.Contains("explicit"), "explicit RaiseEvent dispatches only the requested route");
        calls.Clear(); var part = Native(); child.NativeElement.Children.Add(part);
        part.FilterImportedInput("MouseLeftButtonDown", new Microsoft.Xna.Framework.Point(4,5));
        Require(calls.Contains("child-bubble") && calls.Count(c => c == "root-bubble") == 1, "unwrapped native item parts route input through their imported owner once");
        child.Dispose(); calls.Clear();
        child.NativeElement.FilterImportedInput("MouseLeftButtonDown", new Microsoft.Xna.Framework.Point(4, 5));
        Require(child.NativeElement.ImportedInputFilter == null, "disposing an adapter detaches native input and routed handlers");
    }
    static void CheckAnimationClock()
    {
        var scene = new HeadlessScene(); double value=-1; int completed=0;
        using var clock = new global::ProjectZ.Shared.Animations.PropertyAnimationClock(scene,p=>value=p,()=>value=-1)
        { Duration=TimeSpan.FromSeconds(1), BeginDelay=TimeSpan.FromSeconds(.5), AutoReverse=true, RepeatCount=2 };
        clock.Completed += (_,_)=>completed++;
        clock.Tick(TimeSpan.Zero); clock.Tick(TimeSpan.FromSeconds(.25));
        Require(value==-1,"native property clock defers until BeginTime");
        clock.Tick(TimeSpan.FromSeconds(1)); Require(value==.5,"native frame clock advances interpolation");
        clock.IsPaused=true; clock.Tick(TimeSpan.FromSeconds(2)); Require(value==.5,"paused native clocks retain their current value");
        clock.IsPaused=false; clock.Tick(TimeSpan.FromSeconds(3)); Require(value==.5,"resumed native clocks exclude paused time and reverse");
        clock.Tick(TimeSpan.FromSeconds(5.5)); Require(value==0 && completed==1,"repeat counts and auto reverse complete exactly once");
        clock.Stop(); Require(value==-1,"stopping a native clock restores the original property");
    }
    static void CheckBitmapBuffers()
    {
        byte[] pixels={0,0,255,255, 0,255,0,255, 255,0,0,255, 0,0,0,255};
        var source=ProjectZ.WpfCompatibility.Media.Imaging.BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,pixels,8);
        var copy=new byte[16]; source.CopyPixels(copy,8,0); Require(copy.SequenceEqual(pixels),"native bitmap buffers preserve BGRA pixels and stride");
        var crop=new ProjectZ.WpfCompatibility.Media.Imaging.CroppedBitmap(source,new(1,0,1,2));
        var cropped=new byte[8]; crop.CopyPixels(cropped,4,0); Require(cropped.SequenceEqual(new byte[]{0,255,0,255,0,0,0,255}),"cropped bitmaps use the requested dirty rectangle");
        var writable=new ProjectZ.WpfCompatibility.Media.Imaging.WriteableBitmap(source);
        writable.WritePixels(new(0,1,1,1),new byte[]{255,255,255,255},4,0); writable.CopyPixels(copy,8,0); Require(copy[8]==255&&copy[9]==255&&copy[10]==255,"native writable bitmaps patch only the requested region");
        var translucent=ProjectZ.WpfCompatibility.Media.Imaging.BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{100,200,50,128},4);
        var premultiplied=new ProjectZ.WpfCompatibility.Media.Imaging.FormatConvertedBitmap(translucent,PixelFormats.Pbgra32,null,0); var converted=new byte[4];premultiplied.CopyPixels(converted,4,0);Require(converted.SequenceEqual(new byte[]{50,100,25,128}),"bitmap format conversion premultiplies alpha");
        var encoder=new ProjectZ.WpfCompatibility.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(ProjectZ.WpfCompatibility.Media.Imaging.BitmapFrame.Create(source));using var stream=new MemoryStream();encoder.Save(stream);stream.Position=0;
        var decoded=new ProjectZ.WpfCompatibility.Media.Imaging.BitmapImage(); decoded.BeginInit();decoded.StreamSource=stream;decoded.EndInit();decoded.CopyPixels(copy,8,0);Require(decoded.PixelWidth==2&&decoded.PixelHeight==2&&copy.SequenceEqual(pixels),"PNG encoding and stream decoding round trip actual pixels");
        bool rejected=false;try{source.CopyPixels(new byte[2],8,0);}catch(ArgumentException){rejected=true;}Require(rejected,"bitmap copying rejects undersized buffers before touching memory");
    }
    static void CheckCollectionViews()
    {
        var beta=new ViewRow {Rank=1,Name="Beta"};var able=new ViewRow {Rank=1,Name="Able"};var hidden=new ViewRow {Rank=0,Name="Hidden"};
        var source=new System.Collections.ObjectModel.ObservableCollection<ViewRow> {beta,able,hidden};
        var view=ProjectZ.WpfCompatibility.Data.CollectionViewSource.GetDefaultView(source);
        view.Filter=item=>((ViewRow)item).Rank>0;
        view.SortDescriptions.Add(new(nameof(ViewRow.Rank),System.ComponentModel.ListSortDirection.Ascending));view.SortDescriptions.Add(new(nameof(ViewRow.Name),System.ComponentModel.ListSortDirection.Ascending));
        Require(view.Cast<ViewRow>().SequenceEqual(new[]{able,beta}),"native collection views filter and sort original object identities using multiple properties");
        Require(ReferenceEquals(view,ProjectZ.WpfCompatibility.Data.CollectionViewSource.GetDefaultView(source)) && ReferenceEquals(view.SourceCollection,source),"a source collection retains one shared default native view");
        int resets=0;view.CollectionChanged+=(_,args)=>{if(args.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Reset) resets++;};
        var first=new ViewRow {Rank=2,Name="First"};source.Add(first);
        Require(view.Cast<ViewRow>().Last()==first && resets==1,"observable source updates propagate a rebuilt native collection view");
        beta.Rank=0;view.Refresh();Require(!view.Cast<ViewRow>().Contains(beta),"explicit collection view refresh re-evaluates changed model properties");
        view.SortDescriptions[0]=new(nameof(ViewRow.Rank),System.ComponentModel.ListSortDirection.Descending);
        Require(view.Cast<ViewRow>().First()==first,"native collection views apply descending sort changes");
        view.Filter=_=>false;Require(view.IsEmpty && !view.Cast<object>().Any(),"native collection views expose empty filtered results");
    }
    static void CheckStateTrigger()
    {
        var scene=new HeadlessScene();using var app=new Application(scene);using var element=new HeadlessFramework { Opacity=.9 };
        var visibilityChanges=new List<(bool Old,bool New)>();element.IsVisibleChanged+=(_,args)=>visibilityChanges.Add(((bool)args.OldValue!,(bool)args.NewValue!));
        element.Visibility=Visibility.Hidden;element.Visibility=Visibility.Visible;
        Require(visibilityChanges.SequenceEqual(new[]{(true,false),(false,true)}),"native visibility changes reach imported handlers with their actual old and new values");
        ProjectZ.WpfCompatibility.Controls.Panel.SetZIndex(element,7);Require(element.NativeElement.zIndex==7 && ProjectZ.WpfCompatibility.Controls.Panel.GetZIndex(element)==7,"attached panel order updates native draw and pointer ordering");
        var declaration=XElement.Parse("<Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.25'/></Trigger>");
        using var trigger=new NativeDataTrigger(element,declaration,new SceneXamlParser(scene));
        element.IsEnabled=false;Require(element.NativeElement.Opacity==.25f,"native property triggers observe control state and apply scalar setters");
        element.IsEnabled=true;Require(Math.Abs(element.Opacity-.9)<.0001,"native property triggers restore their base value on state exit");
        using var higher=new NativeDataTrigger(element,XElement.Parse("<Trigger Property='IsHitTestVisible' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>"),new SceneXamlParser(scene));
        element.IsEnabled=false;element.IsHitTestVisible=false;
        // Native hit-test state is observable by the imported property system.
        element.IsEnabled=true;
        Require(element.Opacity==.5,"exiting a lower priority trigger preserves the still active higher priority setter");
        element.IsHitTestVisible=true;Require(Math.Abs(element.Opacity-.9)<.0001,"overlapping native triggers restore the base after the final state exits");
    }
    sealed record MarkupIssue(string File, int Line, int Column, string Message);
}
sealed class ViewRow { public int Rank {get;set;} public string Name {get;set;}=""; }
sealed class TriggerModel : System.ComponentModel.INotifyPropertyChanged
{
    bool enabled;
    public bool Enabled { get => enabled; set { enabled = value; PropertyChanged?.Invoke(this, new(nameof(Enabled))); } }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
sealed class HeadlessScene : ProjectZ.Shared.Drawing.Scene { }
sealed class HeadlessNative : SceneElement
{
    protected override void Draw(GameTime time) { }
}
sealed class HeadlessAdapter : UIElement
{
    public HeadlessAdapter() : base(new HeadlessNative()) { }
    public HeadlessAdapter(HeadlessNative native) : base(native) { }
    // Model the parser writing this flag before the adapter restores explicit geometry.
    protected override void SyncMarkup(string property, object? value)
    {
        if (property == nameof(ClipToBounds)) NativeElement.Clip = (bool)value!;
    }
}
sealed class HeadlessFramework : FrameworkElement { public HeadlessFramework() : base(new HeadlessNative()) { } }
