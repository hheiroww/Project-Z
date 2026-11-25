using System.Collections.ObjectModel;
using ProjectZ.WpfCompatibility;
using ProjectZ.WpfCompatibility.Controls;
using ProjectZ.WpfCompatibility.Threading;

internal static class WrappedItemChecks
{
    internal static void Run() => Hosting.Run(static () => new WrappedItemApplication());

    private sealed class WrappedItemApplication : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            string file = Path.GetFullPath("artifacts/wpf-import/wrapped-item-panels.xaml");
            File.WriteAllText(file, """
                <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        Width="350" Height="190" WindowStyle="None" ShowActivated="False">
                    <StackPanel>
                        <ItemsControl Name="Chips">
                            <ItemsControl.ItemsPanel>
                                <ItemsPanelTemplate><WrapPanel ItemWidth="100" ItemHeight="30"/></ItemsPanelTemplate>
                            </ItemsControl.ItemsPanel>
                            <ItemsControl.ItemTemplate>
                                <DataTemplate><TextBlock Name="Caption" Text="{Binding Name}" Foreground="White"/></DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                        <ScrollViewer Name="Viewport" MaxHeight="60" VerticalScrollBarVisibility="Auto">
                            <ListBox Name="Cards">
                                <ListBox.ItemsPanel>
                                    <ItemsPanelTemplate><WrapPanel ItemWidth="100" ItemHeight="32"/></ItemsPanelTemplate>
                                </ListBox.ItemsPanel>
                                <ListBox.ItemTemplate>
                                    <DataTemplate><TextBlock Name="Caption" Text="{Binding Name}" Foreground="White"/></DataTemplate>
                                </ListBox.ItemTemplate>
                            </ListBox>
                        </ScrollViewer>
                    </StackPanel>
                </Window>
                """);
            var window = new Window();
            XamlLoader.Initialize(window, file);
            var chips = (ItemsControl)window.FindName("Chips")!;
            var cards = (ListBox)window.FindName("Cards")!;
            var viewport = (ScrollViewer)window.FindName("Viewport")!;
            var items = new ObservableCollection<Entry>();
            chips.ItemsSource = items;
            cards.ItemsSource = items;
            window.Show();

            int tick = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            timer.Tick += (_, _) =>
            {
                if (++tick == 2)
                {
                    Require(chips.ActualHeight == 0, "empty wrapped items do not reserve a row");
                    for (int i = 0; i < 7; i++) items.Add(new Entry($"Card {i + 1}"));
                }
                if (tick < 6) return;
                timer.Stop();

                var firstChip = chips.ItemViews[0];
                var firstCard = cards.ItemViews[0];
                var chipLabel = (TextBlock)firstChip.FindName("Caption")!;
                var cardLabel = (TextBlock)firstCard.FindName("Caption")!;
                Require(chips.ActualHeight == 90 && viewport.ActualHeight == 60 && viewport.ScrollableHeight > 0,
                    "appending wrapped items expands auto layout while respecting a bounded scroll host");
                Require(firstChip.NativeElement.Rectangle.Size == new Microsoft.Xna.Framework.Point(100, 30) &&
                    firstCard.NativeElement.Rectangle.Size == new Microsoft.Xna.Framework.Point(100, 32) &&
                    chips.ItemViews[3].NativeElement.Rectangle.Top > firstChip.NativeElement.Rectangle.Top,
                    "wrapped templates stay inside their cells and start a new row when required");
                var pixels = window.CaptureFrame();
                Require(chipLabel.Text == "Card 1" && cardLabel.Text == "Card 1" &&
                    PaintsText(chipLabel, pixels, (int)window.Width) && PaintsText(cardLabel, pixels, (int)window.Width),
                    "wrapped templates paint live bound labels inside both visible viewports");
                window.Close();
            };
            timer.Start();
        }
    }

    private sealed record Entry(string Name);

    private static bool PaintsText(TextBlock label, Microsoft.Xna.Framework.Color[] pixels, int width)
    {
        var bounds = label.NativeElement.Rectangle;
        for (int y = Math.Max(0, bounds.Top); y < Math.Min(pixels.Length / width, bounds.Bottom); y++)
            for (int x = Math.Max(0, bounds.Left); x < Math.Min(width, bounds.Right); x++)
                if (pixels[y * width + x] is { R: > 180, G: > 180, B: > 180 }) return true;
        return false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
