using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace NativeCodegenFixture;

public partial class View : Window
{
    public int Clicks { get; private set; }
    public object? AddedItem { get; private set; }
    public View() => InitializeComponent();
    void OnSelection(object sender, SelectionChangedEventArgs args) => AddedItem = args.AddedItems.Count == 0 ? null : args.AddedItems[0];
    void OnChange(object sender, RoutedEventArgs args)
    {
        Clicks++;
        Models.Items.Add(new ComboBoxItem { Content = "Generated", Tag = "generated" });
        Models.SelectedIndex = Models.Items.Count - 1;
        ((LinearGradientBrush)Surface.Background).GradientStops[0].Color = Colors.Red;
        ((DropShadowEffect)Surface.Effect).ShadowDepth = 9;
        ((BlurEffect)Blurred.Effect).Radius = 5;
    }
}
