#nullable enable
using System.Runtime.InteropServices;
using ProjectZ.WpfCompatibility;
using ProjectZ.WpfCompatibility.Controls;
using ProjectZ.WpfCompatibility.Data;
using ProjectZ.WpfCompatibility.Threading;
using NativeButton = ProjectZ.Shared.Drawing.UI.Input.Button;
using NativeTextBlock = ProjectZ.Shared.Drawing.UI.Primitives.TextElement;

internal static class ImportedLayoutChecks
{
    internal static void Run() => Hosting.Run(static () => new LayoutApplication());

    private sealed class LayoutApplication : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var markup = Path.GetFullPath("artifacts/wpf-import/continuation-imported-layout.xaml");
            File.WriteAllText(markup, """
                <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        Title="Imported layout fixture" Width="352" Height="220" WindowStyle="None" ShowActivated="False">
                    <Grid Margin="8">
                        <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
                        <DockPanel Name="Toolbar">
                            <WrapPanel Name="Actions" DockPanel.Dock="Right" HorizontalAlignment="Right">
                                <Button Name="Export" Content="Export details" Padding="8,3" Margin="0,0,8,0"/>
                                <Button Name="Reset" Content="Reset results" Padding="8,3"/>
                            </WrapPanel>
                            <TextBlock Name="ToolbarTitle" Text="Item details" FontSize="14"/>
                        </DockPanel>
                        <ListBox Name="Results" Grid.Row="1" Margin="0,10,0,0">
                            <ListBox.ItemTemplate>
                                <DataTemplate>
                                    <Border Padding="8" Background="#203043" Margin="0,0,0,4">
                                        <Grid>
                                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                            <StackPanel Orientation="Horizontal">
                                                <TextBlock Name="ResultTitle" Text="{Binding Title}" Foreground="White" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
                                                <TextBlock Text="Ready" Margin="8,0,0,0"/>
                                            </StackPanel>
                                            <StackPanel Grid.Column="1" Orientation="Horizontal" Margin="10,0,0,0" HorizontalAlignment="Right">
                                                <TextBlock Text="Local"/>
                                                <Button Content="Open" Padding="4,1" Margin="6,0,0,0"/>
                                            </StackPanel>
                                        </Grid>
                                    </Border>
                                </DataTemplate>
                            </ListBox.ItemTemplate>
                        </ListBox>
                    </Grid>
                </Window>
                """);

            var window = new Window();
            XamlLoader.Initialize(window, markup);
            var toolbar = (DockPanel)window.FindName("Toolbar")!;
            var actions = (WrapPanel)window.FindName("Actions")!;
            var title = (TextBlock)window.FindName("ToolbarTitle")!;
            var reset = (Button)window.FindName("Reset")!;
            var results = (ListBox)window.FindName("Results")!;
            var source = new[] { new Result("Other A"), new Result("Match with a short title"), new Result("Other B"), new Result("Matching example with a much longer complete title") };
            var view = CollectionViewSource.GetDefaultView(source);
            results.ItemsSource = view;
            window.Show();
            var tabsWindow = OpenTabsFixture();

            int tick = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
            timer.Tick += (_, _) =>
            {
                if (++tick == 3)
                    view.Filter = item => ((Result)item).Title.StartsWith("Match", StringComparison.Ordinal);
                if (tick < 6) return;
                timer.Stop();

                var titles = results.ItemViews.Select(item => (TextBlock)item.FindName("ResultTitle")!).ToArray();
                window.SaveFrame(Path.GetFullPath("artifacts/wpf-import/native-imported-layout.png"));
                Require(titles.Select(item => item.Text).SequenceEqual(source.Where(item => item.Title.StartsWith("Match", StringComparison.Ordinal)).Select(item => item.Title)) &&
                    titles.All(item => item.ActualWidth > 20 && item.ActualHeight > 8),
                    "filtered native rows retain each bound title and give it a visible header");
                foreach (var titleLabel in titles)
                {
                    var titlePanel = (StackPanel)titleLabel.Parent!;
                    var titleText = (NativeTextBlock)titleLabel.NativeElement;
                    var badge = titlePanel.Children[1].NativeElement.Rectangle;
                    var bounds = titleText.Rectangle;
                    var desired = titleText.MeasureXamlText(1).X;
                    titleText.MeasureXamlText(float.MaxValue);
                    Console.WriteLine($"Imported title: panel {titlePanel.NativeElement.Rectangle}, title {bounds}, badge {badge}, after remeasure {titleText.Rectangle}, intrinsic {desired:F1}");
                    Require(bounds == titleText.Rectangle && bounds.Right <= badge.Left && badge.Right <= titlePanel.NativeElement.Rectangle.Right,
                        "trimmable title measurement preserves its arranged width and leaves its badge inside the column");
                }
                var pixels = window.CaptureFrame();
                int width = (int)window.Width;
                int height = pixels.Length / width;
                Require(titles.All(item =>
                {
                    var bounds = item.NativeElement.Rectangle;
                    for (int y = Math.Max(0, bounds.Top); y < Math.Min(height, bounds.Bottom); y++)
                        for (int x = Math.Max(0, bounds.Left); x < Math.Min(width, Math.Min(bounds.Right, bounds.Left + 48)); x++)
                            if (pixels[y * width + x] is { R: > 180, G: > 180, B: > 180 }) return true;
                    return false;
                }), "filtered native row titles paint actual foreground pixels");

                var toolbarBounds = toolbar.NativeElement.Rectangle;
                var actionBounds = actions.NativeElement.Rectangle;
                var resetBounds = reset.NativeElement.Rectangle;
                var titleBounds = title.NativeElement.Rectangle;
                Require(resetBounds.Width > 30 && resetBounds.Right <= toolbarBounds.Right && resetBounds.Right >= actionBounds.Right - 2 &&
                    titleBounds.Right <= actionBounds.Left,
                    $"right-docked actions fit inside the toolbar, leaving the remainder for its title (toolbar {toolbarBounds}, actions {actionBounds}, final button {resetBounds}, title {titleBounds})");
                CheckImportedTabs(tabsWindow);
                CheckNativeClientResize(tabsWindow);
                tabsWindow.Close();
                window.Close();
            };
            timer.Start();
        }
    }

    private static Window OpenTabsFixture()
    {
        var markup = Path.GetFullPath("artifacts/wpf-import/continuation-imported-tabs.xaml");
        File.WriteAllText(markup, """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    Title="Imported tabs fixture" Width="640" Height="250" WindowStyle="None" ShowActivated="False">
                <Grid>
                <Grid.RowDefinitions><RowDefinition Height="34"/><RowDefinition Height="*"/></Grid.RowDefinitions>
                <Menu Name="MainMenu" HorizontalAlignment="Left">
                    <MenuItem Name="FileMenu" Header="File" Padding="8,4">
                        <MenuItem Name="FileOpenMenu" Header="Open Web Chat"/>
                    </MenuItem>
                    <MenuItem Name="ViewMenu" Header="View" Padding="8,4">
                        <MenuItem Header="Diagnostics"/>
                    </MenuItem>
                </Menu>
                <TabControl Name="MainTabs" Grid.Row="1">
                    <TabItem Header="Diagnostics" Name="DiagnosticsTab">
                        <Grid Name="DiagnosticsBody">
                            <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
                            <DockPanel Name="DirectToolbar">
                                <Button Name="Refresh" DockPanel.Dock="Right" Content="Refresh" Padding="8,3"/>
                                <TextBlock Name="DashboardTitle" Text="Workstation diagnostics" Margin="8,2,0,2" FontSize="14"/>
                            </DockPanel>
                            <TextBlock Grid.Row="1" Text="Service status" Margin="8"/>
                        </Grid>
                    </TabItem>
                    <TabItem Name="PhoneTab">
                        <TabItem.Header>
                            <StackPanel Name="PhoneHeader" Orientation="Horizontal">
                                <TextBlock Name="PhoneCaption" Text="Link Phone" VerticalAlignment="Center"/>
                                <Button Name="Reload" Content="↻" Width="24" Height="22" Margin="6,0,4,0"/>
                                <Button Name="OpenExternal" Content="↗" Width="24" Height="22"/>
                            </StackPanel>
                        </TabItem.Header>
                        <TextBlock Text="Connected phone"/>
                    </TabItem>
                    <TabItem Header="Server Management" Name="ServerTab"><TextBlock Text="Server status"/></TabItem>
                    <TabItem Header="Models"><TextBlock Text="Local catalog"/></TabItem>
                    <TabItem Header="GPU"><TextBlock Text="Adapters"/></TabItem>
                </TabControl>
                </Grid>
            </Window>
            """);
        var window = new Window();
        XamlLoader.Initialize(window, markup);
        window.Show();
        return window;
    }

    private static void CheckImportedTabs(Window window)
    {
        var fileMenu = (MenuItem)window.FindName("FileMenu")!;
        var viewMenu = (MenuItem)window.FindName("ViewMenu")!;
        var nestedMenu = (MenuItem)window.FindName("FileOpenMenu")!;
        Require(fileMenu.NativeElement.Rectangle.Width > 20 && viewMenu.NativeElement.Rectangle.Width > 20 &&
            fileMenu.NativeElement.Rectangle.Right <= viewMenu.NativeElement.Rectangle.Left &&
            nestedMenu.NativeElement.RoutedParent == null && !nestedMenu.NativeElement.OwnerScene.ContainsElement(nestedMenu.NativeElement),
            "top-level menu captions fit and detached submenu rows do not paint inside them");
        var tabs = (TabControl)window.FindName("MainTabs")!;
        var headers = tabs.NativeElement.Children.OfType<NativeButton>().ToArray();
        var headerContent = ((StackPanel)window.FindName("PhoneHeader")!).NativeElement.Rectangle;
        var externalButton = ((Button)window.FindName("OpenExternal")!).NativeElement;
        Require(headers.Length == 5 &&
            headers[1].Rectangle.Contains(headerContent) && headerContent.Contains(externalButton.Rectangle) &&
            externalButton.Rectangle.Width == 24 && headers[1].Rectangle.Right <= headers[2].Rectangle.Left,
            $"compound tab headers arrange their action buttons inside their own slot (slot {headers[1].Rectangle}, content {headerContent}, action {externalButton.Rectangle})");
        Require(ReferenceEquals(externalButton.OwnerScene.PointToElement(externalButton.Rectangle.Center), externalButton),
            "tab header action receives input instead of a neighboring tab");
        CheckPlainTabCaptions(window, headers);

        var refresh = ((Button)window.FindName("Refresh")!).NativeElement.Rectangle;
        var title = ((TextBlock)window.FindName("DashboardTitle")!).NativeElement.Rectangle;
        var diagnosticsBody = ((Grid)window.FindName("DiagnosticsBody")!).NativeElement.Rectangle;
        Require(tabs.SelectedItem == window.FindName("DiagnosticsTab") && diagnosticsBody.Width > 200 &&
            refresh.Width > 28 && refresh.Width < 100 && title.Width > 100 && title.Right <= refresh.Left,
            $"selected diagnostics body and direct toolbar title receive space (body {diagnosticsBody}, title {title}, action {refresh})");
        fileMenu.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(nestedMenu.NativeElement.RoutedParent is ProjectZ.Shared.Drawing.UI.Input.ContextMenu popup &&
            popup.OwnerScene == fileMenu.NativeElement.OwnerScene && popup.Rectangle.Top >= fileMenu.NativeElement.Rectangle.Bottom,
            "opening a menu mounts its existing children beneath the header in the same native window");
    }

    private static void CheckPlainTabCaptions(Window window, NativeButton[] headers)
    {
        var pixels = window.CaptureFrame();
        var width = (int)window.ActualWidth;
        var height = pixels.Length / width;
        foreach (var header in headers.Where(header => header.Text.Length > 0))
        {
            var label = header.Children.OfType<NativeTextBlock>().Single();
            var bounds = header.Rectangle;
            int glyphPixels = 0;
            for (int y = Math.Max(0, bounds.Top + 3); y < Math.Min(height, bounds.Bottom - 3); y++)
                for (int x = Math.Max(0, bounds.Left + 3); x < Math.Min(width, bounds.Right - 3); x++)
                    if (pixels[y * width + x] is { R: > 165, G: > 165, B: > 165 })
                        glyphPixels++;
            Console.WriteLine($"Caption {header.Text}: pixels {glyphPixels}, slot {bounds}, label {label.Rectangle}, natural {label.MeasureXamlText(float.MaxValue)}, registered {label.OwnerScene.ContainsElement(label)}, color {label.ForegroundColor}");
            Require(glyphPixels > 10, $"plain tab caption {header.Text} renders visible glyphs");
        }
    }

    private static void CheckNativeClientResize(Window window)
    {
        var action = (Button)window.FindName("Refresh")!;
        var heading = (TextBlock)window.FindName("DashboardTitle")!;
        var buttonSize = action.NativeElement.Size;
        var glyphSize = ((NativeTextBlock)heading.NativeElement).MeasureXamlText(float.MaxValue);

        foreach (var (width, height) in new[] { (356, 218), (560, 296) })
        {
            SendMessage(window.NativeHandle, 0x231, IntPtr.Zero, IntPtr.Zero); // WM_ENTERSIZEMOVE
            Require(SetWindowPos(window.NativeHandle, IntPtr.Zero, 0, 0, width, height, 0x16), "native resize reaches the HWND");
            SendMessage(window.NativeHandle, 0x232, IntPtr.Zero, IntPtr.Zero); // WM_EXITSIZEMOVE
            var pixels = window.CaptureFrame().Length;
            Require(GetClientRect(window.NativeHandle, out var client) && client.Right == width && client.Bottom == height &&
                window.ActualWidth == width && window.ActualHeight == height && pixels == width * height,
                $"native client size {width}x{height} becomes the layout and backbuffer size (client {client.Right}x{client.Bottom}, layout {window.ActualWidth}x{window.ActualHeight}, frame pixels {pixels})");
            Require(action.NativeElement.Size == buttonSize && ((NativeTextBlock)heading.NativeElement).MeasureXamlText(float.MaxValue) == glyphSize,
                "native resizing reflows the available space while preserving the button and glyph dimensions");
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct Bounds { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Bounds bounds);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private sealed record Result(string Title);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
