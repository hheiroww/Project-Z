#nullable enable
using ProjectZ.WpfCompatibility;
using ProjectZ.WpfCompatibility.Controls;
using ProjectZ.WpfCompatibility.Data;
using ProjectZ.WpfCompatibility.Threading;

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
                window.Close();
            };
            timer.Start();
        }
    }

    private sealed record Result(string Title);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
