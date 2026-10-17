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
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, path), LoadOptions.SetBaseUri);
        if ((string?)document.Root?.Attribute("ShutdownMode") is { } shutdownMode) app.ShutdownMode = Enum.Parse<ShutdownMode>(shutdownMode);
        if (document.Root?.Elements().Any(n => n.Name.LocalName == "Application.Resources") == true)
            SceneXamlParser.RegisterApplicationResources(Path.Combine(AppContext.BaseDirectory, path));
        var owner = new FrameworkElement();
        owner.XamlEventOwner = app;
        owner.XamlSourceDirectory = Path.GetDirectoryName(new Uri(document.BaseUri).LocalPath);
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
        var doc = XDocument.Load(path, LoadOptions.SetLineInfo | LoadOptions.SetBaseUri);
        owner.XamlSourceDirectory = Path.GetDirectoryName(path);
        InitializeDocument(owner, doc, owner.GetType().Assembly);
    }
    internal static FrameworkElement LoadTemplate(XElement content, Assembly assembly, object? eventOwner = null, FrameworkElement? resourceOwner = null)
    {
        var owner = new UserControl();
        owner.ResourceOwner = resourceOwner;
        owner.XamlSourceDirectory = resourceOwner?.XamlSourceDirectory;
        var root = new XElement(content.Name.Namespace + "UserControl");
        if (content.Name.LocalName == "DataTemplate")
        {
            foreach (var child in content.Elements())
            {
                var copy = new XElement(child);
                if (copy.Name.LocalName == "DataTemplate.Triggers") copy.Name = copy.Name.Namespace + "UserControl.Triggers";
                if (copy.Name.LocalName == "DataTemplate.Resources") copy.Name = copy.Name.Namespace + "UserControl.Resources";
                root.Add(copy);
            }
        }
        else root.Add(content);
        InitializeDocument(owner, new XDocument(root), assembly, eventOwner);
        return owner;
    }
    static void InitializeDocument(FrameworkElement owner, XDocument doc, Assembly assembly, object? eventOwner = null)
    {
        owner.XamlEventOwner = eventOwner ?? owner;
        MarkupTranslation.Translate(doc);
        var diagnostics = XamlSupport.Validate(doc).ToArray();
        if (diagnostics.Length > 0) throw new NotSupportedException(string.Join(Environment.NewLine, diagnostics.Select(d => $"({d.Line},{d.Column}): {d.Message}")));
        using var construction=Application.Current.ConstructIn(owner.NativeElement.OwnerScene);
        var parser = new SceneXamlParser(owner.NativeElement.OwnerScene) { ImportedEventRouting = true, ImplicitStoryboardTriggersEnabled = false };
        if (owner.ResourceOwner != null)
        {
            var inherited = new XElement("ResourceDictionary");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<FrameworkElement>();
            for (var scopeOwner = owner.ResourceOwner; scopeOwner != null && visited.Add(scopeOwner); scopeOwner = scopeOwner.ResourceOwner ?? scopeOwner.Parent)
                foreach (var entry in scopeOwner.ResourceMarkup)
                    if (keys.Add(entry.Key)) inherited.Add(new XElement(entry.Value));
            parser.ImportedResourceMarkup = inherited.ToString();
        }
        doc = XDocument.Parse(parser.ExpandImportedStyles(doc.ToString()), LoadOptions.SetLineInfo);
        var root = doc.Root ?? throw new InvalidDataException("XAML has no root.");
        root.SetAttributeValue(XNamespace.Xmlns + "x", X.NamespaceName);
        var popups = root.Descendants().Where(node => (node.Name.LocalName.EndsWith(".ToolTip", StringComparison.Ordinal) || node.Name.LocalName.EndsWith(".ContextMenu", StringComparison.Ordinal)) && node.Parent != null && Capabilities.IsVisualObject(node.Parent) && node.Elements().Any()).Select(node => (Owner: node.Parent!, IsToolTip: node.Name.LocalName.EndsWith(".ToolTip", StringComparison.Ordinal), Content: new XElement(node.Elements().First()))).ToList();
        foreach (var popup in popups) popup.Owner.Elements().Where(node => node.Name.LocalName.EndsWith(popup.IsToolTip ? ".ToolTip" : ".ContextMenu", StringComparison.Ordinal)).Remove();
        var documents = root.Descendants().Where(n => n.Name.LocalName == "FlowDocument" && n.Parent?.Name.LocalName is "RichTextBox" or "RichTextBox.Document" && Capabilities.IsVisualObject(n.Parent.Name.LocalName == "RichTextBox" ? n.Parent : n.Parent.Parent!)).Select(n => (Owner: n.Parent!.Name.LocalName == "RichTextBox" ? n.Parent : n.Parent.Parent!, Document: new XElement(n))).ToList();
        foreach (var item in documents) item.Owner.Elements().Where(n => n.Name.LocalName is "FlowDocument" or "RichTextBox.Document").Remove();
        var templates = root.Descendants().Where(n => n.Name.LocalName is "ListBox.ItemTemplate" or "ListView.ItemTemplate" or "ItemsControl.ItemTemplate" && n.Parent != null && Capabilities.IsVisualObject(n.Parent)).Select(n => (Owner: n.Parent!, Content: new XElement(n.Elements().Single()))).ToList();
        foreach (var item in templates) item.Owner.Elements().Where(n => n.Name.LocalName.EndsWith(".ItemTemplate", StringComparison.Ordinal)).Remove();
        var nodes = root.DescendantsAndSelf().Where(Capabilities.IsVisualObject).ToList();
        int index = 0;
        var usedNames = nodes.Select(n => (string?)n.Attribute(X + "Name") ?? (string?)n.Attribute("Name")).Where(n => n != null).ToHashSet(StringComparer.Ordinal);
        var expressions = new List<(string Name, string Property, string Value)>();
        var resourceProperties = new List<(string Name, string Property, string Value)>();
        var adapterProperties = new List<(string Name, string Property, string Value)>();
        var templateEvents = new List<(string Name, string Event, string Handler)>();
        foreach (var node in nodes)
        {
            // Native input adapters own these policies; the scene parser only
            // constructs their common visual surface.
            while (usedNames.Contains("__pz" + index)) index++;
            var name = (string?)node.Attribute(X + "Name") ?? (string?)node.Attribute("Name") ?? "__pz" + index++;
            node.SetAttributeValue(X + "Name", name);
            foreach (var attribute in node.Attributes().ToArray())
            {
                if (node.Name.LocalName == "Window" && attribute.Name.LocalName is "WindowStyle" or "ResizeMode" or "WindowStartupLocation" or "WindowState" or "SizeToContent" or "ShowInTaskbar" or "Topmost" or "ShowActivated" or "AllowsTransparency" or "Left" or "Top" or "Icon")
                {
                    // These settings belong to the owning HWND, rather than its
                    // scene rectangle. Retain normal binding/resource handling.
                    if (!attribute.Value.StartsWith('{')) { adapterProperties.Add((name, attribute.Name.LocalName, attribute.Value)); continue; }
                }
                if(attribute.Name.LocalName is "SelectionMode" or "TickFrequency" or "IsSnapToTickEnabled" or "LineHeight" or "IsReadOnlyCaretVisible" or "Panel.ZIndex" or "SnapsToDevicePixels" or "ScrollViewer.HorizontalScrollBarVisibility" or "ScrollViewer.VerticalScrollBarVisibility" or "ScrollViewer.CanContentScroll" or "CanContentScroll" or "HorizontalScrollBarVisibility" or "VerticalScrollBarVisibility" or "CaretBrush" or "SelectionBrush" or "SelectionOpacity" or "IsEditable" or "IsTextSearchEnabled" or "MaxDropDownHeight" or "TextSearch.TextPath" or "IsTabStop" or "LargeChange" or "SmallChange" or "Placement" or "RadiusX" or "RadiusY") {adapterProperties.Add((name,attribute.Name.LocalName,attribute.Value));continue;}
                if (Capabilities.EventName(attribute.Name.LocalName) != null) { templateEvents.Add((name, attribute.Name.LocalName, attribute.Value)); attribute.Remove(); }
                else if (attribute.Value.StartsWith("{Binding", StringComparison.Ordinal)) { expressions.Add((name, attribute.Name.LocalName, attribute.Value)); attribute.Remove(); }
                else if (attribute.Value.StartsWith("{StaticResource ") && attribute.Name.LocalName is not ("Style" or "Template")) { resourceProperties.Add((name, attribute.Name.LocalName, attribute.Value)); if (attribute.Name.LocalName is not ("Background" or "Foreground" or "BorderBrush" or "Effect")) attribute.Remove(); }
                else if (attribute.Name.LocalName is "Tag" or "DisplayMemberPath" or "SelectedValuePath" or "SelectedIndex" or "SelectedItem" or "SelectedValue" or "IsSelected" or "IsDropDownOpen" or "FontFamily" or "FontSize" or "FontWeight" or "VerticalContentAlignment" or "HorizontalContentAlignment" or "TextAlignment" or "TextDecorations" or "RenderTransformOrigin" or "IsThreeState" or "IsDefault" or "IsCancel" or "AcceptsReturn" or "ToolTip" or "Cursor" or "ClipToBounds" or "AutomationProperties.Name" or "AutomationProperties.HelpText") { adapterProperties.Add((name, attribute.Name.LocalName, attribute.Value)); }
                else if (node.Name.LocalName == "Image" && attribute.Name.LocalName == "Source") adapterProperties.Add((name, "Source", attribute.Value));
            }
        }
        var nativeDocument = new XDocument(doc);
        // Imported images decode to checked CPU pixels before a native texture upload.
        // Letting the parser load Source here would use Texture2D.FromStream first.
        foreach (var image in nativeDocument.Root!.DescendantsAndSelf().Where(n => n.Name.LocalName == "Image")) image.Attribute("Source")?.Remove();
        foreach (var browser in nativeDocument.Root!.DescendantsAndSelf().Where(n => n.Name.LocalName is "WebView2" or "WebView2CompositionControl"))
        {
            browser.Name = browser.Name.Namespace + "Border";
            foreach (var attribute in browser.Attributes().Where(a => a.Name.LocalName is "Source" or "DefaultBackgroundColor" or "ZoomFactor" or "AllowExternalDrop").ToArray()) attribute.Remove();
            browser.Elements().Where(n => n.Name.LocalName.EndsWith(".CreationProperties", StringComparison.Ordinal)).Remove();
        }
        foreach (var item in nativeDocument.Descendants().Where(n => n.Name.LocalName is "ComboBoxItem" or "ListBoxItem" or "ListViewItem").ToArray()) item.Remove();
        var customControls = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        parser.ImportedControlFactory = xml =>
        {
            var typeName = xml.GetAttribute("Type", "urn:projectz:import");
            var assemblyName = xml.GetAttribute("Assembly", "urn:projectz:import");
            var customAssembly = assemblyName.Length == 0 ? assembly : Assembly.Load(assemblyName);
            var type = customAssembly.GetType(typeName) ?? AppDomain.CurrentDomain.GetAssemblies().Select(candidate => candidate.GetType(typeName)).FirstOrDefault(candidate => candidate != null) ?? throw new InvalidDataException("Imported control type is missing: " + typeName);
            var custom = Activator.CreateInstance(type) as FrameworkElement ?? throw new InvalidDataException("Imported control must derive from Project-Z FrameworkElement: " + typeName);
            customControls.Add(xml.GetAttribute("Name", X.NamespaceName), custom);
            return custom.NativeElement;
        };
        var nativeRoot = parser.Parse(nativeDocument.ToString());
        owner.ReplaceNative(nativeRoot);
        var scope = owner.NameScope = new(StringComparer.Ordinal);
        owner.ObjectNameScope = new(StringComparer.Ordinal);
        var wrappers = new Dictionary<XElement, FrameworkElement>();
        foreach (var node in nodes)
        {
            var name = (string)node.Attribute(X + "Name")!;
            var wrapper = customControls.TryGetValue(name, out var custom) ? custom : node.Name.LocalName switch
            {
                "ComboBoxItem" => new ComboBoxItem { Content = (string?)node.Attribute("Content") ?? node.Value },
                "ListBoxItem" => new ListBoxItem { Content = (string?)node.Attribute("Content") ?? node.Value },
                "ListViewItem" => new ListViewItem { Content = (string?)node.Attribute("Content") ?? node.Value },
                "WebView2" => new WebView2(parser.FindName<global::ProjectZ.Shared.Drawing.UI.SceneElement>(name)!),
                "WebView2CompositionControl" => new WebView2CompositionControl(parser.FindName<global::ProjectZ.Shared.Drawing.UI.SceneElement>(name)!),
                _ => node == root ? owner : ControlFactory.Wrap(parser.FindName<global::ProjectZ.Shared.Drawing.UI.SceneElement>(name) ?? throw new InvalidDataException($"Cannot create {name} ({node.Name.LocalName}) in {owner.GetType().FullName}."), node.Name.LocalName)
            };
            var native = wrapper.NativeElement;
            wrapper.WidthSpecified = node.Attribute("Width") is { Value: not "Auto" };
            wrapper.HeightSpecified = node.Attribute("Height") is { Value: not "Auto" };
            wrapper.HorizontalAlignmentSpecified = node.Attribute("HorizontalAlignment") != null;
            wrapper.VerticalAlignmentSpecified = node.Attribute("VerticalAlignment") != null;
            if ((string?)node.Attribute("Visibility") is { } initialVisibility) wrapper.Visibility = Enum.Parse<Visibility>(initialVisibility);
            if (wrapper is ListBoxItem)
                foreach (var attribute in node.Attributes().Where(a => a.Name.NamespaceName.Length == 0 && a.Name.LocalName is not ("Name" or "Content"))) SetProperty(wrapper, attribute.Name.LocalName, attribute.Value);
            wrapper.Name = name; scope.Add(name, wrapper); wrappers.Add(node, wrapper);
            if (!customControls.ContainsKey(name)) { wrapper.XamlSourceDirectory = owner.XamlSourceDirectory; wrapper.XamlEventOwner = owner.XamlEventOwner; }
            if (wrapper is TabItem tabItem) tabItem.Header = (string?)node.Attribute("Header");
            foreach(var attribute in node.Attributes().Where(a=>a.Name.LocalName == "Orientation" && wrapper is ProgressBar || a.Name.LocalName is "IsIndeterminate" or "PasswordChar" || a.Name.LocalName=="TextWrapping" && wrapper is TextBox)) SetProperty(wrapper,attribute.Name.LocalName,attribute.Value);
            if (wrapper is WebView2 browserAdapter)
            {
                foreach (var attribute in node.Attributes().Where(a => a.Name.LocalName is "Source" or "DefaultBackgroundColor" or "ZoomFactor" or "AllowExternalDrop")) SetProperty(browserAdapter, attribute.Name.LocalName, attribute.Value);
                if (node.Elements().FirstOrDefault(n => n.Name.LocalName.EndsWith(".CreationProperties", StringComparison.Ordinal))?.Elements().FirstOrDefault() is { } creationNode)
                {
                    var creation = new CoreWebView2CreationProperties();
                    foreach (var attribute in creationNode.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.NamespaceName.Length == 0))
                    {
                        var property = creation.GetType().GetProperty(attribute.Name.LocalName)!;
                        property.SetValue(creation, BindingExpression.Coerce(attribute.Value, property.PropertyType));
                    }
                    browserAdapter.CreationProperties = creation;
                }
            }
            if (wrapper is Window window) window.Title = (string?)node.Attribute("Title") ?? "";
            wrapper.MarkupSetter = (property, value) => parser.SetImportedProperty(native, property, System.Convert.ToString(value, CultureInfo.InvariantCulture));
            if (wrapper is Grid grid) grid.DefinitionsChanged = parser.InvalidateImportedLayout;
            var parentNode = node.Ancestors().FirstOrDefault(wrappers.ContainsKey);
            if (parentNode != null)
            {
                var parent = wrappers[parentNode];
                if (parent is TabItem headerTab && node.Parent?.Name.LocalName == "TabItem.Header") { headerTab.Header = wrapper; parent.LogicalChildren.Add(wrapper); wrapper.Reparent(parent); }
                else if (parent is TabControl tabs && wrapper is TabItem tab) tabs.Items.Add(tab);
                else if (parent is ItemsControl items && wrapper is ListBoxItem) items.Items.Add(wrapper);
                else if (parent is ContextMenu menu) menu.Items.AddExisting(wrapper);
                else if (parent is MenuItem menuItem) menuItem.Items.AddExisting(wrapper);
                else if (parent is RichTextBox rich) { rich.Elements.AddExisting(wrapper); ((global::ProjectZ.Shared.Drawing.UI.Input.RichTextBox)rich.NativeElement).Elements.Add(native); }
                else if (parent is Panel panel) panel.Children.AddExisting(wrapper);
                else { parent.LogicalChildren.Add(wrapper); wrapper.Reparent(parent); }
            }
        }
        foreach (var (node, wrapper) in wrappers)
        {
            if (wrapper is Grid namedGrid)
            {
                foreach (var definitionProperty in node.Elements().Where(n => n.Name.LocalName is "Grid.RowDefinitions" or "Grid.ColumnDefinitions"))
                {
                    var definitions = definitionProperty.Elements().ToArray();
                    for (var definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
                    {
                        var definitionName = (string?)definitions[definitionIndex].Attribute(X + "Name") ?? (string?)definitions[definitionIndex].Attribute("Name");
                        if (definitionName == null) continue;
                        if (scope.ContainsKey(definitionName)) throw new InvalidDataException("Duplicate XAML name " + definitionName);
                        object definition = definitionProperty.Name.LocalName == "Grid.RowDefinitions" ? namedGrid.RowDefinitions[definitionIndex] : namedGrid.ColumnDefinitions[definitionIndex];
                        owner.ObjectNameScope.Add(definitionName, definition);
                    }
                }
            }
            foreach (var element in node.Elements())
            {
                var objectNode = element.Elements().FirstOrDefault();
                if (objectNode == null) continue;
                if (element.Name.LocalName.EndsWith(".RenderTransform", StringComparison.Ordinal)) { wrapper.RenderTransform = LoadTransform(objectNode); RegisterNamedObject(owner, objectNode, wrapper.RenderTransform); }
                if (element.Name.LocalName.EndsWith(".Fill", StringComparison.Ordinal) || element.Name.LocalName.EndsWith(".Background", StringComparison.Ordinal) || element.Name.LocalName.EndsWith(".Foreground", StringComparison.Ordinal) || element.Name.LocalName.EndsWith(".Stroke", StringComparison.Ordinal))
                {
                    var parserBrush = new SceneXamlParser(wrapper.NativeElement.OwnerScene);
                    Media.Brush? brush = objectNode.Name.LocalName switch
                    {
                        "SolidColorBrush" => new Media.SolidColorBrush(Media.Color.FromNative(parserBrush.ReadImportedColor((string?)objectNode.Attribute("Color") ?? "Black"))),
                        "LinearGradientBrush" or "RadialGradientBrush" => Media.GradientBrush.Wrap(parserBrush.ReadImportedGradient(objectNode.ToString())),
                        "ImageBrush" => LoadImageBrush(objectNode),
                        _ => null
                    };
                    if (brush != null) { SetProperty(wrapper, element.Name.LocalName[(element.Name.LocalName.LastIndexOf('.')+1)..], brush); RegisterNamedObject(owner, objectNode, brush); }
                }
                if (element.Name.LocalName.EndsWith(".Clip", StringComparison.Ordinal) && objectNode.Name.LocalName == "RectangleGeometry") { wrapper.Clip = new Media.RectangleGeometry((Rect)BindingExpression.Coerce((string?)objectNode.Attribute("Rect") ?? "0,0,0,0", typeof(Rect))!); RegisterNamedObject(owner, objectNode, wrapper.Clip); }
                if (wrapper is TextBlock text && element.Name.LocalName.EndsWith(".TextDecorations", StringComparison.Ordinal))
                {
                    var decorations = new TextDecorationCollection();
                    foreach (var declaration in element.Descendants().Where(n => n.Name.LocalName == "TextDecoration")) decorations.Add(new TextDecoration { Location = Enum.Parse<TextDecorationLocation>((string?)declaration.Attribute("Location") ?? "Underline") });
                    text.TextDecorations = decorations;
                }
                if (wrapper is Window window && element.Name.LocalName == "WindowChrome.WindowChrome")
                {
                    var chrome = new Shell.WindowChrome();
                    foreach (var attribute in objectNode.Attributes().Where(n => !n.IsNamespaceDeclaration && n.Name.NamespaceName.Length == 0))
                    {
                        var property = chrome.GetType().GetProperty(attribute.Name.LocalName);
                        if (property?.CanWrite == true) property.SetValue(chrome, BindingExpression.Coerce(attribute.Value, property.PropertyType));
                    }
                    Shell.WindowChrome.SetWindowChrome(window, chrome);
                }
            }
            var resources = node.Elements().FirstOrDefault(n => n.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal));
            if (resources != null) LoadResources(wrapper, resources, assembly);
            if (wrapper is ListView listView && node.Elements().FirstOrDefault(n => n.Name.LocalName == "ListView.View")?.Elements().FirstOrDefault() is { } viewNode)
            {
                var view = new GridView { SourceAssembly = assembly };
                foreach (var columnNode in viewNode.Descendants().Where(n => n.Name.LocalName == "GridViewColumn"))
                {
                    var column = new GridViewColumn { Header = (string?)columnNode.Attribute("Header") ?? "", Width = (string?)columnNode.Attribute("Width") is { } width ? double.Parse(width, CultureInfo.InvariantCulture) : double.NaN };
                    if ((string?)columnNode.Attribute("DisplayMemberBinding") is { } binding) { column.DisplayMemberBinding = ParseBinding(binding, wrapper); column.DisplayMemberExpression = binding; }
                    if (columnNode.Elements().FirstOrDefault(n => n.Name.LocalName == "GridViewColumn.CellTemplate")?.Elements().FirstOrDefault()?.Elements().FirstOrDefault() is { } cell) column.CellMarkup = new(cell);
                    view.Columns.Add(column);
                }
                listView.View = view;
            }
            if (wrapper is ItemsControl && wrapper.NativeElement is global::ProjectZ.Shared.Drawing.UI.Input.ListBox list && node.Elements().FirstOrDefault(n => n.Name.LocalName.EndsWith(".ItemsPanel"))?.Elements().FirstOrDefault()?.Elements().FirstOrDefault() is { } panelNode)
            {
                list.ItemsOrientation = Enum.Parse<global::ProjectZ.Shared.Drawing.UI.Layout.Orientation>((string?)panelNode.Attribute("Orientation") ?? "Vertical");
                list.WrapItems = panelNode.Name.LocalName == "WrapPanel";
                if ((string?)panelNode.Attribute("ItemWidth") is { } itemWidth) list.ItemWidth = float.Parse(itemWidth, CultureInfo.InvariantCulture);
                if ((string?)panelNode.Attribute("ItemHeight") is { } itemHeight) list.ItemHeight = float.Parse(itemHeight, CultureInfo.InvariantCulture);
            }
        }
        foreach (var item in documents) ((RichTextBox)wrappers[item.Owner]).Document = (Documents.FlowDocument)DocumentMarkup.Load(item.Document, child => LoadTemplate(new XElement(child), assembly, owner.XamlEventOwner, wrappers[item.Owner]));
        foreach (var item in templates) ((ItemsControl)wrappers[item.Owner]).ItemTemplate = new(item.Content, assembly, owner.XamlEventOwner, wrappers[item.Owner]);
        foreach (var popup in popups)
        {
            var target = wrappers[popup.Owner];
            FrameworkElement content;
            if (popup.IsToolTip)
            {
                var text = string.Join(Environment.NewLine, popup.Content.DescendantsAndSelf().Where(node => node.Name.LocalName is "TextBlock" or "Run" && !node.Ancestors().Any(ancestor => ancestor.Name.LocalName is "DataTemplate" or "ControlTemplate")).Select(node => (string?)node.Attribute("Text") ?? node.Value));
                var tip = new ToolTip { Content = text, ResourceOwner = target };
                if ((string?)popup.Content.Attribute("Placement") is { } placement) tip.Placement = Enum.Parse<Controls.Primitives.PlacementMode>(placement);
                target.ToolTip = tip; target.TrackBinding(tip); content = tip;
            }
            else
            {
                var host = LoadTemplate(popup.Content, assembly, owner.XamlEventOwner, target);
                var menu = host.LogicalChildren.OfType<ContextMenu>().Single();
                target.ContextMenu = menu; target.TrackBinding(host); content = menu;
            }
            if ((string?)popup.Content.Attribute(X + "Name") is { } popupName) { content.Name = popupName; owner.ObjectNameScope![popupName] = content; }
        }
        foreach (var property in adapterProperties.Where(p => p.Property is not ("SelectedIndex" or "SelectedItem" or "SelectedValue" or "IsSelected" or "IsDropDownOpen"))) SetProperty(scope[property.Name], property.Property, property.Value);
        foreach (var property in resourceProperties)
        {
            var target = scope[property.Name]; SetProperty(target, property.Property, Resource(property.Value, target));
        }
        parser.AttachImportedLayout();
        foreach (var (node, wrapper) in wrappers)
        {
            foreach (var property in new[] { "Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan" })
            {
                if ((string?)node.Attribute(property) is not { } text) continue;
                int value = int.Parse(text, CultureInfo.InvariantCulture);
            if (property == "Grid.Row") Grid.SetRow(wrapper, value);
                else if (property == "Grid.Column") Grid.SetColumn(wrapper, value);
                else if (property == "Grid.RowSpan") Grid.SetRowSpan(wrapper, value);
                else Grid.SetColumnSpan(wrapper, value);
            }
            if ((string?)node.Attribute("DockPanel.Dock") is { } dock) DockPanel.SetDock(wrapper, Enum.Parse<Dock>(dock));
        }
        if (parser.UnsupportedFeatures.Count > 0) throw new NotSupportedException(string.Join(Environment.NewLine, parser.UnsupportedFeatures));
        foreach (var expression in expressions)
        {
            var target = scope[expression.Name]; var binding = ParseBinding(expression.Value, target);
            if (AttachedProperty(expression.Property) is { } attached) BindingOperations.SetBinding(target, attached, binding);
            else BindingOperations.SetBinding(target, expression.Property, binding);
        }
        foreach (var property in adapterProperties.Where(p => p.Property is "SelectedIndex" or "SelectedItem" or "SelectedValue" or "IsSelected" or "IsDropDownOpen")) SetProperty(scope[property.Name], property.Property, property.Value);
        foreach (var (node, wrapper) in wrappers)
            foreach (var trigger in node.Elements().Where(n => n.Name.LocalName.EndsWith(".Triggers", StringComparison.Ordinal)).SelectMany(n => n.Elements()).Where(n => n.Name.LocalName is "DataTrigger" or "Trigger"))
                wrapper.TrackBinding(new NativeDataTrigger(wrapper, trigger, parser));
        foreach (var binding in templateEvents) BindEvent(scope[binding.Name], binding.Event, owner.XamlEventOwner!, binding.Handler);
    }
    static void BindEvent(FrameworkElement element, string eventName, object eventOwner, string handlerName)
    {
        var simpleName = Capabilities.EventName(eventName)!;
        bool attached = eventName.Contains('.');
        var declaringType = element.GetType();
        if (attached)
        {
            var ownerName = eventName[..eventName.LastIndexOf('.')];
            ownerName = ownerName switch { "TextBoxBase" => "TextBox", "ButtonBase" => "Button", "ToggleButton" => "CheckBox", "Selector" => "ItemsControl", "RangeBase" => "Slider", _ => ownerName };
            declaringType = typeof(FrameworkElement).Assembly.GetType(Capabilities.Prefix + (ownerName is "UIElement" or "FrameworkElement" ? "." : ".Controls.") + ownerName) ?? throw new NotSupportedException("Event owner " + ownerName);
        }
        var eventInfo = declaringType.GetEvent(simpleName) ?? throw new NotSupportedException($"XAML event {declaringType.Name}.{simpleName}");
        var parameters = eventInfo.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Select(p => p.ParameterType).ToArray();
        MethodInfo? method = null;
        for (var type = eventOwner.GetType(); type != null && method == null; type = type.BaseType)
            method = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(candidate => candidate.Name.Equals(handlerName, StringComparison.OrdinalIgnoreCase) && candidate.ReturnType == typeof(void))
                .Where(candidate => candidate.GetParameters().Length == parameters.Length && candidate.GetParameters().Select((p, i) => p.ParameterType.IsAssignableFrom(parameters[i])).All(value => value))
                .OrderByDescending(candidate => candidate.GetParameters().Select((p, i) => p.ParameterType == parameters[i]).Count(value => value)).FirstOrDefault();
        if (method == null) throw new MissingMethodException(eventOwner.GetType().FullName, handlerName);
        var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType!, eventOwner, method);
        if (attached)
        {
            element.AddHandler(RoutedEvent.For(simpleName), handler);
            return;
        }
        eventInfo.AddEventHandler(element, handler);
    }
    internal static void SetProperty(FrameworkElement target, string property, object? value)
    {
        if (property == "TextSearch.TextPath" && target is ItemsControl items) { if (items.DisplayMemberPath.Length == 0) items.DisplayMemberPath = value?.ToString() ?? ""; return; }
        if (Equals(value, "{x:Null}")) value = null;
        if (value is string resource && resource.StartsWith("{StaticResource ")) value = Resource(resource, target);
        if (target is Image && property == "Source" && value is string imagePath && !Path.IsPathRooted(imagePath) && !Uri.TryCreate(imagePath, UriKind.Absolute, out _))
        {
            // Relative resource paths may be beside the XAML, beside its project
            // directory, or at the executable's preserved deployment path.
            var directories = new[] { target.XamlSourceDirectory, Path.GetDirectoryName(target.XamlSourceDirectory), AppContext.BaseDirectory };
            value = directories.Where(d => d != null).Select(d => Path.GetFullPath(Path.Combine(d!, imagePath))).FirstOrDefault(File.Exists) ?? imagePath;
        }
        if (AttachedProperty(property) is { } attached) { target.SetValue(attached, BindingExpression.Coerce(value, attached.PropertyType)); return; }
        var member = target.GetType().GetProperty(property) ?? throw new NotSupportedException($"XAML target {target.GetType().Name}.{property}");
        member.SetValue(target, BindingExpression.Coerce(value, member.PropertyType));
    }
    static DependencyProperty? AttachedProperty(string property) => property switch
    {
        "Panel.ZIndex"=>Panel.ZIndexProperty,
        "ScrollViewer.HorizontalScrollBarVisibility" => ScrollViewer.HorizontalScrollBarVisibilityProperty,
        "ScrollViewer.VerticalScrollBarVisibility" => ScrollViewer.VerticalScrollBarVisibilityProperty,
        "ScrollViewer.CanContentScroll" => ScrollViewer.CanContentScrollProperty,
        "AutomationProperties.Name" => Automation.AutomationProperties.NameProperty,
        "AutomationProperties.HelpText" => Automation.AutomationProperties.HelpTextProperty,
        _ => null
    };
    static Media.Transform LoadTransform(XElement node)
    {
        double Number(string property, double fallback = 0) => (string?)node.Attribute(property) is { } text ? double.Parse(text, CultureInfo.InvariantCulture) : fallback;
        if (node.Name.LocalName == "RotateTransform") return new Media.RotateTransform(Number("Angle"), Number("CenterX"), Number("CenterY"));
        if (node.Name.LocalName == "ScaleTransform") return new Media.ScaleTransform(Number("ScaleX", 1), Number("ScaleY", 1)) { CenterX = Number("CenterX"), CenterY = Number("CenterY") };
        if (node.Name.LocalName == "TranslateTransform") return new Media.TranslateTransform(Number("X"), Number("Y"));
        if (node.Name.LocalName == "TransformGroup")
        {
            var group = new Media.TransformGroup();
            foreach (var child in node.Elements().SelectMany(n => n.Name.LocalName.Contains('.') ? n.Elements() : new[] { n })) group.Children.Add(LoadTransform(child));
            return group;
        }
        throw new NotSupportedException("Transform type " + node.Name);
    }
    static void RegisterNamedObject(FrameworkElement owner, XElement declaration, object value)
    {
        while (owner.NameScope == null && owner.Parent != null) owner = owner.Parent;
        var name = (string?)declaration.Attribute(X + "Name");
        if (name != null) { owner.ObjectNameScope ??= new(StringComparer.Ordinal); owner.ObjectNameScope[name] = value; }
        if (value is Media.GradientBrush gradient)
            foreach (var pair in declaration.Descendants().Where(node => node.Name.LocalName == "GradientStop").Zip(gradient.GradientStops)) RegisterNamedObject(owner, pair.First, pair.Second);
        if (value is Media.TransformGroup group)
            foreach (var pair in declaration.Elements().SelectMany(node => node.Name.LocalName.Contains('.') ? node.Elements() : new[] { node }).Zip(group.Children)) RegisterNamedObject(owner, pair.First, pair.Second);
    }
    static Media.ImageBrush LoadImageBrush(XElement node)
    {
        var brush = new Media.ImageBrush();
        foreach (var attribute in node.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.NamespaceName.Length == 0))
            if (brush.GetType().GetProperty(attribute.Name.LocalName) is { CanWrite: true } property) property.SetValue(brush, BindingExpression.Coerce(attribute.Value, property.PropertyType));
        return brush;
    }
    static void LoadResources(FrameworkElement owner, XElement resources, Assembly assembly, HashSet<string>? loading = null)
    {
        loading ??= new(StringComparer.OrdinalIgnoreCase);
        foreach (var merged in resources.Elements().Where(n => n.Name.LocalName == "ResourceDictionary.MergedDictionaries").SelectMany(n => n.Elements())) LoadResources(owner, merged, assembly, loading);
        if (resources.Name.LocalName == "ResourceDictionary" && (string?)resources.Attribute("Source") is { } dictionarySource)
        {
            var directory = owner.XamlSourceDirectory ?? AppContext.BaseDirectory;
            var path = Path.GetFullPath(Path.Combine(directory, dictionarySource));
            if (!loading.Add(path)) throw new InvalidDataException("Circular merged resource dictionary " + path);
            var dictionary = XDocument.Load(path, LoadOptions.SetBaseUri).Root ?? throw new InvalidDataException("Empty resource dictionary " + path);
            var oldDirectory = owner.XamlSourceDirectory;
            owner.XamlSourceDirectory = Path.GetDirectoryName(path);
            try { LoadResources(owner, dictionary, assembly, loading); } finally { owner.XamlSourceDirectory = oldDirectory; loading.Remove(path); }
        }
        foreach (var node in resources.Elements())
        {
            if (node.Name.LocalName == "ResourceDictionary") { LoadResources(owner, node, assembly, loading); continue; }
            var key = (string?)node.Attribute(X + "Key");
            if (key != null || node.Name.LocalName == "Style") owner.ResourceMarkup[key ?? "implicit:" + (string?)node.Attribute("TargetType")] = new XElement(node);
            if (key == null) continue;
            object value;
            if (node.Name.LocalName == "SolidColorBrush")
            {
                var parser = new SceneXamlParser(Application.Current.ControlScene);
                value = new Media.SolidColorBrush(Media.Color.FromNative(parser.ReadImportedColor((string?)node.Attribute("Color") ?? "Black")));
            }
            else if (node.Name.LocalName is "LinearGradientBrush" or "RadialGradientBrush")
                value = Media.GradientBrush.Wrap(new SceneXamlParser(Application.Current.ControlScene).ReadImportedGradient(node.ToString()));
            else if (node.Name.LocalName == "ImageBrush") value = LoadImageBrush(node);
            else if (node.Name.LocalName == "DataTemplate") value = new DataTemplate(new XElement(node), assembly, owner.XamlEventOwner, owner);
            else if(node.Name.LocalName=="Style")
            {
                var name=((string?)node.Attribute("TargetType")??"").Replace("{x:Type ","").TrimEnd('}');
                var type=typeof(FrameworkElement).Assembly.GetType(Capabilities.Prefix+(name=="Window"?".":".Controls.")+name)??typeof(Controls.EmptyControl);
                var style=new Style(type);
                if((string?)node.Attribute("BasedOn") is { } parent) style.BasedOn=Resource(parent,owner) as Style;
                foreach(var setter in node.Elements().Where(n=>n.Name.LocalName=="Setter" && n.Attribute("Value")!=null))
                {
                    var property=(string?)setter.Attribute("Property")??"";if(!type.GetProperties().Any(p=>p.Name==property)) continue;
                    var dp=type.GetField(property+"Property",BindingFlags.Static|BindingFlags.Public|BindingFlags.FlattenHierarchy)?.GetValue(null) as DependencyProperty;
                    if(dp==null) continue;
                    var text=(string?)setter.Attribute("Value");
                    if(text?.StartsWith("{StaticResource ")==true && owner.TryFindResource(text[16..^1].Trim())==null) continue;
                    style.Setters.Add(new Setter(dp,BindingExpression.Coerce(Resource(text??"",owner),dp.PropertyType)));
                }
                if(owner.XamlSourceDirectory!=null && Uri.TryCreate(node.BaseUri,UriKind.Absolute,out var sourceUri) && sourceUri.IsFile) style.ConnectNativeResource(key,sourceUri.LocalPath);
                value=style;
            }
            else if (node.Name.LocalName == "BlurEffect") value = new Media.Effects.BlurEffect { Radius = Number("Radius", 5) };
            else if (node.Name.LocalName is "RotateTransform" or "ScaleTransform" or "TranslateTransform" or "TransformGroup") value = LoadTransform(node);
            else if (node.Name.LocalName == "FlowDocument") value = DocumentMarkup.Load(node, child => LoadTemplate(new XElement(child), assembly, owner.XamlEventOwner, owner));
            else if (node.Name.LocalName == "RectangleGeometry") value = new Media.RectangleGeometry((Rect)BindingExpression.Coerce((string?)node.Attribute("Rect") ?? "0,0,0,0", typeof(Rect))!);
            else if (node.Name.LocalName == "DropShadowEffect") value = new Media.Effects.DropShadowEffect { BlurRadius = Number("BlurRadius", 5), ShadowDepth = Number("ShadowDepth", 5), Direction = Number("Direction", 315), Opacity = Number("Opacity", 1), Color = Media.Color.FromNative(new SceneXamlParser(Application.Current.ControlScene).ReadImportedColor((string?)node.Attribute("Color") ?? "Black")) };
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
            RegisterNamedObject(owner, node, value);
            owner.Resources[key] = value;
            double Number(string property, double fallback) => (string?)node.Attribute(property) is { } text ? double.Parse(text, CultureInfo.InvariantCulture) : fallback;
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
                case "StringFormat": binding.StringFormat=pair[1].Trim('\'','"'); if(binding.StringFormat.StartsWith("{}")) binding.StringFormat=binding.StringFormat[2..]; break;
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
