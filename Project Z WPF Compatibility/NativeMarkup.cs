using System.Xml.Linq;

namespace ProjectZ.WpfCompatibility;

/// <summary>Markup implemented by the shared native parser, including the HeirowSnap appearance path.</summary>
public static class NativeMarkup
{
    static readonly Dictionary<string, string[]> Attributes = new(StringComparer.Ordinal)
    {
        ["Style"] = ["TargetType", "BasedOn"],
        ["Setter"] = ["Property", "Value", "TargetName"],
        ["ControlTemplate"] = ["TargetType"],
        ["Trigger"] = ["Property", "Value", "SourceName"],
        ["ResourceDictionary"] = ["Source"],
        ["LinearGradientBrush"] = ["StartPoint", "EndPoint", "Opacity", "MappingMode", "SpreadMethod"],
        ["RadialGradientBrush"] = ["Center", "GradientOrigin", "RadiusX", "RadiusY", "Opacity", "MappingMode", "SpreadMethod"],
        ["GradientStop"] = ["Offset", "Color"],
        ["ImageBrush"] = ["ImageSource", "Viewbox", "ViewboxUnits", "Stretch", "Opacity"],
        ["BlurEffect"] = ["Radius"],
        ["DropShadowEffect"] = ["BlurRadius", "ShadowDepth", "Direction", "Opacity", "Color"]
        , ["RotateTransform"] = ["Angle", "CenterX", "CenterY"]
        , ["RectangleGeometry"] = ["Rect", "RadiusX", "RadiusY"]
        , ["TextDecoration"] = ["Location"]
        , ["TextDecorationCollection"] = []
        , ["ScaleTransform"] = ["ScaleX", "ScaleY", "CenterX", "CenterY"]
        , ["TranslateTransform"] = ["X", "Y"]
        , ["TransformGroup"] = []
        , ["GridView"] = ["AllowsColumnReorder"]
        , ["GridViewColumn"] = ["Header", "Width", "DisplayMemberBinding", "CellTemplate"]
        , ["ItemsPanelTemplate"] = []
        , ["DataTrigger"] = ["Binding", "Value"]
        , ["EventTrigger"] = ["RoutedEvent", "SourceName"]
        , ["Storyboard"] = ["Duration", "BeginTime", "RepeatBehavior", "AutoReverse", "FillBehavior", "SpeedRatio", "Storyboard.TargetName", "Storyboard.TargetProperty"]
        , ["BeginStoryboard"] = ["Storyboard", "HandoffBehavior", "IsControllable"]
        , ["StopStoryboard"] = ["BeginStoryboardName"]
        , ["DoubleAnimation"] = ["From", "To", "By", "Duration", "BeginTime", "RepeatBehavior", "AutoReverse", "FillBehavior", "SpeedRatio", "Storyboard.TargetName", "Storyboard.TargetProperty"]
        , ["ColorAnimation"] = ["From", "To", "Duration", "BeginTime", "RepeatBehavior", "AutoReverse", "FillBehavior", "SpeedRatio", "Storyboard.TargetName", "Storyboard.TargetProperty"]
        , ["WindowChrome"] = ["CaptionHeight", "ResizeBorderThickness", "GlassFrameThickness", "CornerRadius", "UseAeroCaptionButtons", "NonClientFrameEdges"]
    };
    static NativeMarkup()
    {
        Attributes["CoreWebView2CreationProperties"] = ["BrowserExecutableFolder", "UserDataFolder", "Language", "AdditionalBrowserArguments", "ProfileName", "IsInPrivateModeEnabled"];
        PropertyNodes.UnionWith(["WebView2.CreationProperties", "WebView2CompositionControl.CreationProperties"]);
        var text = new[] { "Foreground", "Background", "FontFamily", "FontSize", "FontWeight", "TextDecorations" };
        foreach (var name in new[] { "FlowDocument", "Paragraph", "Section", "List", "ListItem", "Table", "TableColumn", "TableRowGroup", "TableRow", "TableCell", "BlockUIContainer", "InlineUIContainer", "Run", "Span", "Bold", "Italic", "Underline", "Hyperlink", "LineBreak" })
            Attributes[name] = text.Concat(name switch
            {
                "FlowDocument" => ["PagePadding", "ColumnGap", "LineHeight", "TextAlignment"],
                "Run" => ["Text"], "Hyperlink" => ["NavigateUri", "ToolTip", "RequestNavigate"],
                "List" => ["Margin", "Padding", "MarkerStyle"],
                "TableColumn" => ["Width"],
                "TableCell" => ["BorderBrush", "BorderThickness", "Padding", "TextAlignment"],
                "Paragraph" or "Section" or "Table" or "BlockUIContainer" => ["Margin", "Padding", "BorderBrush", "BorderThickness", "LineHeight", "TextAlignment"],
                _ => Array.Empty<string>()
            }).ToArray();
    }
    static readonly HashSet<string> PropertyNodes = new(StringComparer.Ordinal)
    { "Setter.Value", "Style.Resources", "Style.Triggers", "ControlTemplate.Triggers", "DataTemplate.Triggers", "DataTemplate.Resources", "ResourceDictionary.MergedDictionaries", "Trigger.EnterActions", "Trigger.ExitActions", "DataTrigger.EnterActions", "DataTrigger.ExitActions", "EventTrigger.Actions", "GridView.Columns", "GridViewColumn.CellTemplate", "GridViewColumn.HeaderTemplate", "TransformGroup.Children", "WindowChrome.WindowChrome", "GradientBrush.GradientStops", "LinearGradientBrush.GradientStops", "RadialGradientBrush.GradientStops" };
    public static bool IsDeclaration(XElement element) => Attributes.ContainsKey(element.Name.LocalName) && (element.Name.NamespaceName == "http://schemas.microsoft.com/winfx/2006/xaml/presentation" || element.Name.LocalName == "WindowChrome" && element.Name.NamespaceName.StartsWith("clr-namespace:System.Windows.Shell"));
    public static bool IsPropertyElement(string name)
    {
        if (PropertyNodes.Contains(name)) return true;
        var parts = name.Split('.');
        if (parts.Length == 2 && Attributes.ContainsKey(parts[0]) && parts[1] is "Blocks" or "Inlines" or "ListItems" or "Columns" or "RowGroups" or "Rows" or "Cells") return true;
        return parts.Length == 2 && Capabilities.Controls.Contains(parts[0]) && parts[1] is "Background" or "Foreground" or "Fill" or "Stroke" or "BorderBrush" or "Effect" or "RenderTransform" or "Clip" or "TextDecorations" or "Template" or "Style" or "Triggers" or "ToolTip" or "ContextMenu" or "ItemTemplate" or "ItemContainerStyle" or "ItemsPanel" or "View" or "Document";
    }
    static string TargetType(XElement? declaration) => ((string?)declaration?.Attribute("TargetType") ?? "").Replace("{x:Type ", "").TrimEnd('}');
    static XElement? Surface(XElement template) => template.Elements().FirstOrDefault(n => !n.Name.LocalName.Contains('.') && n.Name.LocalName == "Border");
    static bool IsSurfaceTemplate(XElement template)
    {
        if (!Capabilities.Controls.Contains(TargetType(template))) return false;
        var roots = template.Elements().Where(n => !n.Name.LocalName.Contains('.')).ToArray();
        if(roots.Length==1 && roots[0].Name.LocalName=="ScrollViewer" && TargetType(template) is "TextBox" or "PasswordBox" && !roots[0].Elements().Any(n=>!n.Name.LocalName.Contains('.'))) return true;
        if (roots.Length != 1 || roots[0].Name.LocalName != "Border") return false;
        return roots[0].Elements().Where(n => !n.Name.LocalName.Contains('.')).All(n => n.Name.LocalName == "EmptyControl" || n.Name.LocalName=="ScrollViewer" && TargetType(template) is "TextBox" or "PasswordBox" or "ListBox" or "ListView" && n.Descendants().Where(c=>!c.Name.LocalName.Contains('.')).All(c=>c.Name.LocalName=="EmptyControl"));
    }
    public static IEnumerable<(XObject Node, string Message)> Validate(XElement element)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var attr in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
        {
            if (attr.Name == x + "Key" || attr.Name == x + "Name" && element.Name.LocalName != "Style") continue;
            if (!Attributes[element.Name.LocalName].Contains(attr.Name.LocalName)) yield return (attr, "Unsupported native markup property " + element.Name.LocalName + "." + attr.Name.LocalName);
            if (attr.Value.StartsWith('{') && attr.Value!="{x:Null}" && !((element.Name.LocalName is "Style" or "ControlTemplate") && attr.Name.LocalName == "TargetType" && attr.Value.StartsWith("{x:Type ")) && !attr.Value.StartsWith("{StaticResource ") && !attr.Value.StartsWith("{Binding"))
                yield return (attr, "Unsupported native markup expression " + attr.Value);
        }
        if (element.Name.LocalName == "Style")
        {
            var type = ((string?)element.Attribute("TargetType") ?? "").Replace("{x:Type ", "").TrimEnd('}');
            if (!Capabilities.Controls.Contains(type)) yield return (element, "Unsupported native style TargetType " + type);
            foreach (var child in element.Elements().Where(c => c.Name.LocalName is not ("Setter" or "Style.Resources" or "Style.Triggers"))) yield return (child, "Unsupported native style feature " + child.Name.LocalName);
        }
        if (element.Name.LocalName is "DataTrigger" or "EventTrigger")
        {
            // Named parts of complex control templates remain guarded by template validation.
            // Ordinary data and event triggers are attached by the compatibility loader.
            foreach (var child in element.Elements().Where(n => n.Name.LocalName is not ("Setter" or "BeginStoryboard" or "StopStoryboard") && !IsPropertyElement(n.Name.LocalName)))
                yield return (child, "Unsupported native trigger action " + child.Name.LocalName);
        }
        if (element.Name.LocalName is "DoubleAnimation" or "ColorAnimation")
        {
            if (element.Attribute("To") == null && element.Attribute("By") == null) yield return (element, "Native animation requires To or By.");
        }
        if (element.Name.LocalName is "Storyboard" or "DoubleAnimation" or "ColorAnimation")
        {
            if ((string?)element.Attribute("RepeatBehavior") is { } repeat && repeat is not ("Forever" or "1x")) yield return (element, "Native animation repeat counts currently support 1x or Forever.");
            if ((string?)element.Attribute("FillBehavior") is { } fill && fill is not ("HoldEnd" or "Stop")) yield return (element, "Unsupported native animation fill behavior " + fill);
        }
        if (element.Name.LocalName == "ItemsPanelTemplate" && (element.Elements().Count() != 1 || element.Elements().Single().Name.LocalName is not ("StackPanel" or "WrapPanel" or "Grid")))
            yield return (element, "Native items panels require a StackPanel, WrapPanel, or Grid.");
        if (element.Name.LocalName == "ControlTemplate")
        {
            if (!IsSurfaceTemplate(element)) yield return (element, "Native control templates require a supported target and one Border surface around blank content presentation.");
            if (Surface(element) is { } surface)
            {
                foreach (var attribute in surface.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name != x + "Name" && a.Name.LocalName is not ("Name" or "Background" or "BorderBrush" or "BorderThickness" or "Padding" or "CornerRadius" or "SnapsToDevicePixels")))
                    yield return (attribute, "Unsupported projected template surface property " + attribute.Name.LocalName);
                foreach (var property in surface.Elements().Where(n => n.Name.LocalName.Contains('.') && n.Name.LocalName != "Border.Effect"))
                    yield return (property, "Unsupported projected template surface feature " + property.Name.LocalName);
            }
            foreach (var child in element.Elements().Where(n => n.Name.LocalName.Contains('.') && n.Name.LocalName != "ControlTemplate.Triggers")) yield return (child, "Unsupported native template feature " + child.Name.LocalName);
        }
        if (element.Name.LocalName == "Trigger")
        {
            var template = element.Ancestors().FirstOrDefault(n => n.Name.LocalName == "ControlTemplate");
            var style = element.Ancestors().FirstOrDefault(n => n.Name.LocalName == "Style");
            var state=(string?)element.Attribute("Property")??"";
            if(state=="IsChecked" && TargetType(template??style) is not ("CheckBox" or "RadioButton" or "MenuItem" or "ToggleButton" or "EmptyControl")) yield return (element,"Unsupported native trigger state IsChecked on "+TargetType(template??style));
            if(!Capabilities.Properties.Contains(state) && state is not ("IsMouseOver" or "IsPressed" or "IsKeyboardFocusWithin" or "IsKeyboardFocused")) yield return (element,"Unsupported native trigger state "+state);
            foreach (var child in element.Elements())
                if (child.Name.LocalName != "Setter" && !IsPropertyElement(child.Name.LocalName)) yield return (child,"Unsupported native trigger action "+child.Name.LocalName);
        }
        if (element.Name.LocalName == "Setter")
        {
            var property = (string?)element.Attribute("Property") ?? "";
            var style = element.Ancestors().FirstOrDefault(p => p.Name.LocalName == "Style");
            var type = ((string?)style?.Attribute("TargetType") ?? "").Replace("{x:Type ", "").TrimEnd('}');
            if (element.Attribute("TargetName") is { } target)
            {
                var template = element.Ancestors().FirstOrDefault(n => n.Name.LocalName == "ControlTemplate");
                var surface = template == null ? null : Surface(template);
                var dataTrigger = element.Ancestors().FirstOrDefault(n => n.Name.LocalName == "DataTrigger");
                var dataTemplate = element.Ancestors().FirstOrDefault(n => n.Name.LocalName == "DataTemplate");
                bool dataTarget = dataTrigger != null && (dataTemplate ?? element.Document?.Root)?.DescendantsAndSelf().Any(n => (string?)n.Attribute(x + "Name") == target.Value || (string?)n.Attribute("Name") == target.Value) == true;
                if (!dataTarget && (template == null || !IsSurfaceTemplate(template) || !element.Ancestors().Any(n => n.Name.LocalName == "Trigger") || target.Value != ((string?)surface?.Attribute(x + "Name") ?? (string?)surface?.Attribute("Name"))))
                    yield return (target, "Native setter TargetName must identify the projected template Border surface.");
            }
            if ((!Capabilities.Properties.Contains(property) && property != "Template") || property == "Style") yield return (element, "Unsupported native style setter " + property);
            else if (property != "Template" && Capabilities.Controls.Contains(type))
            {
                var probe = new XElement(element.Name.Namespace + type, new XAttribute(property, (string?)element.Attribute("Value") ?? "{StaticResource __setter_value}"));
                foreach (var issue in XamlSupport.Validate(new XDocument(probe))) yield return (element, issue.Message);
            }
            if (!element.HasAttributes || element.Attribute("Value") == null && element.Element(element.Name.Namespace + "Setter.Value")?.Elements().Count() != 1) yield return (element, "A native setter requires Value or one Setter.Value object.");
            if (element.Element(element.Name.Namespace + "Setter.Value")?.Elements().FirstOrDefault() is { } value)
            {
                bool supported = property switch
                {
                    "Background" => value.Name.LocalName is "SolidColorBrush" or "LinearGradientBrush" or "RadialGradientBrush",
                    "Effect" => value.Name.LocalName is "BlurEffect" or "DropShadowEffect",
                    "RenderTransform" => value.Name.LocalName == "RotateTransform",
                    "Clip" => value.Name.LocalName == "RectangleGeometry",
                    "Template" => value.Name.LocalName == "ControlTemplate" && IsSurfaceTemplate(value),
                    _ => false
                };
                if (!supported) yield return (value, "Unsupported native setter value for " + property + ": " + value.Name.LocalName);
            }
        }
        if (element.Name.LocalName.EndsWith("GradientBrush", StringComparison.Ordinal))
        {
            foreach (var child in element.Elements().Where(c => c.Name.LocalName is not ("GradientStop" or "GradientBrush.GradientStops" or "LinearGradientBrush.GradientStops" or "RadialGradientBrush.GradientStops"))) yield return (child, "Unsupported gradient content " + child.Name.LocalName);
            var spread = (string?)element.Attribute("SpreadMethod");
            if (spread != null && spread is not ("Pad" or "Repeat" or "Reflect")) yield return (element, "Unsupported gradient spread " + spread);
            var mapping = (string?)element.Attribute("MappingMode");
            if (mapping != null && mapping is not ("Absolute" or "RelativeToBoundingBox")) yield return (element, "Unsupported gradient mapping " + mapping);
        }
    }
}
