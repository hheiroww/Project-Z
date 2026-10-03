using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class Program
{
    private const string CustomBegin = "' <PROJECTZ-CUSTOM-CODE-BEGIN>";
    private const string CustomEnd = "' <PROJECTZ-CUSTOM-CODE-END>";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly HashSet<string> SupportedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "Scene", "Window", "MetroWindow", "UserControl", "Page", "Rectangle", "RectangleElement",
        "Circle", "CircleElement", "Ellipse", "Text", "TextElement", "TextBlock", "Label", "Image",
        "Button", "CheckBox", "RadioButton", "TextBox", "PasswordBox", "RichTextBox", "ToggleButton",
        "ToggleSwitch", "Slider", "Trackbar", "ProgressBar", "ComboBox", "ListBox", "TabControl",
        "TabItem", "ScrollViewer", "wScrollViewer", "ClipboardHistoryScrollViewer", "Separator", "Expander",
        "ToolTip", "GroupBox", "NumericUpDown", "Popup", "ContextMenu", "MenuItem", "MediaElement",
        "MediaControl", "LoadingCircle", "StackPanel", "Canvas", "WrapPanel", "UniformGrid", "DockPanel",
        "Grid", "Panel", "Border",
        // Resource/timeline nodes consumed by SceneXamlParser. They do not
        // become visual panels and therefore must not be emitted as unsupported
        // code-behind work after the animation translator has handled them.
        "Storyboard", "DoubleAnimation", "ColorAnimation", "DoubleAnimationUsingKeyFrames",
        "ColorAnimationUsingKeyFrames", "ObjectAnimationUsingKeyFrames", "EasingDoubleKeyFrame",
        "LinearDoubleKeyFrame", "SplineDoubleKeyFrame", "DiscreteDoubleKeyFrame", "EasingColorKeyFrame",
        "LinearColorKeyFrame", "SplineColorKeyFrame", "DiscreteColorKeyFrame", "DiscreteObjectKeyFrame",
        "CircleEase", "PowerEase", "SineEase", "TransformGroup", "ScaleTransform", "TranslateTransform",
        "CornerRadius", "GradientStop", "SolidColorBrush", "LinearGradientBrush", "RadialGradientBrush"
    };

    private static readonly HashSet<string> SupportedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name", "Key", "Class", "Uid", "FieldModifier", "Width", "Height", "MinWidth", "MinHeight",
        "MaxWidth", "MaxHeight", "X", "Y", "Left", "Top", "Margin", "Padding", "Visibility", "Opacity",
        "IsEnabled", "IsHitTestVisible", "ClipToBounds", "HorizontalAlignment", "VerticalAlignment", "ZIndex",
        "Background", "Foreground", "Fill", "Stroke", "StrokeThickness", "CornerRadius", "FontSize",
        "FontFamily", "FontWeight", "FontStyle", "Text", "Content", "TextWrapping", "TextTrimming",
        "TextAlignment", "Source", "Stretch", "ToolTip", "IsChecked", "Value", "Minimum", "Maximum",
        "Orientation", "ItemsSource", "SelectedIndex", "Header", "IsExpanded", "AutoSizeWidthOnTextOverflow",
        "CanContentScroll", "HorizontalScrollBarVisibility", "VerticalScrollBarVisibility", "AllowDrop",
        "ResizeMode", "WindowStyle", "Topmost", "ShowInTaskbar", "Title", "Icon", "AllowsTransparency",
        "LastChildFill", "Row", "Column", "RowSpan", "ColumnSpan", "Dock", "NoLeftClickDelay"
    };

    private static readonly Dictionary<string, string> EventMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Click"] = "MouseLeftClick",
        ["MouseLeftButtonDown"] = "MouseLeftDown",
        ["PreviewMouseLeftButtonDown"] = "MouseLeftDown",
        ["MouseLeftButtonUp"] = "MouseLeftUp",
        ["PreviewMouseLeftButtonUp"] = "MouseLeftUp",
        ["MouseRightButtonUp"] = "MouseRightClick",
        ["PreviewMouseRightButtonUp"] = "MouseRightClick",
        ["MouseEnter"] = "MouseEnter",
        ["MouseLeave"] = "MouseLeave",
        ["MouseMove"] = "MouseMove",
        ["MouseWheel"] = "MouseWheel",
        ["PreviewMouseWheel"] = "MouseWheel",
        ["Loaded"] = "Loaded",
        ["SizeChanged"] = "SizeChanged"
    };

    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "build-views") return BuildViewImporter.Run(args[1]);
        if (args.Length > 0 && args[0] == "import-view") return ViewImporter.Run(args[1..]);
        if (args.Length > 0 && args[0] == "import") return ProjectImporter.Run(args[1..]);
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: ProjectZ.XamlCodegen <legacy-xaml-root> <generated-output-root>");
            return 2;
        }

        var inputRoot = Path.GetFullPath(args[0]);
        var outputRoot = Path.GetFullPath(args[1]);
        if (!Directory.Exists(inputRoot))
        {
            Console.Error.WriteLine($"Legacy XAML directory not found: {inputRoot}");
            return 3;
        }

        Directory.CreateDirectory(outputRoot);
        var sourceProjects = Directory.EnumerateFiles(inputRoot, "*.vbproj", SearchOption.TopDirectoryOnly).ToArray();
        var defaultNamespace = sourceProjects.Length == 1
            ? XDocument.Load(sourceProjects[0]).Descendants().FirstOrDefault(e => e.Name.LocalName == "RootNamespace")?.Value ?? "ProjectZImported"
            : "ProjectZImported";
        var generated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        foreach (var xamlPath in Directory.EnumerateFiles(inputRoot, "*.xaml", SearchOption.AllDirectories).OrderBy(p => p))
        {
            try
            {
                var document = XDocument.Load(xamlPath, LoadOptions.SetLineInfo);
                if (document.Root is null) continue;
                var className = document.Root.Attribute(Xaml + "Class")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(className)) continue;

                var fileName = SafeFileName(className) + ".ProjectZ.g.vb";
                var outputPath = Path.Combine(outputRoot, fileName);
                var preserved = ReadPreservedCustomCode(outputPath);
                var relative = Path.GetRelativePath(inputRoot, xamlPath);
                var source = Generate(document.Root, className, relative, preserved, defaultNamespace, new DirectoryInfo(inputRoot).Name);
                WriteIfChanged(outputPath, source);
                generated.Add(Path.GetFullPath(outputPath));
                Console.WriteLine($"Project-Z XAML: {relative} -> {fileName}");
            }
            catch (Exception ex)
            {
                failures.Add($"{xamlPath}: {ex.Message}");
            }
        }

        foreach (var stale in Directory.EnumerateFiles(outputRoot, "*.ProjectZ.g.vb", SearchOption.TopDirectoryOnly))
        {
            if (generated.Contains(Path.GetFullPath(stale))) continue;
            var orphan = stale + ".orphaned";
            if (File.Exists(orphan)) File.Delete(orphan);
            File.Move(stale, orphan);
            Console.WriteLine($"Project-Z XAML: preserved stale custom code as {Path.GetFileName(orphan)}");
        }

        if (failures.Count == 0) return 0;
        Console.Error.WriteLine("Project-Z XAML code generation failed:");
        foreach (var failure in failures) Console.Error.WriteLine("  " + failure);
        return 1;
    }

    private static string Generate(XElement root, string xamlClass, string relativePath, string customCode, string defaultNamespace, string sourceDirectory)
    {
        var (nameSpace, shortName) = SplitClassName(xamlClass, defaultNamespace);
        var named = root.DescendantsAndSelf()
            .Select(e => (Element: e, Name: e.Attribute(Xaml + "Name")?.Value ?? e.Attribute("Name")?.Value))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name!, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        var unsupported = FindUnsupported(root).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var bindings = FindEventBindings(named, unsupported);
        var wpfBase = root.Name.LocalName switch
        {
            "Window" or "MetroWindow" => "System.Windows.Window",
            "Application" => "System.Windows.Application",
            "Page" => "System.Windows.Controls.Page",
            _ => "System.Windows.Controls.UserControl"
        };

        var sb = new StringBuilder();
        sb.AppendLine("' <auto-generated>");
        sb.AppendLine("' Regenerated on every build by ProjectZ.XamlCodegen.");
        sb.AppendLine("' Edit only the marked custom-code section; it is preserved verbatim.");
        sb.AppendLine("' </auto-generated>");
        sb.AppendLine("Imports ProjectZ.Shared.Drawing");
        sb.AppendLine("Imports ProjectZ.Shared.Drawing.Designer");
        sb.AppendLine("Imports ProjectZ.Shared.Drawing.UI");
        sb.AppendLine("Imports Microsoft.Xna.Framework");
        sb.AppendLine();
        sb.AppendLine($"Namespace Global.{nameSpace}");
        sb.AppendLine($"    Partial Public Class {VbIdentifier(shortName)}");
        sb.AppendLine($"        Inherits {wpfBase}");
        sb.AppendLine();
        sb.AppendLine("        Private _projectZParser As SceneXamlParser");
        sb.AppendLine("        Public ReadOnly Property ProjectZRoot As SceneElement");
        sb.AppendLine("        Public Property ProjectZEventHandler As Action(Of String, SceneElement, Object)");
        sb.AppendLine();
        sb.AppendLine("        Public Sub InitializeProjectZ(scene As Scene, Optional xamlPath As String = Nothing)");
        sb.AppendLine($"            If String.IsNullOrWhiteSpace(xamlPath) Then xamlPath = IO.Path.Combine(AppContext.BaseDirectory, \"LegacyXaml\", \"{VbString(sourceDirectory)}\", \"{VbString(relativePath)}\")");
        sb.AppendLine("            _projectZParser = New SceneXamlParser(scene)");
        sb.AppendLine("            _ProjectZRoot = _projectZParser.ParseLegacyWindow(xamlPath)");
        sb.AppendLine("            BindProjectZEvents()");
        sb.AppendLine("            InitializeProjectZCustomCode()");
        sb.AppendLine("        End Sub");
        sb.AppendLine();
        foreach (var item in named)
        {
            sb.AppendLine($"        Public ReadOnly Property {VbIdentifier(item.Name!)} As SceneElement");
            sb.AppendLine("            Get");
            sb.AppendLine($"                Return If(_projectZParser Is Nothing, Nothing, _projectZParser.FindName(Of SceneElement)(\"{VbString(item.Name!)}\"))");
            sb.AppendLine("            End Get");
            sb.AppendLine("        End Property");
        }

        sb.AppendLine();
        sb.AppendLine("        Private Sub BindProjectZEvents()");
        foreach (var binding in bindings)
        {
            var local = "element_" + SafeIdentifier(binding.Name) + "_" + binding.Index;
            sb.AppendLine($"            Dim {local} = _projectZParser.FindName(Of SceneElement)(\"{VbString(binding.Name)}\")");
            sb.AppendLine($"            If {local} IsNot Nothing Then");
            sb.AppendLine(EventAddHandler(local, binding.ProjectZEvent, binding.Handler));
            sb.AppendLine("            End If");
        }
        sb.AppendLine("        End Sub");
        sb.AppendLine();
        sb.AppendLine("        Private Sub DispatchProjectZEvent(handlerName As String, sender As SceneElement, eventData As Object)");
        sb.AppendLine("            ProjectZEventHandler?.Invoke(handlerName, sender, eventData)");
        sb.AppendLine("        End Sub");
        sb.AppendLine();
        sb.AppendLine("#Region \"PROJECT-Z USER UPDATE REQUIRED\"");
        sb.AppendLine("        Public Shared ReadOnly UnsupportedProjectZFeatures As String() = {");
        if (unsupported.Count == 0)
            sb.AppendLine("            \"None\"");
        else
            sb.AppendLine(string.Join("," + Environment.NewLine, unsupported.Select(x => $"            \"{VbString(x)}\"")));
        sb.AppendLine("        }");
        sb.AppendLine("#End Region");
        sb.AppendLine();
        sb.AppendLine("        Private Sub InitializeProjectZCustomCode()");
        sb.AppendLine("#Region \"PROJECT-Z CUSTOM CODE - PRESERVED ON BUILD\"");
        sb.AppendLine("        " + CustomBegin);
        if (string.IsNullOrWhiteSpace(customCode))
        {
            sb.AppendLine("            ' Put Project-Z-only event dispatch or unsupported-feature replacements here.");
            sb.AppendLine("            ' This exact section is copied forward unchanged by every build.");
        }
        else
        {
            sb.Append(customCode);
            if (!customCode.EndsWith(Environment.NewLine, StringComparison.Ordinal)) sb.AppendLine();
        }
        sb.AppendLine("        " + CustomEnd);
        sb.AppendLine("#End Region");
        sb.AppendLine("        End Sub");
        sb.AppendLine("    End Class");
        sb.AppendLine("End Namespace");
        return sb.ToString();
    }

    private static List<EventBinding> FindEventBindings(List<(XElement Element, string? Name)> named, List<string> unsupported)
    {
        var result = new List<EventBinding>();
        var index = 0;
        foreach (var item in named)
        {
            foreach (var attr in item.Element.Attributes().Where(a => !a.IsNamespaceDeclaration))
            {
                if (!LooksLikeHandler(attr.Value)) continue;
                if (EventMap.TryGetValue(attr.Name.LocalName, out var projectZEvent))
                    result.Add(new EventBinding(item.Name!, attr.Value, projectZEvent, index++));
                else if (IsLikelyEvent(attr.Name.LocalName))
                    unsupported.Add($"Event {item.Element.Name.LocalName}.{attr.Name.LocalName}={attr.Value} needs a Project-Z adapter");
            }
        }
        return result;
    }

    private static IEnumerable<string> FindUnsupported(XElement root)
    {
        foreach (var element in root.DescendantsAndSelf())
        {
            if (element.Name.LocalName.Contains('.')) continue;
            if (!SupportedElements.Contains(element.Name.LocalName) && element != root)
                yield return $"Element {element.Name.LocalName} is mapped to a generic Project-Z panel";

            foreach (var attr in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
            {
                var name = attr.Name.LocalName;
                if (EventMap.ContainsKey(name) || SupportedProperties.Contains(name)) continue;
                if (name.StartsWith("xmlns", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsLikelyEvent(name) && LooksLikeHandler(attr.Value)) continue;
                if (attr.Value.StartsWith("{Binding", StringComparison.OrdinalIgnoreCase) ||
                    attr.Value.StartsWith("{DynamicResource", StringComparison.OrdinalIgnoreCase) ||
                    attr.Value.StartsWith("{StaticResource", StringComparison.OrdinalIgnoreCase))
                    yield return $"Markup expression {element.Name.LocalName}.{name}={attr.Value}";
            }
        }
    }

    private static string EventAddHandler(string local, string eventName, string handler)
    {
        var dispatch = $"DispatchProjectZEvent(\"{VbString(handler)}\", {local}, data)";
        return eventName switch
        {
            "MouseEnter" or "MouseLeave" or "Loaded" => $"                AddHandler {local}.{eventName}, Sub() DispatchProjectZEvent(\"{VbString(handler)}\", {local}, Nothing)",
            "MouseMove" or "SizeChanged" => $"                AddHandler {local}.{eventName}, Sub(a, b) DispatchProjectZEvent(\"{VbString(handler)}\", {local}, New Object() {{a, b}})",
            "MouseWheel" => $"                AddHandler {local}.{eventName}, Sub(delta, point) DispatchProjectZEvent(\"{VbString(handler)}\", {local}, New Object() {{delta, point}})",
            _ => $"                AddHandler {local}.{eventName}, Sub(data) {dispatch}"
        };
    }

    private static (string Namespace, string Name) SplitClassName(string value, string defaultNamespace)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return (defaultNamespace, parts[0]);
        return (string.Join('.', parts[..^1]), parts[^1]);
    }

    private static string ReadPreservedCustomCode(string path)
    {
        if (!File.Exists(path)) return string.Empty;
        var text = File.ReadAllText(path);
        var begin = text.IndexOf(CustomBegin, StringComparison.Ordinal);
        if (begin < 0) return string.Empty;
        begin = text.IndexOf('\n', begin);
        if (begin < 0) return string.Empty;
        begin++;
        var endMarker = text.IndexOf(CustomEnd, begin, StringComparison.Ordinal);
        if (endMarker < 0) return string.Empty;
        var endLine = text.LastIndexOf('\n', endMarker);
        var end = endLine >= begin ? endLine + 1 : endMarker;
        return text[begin..end];
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path) && File.ReadAllText(path) == content) return;
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static bool LooksLikeHandler(string value) =>
        !bool.TryParse(value, out _) &&
        !string.Equals(value, "Nothing", StringComparison.OrdinalIgnoreCase) &&
        Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*$");
    private static bool IsLikelyEvent(string name) => name.Contains("Click", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Mouse", StringComparison.OrdinalIgnoreCase) || name is "Loaded" or "Drop" or "DragEnter" or
        "DragLeave" or "DragOver" or "TextChanged" or "SelectionChanged" or "Checked" or "Unchecked" or "Closing";
    private static string VbString(string value) => value.Replace("\"", "\"\"");
    private static string SafeFileName(string value) => Regex.Replace(value, @"[^A-Za-z0-9_.-]", "_");
    private static string SafeIdentifier(string value) => Regex.Replace(value, @"[^A-Za-z0-9_]", "_");
    private static string VbIdentifier(string value) => "[" + value.Replace("]", "_") + "]";
    private sealed record EventBinding(string Name, string Handler, string ProjectZEvent, int Index);
}
