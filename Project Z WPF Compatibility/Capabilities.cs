namespace ProjectZ.WpfCompatibility;

/// <summary>Shared import/runtime contract. Unsupported visuals use the explicit empty-control policy; other missing contracts remain diagnostics.</summary>
public static class Capabilities
{
    public const string Prefix = "ProjectZ.WpfCompatibility";
    public static readonly IReadOnlyDictionary<string, string> TypeAliases = new Dictionary<string, string>
    {
        ["System.Windows.Media.Visual"] = Prefix + ".UIElement",
        ["System.Windows.FrameworkContentElement"] = Prefix + ".Documents.TextElement",
        ["System.Windows.Controls.Primitives.ToggleButton"] = Prefix + ".Controls.ToggleButton",
        ["System.Windows.Controls.Primitives.ButtonBase"] = Prefix + ".Controls.ButtonBase",
        ["System.Windows.Controls.Primitives.Selector"] = Prefix + ".Controls.ItemsControl",
        ["System.Windows.Controls.Primitives.UniformGrid"] = Prefix + ".Controls.UniformGrid",
        ["System.Windows.Shapes.Rectangle"] = Prefix + ".Shapes.Rectangle",
        ["System.Windows.Automation.AutomationProperties"] = Prefix + ".Automation.AutomationProperties",
        ["Microsoft.Web.WebView2.Wpf.WebView2"] = Prefix + ".Controls.WebView2",
        ["Microsoft.Web.WebView2.Wpf.WebView2CompositionControl"] = Prefix + ".Controls.WebView2CompositionControl",
        ["Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties"] = Prefix + ".Controls.CoreWebView2CreationProperties",
        ["System.Windows.IDataObject"] = "System.Windows.Forms.IDataObject",
        ["System.Windows.DataObject"] = Prefix + ".DataObject",
        ["System.ComponentModel.ICollectionView"] = Prefix + ".Data.ICollectionView",
        ["System.ComponentModel.SortDescription"] = Prefix + ".Data.SortDescription",
        ["System.ComponentModel.SortDescriptionCollection"] = Prefix + ".Data.SortDescriptionCollection",
        ["System.Windows.DataFormats"] = "System.Windows.Forms.DataFormats"
    };
    public static readonly HashSet<string> Controls = new(StringComparer.Ordinal)
    { "ContentControl", "UniformGrid", "ItemsControl", "ComboBoxItem", "ListBoxItem", "Separator", "Slider", "PasswordBox", "RadioButton", "Label", "WrapPanel", "ScrollViewer", "Expander", "GroupBox", "Window", "UserControl", "Page", "Grid", "StackPanel", "Canvas", "Border", "Button", "TextBlock", "TextBox", "CheckBox", "ProgressBar", "ListBox", "ComboBox" };
    public static readonly HashSet<string> Events = new(StringComparer.Ordinal)
    { "PasswordChanged", "ValueChanged", "Expanded", "Collapsed", "Loaded", "Unloaded", "SizeChanged", "Click", "MouseEnter", "MouseLeave", "MouseMove", "MouseLeftButtonDown", "MouseLeftButtonUp", "PreviewMouseLeftButtonDown", "PreviewMouseLeftButtonUp", "MouseWheel", "PreviewMouseWheel", "KeyDown", "KeyUp", "PreviewKeyDown", "PreviewKeyUp", "GotFocus", "LostFocus", "TextChanged", "SelectionChanged", "Checked", "Unchecked", "Indeterminate", "Closing", "Closed" };
    public static readonly HashSet<string> Properties = new(StringComparer.Ordinal)
    { "Rows", "Columns", "Tag", "DisplayMemberPath", "SelectedValuePath", "SelectedValue", "IsSelected", "IsDropDownOpen", "TextWrapping", "TextTrimming", "FontWeight", "Password", "CornerRadius", "GroupName", "BorderBrush", "BorderThickness", "Header", "IsExpanded", "ItemWidth", "ItemHeight", "Name", "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight", "Margin", "Padding", "HorizontalAlignment", "VerticalAlignment", "Visibility", "Opacity", "IsEnabled", "IsHitTestVisible", "Background", "Foreground", "Text", "IsReadOnly", "Content", "FontSize", "FontFamily", "Title", "IsChecked", "Minimum", "Maximum", "Value", "ItemsSource", "SelectedIndex", "SelectedItem", "DataContext", "Command", "CommandParameter", "Orientation", "Canvas.Left", "Canvas.Top", "Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan" };
    public static readonly HashSet<string> BindingOptions = new(StringComparer.Ordinal)
    { "Path", "Mode", "UpdateSourceTrigger", "ElementName", "Converter", "Source", "ConverterParameter", "TargetNullValue", "FallbackValue", "RelativeSource" };
    public static readonly HashSet<string> PropertyElements = new(StringComparer.Ordinal)
    { "ComboBox.Items", "ListBox.Items", "ComboBox.Resources", "Window.Resources", "UserControl.Resources", "Page.Resources", "Grid.Resources", "StackPanel.Resources", "ListBox.Resources", "ResourceDictionary", "ListBox.ItemTemplate", "DataTemplate", "Grid.RowDefinitions", "Grid.ColumnDefinitions" };
    public static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "System.Windows.Application", "System.Windows.StartupEventArgs", "System.Windows.ExitEventArgs", "System.Windows.StartupEventHandler", "System.Windows.ExitEventHandler", "System.Windows.Window", "System.Windows.FrameworkElement", "System.Windows.UIElement",
        "System.Windows.DependencyObject", "System.Windows.DependencyProperty", "System.Windows.PropertyMetadata", "System.Windows.PropertyChangedCallback", "System.Windows.DependencyPropertyChangedEventArgs",
        "System.Windows.RoutedPropertyChangedEventArgs<double>", "System.Windows.RoutedPropertyChangedEventHandler<double>",
        "System.Windows.RoutedEventArgs", "System.Windows.RoutedEventHandler", "System.Windows.SizeChangedEventArgs", "System.Windows.SizeChangedEventHandler",
        "System.Windows.Thickness", "System.Windows.CornerRadius", "System.Windows.Visibility", "System.Windows.HorizontalAlignment", "System.Windows.VerticalAlignment", "System.Windows.Point", "System.Windows.Size", "System.Windows.ResourceDictionary", "System.Windows.PropertyPath", "System.Windows.DataTemplate",
        "System.Windows.GridLength", "System.Windows.GridUnitType", "System.Windows.Controls.RowDefinition", "System.Windows.Controls.ColumnDefinition", "System.Windows.Controls.RowDefinitionCollection", "System.Windows.Controls.ColumnDefinitionCollection",
        "System.Windows.ShutdownMode",
        "System.Windows.WindowCollection",
        "System.Windows.TextWrapping", "System.Windows.TextTrimming",
        "System.Windows.FontWeight", "System.Windows.FontWeights", "System.Windows.Media.FontFamily",
        "System.Windows.Threading.Dispatcher", "System.Windows.Threading.DispatcherTimer", "System.Windows.Threading.DispatcherPriority", "System.Windows.Threading.DispatcherOperation", "System.Windows.Threading.DispatcherOperation<TResult>", "System.Windows.Threading.DispatcherOperationStatus", "System.Windows.Threading.DispatcherUnhandledExceptionEventArgs", "System.Windows.Threading.DispatcherUnhandledExceptionEventHandler", "System.Windows.Data.Binding", "System.Windows.Data.BindingMode", "System.Windows.Data.UpdateSourceTrigger", "System.Windows.Data.BindingOperations", "System.Windows.Data.BindingExpression", "System.Windows.Data.IValueConverter", "System.Windows.Data.RelativeSource", "System.Windows.Data.RelativeSourceMode",
        "System.Windows.Input.MouseEventArgs", "System.Windows.Input.MouseButtonEventArgs", "System.Windows.Input.MouseWheelEventArgs", "System.Windows.Input.KeyEventArgs", "System.Windows.Input.Key", "System.Windows.Input.MouseButton", "System.Windows.Input.MouseButtonState",
        "System.Windows.Media.Brush", "System.Windows.Media.SolidColorBrush", "System.Windows.Media.Color", "System.Windows.Media.Colors", "System.Windows.Media.Brushes",
        "System.Windows.Controls.ItemsControl", "System.Windows.Controls.ItemCollection", "System.Windows.Controls.Control", "System.Windows.Controls.ContentControl", "System.Windows.Controls.Panel", "System.Windows.Controls.Orientation", "System.Windows.Controls.TextChangedEventArgs", "System.Windows.Controls.SelectionChangedEventArgs"
    };
    static Capabilities()
    {
        Controls.UnionWith(["TabControl", "TabItem", "Menu", "ToggleButton", "Rectangle"]);
        Types.UnionWith(["System.Windows.Media.TextOptions", "System.Windows.Media.TextFormattingMode", "System.Windows.Media.TextRenderingMode", "System.Windows.Media.RenderOptions", "System.Windows.Media.BitmapScalingMode", "System.Windows.Interop.RenderMode", "System.Windows.Media.StreamGeometry", "System.Windows.Media.StreamGeometryContext", "System.Windows.Documents.AdornerLayer"]);
        Properties.UnionWith(["IsEditable", "IsTextSearchEnabled", "MaxDropDownHeight", "TextSearch.TextPath", "IsTabStop", "LargeChange", "SmallChange", "Placement", "RadiusX", "RadiusY"]);
        Events.Add("MouseDoubleClick");
        Types.Add("System.Windows.Controls.SelectionMode");
        Types.UnionWith(["System.Windows.TextDataFormat", "System.Windows.Media.Pen", "System.Windows.Media.PenLineCap", "System.Windows.Media.PenLineJoin"]);
        Types.UnionWith(["System.Windows.DataObjectPastingEventArgs", "System.Windows.DataObjectEventArgs", "System.Windows.DataObjectPastingEventHandler"]);
        Types.Add("System.Windows.Data.CollectionViewSource");
        Properties.UnionWith(["ScrollViewer.HorizontalScrollBarVisibility", "ScrollViewer.VerticalScrollBarVisibility", "ScrollViewer.CanContentScroll", "CanContentScroll", "CaretBrush", "SelectionBrush", "SelectionOpacity"]);
        Types.UnionWith(["System.Windows.Media.TransformCollection","System.Windows.Media.ColorConverter"]);
        Types.UnionWith(["System.Windows.Documents.TableRowCollection","System.Windows.Documents.TableCellCollection","System.Windows.Documents.TableColumnCollection","System.Windows.Documents.TableRowGroupCollection"]);
        Types.Add("System.Windows.RoutedPropertyChangedEventArgs<object>");
        Properties.Add("SelectionMode");
        Properties.UnionWith(["TickFrequency","IsSnapToTickEnabled","LineHeight"]);
        Properties.Add("SnapsToDevicePixels");
        Properties.UnionWith(["Panel.ZIndex","IsHighlighted"]);
        Types.Add("System.Windows.DependencyPropertyChangedEventHandler");
        Events.Add("IsVisibleChanged");
        BindingOptions.Add("StringFormat");
        Properties.UnionWith(["IsIndeterminate", "PasswordChar"]);
        Types.UnionWith(["System.Windows.FrameworkPropertyMetadata", "System.Windows.FrameworkPropertyMetadataOptions"]);
        Types.UnionWith(["System.Windows.Style", "System.Windows.Setter", "System.Windows.SetterBaseCollection"]);
        Types.UnionWith(["System.Windows.Freezable", "System.Windows.Duration"]);
        Types.UnionWith(["System.Windows.Int32Rect", "System.Windows.Media.PixelFormat", "System.Windows.Media.PixelFormats", "System.Windows.Media.TileBrush", "System.Windows.Media.VisualBrush", "System.Windows.Media.ImageBrush", "System.Windows.Media.DrawingVisual", "System.Windows.Media.DrawingContext"]);
        foreach(var name in new[]{"BitmapSource","WriteableBitmap","CroppedBitmap","FormatConvertedBitmap","BitmapFrame","BitmapEncoder","PngBitmapEncoder","JpegBitmapEncoder" ,"RenderTargetBitmap"}) Types.Add("System.Windows.Media.Imaging."+name);
        Types.UnionWith(["System.Windows.PresentationSource", "System.Windows.DpiScale", "System.Windows.Media.VisualTreeHelper", "System.Windows.Media.Matrix", "System.Windows.Media.CompositionTarget", "System.Windows.Media.RenderingEventArgs", "System.Windows.Interop.HwndSource", "System.Windows.Interop.HwndTarget", "System.Windows.Interop.HwndSourceHook"]);
        Types.UnionWith(["System.Windows.MessageBox", "System.Windows.MessageBoxResult", "System.Windows.MessageBoxButton", "System.Windows.MessageBoxImage", "System.Windows.MessageBoxOptions"]);
        Types.UnionWith(["System.Windows.SystemParameters", "System.Windows.SystemCommands", "System.Windows.WindowState", "System.Windows.WindowStyle", "System.Windows.WindowStartupLocation", "System.Windows.ResizeMode", "System.Windows.SizeToContent", "System.Windows.Interop.WindowInteropHelper"]);
        Properties.UnionWith(["WindowState", "WindowStyle", "WindowStartupLocation", "ResizeMode", "SizeToContent", "ShowInTaskbar", "Topmost", "ShowActivated", "AllowsTransparency", "Left", "Top", "Icon"]);
        Events.UnionWith(["SourceInitialized", "StateChanged", "Activated", "Deactivated", "LocationChanged"]);
        foreach (var name in new[] { "Animatable", "Timeline", "TimelineGroup", "TimelineCollection", "AnimationTimeline", "DoubleAnimation", "ThicknessAnimation", "ColorAnimation", "ColorAnimationUsingKeyFrames", "ColorKeyFrameCollection", "LinearColorKeyFrame", "KeyTime", "Storyboard", "RepeatBehavior", "HandoffBehavior", "FillBehavior", "IEasingFunction", "EasingFunctionBase", "EasingMode", "SineEase", "CubicEase", "QuadraticEase", "PowerEase" }) Types.Add("System.Windows.Media.Animation." + name);
        Controls.UnionWith(["WebView2", "WebView2CompositionControl"]);
        Properties.UnionWith(["DefaultBackgroundColor", "ZoomFactor", "AllowExternalDrop", "ItemTemplate"]);
        Events.UnionWith(["CoreWebView2InitializationCompleted", "NavigationStarting", "NavigationCompleted", "ContentLoading", "SourceChanged", "WebMessageReceived"]);
        Events.UnionWith(["PreviewMouseMove", "PreviewMouseDown", "MouseDown", "PreviewMouseUp", "MouseUp", "PreviewMouseRightButtonDown", "MouseRightButtonDown", "PreviewMouseRightButtonUp", "MouseRightButtonUp", "PreviewTextInput", "TextInput", "GotMouseCapture", "LostMouseCapture", "Opened"]);
        Types.UnionWith(["System.Windows.IInputElement", "System.Windows.RoutingStrategy", "System.Windows.EventManager", "System.Windows.Input.InputEventArgs", "System.Windows.Input.Mouse", "System.Windows.Input.MouseDevice", "System.Windows.Input.Keyboard", "System.Windows.Input.KeyboardDevice", "System.Windows.Input.ModifierKeys", "System.Windows.Input.KeyInterop", "System.Windows.Input.FocusNavigationDirection", "System.Windows.Input.TraversalRequest", "System.Windows.Input.InputManager", "System.Windows.Input.TextComposition", "System.Windows.Input.TextCompositionEventArgs", "System.Windows.Input.TextCompositionManager", "System.Windows.Input.MouseEventHandler", "System.Windows.Input.MouseButtonEventHandler", "System.Windows.Input.MouseWheelEventHandler", "System.Windows.Input.KeyEventHandler", "System.Windows.Input.TextCompositionEventHandler"]);
        Properties.Add("Focusable");
        Controls.UnionWith(["DockPanel", "Decorator", "Image", "ListView", "ListViewItem", "MenuItem", "ContextMenu", "RichTextBox", "Polygon", "EmptyControl", "ToolTip"]);
        Properties.UnionWith(["DockPanel.Dock", "LastChildFill", "Source", "Stretch", "Fill", "Points", "HorizontalContentAlignment", "VerticalContentAlignment", "IsThreeState", "IsDefault", "IsCancel", "TextDecorations", "RenderTransformOrigin", "RenderTransform", "Clip", "ToolTip", "Cursor", "AcceptsReturn", "IsReadOnlyCaretVisible", "IsDocumentEnabled", "IsCheckable", "HorizontalScrollBarVisibility", "VerticalScrollBarVisibility"]);
        Types.UnionWith(["System.Windows.Input.Cursor", "System.Windows.Input.Cursors", "System.Windows.Input.ApplicationCommands"]);
        Types.UnionWith(["System.Windows.Media.ScaleTransform", "System.Windows.Media.TranslateTransform", "System.Windows.Media.TransformGroup", "System.Windows.Controls.GridView", "System.Windows.Controls.GridViewColumn", "System.Windows.Controls.GridViewColumnCollection", "System.Windows.ItemsPanelTemplate", "System.Windows.Shell.WindowChrome"]);
        Types.UnionWith(TypeAliases.Keys);
        Types.UnionWith(["System.Windows.TextAlignment", "System.Windows.TextMarkerStyle", "System.Windows.Navigation.RequestNavigateEventArgs"]);
        foreach (var documentType in new[] { "TextElement", "TextElementCollection<T>", "Block", "Inline", "Run", "LineBreak", "Span", "Bold", "Italic", "Underline", "Hyperlink", "Paragraph", "Section", "ListItem", "List", "Table", "TableColumn", "TableRowGroup", "TableRow", "TableCell", "BlockUIContainer", "InlineUIContainer", "FlowDocument", "BlockCollection", "InlineCollection", "TextPointer", "TextRange", "TextSelection" }) Types.Add("System.Windows.Documents." + documentType);
        PropertyElements.UnionWith(["ContextMenu.Items", "MenuItem.Items", "ListView.Items", "ListView.ItemTemplate", "RichTextBox.Elements", "RichTextBox.Document", "UIElement.Clip", "UIElement.RenderTransform", "TextBlock.TextDecorations", "EmptyControl.Resources", "TabItem.Header", "TabItem.Content", "TabControl.Items"]);
        Types.UnionWith(["System.Windows.Clipboard", "System.Windows.RoutedEvent", "System.Windows.Rect", "System.Windows.Automation.AutomationProperties", "System.Windows.Controls.Dock", "System.Windows.Controls.ScrollBarVisibility", "System.Windows.Controls.Primitives.PlacementMode", "System.Windows.Media.ImageSource", "System.Windows.Media.Stretch", "System.Windows.Media.PointCollection", "System.Windows.Media.Geometry", "System.Windows.Media.RectangleGeometry", "System.Windows.Media.Transform", "System.Windows.Media.RotateTransform", "System.Windows.Shapes.Polygon", "System.Windows.TextDecoration", "System.Windows.TextDecorationCollection", "System.Windows.TextDecorations", "System.Windows.TextDecorationLocation", "System.Windows.Media.Imaging.BitmapImage", "System.Windows.Media.Imaging.BitmapCacheOption", "System.Windows.Media.Imaging.BitmapCreateOptions", "Microsoft.Win32.OpenFileDialog", "Microsoft.Win32.SaveFileDialog", "Microsoft.Win32.OpenFolderDialog", "Microsoft.Win32.CommonDialog", "Microsoft.Win32.FileDialog"]);
        Properties.UnionWith(["Style", "Template", "Effect", "TextAlignment", "ClipToBounds", "AutomationProperties.Name", "AutomationProperties.HelpText"]);
        Types.UnionWith(["System.Windows.Media.GradientBrush", "System.Windows.Media.LinearGradientBrush", "System.Windows.Media.RadialGradientBrush", "System.Windows.Media.GradientStop", "System.Windows.Media.GradientStopCollection", "System.Windows.Media.BrushMappingMode", "System.Windows.Media.GradientSpreadMethod", "System.Windows.Media.Effects.Effect", "System.Windows.Media.Effects.BlurEffect", "System.Windows.Media.Effects.DropShadowEffect"]);
        foreach (var name in Controls.Where(n => n != "Window" && n != "Polygon")) Types.Add("System.Windows.Controls." + name);
    }
    public static bool IsWpfAssembly(string? name) => name is "PresentationCore" or "WindowsBase" or "System.Xaml" || name?.StartsWith("PresentationFramework", StringComparison.Ordinal) == true;
    public static string? EventName(string name)
    {
        if (Events.Contains(name)) return name;
        var separator = name.LastIndexOf('.');
        if (separator < 0 || !Events.Contains(name[(separator + 1)..])) return null;
        var owner = name[..separator];
        return Controls.Contains(owner) || owner is "UIElement" or "FrameworkElement" or "TextBoxBase" or "ButtonBase" or "ToggleButton" or "Selector" or "RangeBase" ? name[(separator + 1)..] : null;
    }
    public static bool IsVisualObject(System.Xml.Linq.XElement element) => Controls.Contains(element.Name.LocalName) && !element.Ancestors().Any(p => p.Name.LocalName is "DataTemplate" or "ControlTemplate" or "ItemsPanelTemplate" or "Style" or "ResourceDictionary" || p.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal));
    public static string Translate(string fullName) => TypeAliases.TryGetValue(fullName, out var alias) ? alias : fullName == "System.Windows.Automation" ? fullName : fullName == "Microsoft.Web.WebView2.Wpf" ? Prefix + ".Controls" : fullName is "Microsoft.Win32.OpenFileDialog" or "Microsoft.Win32.SaveFileDialog" or "Microsoft.Win32.OpenFolderDialog" or "Microsoft.Win32.FileDialog" ? Prefix + ".Dialogs." + fullName["Microsoft.Win32.".Length..] : fullName == "Microsoft.Win32.CommonDialog" ? Prefix + ".Dialogs.FileDialog" : (fullName == "System.Windows" || fullName.StartsWith("System.Windows.", StringComparison.Ordinal)) && !fullName.StartsWith("System.Windows.Forms", StringComparison.Ordinal)
        ? Prefix + fullName["System.Windows".Length..] : fullName;
}
