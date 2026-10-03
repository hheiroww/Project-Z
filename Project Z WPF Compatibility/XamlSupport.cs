using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace ProjectZ.WpfCompatibility;

public static class XamlSupport
{
    public sealed record Diagnostic(int Line, int Column, string Message);
    public static IEnumerable<Diagnostic> Validate(XDocument document)
    {
        var issues = new List<Diagnostic>();
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        void Error(XObject node, string message) { var line = (IXmlLineInfo)node; issues.Add(new(line.LineNumber, line.LinePosition, message)); }
        foreach (var node in document.Descendants())
        {
            if (node.Name.LocalName is "Application" or "Application.Resources")
            {
                foreach (var a in node.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name != x + "Class" && a.Name.LocalName is not ("StartupUri" or "Startup" or "Exit"))) Error(a, "Unsupported application property " + a.Name);
                continue;
            }
            if (Capabilities.PropertyElements.Contains(node.Name.LocalName))
            {
                foreach (var a in node.Attributes().Where(a => !a.IsNamespaceDeclaration)) Error(a, "Unsupported property-element option " + a.Name);
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
                foreach (var a in node.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.LocalName != (node.Name.LocalName == "RowDefinition" ? "Height" : "Width"))) Error(a, "Unsupported grid definition property " + a.Name);
                continue;
            }
            if (!Capabilities.Controls.Contains(node.Name.LocalName)) { Error(node, "Unsupported XAML element " + node.Name); continue; }
            foreach (var attr in node.Attributes().Where(a => !a.IsNamespaceDeclaration))
            {
                if (attr.Name.Namespace == x && attr.Name.LocalName is "Class" or "Name" or "FieldModifier") continue;
                if (attr.Name.NamespaceName.Contains("blend") || attr.Name.NamespaceName.Contains("markup-compatibility")) continue;
                if (!Capabilities.Properties.Contains(attr.Name.LocalName) && !Capabilities.Events.Contains(attr.Name.LocalName)) Error(attr, "Unsupported XAML property/event " + attr.Name.LocalName);
                string[]? owners = attr.Name.LocalName switch
                {
                    "Text" => ["TextBox", "TextBlock"], "IsReadOnly" => ["TextBox"], "ItemsSource" or "SelectedIndex" or "SelectedItem" => ["ListBox", "ComboBox"],
                    "IsChecked" => ["CheckBox", "RadioButton"], "Minimum" or "Maximum" or "Value" => ["ProgressBar", "Slider"], "Command" or "CommandParameter" => ["Button"],
                    "Orientation" => ["StackPanel", "WrapPanel", "Separator"], "Title" => ["Window"], "Content" => ["Button", "CheckBox", "RadioButton", "Label"],
                    "Password" => ["PasswordBox"], "GroupName" => ["RadioButton"], "Header" => ["GroupBox", "Expander"], "IsExpanded" => ["Expander"], "ItemWidth" or "ItemHeight" => ["WrapPanel"],
                    _ => null
                };
                if (owners != null && !owners.Contains(node.Name.LocalName)) Error(attr, $"Unsupported property {node.Name.LocalName}.{attr.Name.LocalName}");
                if (node.Ancestors().Any(p => p.Name.LocalName == "DataTemplate") && Capabilities.Events.Contains(attr.Name.LocalName)) Error(attr, "Template event handlers require a command binding in this release.");
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
                else if (attr.Value.StartsWith("{") && !attr.Value.StartsWith("{StaticResource ")) Error(attr, "Unsupported markup expression " + attr.Value);
            }
        }
        return issues;
    }
}
