#nullable enable
using System.Diagnostics;
using ProjectZ.WpfCompatibility;
using ProjectZ.WpfCompatibility.Controls;

internal static class TabNavigation
{
    public static void Verify(Action<bool, string> require)
    {
        const int tabCount = 192;
        const int selections = 500;
        int realized = 0;
        using var tabs = new TabControl
        {
            ItemTemplate = new DataTemplate(() =>
            {
                realized++;
                var body = new StackPanel();
                for (int row = 0; row < 24; row++) body.Children.Add(new TextBlock { Text = $"Panel row {row}" });
                return body;
            })
        };

        var elapsed = Stopwatch.StartNew();
        tabs.ItemsSource = Enumerable.Range(0, tabCount);
        double setupMs = elapsed.Elapsed.TotalMilliseconds;
        require(tabs.Items.Count == tabCount && tabs.SelectedIndex == 0 && realized == 1,
            "large tab sources create only the selected content template");
        var first = tabs.Items[0].Content;
        tabs.SelectedIndex = 1;
        var second = tabs.Items[1].Content;
        tabs.SelectedIndex = 0;
        require(realized == 2 && ReferenceEquals(first, tabs.Items[0].Content),
            "returning to a tab retains its previously realized content");

        int selectionNotifications = 0;
        foreach (var tab in tabs.Items)
            tab.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(TabItem.IsSelected)) selectionNotifications++; };
        elapsed.Restart();
        for (int change = 0; change < selections; change++) tabs.SelectedIndex = 1 - tabs.SelectedIndex;
        double selectionMs = elapsed.Elapsed.TotalMilliseconds;
        require(selectionNotifications == 2 * selections && realized == 2
            && ReferenceEquals(first, tabs.Items[0].Content) && ReferenceEquals(second, tabs.Items[1].Content),
            "tab selection only notifies the departing and arriving tabs and reuses their visual content");
        Console.WriteLine($"TAB_NAVIGATION tabs={tabCount} realized={realized} selections={selections} setup_ms={setupMs:F2} selection_ms={selectionMs:F2}");

        object? active = tabs.SelectedItem;
        tabs.Items.Insert(0, new TabItem { Header = "Inserted" });
        require(ReferenceEquals(active, tabs.SelectedItem), "inserting another tab preserves the selected content identity");
    }
}
