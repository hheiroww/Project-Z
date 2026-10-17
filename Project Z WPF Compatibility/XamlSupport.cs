using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace ProjectZ.WpfCompatibility;

public static class XamlSupport
{
    public sealed record Diagnostic(int Line, int Column, string Message);
    public static IEnumerable<Diagnostic> Validate(XDocument document)
    {
        var original = document;
        document = new XDocument(document);
        var sourceMap = original.Descendants().SelectMany(e => new XObject[] { e }.Concat(e.Attributes())).Zip(document.Descendants().SelectMany(e => new XObject[] { e }.Concat(e.Attributes()))).ToDictionary(p => p.Second, p => p.First);
        MarkupTranslation.Translate(document);
        var issues = new List<Diagnostic>();
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        void Error(XObject node, string message) { var line = (IXmlLineInfo)(sourceMap.TryGetValue(node, out var originalNode) ? originalNode : node); issues.Add(new(line.LineNumber, line.LinePosition, message)); }
        foreach (var node in document.Descendants())
        {
            if (node.Name.LocalName is "Application" or "Application.Resources")
            {
                foreach (var a in node.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name != x + "Class" && a.Name.LocalName is not ("StartupUri" or "Startup" or "Exit" or "ShutdownMode"))) Error(a, "Unsupported application property " + a.Name);
                continue;
            }
            if (NativeMarkup.IsDeclaration(node))
            {
                foreach (var (source, message) in NativeMarkup.Validate(node)) Error(source, message);
                continue;
            }
            if (Capabilities.PropertyElements.Contains(node.Name.LocalName) || NativeMarkup.IsPropertyElement(node.Name.LocalName))
            {
                foreach (var a in node.Attributes().Where(a => !a.IsNamespaceDeclaration && !(node.Name.LocalName is "DataTemplate" or "ResourceDictionary" && a.Name == x + "Key"))) Error(a, "Unsupported property-element option " + a.Name);
                continue;
            }
            if (node.Name.LocalName == "SolidColorBrush")
            {
                foreach (var a in node.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name != x + "Key" && a.Name.LocalName != "Color")) Error(a, "Unsupported brush property " + a.Name);
                continue;
            }
            if (node.Name.NamespaceName.StartsWith("clr-namespace:") && node.Ancestors().Any(p => p.Name.LocalName.EndsWith(".Resources"))) continue;
            if (node.Name.NamespaceName != "http://schemas.microsoft.com/winfx/2006/xaml/presentation") { Error(node, "Unsupported XAML namespace/type " + node.Name); continue; }
            if (node.Name.LocalName is "RowDefinition" or "ColumnDefinition")
            {
                var names = node.Name.LocalName == "RowDefinition" ? new[] { "Height", "MinHeight", "MaxHeight" } : new[] { "Width", "MinWidth", "MaxWidth" };
                foreach (var a in node.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name != x + "Name" && !names.Contains(a.Name.LocalName))) Error(a, "Unsupported grid definition property " + a.Name);
                continue;
            }
            if (!Capabilities.Controls.Contains(node.Name.LocalName)) { Error(node, "Unsupported XAML element " + node.Name); continue; }
            foreach (var attr in node.Attributes().Where(a => !a.IsNamespaceDeclaration))
            {
                if (attr.Name.Namespace == x && attr.Name.LocalName is "Class" or "Name" or "FieldModifier") continue;
                if (attr.Name.NamespaceName.Contains("blend") || attr.Name.NamespaceName.Contains("markup-compatibility")) continue;
                if (attr.Name.NamespaceName == "urn:projectz:import") continue;
                if (!Capabilities.Properties.Contains(attr.Name.LocalName) && Capabilities.EventName(attr.Name.LocalName) == null) Error(attr, "Unsupported XAML property/event " + attr.Name.LocalName);
                string[]? owners = attr.Name.LocalName switch
                {
                    "Text" or "TextAlignment" => ["TextBox", "TextBlock", "RichTextBox"],
                    "TextWrapping" => ["TextBlock","TextBox","RichTextBox"], "TextTrimming" => ["TextBlock"],
                    "IsReadOnly" => ["TextBox", "RichTextBox"], "ItemsSource" or "DisplayMemberPath" => ["ItemsControl", "ListBox", "ListView", "ComboBox", "TabControl"], "SelectedIndex" or "SelectedItem" or "SelectedValue" or "SelectedValuePath" => ["ListBox", "ListView", "ComboBox", "TabControl"],
                    "IsChecked" => ["CheckBox", "RadioButton", "MenuItem", "ToggleButton"], "Minimum" or "Maximum" or "Value" => ["ProgressBar", "Slider"], "Command" or "CommandParameter" => ["Button", "MenuItem"],
                    "Orientation" => ["StackPanel", "WrapPanel", "Separator", "ProgressBar", "Slider"], "Title" => ["Window"], "Content" => ["ContentControl", "Button", "CheckBox", "RadioButton", "Label", "ComboBoxItem", "ListBoxItem", "ListViewItem", "TabItem", "ToggleButton"],
                    "IsSelected" => ["ComboBoxItem", "ListBoxItem", "ListViewItem", "TabItem"], "IsDropDownOpen" => ["ComboBox"],
                    "Password" => ["PasswordBox"], "GroupName" => ["RadioButton"], "Header" => ["GroupBox", "Expander", "MenuItem", "TabItem"], "IsExpanded" => ["Expander"], "ItemWidth" or "ItemHeight" => ["WrapPanel"],
                    "Rows" or "Columns" => ["UniformGrid"],
                    _ => null
                };
                if (owners != null && !owners.Contains(node.Name.LocalName)) Error(attr, $"Unsupported property {node.Name.LocalName}.{attr.Name.LocalName}");
                if (node.Name.LocalName is "ComboBoxItem" or "ListBoxItem" && node.Elements().Any()) Error(node, "Item containers currently require scalar Content.");
                if (attr.Name.LocalName == "BorderThickness" && !attr.Value.StartsWith("{"))
{
    var parts = attr.Value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 1 && parts.Length != 4)
    {
        Error(attr, "Asymmetric border strokes are not supported yet.");
        continue;
    }

    double[] thicknessValues = new double[parts.Length];
    for (int i = 0; i < parts.Length; i++)
    {
        if (!double.TryParse(parts[i], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out thicknessValues[i]) || !double.IsFinite(thicknessValues[i]) || thicknessValues[i] < 0 || thicknessValues[i] > float.MaxValue)
        {
            Error(attr, "Invalid BorderThickness.");
            continue;
        }
    }

    if (parts.Length == 4 && (thicknessValues[0] != thicknessValues[1] || thicknessValues[1] != thicknessValues[2] || thicknessValues[2] != thicknessValues[3]))
    {
        Error(attr, "Asymmetric border strokes are not supported yet.");
        continue;
    }
}

if (attr.Value.StartsWith("{Binding"))
                {
                    foreach (Match match in Regex.Matches(attr.Value, @"(?:^\{Binding\s+|,\s*)(\w+)\s*="))
                        if (!Capabilities.BindingOptions.Contains(match.Groups[1].Value) && match.Groups[1].Value is not ("AncestorType" or "AncestorLevel")) Error(attr, "Unsupported binding option " + match.Groups[1].Value);
                    if (attr.Value.Contains("MultiBinding") || attr.Value.Contains("TemplatedParent")) Error(attr, "Unsupported binding expression " + attr.Value);
                    if (Regex.IsMatch(attr.Value, @"\{Binding\s+(?:Path\s*=\s*)?[^,}]*[\[\]()/]")) Error(attr, "Only dotted CLR property paths are supported by this binding engine.");
                    var mode = Regex.Match(attr.Value, @"\bMode\s*=\s*(\w+)");
                    if (mode.Success && mode.Groups[1].Value is not ("Default" or "OneTime" or "OneWay" or "TwoWay")) Error(attr, "Unsupported binding mode " + mode.Groups[1].Value);
                    var trigger = Regex.Match(attr.Value, @"\bUpdateSourceTrigger\s*=\s*(\w+)");
                    if (trigger.Success && trigger.Groups[1].Value is not ("Default" or "PropertyChanged" or "LostFocus" or "Explicit")) Error(attr, "Unsupported update trigger " + trigger.Groups[1].Value);
                }
                else if (attr.Value.StartsWith("{TemplateBinding ") && node.Ancestors().Any(n => n.Name.LocalName == "ControlTemplate"))
                {
                    var sourceProperty = attr.Value[17..].TrimEnd('}').Trim();
                    if (!Capabilities.Properties.Contains(sourceProperty)) Error(attr, "Unsupported template binding property " + sourceProperty);
                    if (node.Name.LocalName != "EmptyControl" && attr.Name.LocalName != sourceProperty && !(node.Name.LocalName=="ScrollViewer" && attr.Name.LocalName=="Margin" && sourceProperty=="Padding")) Error(attr, "Native template bindings require the same surface and control property.");
                }
                else if (attr.Value.StartsWith("{") && attr.Value != "{x:Null}" && !attr.Value.StartsWith("{StaticResource ") && !attr.Value.StartsWith("{}")) Error(attr, "Unsupported markup expression " + attr.Value);
            }
        }
        return issues;
    }
}
