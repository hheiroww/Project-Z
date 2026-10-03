using System.Globalization;
using System.Xml.Linq;
using System.Reflection;
using ProjectZ.Shared.Drawing.Designer;
using ProjectZ.WpfCompatibility.Controls;
using ProjectZ.WpfCompatibility.Data;

namespace ProjectZ.WpfCompatibility;

public static class XamlLoader
{
    static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    public static void InitializeApplication(Application app, string path)
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, path));
        if (document.Root?.Elements().Any(n => n.Name.LocalName == "Application.Resources") == true)
            SceneXamlParser.RegisterApplicationResources(Path.Combine(AppContext.BaseDirectory, path));
        var owner = new FrameworkElement();
        try
        {
            var resources = document.Root?.Elements().FirstOrDefault(n => n.Name.LocalName == "Application.Resources");
            if (resources != null) LoadResources(owner, resources, app.GetType().Assembly);
            foreach (var resource in owner.Resources) app.Resources[resource.Key] = resource.Value;
        }
        finally { owner.Dispose(); owner.NativeElement.Dispose(); }
    }
    public static void Initialize(FrameworkElement owner, string path)
    {
        if (owner.NameScope != null) return;
        path = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        var doc = XDocument.Load(path, LoadOptions.SetLineInfo);
        InitializeDocument(owner, doc, owner.GetType().Assembly);
    }
    internal static FrameworkElement LoadTemplate(XElement content, Assembly assembly)
    {
        var owner = new UserControl();
        InitializeDocument(owner, new XDocument(new XElement(content.Name.Namespace + "UserControl", new XAttribute("Height", "24"), content)), assembly);
        return owner;
    }
    static void InitializeDocument(FrameworkElement owner, XDocument doc, Assembly assembly)
    {
        var diagnostics = XamlSupport.Validate(doc).ToArray();
        if (diagnostics.Length > 0) throw new NotSupportedException(string.Join(Environment.NewLine, diagnostics.Select(d => $"({d.Line},{d.Column}): {d.Message}")));
        var root = doc.Root ?? throw new InvalidDataException("XAML has no root.");
        root.SetAttributeValue(XNamespace.Xmlns + "x", X.NamespaceName);
        var templates = root.Descendants().Where(n => n.Name.LocalName == "ListBox.ItemTemplate").Select(n => (Owner: n.Parent!, Content: new XElement(n.Elements().Single().Elements().Single()))).ToList();
        foreach (var node in root.Descendants().Where(n => n.Name.LocalName == "ListBox.ItemTemplate").ToList()) node.Remove();
        var nodes = root.DescendantsAndSelf().Where(n => Capabilities.Controls.Contains(n.Name.LocalName)).ToList();
        int index = 0;
        var usedNames = nodes.Select(n => (string?)n.Attribute(X + "Name") ?? (string?)n.Attribute("Name")).Where(n => n != null).ToHashSet(StringComparer.Ordinal);
        var expressions = new List<(string Name, string Property, string Value)>();
        var resourceProperties = new List<(string Name, string Property, string Value)>();
        foreach (var node in nodes)
        {
            while (usedNames.Contains("__pz" + index)) index++;
            var name = (string?)node.Attribute(X + "Name") ?? (string?)node.Attribute("Name") ?? "__pz" + index++;
            node.SetAttributeValue(X + "Name", name);
            foreach (var attribute in node.Attributes().ToArray())
            {
                if (Capabilities.Events.Contains(attribute.Name.LocalName)) attribute.Remove();
                else if (attribute.Value.StartsWith("{Binding", StringComparison.Ordinal)) { expressions.Add((name, attribute.Name.LocalName, attribute.Value)); attribute.Remove(); }
                else if (attribute.Value.StartsWith("{StaticResource ")) { resourceProperties.Add((name, attribute.Name.LocalName, attribute.Value)); attribute.Remove(); }
            }
        }
        var parser = new SceneXamlParser(Application.Current.Scene);
        var nativeRoot = parser.Parse(doc.ToString());
        owner.ReplaceNative(nativeRoot);
        var scope = owner.NameScope = new(StringComparer.Ordinal);
        var wrappers = new Dictionary<XElement, FrameworkElement>();
        foreach (var node in nodes)
        {
            var name = (string)node.Attribute(X + "Name")!;
            var native = parser.FindName<global::ProjectZ.Shared.Drawing.UI.SceneElement>(name) ?? throw new InvalidDataException($"Cannot create {name}.");
            var wrapper = node == root ? owner : ControlFactory.Wrap(native, node.Name.LocalName);
            wrapper.Name = name; scope.Add(name, wrapper); wrappers.Add(node, wrapper);
            if (wrapper is Window window) window.Title = (string?)node.Attribute("Title") ?? "";
            wrapper.MarkupSetter = (property, value) => parser.SetImportedProperty(native, property, System.Convert.ToString(value, CultureInfo.InvariantCulture));
            var parentNode = node.Ancestors().FirstOrDefault(wrappers.ContainsKey);
            if (parentNode != null)
            {
                var parent = wrappers[parentNode];
                if (parent is Panel panel) panel.Children.AddExisting(wrapper);
                else { parent.LogicalChildren.Add(wrapper); wrapper.Reparent(parent); }
            }
        }
        foreach (var (node, wrapper) in wrappers)
        {
            var resources = node.Elements().FirstOrDefault(n => n.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal));
            if (resources != null) LoadResources(wrapper, resources, assembly);
        }
        foreach (var item in templates) ((ListBox)wrappers[item.Owner]).ItemTemplate = new(item.Content, assembly);
        foreach (var property in resourceProperties)
        {
            var target = scope[property.Name]; var member = target.GetType().GetProperty(property.Property) ?? throw new NotSupportedException($"Resource target {target.GetType().Name}.{property.Property}"); member.SetValue(target, BindingExpression.Coerce(Resource(property.Value, target), member.PropertyType));
        }
        parser.AttachImportedLayout();
        if (parser.UnsupportedFeatures.Count > 0) throw new NotSupportedException(string.Join(Environment.NewLine, parser.UnsupportedFeatures));
        foreach (var expression in expressions) BindingOperations.SetBinding(scope[expression.Name], expression.Property, ParseBinding(expression.Value, scope[expression.Name]));
    }
    static void LoadResources(FrameworkElement owner, XElement resources, Assembly assembly)
    {
        foreach (var node in resources.Elements())
        {
            if (node.Name.LocalName == "ResourceDictionary") { LoadResources(owner, node, assembly); continue; }
            var key = (string?)node.Attribute(X + "Key"); if (key == null) continue;
            object value;
            if (node.Name.LocalName == "SolidColorBrush")
            {
                var text = ((string?)node.Attribute("Color") ?? "#000000").TrimStart('#');
                if (text.Length is not (6 or 8)) throw new NotSupportedException("Resource brush colors must be #RRGGBB or #AARRGGBB.");
                var argb = uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                value = new Media.SolidColorBrush(new(text.Length == 8 ? (byte)(argb >> 24) : (byte)255, (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
            }
            else if (node.Name.NamespaceName.StartsWith("clr-namespace:"))
            {
                var parts = node.Name.NamespaceName[14..].Split(';');
                var source = parts.Length > 1 ? Assembly.Load(parts[1].Replace("assembly=", "")) : assembly;
                var type = source.GetType(parts[0] + "." + node.Name.LocalName, true)!;
                value = Activator.CreateInstance(type) ?? throw new InvalidDataException($"Cannot create resource {type}");
                foreach (var attribute in node.Attributes().Where(a => a.Name.NamespaceName.Length == 0))
                {
                    var property = type.GetProperty(attribute.Name.LocalName) ?? throw new MissingMemberException(type.FullName, attribute.Name.LocalName);
                    property.SetValue(value, BindingExpression.Coerce(attribute.Value, property.PropertyType));
                }
            }
            else continue; // Native brushes/storyboards are owned and validated by SceneXamlParser.
            owner.Resources[key] = value;
        }
    }
    public static Binding ParseBinding(string expression, FrameworkElement target)
    {
        var binding = new Binding();
        foreach (var part in Split(expression[8..^1].Trim()))
        {
            var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length == 1) { binding.Path = new(pair[0]); continue; }
            switch (pair[0])
            {
                case "Path": binding.Path = new(pair[1]); break;
                case "Mode": binding.Mode = Enum.Parse<BindingMode>(pair[1]); break;
                case "UpdateSourceTrigger": binding.UpdateSourceTrigger = Enum.Parse<UpdateSourceTrigger>(pair[1]); break;
                case "ElementName": binding.ElementName = pair[1]; break;
                case "Converter": binding.Converter = (IValueConverter)Resource(pair[1], target)!; break;
                case "Source": binding.Source = Resource(pair[1], target); break;
                case "ConverterParameter": binding.ConverterParameter = pair[1]; break;
                case "TargetNullValue": binding.TargetNullValue = pair[1]; break;
                case "FallbackValue": binding.FallbackValue = pair[1]; break;
                case "RelativeSource":
                    if (pair[1] == "{RelativeSource Self}") binding.RelativeSource = RelativeSource.Self;
                    else
                    {
                        var relative = new RelativeSource(RelativeSourceMode.FindAncestor);
                        foreach (var option in Split(pair[1][16..^1].Trim()))
                        {
                            var setting = option.Split('=', 2, StringSplitOptions.TrimEntries);
                            if (setting.Length == 1 && setting[0] == "FindAncestor") continue;
                            if (setting[0] == "AncestorLevel") relative.AncestorLevel = int.Parse(setting[1], CultureInfo.InvariantCulture);
                            else if (setting[0] == "AncestorType")
                            {
                                var name = setting[1].Replace("{x:Type ", "").TrimEnd('}').Trim();
                                relative.AncestorType = typeof(FrameworkElement).Assembly.GetType(Capabilities.Prefix + (name == "Window" ? "." : ".Controls.") + name) ?? throw new NotSupportedException("Ancestor type " + name);
                            }
                            else throw new NotSupportedException("RelativeSource option " + setting[0]);
                        }
                        binding.RelativeSource = relative;
                    }
                    break;
                default: throw new NotSupportedException($"Binding option {pair[0]}");
            }
        }
        return binding;
    }
    static object? Resource(string value, FrameworkElement target) => value.StartsWith("{StaticResource ") ? target.FindResource(value[16..^1].Trim()) : value;
    internal static IEnumerable<string> Split(string text)
    {
        int depth = 0, start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '{') depth++; else if (text[i] == '}') depth--;
            else if (text[i] == ',' && depth == 0) { yield return text[start..i].Trim(); start = i + 1; }
        }
        if (start < text.Length) yield return text[start..].Trim();
    }
}
