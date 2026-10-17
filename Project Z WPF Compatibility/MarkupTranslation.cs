using System.Xml.Linq;

namespace ProjectZ.WpfCompatibility;

/// <summary>Shared policy for unsupported visual controls: retain an empty, named layout element.</summary>
public static class MarkupTranslation
{
    static readonly HashSet<string> Objects = new(StringComparer.Ordinal) { "Application", "Application.Resources", "RowDefinition", "ColumnDefinition", "SolidColorBrush", "Style", "Setter", "Trigger", "MultiTrigger", "DataTrigger", "ControlTemplate", "DataTemplate", "ResourceDictionary", "LinearGradientBrush", "RadialGradientBrush", "GradientStop", "BlurEffect", "DropShadowEffect", "RotateTransform", "RectangleGeometry", "TextDecoration", "FlowDocument", "Paragraph", "Run", "Span", "Bold", "Italic", "Underline", "LineBreak", "Section", "List", "ListItem", "Table", "TableColumn", "TableRowGroup", "TableRow", "TableCell", "BlockUIContainer", "InlineUIContainer", "Hyperlink" };
    static readonly HashSet<string> LayoutProperties = new(StringComparer.Ordinal) { "Name", "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight", "Margin", "Padding", "HorizontalAlignment", "VerticalAlignment", "Visibility", "Opacity", "IsEnabled", "IsHitTestVisible", "ClipToBounds", "AutomationProperties.Name", "AutomationProperties.HelpText", "DataContext", "Tag", "Canvas.Left", "Canvas.Top", "Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan", "DockPanel.Dock" };
    static readonly string[] NonvisualSuffixes = ["Animation", "AnimationUsingKeyFrames", "KeyFrame", "Transform", "Transform3D", "Rotation3D", "Geometry", "Brush", "Segment", "Converter", "Extension", "BitmapDecoder", "Drawing", "Ease"];
    static MarkupTranslation() => Objects.UnionWith(["Storyboard", "BeginStoryboard", "StopStoryboard", "PauseStoryboard", "ResumeStoryboard", "RemoveStoryboard", "DoubleAnimation", "ColorAnimation", "ThicknessAnimation", "ObjectAnimationUsingKeyFrames", "DoubleAnimationUsingKeyFrames", "DiscreteObjectKeyFrame", "SplineDoubleKeyFrame", "LinearDoubleKeyFrame", "EasingDoubleKeyFrame", "EventTrigger", "Condition", "MultiDataTrigger", "Binding", "MultiBinding", "PriorityBinding", "ItemsPanelTemplate", "GridView", "GridViewColumn", "ScaleTransform", "TranslateTransform", "SkewTransform", "TransformGroup", "MatrixTransform", "PathGeometry", "EllipseGeometry", "GeometryGroup", "CombinedGeometry", "StreamGeometry", "PathFigure", "PathSegmentCollection", "LineSegment", "BezierSegment", "ArcSegment", "PolyLineSegment", "Point", "Thickness", "CornerRadius", "Color", "FontFamily"]);
    public static bool IsVisualDeclaration(XElement node)
    {
        if (node.Name.LocalName.Contains('.') || Objects.Contains(node.Name.LocalName) || NonvisualSuffixes.Any(suffix => node.Name.LocalName.EndsWith(suffix, StringComparison.Ordinal)) || node.Name.LocalName is "CoreWebView2CreationProperties" or "HierarchicalDataTemplate" or "WindowChrome" or "DrawingImage" or "DrawingGroup" or "BitmapImage" or "BitmapSource" or "RenderTargetBitmap" or "WriteableBitmap" or "Pen" or "TextDecorationCollection") return false;
        if (Capabilities.IsVisualObject(node)) return true;
        // Templates are visual content even when declared in a resource dictionary.
        // Resource objects outside templates must retain their nonvisual diagnostics.
        if (node.Ancestors().Any(p => p.Name.LocalName is "DataTemplate" or "ControlTemplate")) return true;
        if (node.Ancestors().Any(p => p.Name.LocalName == "ResourceDictionary" || p.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal))) return false;
        return !node.Ancestors().Any(p => p.Name.LocalName == "Style");
    }
    public static IReadOnlyList<string> Translate(XDocument document)
    {
        var fallbacks = new List<string>();
        var emptyTypes = new HashSet<string>(StringComparer.Ordinal);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        foreach (var node in document.Descendants().Where(n => n.Name.NamespaceName.StartsWith("clr-namespace:Microsoft.Web.WebView2.Wpf", StringComparison.Ordinal)).ToArray())
            if (node.Name.LocalName is "WebView2" or "WebView2CompositionControl" or "CoreWebView2CreationProperties" or "WebView2.CreationProperties" or "WebView2CompositionControl.CreationProperties") node.Name = presentation + node.Name.LocalName;
        foreach (var node in document.Descendants().Where(IsVisualDeclaration).ToArray())
        {
            if (node.Document == null) continue;
            if (node.Name.NamespaceName.StartsWith("clr-namespace:") && !node.Name.NamespaceName.StartsWith("clr-namespace:System.Windows"))
            {
                var parts = node.Name.NamespaceName[14..].Split(';');
                node.SetAttributeValue(XName.Get("Type", "urn:projectz:import"), parts[0] + "." + node.Name.LocalName);
                var assembly = parts.FirstOrDefault(part => part.StartsWith("assembly="));
                if (assembly != null) node.SetAttributeValue(XName.Get("Assembly", "urn:projectz:import"), assembly[9..]);
                node.Name = presentation + "UserControl";
                continue;
            }
            if (Capabilities.Controls.Contains(node.Name.LocalName) && node.Name.Namespace == presentation) continue;
            fallbacks.Add(node.Name.ToString());
            var originalName = node.Name.LocalName;
            emptyTypes.Add(originalName);
            node.Name = presentation + "EmptyControl";
            foreach (var attribute in node.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.NamespaceName.Length == 0 && !LayoutProperties.Contains(a.Name.LocalName)).ToArray()) attribute.Remove();
            foreach (var attribute in node.Attributes().Where(a => Capabilities.Events.Contains(a.Name.LocalName)).ToArray()) attribute.Remove();
            // A blank control has no visual template or item view. Preserve named content children
            // so generated codebehind references still resolve, while the native parent hides them.
            foreach (var property in node.Elements().Where(p => p.Name.LocalName.Contains('.')).ToArray())
            {
                if (property.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal)) { property.Name = presentation + "EmptyControl.Resources"; continue; }
                if (property.Name.LocalName.EndsWith(".RenderTransform", StringComparison.Ordinal) || property.Name.LocalName.EndsWith(".Clip", StringComparison.Ordinal)) { property.Name = presentation + "EmptyControl." + property.Name.LocalName[(property.Name.LocalName.LastIndexOf('.') + 1)..]; continue; }
                var children = property.Descendants().Where(IsVisualDeclaration).Where(child => !child.Ancestors().TakeWhile(parent => parent != property).Any(parent => IsVisualDeclaration(parent) || parent.Name.LocalName is "Style" or "ControlTemplate" or "DataTemplate" or "ResourceDictionary")).ToArray();
                foreach (var child in children) { child.Remove(); property.AddBeforeSelf(child); }
                property.Remove();
            }
        }
        foreach (var style in document.Descendants().Where(p => p.Name.LocalName == "Style").ToArray())
        {
            var type = ((string?)style.Attribute("TargetType") ?? "").Replace("{x:Type ", "").TrimEnd('}');
            if (emptyTypes.Contains(type) || type.Length > 0 && !Capabilities.Controls.Contains(type) && !Objects.Contains(type) && !NonvisualSuffixes.Any(suffix => type.EndsWith(suffix, StringComparison.Ordinal)))
            {
                if (!emptyTypes.Contains(type)) fallbacks.Add((presentation + type).ToString());
                style.Remove();
            }
        }
        ProjectNativeTemplates(document, fallbacks);
        return fallbacks;
    }
    // These WPF hints have no equivalent in the native vector renderer. Removing
    // them changes rendering policy or decoration, never content or event wiring.
    static readonly HashSet<string> Hints = new(StringComparer.Ordinal)
    {
        "VirtualizingPanel.ScrollUnit", "VirtualizingPanel.IsVirtualizing", "VirtualizingPanel.VirtualizationMode",
        "TextOptions.TextFormattingMode", "TextOptions.TextRenderingMode", "RenderOptions.BitmapScalingMode",
        "KeyboardNavigation.DirectionalNavigation", "KeyboardNavigation.TabNavigation", "IsItemsHost",
        "WindowChrome.IsHitTestVisibleInChrome", "shell:WindowChrome.IsHitTestVisibleInChrome",
        "ToolTipService.BetweenShowDelay", "ToolTipService.InitialShowDelay", "ToolTipService.ShowDuration",
        "UseLayoutRounding", "OverridesDefaultStyle", "HasDropShadow", "PanningMode", "TextElement.Foreground"
    };
    static void ProjectNativeTemplates(XDocument document, List<string> fallbacks)
    {
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        foreach (var node in document.Descendants().ToArray())
        {
            if (node.Name.LocalName.EndsWith(".OpacityMask", StringComparison.Ordinal)) { fallbacks.Add("Optional native opacity-mask decoration"); node.Remove(); continue; }
            if (node.Name.LocalName == "Trigger" && (string?)node.Attribute("Property") is "Role" or "IsSubmenuOpen" or "TabStripPlacement")
            { fallbacks.Add("Native menu decoration state: " + (string?)node.Attribute("Property")); node.Remove(); continue; }
            if (node.Attribute("BorderThickness") is {} stroke && !stroke.Value.StartsWith('{'))
            {
                var values = stroke.Value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
                if (values.Length == 4 && values.All(v => double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) && number >= 0))
                {
                    var widths = values.Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    if (widths.Distinct().Count() > 1) { fallbacks.Add("Native uniform border stroke: " + stroke.Value); stroke.Value = widths.Max().ToString(System.Globalization.CultureInfo.InvariantCulture); }
                }
            }
            foreach (var attr in node.Attributes().Where(a => !a.IsNamespaceDeclaration && Hints.Contains(a.Name.LocalName)).ToArray())
            { fallbacks.Add("Native rendering policy: " + attr.Name.LocalName); attr.Remove(); }
            if (node.Name.LocalName == "Setter" && Hints.Contains((string?)node.Attribute("Property") ?? ""))
            { fallbacks.Add("Native rendering policy: " + (string?)node.Attribute("Property")); node.Remove(); }
        }
        foreach (var template in document.Descendants(p + "ControlTemplate").Reverse().ToArray())
        {
            if (template.Document == null) continue;
            var target = ((string?)template.Attribute("TargetType") ?? "").Replace("{x:Type ", "").TrimEnd('}');
            if (!Capabilities.Controls.Contains(target)) continue;
            var roots = template.Elements().Where(n => !n.Name.LocalName.Contains('.')).ToArray();
            var surface = roots.Length == 1 && roots[0].Name.LocalName == "Border" ? roots[0] : new XElement(p + "Border");
            var surfaceName = (string?)surface.Attribute(x + "Name") ?? (string?)surface.Attribute("Name");
            // The native control provides its own editable input, content, item
            // viewport, menu popup, and selection. Template parts only decorate it.
            foreach (var attr in surface.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name != x + "Name" && a.Name.LocalName is not ("Name" or "Background" or "BorderBrush" or "BorderThickness" or "Padding" or "CornerRadius" or "SnapsToDevicePixels")).ToArray())
            { fallbacks.Add("Native template layout: " + target + "." + attr.Name.LocalName); attr.Remove(); }
            foreach (var attr in surface.Attributes().Where(a => a.Value.StartsWith("{TemplateBinding ") && a.Value[17..].TrimEnd('}').Trim() != a.Name.LocalName).ToArray()) attr.Remove();
            var presenter = surface.Elements(p + "EmptyControl").FirstOrDefault();
            presenter = presenter == null ? new XElement(p + "EmptyControl") : new XElement(presenter);
            foreach (var child in surface.Elements().Where(n => n.Name.LocalName != "Border.Effect").ToArray()) child.Remove();
            surface.Add(presenter);
            foreach (var root in roots) root.Remove();
            template.AddFirst(surface);
            foreach (var trigger in template.Descendants(p + "Trigger").ToArray())
            {
                if ((string?)trigger.Attribute("Property") is "Role" or "IsSubmenuOpen" || trigger.Attribute("SourceName") is {} source && source.Value != surfaceName)
                { fallbacks.Add("Native template decoration state: " + (string?)trigger.Attribute("Property")); trigger.Remove(); }
            }
            foreach (var setter in template.Descendants(p + "Setter").Where(n => n.Attribute("TargetName") is {} name && name.Value != surfaceName).ToArray())
            { fallbacks.Add("Empty template decoration: " + (string?)setter.Attribute("TargetName")); setter.Remove(); }
            if (roots.Length != 1 || roots[0] != surface) fallbacks.Add("Native control template: " + target);
        }
    }
}
