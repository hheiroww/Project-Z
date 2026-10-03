namespace ProjectZ.WpfCompatibility;

/// <summary>Shared import/runtime contract. A missing entry is a diagnostic, never a stub.</summary>
public static class Capabilities
{
    public const string Prefix = "ProjectZ.WpfCompatibility";
    public static readonly HashSet<string> Controls = new(StringComparer.Ordinal)
    { "Separator", "Slider", "PasswordBox", "RadioButton", "Label", "WrapPanel", "ScrollViewer", "Expander", "GroupBox", "Window", "UserControl", "Page", "Grid", "StackPanel", "Canvas", "Border", "Button", "TextBlock", "TextBox", "CheckBox", "ProgressBar", "ListBox", "ComboBox" };
    public static readonly HashSet<string> Events = new(StringComparer.Ordinal)
    { "PasswordChanged", "ValueChanged", "Expanded", "Collapsed", "Loaded", "Unloaded", "SizeChanged", "Click", "MouseEnter", "MouseLeave", "MouseMove", "MouseLeftButtonDown", "MouseLeftButtonUp", "PreviewMouseLeftButtonDown", "PreviewMouseLeftButtonUp", "MouseWheel", "PreviewMouseWheel", "KeyDown", "KeyUp", "PreviewKeyDown", "PreviewKeyUp", "GotFocus", "LostFocus", "TextChanged", "SelectionChanged", "Checked", "Unchecked", "Indeterminate", "Closing", "Closed" };
    public static readonly HashSet<string> Properties = new(StringComparer.Ordinal)
    { "Password", "GroupName", "Header", "IsExpanded", "ItemWidth", "ItemHeight", "Name", "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight", "Margin", "Padding", "HorizontalAlignment", "VerticalAlignment", "Visibility", "Opacity", "IsEnabled", "IsHitTestVisible", "Background", "Foreground", "Text", "IsReadOnly", "Content", "FontSize", "FontFamily", "Title", "IsChecked", "Minimum", "Maximum", "Value", "ItemsSource", "SelectedIndex", "SelectedItem", "DataContext", "Command", "CommandParameter", "Orientation", "Canvas.Left", "Canvas.Top", "Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan" };
    public static readonly HashSet<string> BindingOptions = new(StringComparer.Ordinal)
    { "Path", "Mode", "UpdateSourceTrigger", "ElementName", "Converter", "Source", "ConverterParameter", "TargetNullValue", "FallbackValue", "RelativeSource" };
    public static readonly HashSet<string> PropertyElements = new(StringComparer.Ordinal)
    { "Window.Resources", "UserControl.Resources", "Page.Resources", "Grid.Resources", "StackPanel.Resources", "ListBox.Resources", "ResourceDictionary", "ListBox.ItemTemplate", "DataTemplate", "Grid.RowDefinitions", "Grid.ColumnDefinitions" };
    public static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "System.Windows.Application", "System.Windows.StartupEventArgs", "System.Windows.ExitEventArgs", "System.Windows.StartupEventHandler", "System.Windows.ExitEventHandler", "System.Windows.Window", "System.Windows.FrameworkElement", "System.Windows.UIElement",
        "System.Windows.DependencyObject", "System.Windows.DependencyProperty", "System.Windows.PropertyMetadata", "System.Windows.PropertyChangedCallback", "System.Windows.DependencyPropertyChangedEventArgs",
        "System.Windows.RoutedPropertyChangedEventArgs<double>", "System.Windows.RoutedPropertyChangedEventHandler<double>",
        "System.Windows.RoutedEventArgs", "System.Windows.RoutedEventHandler", "System.Windows.SizeChangedEventArgs", "System.Windows.SizeChangedEventHandler",
        "System.Windows.Thickness", "System.Windows.Visibility", "System.Windows.HorizontalAlignment", "System.Windows.VerticalAlignment", "System.Windows.Point", "System.Windows.Size", "System.Windows.ResourceDictionary", "System.Windows.PropertyPath", "System.Windows.DataTemplate",
        "System.Windows.Threading.Dispatcher", "System.Windows.Data.Binding", "System.Windows.Data.BindingMode", "System.Windows.Data.UpdateSourceTrigger", "System.Windows.Data.BindingOperations", "System.Windows.Data.BindingExpression", "System.Windows.Data.IValueConverter", "System.Windows.Data.RelativeSource", "System.Windows.Data.RelativeSourceMode",
        "System.Windows.Input.MouseEventArgs", "System.Windows.Input.MouseButtonEventArgs", "System.Windows.Input.MouseWheelEventArgs", "System.Windows.Input.KeyEventArgs", "System.Windows.Input.Key", "System.Windows.Input.MouseButton", "System.Windows.Input.MouseButtonState",
        "System.Windows.Media.Brush", "System.Windows.Media.SolidColorBrush", "System.Windows.Media.Color", "System.Windows.Media.Colors", "System.Windows.Media.Brushes",
        "System.Windows.Controls.Control", "System.Windows.Controls.ContentControl", "System.Windows.Controls.Panel", "System.Windows.Controls.Orientation", "System.Windows.Controls.TextChangedEventArgs", "System.Windows.Controls.SelectionChangedEventArgs"
    };
    static Capabilities()
    {
        foreach (var name in Controls.Where(n => n != "Window")) Types.Add("System.Windows.Controls." + name);
    }
    public static bool IsWpfAssembly(string? name) => name is "PresentationCore" or "WindowsBase" or "System.Xaml" || name?.StartsWith("PresentationFramework", StringComparison.Ordinal) == true;
    public static string Translate(string fullName) => (fullName == "System.Windows" || fullName.StartsWith("System.Windows.", StringComparison.Ordinal)) && !fullName.StartsWith("System.Windows.Forms", StringComparison.Ordinal)
        ? Prefix + fullName["System.Windows".Length..] : fullName;
}
