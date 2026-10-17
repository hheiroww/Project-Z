#nullable enable annotations
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using ProjectZ.WpfCompatibility;
using ProjectZ.Shared.Drawing.UI.Primitives;

internal static class Program
{
    [STAThread] static void Main()
    {
        using var first = new NativeWindowSurface("Project-Z native owner probe", 300, 180);
        using var second = new NativeWindowSurface("Project-Z native child probe", 240, 160);
        var ownerHandle = first.Handle; var childHandle = second.Handle;
        Require(ownerHandle != childHandle && IsWindow(ownerHandle) && IsWindow(childHandle), "independent native HWNDs exist");
        second.SetOwner(first);
        Require(GetWindow(childHandle, 4) == ownerHandle, "native top-level ownership is attached");
        first.Scene.BackgroundColor = new Color(10, 24, 32); second.Scene.BackgroundColor = new Color(32, 24, 10);
        var textureField = typeof(RectangleElement).GetField("Texture", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var reusedRectangle = new RectangleElement(first.Scene);
        using var survivingRectangle = new RectangleElement(first.Scene);
        var sharedWhite = (Microsoft.Xna.Framework.Graphics.Texture2D)textureField.GetValue(reusedRectangle)!;
        Require(ReferenceEquals(sharedWhite, textureField.GetValue(survivingRectangle)), "rectangle primitives reuse one white texture per graphics device");
        reusedRectangle.Dispose();
        Require(!sharedWhite.IsDisposed, "disposing a primitive preserves the borrowed texture of surviving controls");
        using (var appendedRows = new ProjectZ.Shared.Drawing.UI.Input.ListBox(first.Scene))
        {
            appendedRows.VirtualizeItemTemplates = false;
            int rowMeasurements = 0;
            appendedRows.ItemVisualFactory = _ => new RectangleElement(first.Scene)
            {
                ImportedMeasure = width => { rowMeasurements++; return new Vector2(width, 40); }
            };
            for (int row = 0; row < 300; row++) appendedRows.AddItem("application " + row);
            Require(rowMeasurements == 0, "appending an application inventory defers row measurement until scene validation");
            appendedRows.ValidationCheck();
            Require(rowMeasurements == 300 && appendedRows.Children.Last().Size.Y == 40 && appendedRows.Children.Last().Position.Y == 299 * 40,
                "a batch of template rows receives one linear measurement pass with correct final geometry");
            appendedRows.ScrollIntoView(299);
            Require(appendedRows.Children.Last().Rectangle.Bottom <= appendedRows.Rectangle.Bottom,
                "scrolling to an appended variable-height row resolves its layout before visibility");
        }
        using (var virtualRows = new ProjectZ.Shared.Drawing.UI.Input.ListBox(first.Scene))
        {
            int realized = 0, released = 0;
            virtualRows.ItemVisualFactory = _ => { realized++; return new RectangleElement(first.Scene) { BackgroundColor = Color.Lime, ImportedMeasure = width => new Vector2(width,40) }; };
            virtualRows.ReleaseItemVisual = (_, _) => released++;
            virtualRows.Items = Enumerable.Range(0,300).Select(row=>"application "+row).ToList();
            Require(realized==0,"an inactive large inventory does not construct offscreen XAML templates");
            first.Scene.AddElement(virtualRows); first.RenderFrame();
            Require(realized>0 && realized<20,"a large inventory realizes only rows near its viewport");
            virtualRows.ScrollIntoView(299); first.RenderFrame();
            Require(realized<40 && released>0 && virtualRows.Children.Last().Children.Count==1,"scrolling realizes the destination row and releases offscreen template controls");
            first.Scene.RemoveElement(virtualRows);
        }
        using (var idleSurface = new NativeWindowSurface("Project-Z idle presentation probe",96,64))
        {
            var visual = new RectangleElement(idleSurface.Scene) { Position=new(4,4),Size=new(40,40),BackgroundColor=Color.Red };
            idleSurface.Scene.AddElement(visual); idleSurface.RenderFrame();
            var pacedRender=typeof(NativeWindowSurface).GetMethod("RenderFrame",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(bool)},null)!;
            int drawn=idleSurface.FrameCount;
            for(int tick=0;tick<6;tick++) {pacedRender.Invoke(idleSurface,new object[]{false});Thread.Sleep(5);}
            Require(idleSurface.FrameCount==drawn,"unchanged imported UI continues ticking without submitting or presenting duplicate frames");
            visual.BackgroundColor=Color.Blue; pacedRender.Invoke(idleSurface,new object[]{false});
            Require(idleSurface.FrameCount==drawn+1 && idleSurface.CaptureFrame().Count(c=>c.B>200&&c.R<30)>100,"a native color change immediately renders actual updated pixels");
            visual.Position=new(12,12); pacedRender.Invoke(idleSurface,new object[]{false});
            Require(idleSurface.FrameCount==drawn+2,"native geometry changes invalidate an idle frame immediately");
        }
        first.Scene.AddElement(new RectangleElement(first.Scene) { Position = new(10, 10), Size = new(80, 80), BackgroundColor = Color.Red });
        second.Scene.AddElement(new RectangleElement(second.Scene) { Position = new(10, 10), Size = new(80, 80), BackgroundColor = Color.Blue });
        for (int i = 0; i < 5; i++) { first.RenderFrame(); second.RenderFrame(); }
        var firstPixels = first.CaptureFrame(); var secondPixels = second.CaptureFrame();
        Require(firstPixels.Count(c => c.R > 200 && c.G < 30 && c.B < 30) > 100, "owner scene renders its native red content");
        Require(secondPixels.Count(c => c.B > 200 && c.G < 30 && c.R < 30) > 100, "child scene renders its native blue content");
        second.SetVisible(false); Require(!IsWindowVisible(childHandle), "native hide releases visibility without closing the HWND");
        second.SetVisible(true); Require(IsWindowVisible(childHandle), "native show restores the child HWND");
        var previousFrames = first.FrameCount; second.Dispose();
        Require(!IsWindow(childHandle) && IsWindow(ownerHandle), "closing one native window preserves the other HWND");
        first.RenderFrame(); Require(first.FrameCount > previousFrames, "owner continues rendering after child disposal");
        first.Dispose(); Require(!IsWindow(ownerHandle), "native window disposal releases its HWND");
        Require(sharedWhite.IsDisposed, "graphics device disposal releases its shared primitive texture");
        try { Hosting.Run(()=>new ImportedWindowApplication()); }
        catch(Exception error) { Console.Error.WriteLine(error); Environment.ExitCode=1; return; }
        Console.WriteLine("NATIVE_WINDOWS_READY");
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle, int command);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr handle, uint command);
    sealed class ImportedWindowApplication : Application
    {
        ProjectZ.WpfCompatibility.Threading.DispatcherTimer timer;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var first=new Window { Title="Project-Z imported owner", Width=300, Height=180, Left=140, Top=140 };
            var windowMarkup=Path.GetFullPath("artifacts/wpf-import/continuation-borderless-window.xaml");
            File.WriteAllText(windowMarkup,"""
                <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Title="Project-Z imported owner"
                        Width="300" Height="180" MinWidth="240" MinHeight="140" WindowStyle="None" ResizeMode="NoResize"
                        AllowsTransparency="True" ShowInTaskbar="False" ShowActivated="False" WindowStartupLocation="Manual" Left="140" Top="140" />
                """);
            XamlLoader.Initialize(first,windowMarkup);
            Require(first.WindowStyle==WindowStyle.None && first.ResizeMode==ResizeMode.NoResize && first.AllowsTransparency && !first.ShowInTaskbar && !first.ShowActivated,"XAML window settings reach the native window adapter");
            var sharedBrush=new ProjectZ.WpfCompatibility.Media.LinearGradientBrush(ProjectZ.WpfCompatibility.Media.Colors.Red,ProjectZ.WpfCompatibility.Media.Colors.Red,0);
            var ownerRoot=new ProjectZ.WpfCompatibility.Controls.Canvas();
            ownerRoot.Children.Add(new ProjectZ.WpfCompatibility.Controls.Border { Background=sharedBrush, Width=80, Height=80,Child=new ProjectZ.WpfCompatibility.Controls.TextBlock {Text="owner",Foreground=ProjectZ.WpfCompatibility.Media.Brushes.White} });
            var ownerTabs=new ProjectZ.WpfCompatibility.Controls.TabControl { Width=180, Height=120 };
            var retainedTabBody=new ProjectZ.WpfCompatibility.Controls.Border { Background=ProjectZ.WpfCompatibility.Media.Brushes.Green };
            var ownerTab=new ProjectZ.WpfCompatibility.Controls.TabItem { Header="Manage", Content=retainedTabBody };
            ownerTabs.Items.Add(ownerTab);
            ownerTabs.Items.Add(new ProjectZ.WpfCompatibility.Controls.TabItem {Header="Other",Content=new ProjectZ.WpfCompatibility.Controls.Border {Background=ProjectZ.WpfCompatibility.Media.Brushes.Blue}});
            ProjectZ.WpfCompatibility.Controls.Canvas.SetLeft(ownerTabs,100);ownerRoot.Children.Add(ownerTabs);first.Content=ownerRoot;
            var cardsMarkup=Path.GetFullPath("artifacts/wpf-import/continuation-measured-cards.xaml");
            File.WriteAllText(cardsMarkup,"""
                <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="90" Height="100">
                    <ListBox Name="Cards" Width="90" Height="100">
                        <ListBox.ItemTemplate><DataTemplate><Border Height="44" Background="Lime" Margin="0,0,0,4"><TextBlock Text="{Binding}" /></Border></DataTemplate></ListBox.ItemTemplate>
                    </ListBox>
                </UserControl>
                """);
            var cardsHost=new ProjectZ.WpfCompatibility.Controls.UserControl();XamlLoader.Initialize(cardsHost,cardsMarkup);
            var cards=(ProjectZ.WpfCompatibility.Controls.ListBox)cardsHost.FindName("Cards")!;cards.ItemsSource=new[]{"card one","card two"};
            ProjectZ.WpfCompatibility.Controls.Canvas.SetTop(cardsHost,80);ownerRoot.Children.Add(cardsHost);
            first.Show();
            Require((GetWindowLongPtr(first.NativeHandle,-16).ToInt64() & 0x00c40000L)==0,"a non-resizable borderless XAML window has no native caption or sizing frame");
            Require(GetClientRect(first.NativeHandle,out var ownerClient) && GetWindowRect(first.NativeHandle,out var ownerBounds) && ownerClient.Right==ownerBounds.Right-ownerBounds.Left && ownerClient.Bottom==ownerBounds.Bottom-ownerBounds.Top,"borderless loader content occupies the entire native window");
            Require((GetWindowLongPtr(first.NativeHandle,-20).ToInt64() & 0x00040000L)==0,"XAML ShowInTaskbar=false is applied to the native HWND");
            var second=new Window { Title="Project-Z imported child", Owner=first, Width=240,Height=160,Left=470,Top=140,ShowInTaskbar=false };
            File.WriteAllText(windowMarkup,"""
                <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
                        Title="Project-Z imported child" Width="240" Height="160" MinWidth="200" MinHeight="120" WindowStyle="None" ResizeMode="CanResize">
                    <shell:WindowChrome.WindowChrome><shell:WindowChrome CaptionHeight="44" ResizeBorderThickness="7" GlassFrameThickness="0" CornerRadius="6" UseAeroCaptionButtons="False" /></shell:WindowChrome.WindowChrome>
                </Window>
                """);
            XamlLoader.Initialize(second,windowMarkup);
            var secondContent=new ProjectZ.WpfCompatibility.Controls.Border { Background=ProjectZ.WpfCompatibility.Media.Brushes.Blue, Width=80,Height=80 };
            var imagePixels=Enumerable.Range(0,16*16).SelectMany(_=>new byte[]{0,255,0,255}).ToArray();
            var imageSource=ProjectZ.WpfCompatibility.Media.Imaging.BitmapSource.Create(16,16,96,96,ProjectZ.WpfCompatibility.Media.PixelFormats.Bgra32,null,imagePixels,64);
            secondContent.Child=new ProjectZ.WpfCompatibility.Controls.Image { Source=imageSource, Width=16,Height=16 };
            var secondRoot=new ProjectZ.WpfCompatibility.Controls.Canvas();secondRoot.Children.Add(secondContent);
            var scrollMarkup=Path.GetFullPath("artifacts/wpf-import/continuation-scrolled-fields.xaml");
            File.WriteAllText(scrollMarkup,"""
                <UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="80" Height="65">
                    <ScrollViewer Name="FieldsScroll" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
                        <StackPanel><Grid Height="140"><Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="*" /></Grid.ColumnDefinitions>
                            <StackPanel><TextBlock Text="A" /><TextBox Name="LeftField" Text="left" Height="24" /></StackPanel>
                            <StackPanel Grid.Column="1"><TextBlock Text="B" /><TextBox Name="RightField" Text="right" Height="24" /></StackPanel>
                        </Grid></StackPanel>
                    </ScrollViewer>
                </UserControl>
                """);
            var fieldsHost=new ProjectZ.WpfCompatibility.Controls.UserControl();XamlLoader.Initialize(fieldsHost,scrollMarkup);
            ProjectZ.WpfCompatibility.Controls.Canvas.SetLeft(fieldsHost,90);secondRoot.Children.Add(fieldsHost);
            var leftField=(ProjectZ.WpfCompatibility.Controls.TextBox)fieldsHost.FindName("LeftField")!;
            var rightField=(ProjectZ.WpfCompatibility.Controls.TextBox)fieldsHost.FindName("RightField")!;
            var fieldsScroll=(ProjectZ.WpfCompatibility.Controls.ScrollViewer)fieldsHost.FindName("FieldsScroll")!;
            var blankShape=new ProjectZ.WpfCompatibility.Controls.EmptyControl { X1=1, Y1=2, X2=3, Y2=4, StrokeStartLineCap=ProjectZ.WpfCompatibility.Media.PenLineCap.Round, StrokeEndLineCap=ProjectZ.WpfCompatibility.Media.PenLineCap.Round, Stretch=ProjectZ.WpfCompatibility.Media.Stretch.Fill, Columns=3 };
            Require(blankShape.NativeElement.SuppressVisual && blankShape.X2==3 && blankShape.StrokeEndLineCap==ProjectZ.WpfCompatibility.Media.PenLineCap.Round && blankShape.GetCopyText()=="", "unsupported shape and layout controls retain codebehind metadata while remaining blank");
            blankShape.Dispose();
            var imageAsset=Path.GetFullPath("artifacts/wpf-import/continuation-imported-image.png");
            var encoder=new ProjectZ.WpfCompatibility.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(ProjectZ.WpfCompatibility.Media.Imaging.BitmapFrame.Create(imageSource));
            using(var imageFile=File.Create(imageAsset)) encoder.Save(imageFile);
            var imageMarkup=Path.GetFullPath("artifacts/wpf-import/continuation-imported-image.xaml");
            var imageDocument=new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement(System.Xml.Linq.XName.Get("UserControl","http://schemas.microsoft.com/winfx/2006/xaml/presentation"),new System.Xml.Linq.XAttribute("Width",16),new System.Xml.Linq.XAttribute("Height",16),new System.Xml.Linq.XElement(System.Xml.Linq.XName.Get("Image","http://schemas.microsoft.com/winfx/2006/xaml/presentation"),new System.Xml.Linq.XAttribute("Name","ImportedImage"),new System.Xml.Linq.XAttribute("Source",Path.GetFileName(imageAsset)))));
            imageDocument.Save(imageMarkup);
            var importedImageHost=new ProjectZ.WpfCompatibility.Controls.UserControl();XamlLoader.Initialize(importedImageHost,imageMarkup);
            var importedImage=(ProjectZ.WpfCompatibility.Controls.Image)importedImageHost.FindName("ImportedImage")!;
            var uploadedPixels=new Color[256];((ProjectZ.Shared.Drawing.UI.Advanced.ImageElement)importedImage.NativeElement).Texture.GetData(uploadedPixels);
            Require(importedImage.Source is ProjectZ.WpfCompatibility.Media.Imaging.BitmapImage && uploadedPixels.All(c=>c.G==255 && c.R==0 && c.B==0),"XAML image sources decode and upload their real pixels through the compatibility bitmap path");
            ProjectZ.WpfCompatibility.Controls.Canvas.SetLeft(importedImageHost,100);secondRoot.Children.Add(importedImageHost);
            var sharedSurface=new ProjectZ.WpfCompatibility.Controls.Border {Background=sharedBrush,Width=80,Height=25};ProjectZ.WpfCompatibility.Controls.Canvas.SetTop(sharedSurface,95);secondRoot.Children.Add(sharedSurface);
            var rows=new ProjectZ.WpfCompatibility.Controls.ListBox {Width=120,Height=32,SelectionMode=ProjectZ.WpfCompatibility.Controls.SelectionMode.Extended,ItemTemplate=new DataTemplate(()=>new ProjectZ.WpfCompatibility.Controls.TextBlock {Text="native row",Foreground=ProjectZ.WpfCompatibility.Media.Brushes.White})};ProjectZ.WpfCompatibility.Controls.Canvas.SetTop(rows,125);rows.ItemsSource=new[]{"one","two","three"};secondRoot.Children.Add(rows);
            var paint=new ProjectZ.WpfCompatibility.Media.SolidColorBrush(ProjectZ.WpfCompatibility.Media.Colors.Blue);
            var paintedSurface=new ProjectZ.WpfCompatibility.Controls.Border {Background=paint,Width=16,Height=16};
            using (var choice = new ProjectZ.WpfCompatibility.Controls.ComboBox { IsEditable = true })
            {
                choice.Text = "typed native model";
                Require(choice.Text == "typed native model" && ((ProjectZ.Shared.Drawing.UI.Input.ComboBox)choice.NativeElement).EditableText == choice.Text, "editable imported dropdowns retain native input text");
                var model = new ChoiceModel();
                var style = new Style(typeof(ProjectZ.WpfCompatibility.Controls.ComboBoxItem));
                style.Setters.Add(new Setter(UIElement.IsEnabledProperty, new ProjectZ.WpfCompatibility.Data.Binding(nameof(ChoiceModel.Enabled))));
                choice.ItemContainerStyle = style; choice.ItemsSource = new[] { model };
                var nativeChoice = (ProjectZ.Shared.Drawing.UI.Input.ComboBox)choice.NativeElement;
                Require(!nativeChoice.ItemIsEnabled(0), "native dropdown item-container bindings disable unavailable model choices");
                model.Enabled = true;
                Require(nativeChoice.ItemIsEnabled(0), "model changes refresh native dropdown item-container bindings");
                int doubleClicks = 0;
                choice.MouseDoubleClick += (_, args) => { Require(args.ClickCount == 2, "native double-click handlers receive the click count"); doubleClicks++; };
                nativeChoice.ImportedInputFilter("MouseLeftButtonDown", new object[] { new Microsoft.Xna.Framework.Point(2,2) });
                nativeChoice.ImportedInputFilter("MouseLeftButtonDown", new object[] { new Microsoft.Xna.Framework.Point(2,2) });
                Require(doubleClicks == 1, "native pointer input routes imported double-click handlers once");
            }
            using (var scrolling = new ProjectZ.WpfCompatibility.Controls.ScrollViewer { Width=100, Height=80, HorizontalScrollBarVisibility=ProjectZ.WpfCompatibility.Controls.ScrollBarVisibility.Hidden, VerticalScrollBarVisibility=ProjectZ.WpfCompatibility.Controls.ScrollBarVisibility.Hidden })
            {
                var oversized = new ProjectZ.WpfCompatibility.Controls.Border { Width=200, Height=180 };
                scrolling.Content=oversized; scrolling.ScrollToHorizontalOffset(250); scrolling.ScrollToVerticalOffset(250);
                var nativeScrolling=(ProjectZ.Shared.Drawing.UI.Input.ScrollViewer)scrolling.NativeElement;
                Require(scrolling.ScrollableWidth==100 && scrolling.ScrollableHeight==100 && scrolling.HorizontalOffset==100 && scrolling.VerticalOffset==100, "hidden native scrollbars retain scrolling and clamp against the full viewport");
                Require(!nativeScrolling.HorizontalScrollBarIsVisible && !nativeScrolling.VerticalScrollBarIsVisible, "hidden scrollbar policy suppresses both native tracks");
                scrolling.VerticalScrollBarVisibility=ProjectZ.WpfCompatibility.Controls.ScrollBarVisibility.Disabled;
                Require(scrolling.VerticalOffset==0 && scrolling.ScrollableHeight==0, "disabled native scrolling resets the axis and rejects overflow");
                oversized.Width=10; oversized.Height=10;
                scrolling.HorizontalScrollBarVisibility=ProjectZ.WpfCompatibility.Controls.ScrollBarVisibility.Visible;
                scrolling.VerticalScrollBarVisibility=ProjectZ.WpfCompatibility.Controls.ScrollBarVisibility.Visible;
                Require(nativeScrolling.HorizontalScrollBarIsVisible && nativeScrolling.VerticalScrollBarIsVisible && scrolling.ScrollableHeight==0, "visible native scrollbar policy retains both tracks when content fits");
            }
            ProjectZ.WpfCompatibility.Controls.Canvas.SetLeft(paintedSurface,180);ProjectZ.WpfCompatibility.Controls.Canvas.SetTop(paintedSurface,10);
            secondRoot.Children.Add(paintedSurface);
            int nativeWindowClicks=0;
            int tabRefreshPhase=0;
            var nativeWindowButton=new ProjectZ.WpfCompatibility.Controls.Button {Content="OS click",Width=110,Height=25};
            ProjectZ.WpfCompatibility.Controls.Canvas.SetLeft(nativeWindowButton,90);ProjectZ.WpfCompatibility.Controls.Canvas.SetTop(nativeWindowButton,110);
            nativeWindowButton.Click+=(_,_)=>nativeWindowClicks++;
            int pointerDowns=0,pointerUps=0,pointerClicks=0;
            nativeWindowButton.NativeElement.MouseLeftDown+=_=>pointerDowns++;
            nativeWindowButton.NativeElement.MouseLeftUp+=_=>pointerUps++;
            nativeWindowButton.NativeElement.MouseLeftClick+=_=>pointerClicks++;
            secondRoot.Children.Add(nativeWindowButton);
            var nativeWindowInput=new ProjectZ.WpfCompatibility.Controls.TextBox {Width=95,Height=28};
            ProjectZ.WpfCompatibility.Controls.Canvas.SetLeft(nativeWindowInput,135);ProjectZ.WpfCompatibility.Controls.Canvas.SetTop(nativeWindowInput,70);
            secondRoot.Children.Add(nativeWindowInput);second.Content=secondRoot; second.Show();
            var colorMotion=new ProjectZ.WpfCompatibility.Media.Animation.ColorAnimation {From=ProjectZ.WpfCompatibility.Media.Colors.Blue,To=ProjectZ.WpfCompatibility.Media.Color.FromRgb(255,255,0),Duration=TimeSpan.FromMilliseconds(150)};
            bool colorCompleted=false;colorMotion.Completed+=(_,_)=>colorCompleted=true;paint.BeginAnimation(ProjectZ.WpfCompatibility.Media.SolidColorBrush.ColorProperty,colorMotion);
            var repeatingTransform=new ProjectZ.WpfCompatibility.Media.TranslateTransform();paintedSurface.RenderTransform=repeatingTransform;
            var repeatedMotion=new ProjectZ.WpfCompatibility.Media.Animation.DoubleAnimation(0,15,TimeSpan.FromMilliseconds(60));
            var repeatingStoryboard=new ProjectZ.WpfCompatibility.Media.Animation.Storyboard {AutoReverse=true,RepeatBehavior=new(2)};
            ProjectZ.WpfCompatibility.Media.Animation.Storyboard.SetTarget(repeatedMotion,repeatingTransform);ProjectZ.WpfCompatibility.Media.Animation.Storyboard.SetTargetProperty(repeatedMotion,new PropertyPath(ProjectZ.WpfCompatibility.Media.TranslateTransform.XProperty));repeatingStoryboard.Children.Add(repeatedMotion);
            bool repeatCompleted=false,sawMovement=false;repeatingStoryboard.Completed+=(_,_)=>repeatCompleted=true;repeatingStoryboard.Begin();
            var childHandle=second.NativeHandle; var ownerHandle=first.NativeHandle;
            Require((GetWindowLongPtr(childHandle,-16).ToInt64() & 0x00c00000L)==0 && (GetWindowLongPtr(childHandle,-16).ToInt64() & 0x00040000L)!=0,"resizable borderless XAML windows retain sizing without a Windows title bar");
            bool readChildClient=GetClientRect(childHandle,out var childClient), readChildBounds=GetWindowRect(childHandle,out var childBounds);
            Require(readChildClient && readChildBounds && childClient.Right==childBounds.Right-childBounds.Left && childClient.Bottom==childBounds.Bottom-childBounds.Top,"the custom main window frame has no non-client border");
            static IntPtr ScreenPoint(int x,int y)=>new(unchecked((uint)(ushort)x | ((uint)(ushort)y<<16)));
            Require(SendMessage(childHandle,0x84,IntPtr.Zero,ScreenPoint(childBounds.Left+2,childBounds.Top+2)).ToInt64()==13 && SendMessage(childHandle,0x84,IntPtr.Zero,ScreenPoint(childBounds.Right-2,childBounds.Bottom-2)).ToInt64()==17,"custom borderless windows route native corner resizing");
            Require(SendMessage(childHandle,0x84,IntPtr.Zero,ScreenPoint(childBounds.Left+60,childBounds.Top+40)).ToInt64()==1,"application header content remains client UI");
            Require(childHandle!=ownerHandle && GetWindow(childHandle,4)==ownerHandle,"imported windows own separate Project-Z HWNDs and honor Owner");
            Require(Math.Abs(first.Left-140)<2 && Math.Abs(second.Left-470)<2,"imported window positioning reaches the native HWNDs");
            var translation=new ProjectZ.WpfCompatibility.Media.TranslateTransform();
            secondContent.RenderTransform=translation;
            var animation=new ProjectZ.WpfCompatibility.Media.Animation.DoubleAnimation(0,40,TimeSpan.FromMilliseconds(150));
            var storyboard=new ProjectZ.WpfCompatibility.Media.Animation.Storyboard();
            ProjectZ.WpfCompatibility.Media.Animation.Storyboard.SetTarget(animation,translation);
            ProjectZ.WpfCompatibility.Media.Animation.Storyboard.SetTargetProperty(animation,new PropertyPath(ProjectZ.WpfCompatibility.Media.TranslateTransform.XProperty));
            storyboard.Children.Add(animation); bool completed=false; storyboard.Completed+=(_,_)=>completed=true; storyboard.Begin();
            var deadline=DateTime.UtcNow.AddSeconds(12);
            int phase=0;
            int minimizedFrames=0;
            var ownerSurface=(NativeWindowSurface)typeof(Window).GetField("surface",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(first)!;
            var childSurface=(NativeWindowSurface)typeof(Window).GetField("surface",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(second)!;
            int nativeMousePhase=0;
            timer=new() { Interval=TimeSpan.FromMilliseconds(40) };
            timer.Tick+=(_,_)=>
            {
                if(DateTime.UtcNow>deadline) throw new TimeoutException($"Imported window probe timed out: input phase={nativeMousePhase}, clicks={nativeWindowClicks}, pointer={pointerDowns}/{pointerUps}/{pointerClicks}, tabs={tabRefreshPhase}, animation={completed}/{colorCompleted}/{repeatCompleted}, scene elements={second.NativeElement.OwnerScene.Elements.Count}, mouse={second.NativeElement.OwnerScene.MousePosition}, button={nativeWindowButton.NativeElement.Rectangle}, hitMatches={ReferenceEquals(second.NativeElement.OwnerScene.PointToElement(new Microsoft.Xna.Framework.Point(120,120)),nativeWindowButton.NativeElement)}.");
                sawMovement|=repeatingTransform.X>1;
                var nativeMousePoint=new IntPtr(120 | (122 << 16));
                if(nativeMousePhase==0 && (ownerSurface.FrameCount<3 || childSurface.FrameCount<3)) return;
                if(nativeMousePhase==0) {SendMessage(childHandle,0x200,IntPtr.Zero,nativeMousePoint);SendMessage(childHandle,0x201,new IntPtr(1),nativeMousePoint);SendMessage(childHandle,0x202,IntPtr.Zero,nativeMousePoint);nativeMousePhase=2;return;}
                if(nativeMousePhase==2) {
                    if(nativeWindowClicks==0) return; // Dispatcher ticks can precede the next native surface update.
                    var mouseScene=second.NativeElement.OwnerScene;
                    var mouseManager=(ProjectZ.Shared.Drawing.SceneManager)typeof(ProjectZ.Shared.Drawing.Scene).GetField("ownerManager",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(mouseScene)!;
                    var inputWindow=(System.Windows.Forms.NativeWindow)typeof(ProjectZ.Shared.Drawing.SceneManager).GetField("nativeKeyboard",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(mouseManager)!;
                    Console.WriteLine($"HWND_INPUT clicks={nativeWindowClicks}; point={mouseScene.MousePosition}; button={nativeWindowButton.NativeElement.Rectangle}; nativeInput={mouseManager.UseWindowMessagesInput}; keyboard={inputWindow.Handle}; expected={childHandle}");
                    Require(inputWindow.Handle==childHandle,"native input queue belongs to the rendered child HWND");
                    Require(nativeWindowClicks==1,"real HWND mouse messages reach the owning window's native Project-Z Click event");nativeMousePhase=3;return;
                }
                var inputPoint=new IntPtr(160 | (83 << 16));
                if(nativeMousePhase==3) {SendMessage(childHandle,0x201,new IntPtr(1),nativeMousePoint);SendMessage(childHandle,0x1F,IntPtr.Zero,IntPtr.Zero);SendMessage(childHandle,0x202,IntPtr.Zero,nativeMousePoint);nativeMousePhase=4;return;}
                if(nativeMousePhase==4) {Require(nativeWindowClicks==1,"an interrupted native pointer gesture does not click");SendMessage(childHandle,0x200,IntPtr.Zero,inputPoint);SendMessage(childHandle,0x201,new IntPtr(1),inputPoint);SendMessage(childHandle,0x202,IntPtr.Zero,inputPoint);SendMessage(childHandle,0x100,new IntPtr(0x5A),new IntPtr(1));SendMessage(childHandle,0x102,new IntPtr('Z'),new IntPtr(1));SendMessage(childHandle,0x101,new IntPtr(0x5A),new IntPtr(0xC0000001L));nativeMousePhase=5;return;}
                if(nativeMousePhase==5) {if(nativeWindowInput.Text.Length==0) return;Console.WriteLine($"HWND_TEXT value={nativeWindowInput.Text}");Require(nativeWindowInput.Text=="Z","native HWND keyboard and Unicode messages edit Project-Z input exactly once");nativeMousePhase=6;}
                if(tabRefreshPhase==0)
                {
                    Require(first.NativeElement.OwnerScene.ContainsElement(retainedTabBody.NativeElement),"initial active tab subtree is registered");
                    ownerTab.Header="Manage models";ownerTabs.SelectedIndex=1;ownerTabs.SelectedIndex=0;
                    ownerRoot.NativeElement.Children.Remove(ownerTabs.NativeElement);ownerRoot.NativeElement.Children.Add(ownerTabs.NativeElement);
                    tabRefreshPhase=1;return;
                }
                if(tabRefreshPhase==1)
                {
                    Require(first.NativeElement.OwnerScene.ContainsElement(retainedTabBody.NativeElement) && !retainedTabBody.NativeElement.IsDisposed,"tab refresh and same-frame subtree reattachment preserve native content registration");
                    first.SaveFrame(Path.GetFullPath("artifacts/wpf-import/continuation-retained-tabs.png"));
                    Console.WriteLine($"TAB_BODY rectangle={retainedTabBody.NativeElement.Rectangle}; parent={ownerTab.NativeElement.Rectangle}; tabs={ownerTabs.NativeElement.Rectangle}; renderException={ProjectZ.Shared.Drawing.UI.SceneElement.FirstRenderException}");
                    Require(first.CaptureFrame().Count(c=>c.G>80&&c.R<30&&c.B<30)>100,"refreshed active tab body still renders actual pixels");
                    Require(first.NativeElement.OwnerScene.Quality.AntiAliasingSamples==4 && first.NativeElement.OwnerScene.Quality.Filtering==ProjectZ.Shared.Drawing.TextureFiltering.Anisotropic16x,"imported windows enable 4x AA and 16x AF");
                    tabRefreshPhase=2;
                }
                if(phase==1) {first.WindowState=WindowState.Minimized;minimizedFrames=ownerSurface.FrameCount;phase=2;return;}
                if(phase==2) {Require(ownerSurface.FrameCount==minimizedFrames,"minimized imported windows do not render or present additional frames");first.WindowState=WindowState.Normal;phase=3;return;}
                if(phase==3) { timer.Stop();Require(first.CaptureFrame().Count(c=>c.R>200&&c.G<30)>100,"closing a window with a shared brush preserves rendering in the surviving window");first.Close();return; }
                if(!completed || !colorCompleted || !repeatCompleted) return;
                Require(translation.X==40 && secondContent.NativeElement.RenderTranslation.X==40,"codebehind storyboard updates native transforms and completes on the frame clock");
                Require(first.CaptureFrame().Count(c=>c.R>200&&c.B<30)>100 && second.CaptureFrame().Count(c=>c.B>200&&c.R<30)>100,"imported windows render their distinct native content");
                second.SaveFrame(Path.GetFullPath("artifacts/wpf-import/continuation-child.png"));
                Require(second.CaptureFrame().Count(c=>c.G>200&&c.R<30&&c.B<30)>100,"native bitmap uploads render their actual decoded pixels through DX12");
                Require(ReferenceEquals(paintedSurface.Background,paint) && ((ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement)paintedSurface.NativeElement).BackgroundColor==Color.Yellow && second.CaptureFrame().Count(c=>c.R>200&&c.G>200&&c.B<30)>100,"codebehind brush animations update actual pixels on the owning window frame clock");
                Require(sawMovement && Math.Abs(repeatingTransform.X)<0.001,"storyboard auto-reverse and repeat settings drive the native child timeline as a group");
                Require(first.CaptureFrame().Count(c=>c.R>200&&c.G>200&&c.B>200)>30,"imported windows render vector text on their own graphics device");
                Console.WriteLine("CARDS "+string.Join("; ",cards.ItemViews.Select(v=>$"{v.NativeElement.Rectangle}, desired={v.NativeElement.ImportedMeasure?.Invoke(90)}")));
                Require(cards.ItemViews.Count==2 && cards.ItemViews[0].ActualHeight>=44 && cards.ItemViews[1].NativeElement.Rectangle.Top>=cards.ItemViews[0].NativeElement.Rectangle.Bottom,"imported item templates measure full card heights and place successive cards without overlap");
                var cardPixel=first.CaptureFrame()[170*300+10];
                Require(cardPixel.G>220 && cardPixel.R<30 && cardPixel.B<30,"the lower portion of the second native template card is actually rendered");
                Require(leftField.ActualWidth>25 && rightField.ActualWidth>25 && rightField.NativeElement.Rectangle.Left>=leftField.NativeElement.Rectangle.Right && fieldsScroll.ScrollableHeight>=75,"scrolling imported forms retain responsive columns and their natural vertical extent");
                Require(rows.ItemViews.Count==3 && ReferenceEquals(rows.ItemViews[0].NativeElement.OwnerScene,second.NativeElement.OwnerScene),"data template controls are constructed in the owning window scene");
                int selectedEvents=0; rows.SelectionChanged+=(_,args)=> {selectedEvents++;Require(args.AddedItems.Count+args.RemovedItems.Count>0,"multiple selection emits actual changed object items");};
                rows.SelectedItems.Add("one");rows.SelectedItems.Add("three");
                Require(rows.SelectedItems.Cast<string>().SequenceEqual(new[]{"one","three"}) && ((ProjectZ.Shared.Drawing.UI.Input.ListBox)rows.NativeElement).SelectedIndexes.SequenceEqual(new[]{0,2}),"SelectedItems preserves object identity and selects native rows");
                ((ProjectZ.Shared.Drawing.UI.Input.ListBox)rows.NativeElement).SelectIndex(1,false,true);
                Require(rows.SelectedItems.Count==2 && rows.SelectedItems.Contains("two"),"extended native selection supports an anchored shift range");
                rows.SelectedItems.Clear();Require(rows.SelectedItems.Count==0&&rows.SelectedIndex==-1&&selectedEvents==4,"clearing multiple selection updates native state and routed events");
                Require(second.CaptureFrame().Count(c=>c.R>200&&c.G<30&&c.B<30)>100,"shared gradient resources render on separate native graphics devices");
                var capture=new ProjectZ.WpfCompatibility.Media.Imaging.RenderTargetBitmap(80,80,96,96,ProjectZ.WpfCompatibility.Media.PixelFormats.Pbgra32);capture.Render(secondContent);var captureBytes=new byte[80*80*4];capture.CopyPixels(captureBytes,80*4,0);Require(captureBytes.Where((b,i)=>i%4==1&&b>200).Count()>100,"native visual capture returns actual rendered element pixels");
                var point=secondContent.PointToScreen(new(7,9));Require(secondContent.PointFromScreen(point)==new ProjectZ.WpfCompatibility.Point(7,9),"native visual coordinates round trip through its own HWND");
                int stateEvents=0; second.StateChanged+=(_,_)=>stateEvents++;
                ShowWindow(childHandle,6); Require(second.WindowState==WindowState.Minimized && stateEvents==1,"native system minimization routes the original StateChanged event exactly once");
                second.WindowState=WindowState.Normal; Require(second.WindowState==WindowState.Normal,"imported restore updates native state");
                Require(stateEvents==2,"restoring through the translated property routes one additional state event");
                second.Hide(); Require(!IsWindowVisible(childHandle),"imported Hide hides the native window"); second.Show(); Require(IsWindowVisible(childHandle),"imported Show restores a hidden native window");
                bool cancelClose=true;second.Closing+=(_,args)=>args.Cancel=cancelClose;
                SendMessage(childHandle,0x10,IntPtr.Zero,IntPtr.Zero);Require(IsWindow(childHandle),"native close requests honor the original Closing cancellation handler");
                cancelClose=false;SendMessage(childHandle,0x10,IntPtr.Zero,IntPtr.Zero); Require(!IsWindow(childHandle) && IsWindow(ownerHandle),"native close requests route Closing and destroy only their own window");
                phase=1;
            };
            timer.Start();
        }
    }
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [StructLayout(LayoutKind.Sequential)] struct NativeBounds {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hwnd,out NativeBounds bounds);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out NativeBounds bounds);
}
sealed class ChoiceModel : System.ComponentModel.INotifyPropertyChanged
{
    bool enabled;
    public bool Enabled { get => enabled; set { enabled = value; PropertyChanged?.Invoke(this, new(nameof(Enabled))); } }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
