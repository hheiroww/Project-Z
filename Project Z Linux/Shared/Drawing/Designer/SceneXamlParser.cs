using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing.UI.Layout;
using ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.Shared.Drawing.Designer {

    /// <summary>
    /// Parses XAML-like markup and creates Project Z UI elements.
    /// Supports a subset of WPF XAML syntax adapted for Project Z.
    /// </summary>
    public class SceneXamlParser {

        #region Constants

        private const string NAMESPACE_PREFIX = "pz";

        #endregion

        #region Fields

        private readonly Scene _scene;
        private readonly Dictionary<string, Func<XmlElement, SceneElement>> _elementFactories = new Dictionary<string, Func<XmlElement, SceneElement>>();
        private readonly Dictionary<string, SceneElement> _namedElements = new Dictionary<string, SceneElement>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the dictionary of named elements (elements with x:Name attribute).
        /// </summary>
        public Dictionary<string, SceneElement> NamedElements {
            get {
                return _namedElements;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new XAML parser for the specified scene.
        /// </summary>
        public SceneXamlParser(Scene scene) {
            _scene = scene;
            RegisterElementFactories();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Parses XAML markup and returns the root element.
        /// </summary>
        public SceneElement Parse(string xaml) {
            var doc = new XmlDocument();
            doc.LoadXml(xaml);
            return ParseElement(doc.DocumentElement);
        }

        /// <summary>
        /// Parses XAML from a file and returns the root element.
        /// </summary>
        public SceneElement ParseFile(string filePath) {
            string xaml = File.ReadAllText(filePath);
            return Parse(xaml);
        }

        /// <summary>
        /// Gets a named element by its x:Name.
        /// </summary>
        public T FindName<T>(string name) where T : SceneElement {
            if (_namedElements.ContainsKey(name)) {
                return (T)_namedElements[name];
            }
            return null;
        }

        #endregion

        #region Element Factory Registration

        private void RegisterElementFactories() {
            // Root elements
            _elementFactories["Scene"] = new Func<XmlElement, SceneElement>(e => CreateSceneRoot(e));
            _elementFactories["Window"] = new Func<XmlElement, SceneElement>(e => CreateSceneRoot(e));
            _elementFactories["UserControl"] = new Func<XmlElement, SceneElement>(e => CreateSceneRoot(e));
            _elementFactories["Page"] = new Func<XmlElement, SceneElement>(e => CreateSceneRoot(e));

            // Primitives
            _elementFactories["Rectangle"] = new Func<XmlElement, SceneElement>(e => CreateRectangle(e));
            _elementFactories["RectangleElement"] = new Func<XmlElement, SceneElement>(e => CreateRectangle(e));
            _elementFactories["Circle"] = new Func<XmlElement, SceneElement>(e => CreateCircle(e));
            _elementFactories["CircleElement"] = new Func<XmlElement, SceneElement>(e => CreateCircle(e));
            _elementFactories["Text"] = new Func<XmlElement, SceneElement>(e => CreateText(e));
            _elementFactories["TextElement"] = new Func<XmlElement, SceneElement>(e => CreateText(e));
            _elementFactories["TextBlock"] = new Func<XmlElement, SceneElement>(e => CreateText(e));

            // Input Controls
            _elementFactories["Button"] = new Func<XmlElement, SceneElement>(e => CreateButton(e));
            _elementFactories["CheckBox"] = new Func<XmlElement, SceneElement>(e => CreateCheckBox(e));
            _elementFactories["RadioButton"] = new Func<XmlElement, SceneElement>(e => CreateRadioButton(e));
            _elementFactories["TextBox"] = new Func<XmlElement, SceneElement>(e => CreateTextBox(e));
            _elementFactories["Slider"] = new Func<XmlElement, SceneElement>(e => CreateSlider(e));
            _elementFactories["Trackbar"] = new Func<XmlElement, SceneElement>(e => CreateSlider(e));
            _elementFactories["ProgressBar"] = new Func<XmlElement, SceneElement>(e => CreateProgressBar(e));

            // Advanced Controls
            _elementFactories["ComboBox"] = new Func<XmlElement, SceneElement>(e => CreateComboBox(e));
            _elementFactories["ListBox"] = new Func<XmlElement, SceneElement>(e => CreateListBox(e));
            _elementFactories["TabControl"] = new Func<XmlElement, SceneElement>(e => CreateTabControl(e));
            _elementFactories["ScrollViewer"] = new Func<XmlElement, SceneElement>(e => CreateScrollViewer(e));
            _elementFactories["Separator"] = new Func<XmlElement, SceneElement>(e => CreateSeparator(e));
            _elementFactories["Expander"] = new Func<XmlElement, SceneElement>(e => CreateExpander(e));
            _elementFactories["ToolTip"] = new Func<XmlElement, SceneElement>(e => CreateToolTip(e));
            _elementFactories["GroupBox"] = new Func<XmlElement, SceneElement>(e => CreateGroupBox(e));
            _elementFactories["NumericUpDown"] = new Func<XmlElement, SceneElement>(e => CreateNumericUpDown(e));

            // Layout
            _elementFactories["StackPanel"] = new Func<XmlElement, SceneElement>(e => CreateStackPanel(e));
            _elementFactories["Grid"] = new Func<XmlElement, SceneElement>(e => CreateGrid(e));
            _elementFactories["Panel"] = new Func<XmlElement, SceneElement>(e => CreatePanel(e));
            _elementFactories["Border"] = new Func<XmlElement, SceneElement>(e => CreateBorder(e));
        }

        #endregion

        #region Element Parsing

        private SceneElement ParseElement(XmlElement xmlElement) {
            string elementName = xmlElement.LocalName;

            // Check if we have a factory for this element
            if (!_elementFactories.ContainsKey(elementName)) {
                throw new NotSupportedException($"Unknown element type: {elementName}");
            }

            var element = _elementFactories[elementName](xmlElement);

            // Handle x:Name attribute
            string nameAttr = xmlElement.GetAttribute("x:Name");
            if (string.IsNullOrEmpty(nameAttr)) {
                nameAttr = xmlElement.GetAttribute("Name");
            }
            if (!string.IsNullOrEmpty(nameAttr)) {
                _namedElements[nameAttr] = element;
            }

            // Parse common properties
            ApplyCommonProperties(element, xmlElement);

            // Parse children with proper z-index assignment
            // WPF behavior: elements later in XAML appear on top (higher z-order)
            // childIndex increases for each child, so later elements get higher z-index
            int childIndex = 0;
            foreach (XmlNode childNode in xmlElement.ChildNodes) {
                if (childNode is XmlElement) {
                    XmlElement childXml = (XmlElement)childNode;
                    // Skip property elements (e.g., Grid.RowDefinitions)
                    if (!childXml.LocalName.Contains(".")) {
                        var childElement = ParseElement(childXml);
                        // Assign z-index based on document order if not explicitly set via Panel.ZIndex
                        // This matches WPF behavior where later elements appear on top
                        if (!childXml.HasAttribute("ZIndex") && !childXml.HasAttribute("Panel.ZIndex")) {
                            childElement.zIndex = childIndex;
                        }
                        childElement.Parent = element;
                        // For Grid parents, use AddChild to set attached properties
                        if (element is Grid) {
                            Grid grid = (Grid)element;
                            int gridRow = GetAttributeInteger(childXml, "Grid.Row", 0);
                            int gridCol = GetAttributeInteger(childXml, "Grid.Column", 0);
                            int gridRowSpan = GetAttributeInteger(childXml, "Grid.RowSpan", 1);
                            int gridColSpan = GetAttributeInteger(childXml, "Grid.ColumnSpan", 1);
                            grid.AddChild(childElement, gridRow, gridCol, gridRowSpan, gridColSpan);
                        } else {
                            element.Children.Add(childElement);
                        }
                        // Re-trigger text wrapping now that parent is assigned,
                        // so wrap width can be derived from parent bounds.
                        if (childElement is TextElement) {
                            TextElement textEl = (TextElement)childElement;
                            textEl.Text = textEl.Text;
                        }
                        childIndex += 1;
                    }
                }
            }

            return element;
        }

        private void ApplyCommonProperties(SceneElement element, XmlElement xmlElement) {
            // --- Position: Canvas.Left/Top take precedence over X/Y ---
            // These values are incorporated into the element's Margin so the
            // layout system (AlignChild) uses them as positional offsets.
            float x = 0f;
            float y = 0f;
            if (xmlElement.HasAttribute("Canvas.Left")) {
                x = GetAttributeSingle(xmlElement, "Canvas.Left", 0f);
            } else if (xmlElement.HasAttribute("X")) {
                x = GetAttributeSingle(xmlElement, "X", 0f);
            }
            if (xmlElement.HasAttribute("Canvas.Top")) {
                y = GetAttributeSingle(xmlElement, "Canvas.Top", 0f);
            } else if (xmlElement.HasAttribute("Y")) {
                y = GetAttributeSingle(xmlElement, "Y", 0f);
            }

            // Set initial position for immediate use before layout runs
            element.Position = new Vector2(x, y);

            // --- Size: Always set if present, even if zero ---
            bool widthSet = xmlElement.HasAttribute("Width");
            bool heightSet = xmlElement.HasAttribute("Height");
            float width = GetAttributeSingle(xmlElement, "Width", element.Size.X);
            float height = GetAttributeSingle(xmlElement, "Height", element.Size.Y);
            if (widthSet | heightSet) {
                element.Size = new Vector2(width, height);
            }

            // --- Min/Max size (optional, for stricter WPF compatibility) ---
            if (xmlElement.HasAttribute("MinWidth")) {
                element.MinSize = new Vector2(GetAttributeSingle(xmlElement, "MinWidth", element.MinSize.X), element.MinSize.Y);
            }
            if (xmlElement.HasAttribute("MinHeight")) {
                element.MinSize = new Vector2(element.MinSize.X, GetAttributeSingle(xmlElement, "MinHeight", element.MinSize.Y));
            }
            if (xmlElement.HasAttribute("MaxWidth")) {
                element.MaxSize = new Vector2(GetAttributeSingle(xmlElement, "MaxWidth", element.MaxSize.X), element.MaxSize.Y);
            }
            if (xmlElement.HasAttribute("MaxHeight")) {
                element.MaxSize = new Vector2(element.MaxSize.X, GetAttributeSingle(xmlElement, "MaxHeight", element.MaxSize.Y));
            }

            // Visibility
            string visibility = GetAttributeString(xmlElement, "Visibility", "Visible");
            element.isVisible = visibility.ToLower() != "collapsed" && visibility.ToLower() != "hidden";

            // IsEnabled
            element.isEnabled = GetAttributeBoolean(xmlElement, "IsEnabled", true);

            // Margin - combine XAML Margin with X/Y position offsets
            // The layout system (AlignChild) uses Margin.Left/Top for positioning,
            // so X/Y/Canvas.Left/Canvas.Top must be incorporated here.
            string marginStr = GetAttributeString(xmlElement, "Margin", "");
            Thickness margin;
            if (!string.IsNullOrEmpty(marginStr)) {
                margin = ParseThickness(marginStr);
            } else {
                margin = new Thickness(0);
            }
            if (x != 0f || y != 0f) {
                margin = new Thickness(margin.Left + (int)Math.Round(x), margin.Top + (int)Math.Round(y), margin.Right, margin.Bottom);
            }
            element.Margin = margin;

            // Padding
            string paddingStr = GetAttributeString(xmlElement, "Padding", "");
            if (!string.IsNullOrEmpty(paddingStr)) {
                element.Padding = ParseThickness(paddingStr);
            }

            // Alignment
            string hAlign = GetAttributeString(xmlElement, "HorizontalAlignment", "");
            if (!string.IsNullOrEmpty(hAlign)) {
                element.HorizontalAlign = ParseHorizontalAlignment(hAlign);
            }

            string vAlign = GetAttributeString(xmlElement, "VerticalAlignment", "");
            if (!string.IsNullOrEmpty(vAlign)) {
                element.VerticalAlign = ParseVerticalAlignment(vAlign);
            }

            // --- Stretch support: if alignment is Stretch and no explicit size, fill parent ---
            if ((hAlign.ToLower() == "stretch" | vAlign.ToLower() == "stretch") & !(widthSet | heightSet)) {
                // For Canvas, stretching means fill available space (parent size minus margin)
                // This requires parent size, so you may need to handle this after tree is built.
                element.StretchToParent = true;
            }

            // ZIndex
            element.zIndex = GetAttributeInteger(xmlElement, "ZIndex", element.zIndex);
            if (xmlElement.HasAttribute("Panel.ZIndex")) {
                element.zIndex = GetAttributeInteger(xmlElement, "Panel.ZIndex", element.zIndex);
            }
        }

        #endregion

        #region Element Creators

        /// <summary>
        /// Creates a root container element for the Scene.
        /// This is a virtual element that holds all child elements.
        /// </summary>
        private SceneElement CreateSceneRoot(XmlElement xmlElement) {
            // Create a transparent container to hold scene children
            var root = new RectangleElement(_scene);
            root.BackgroundColor = Color.Transparent;
            root.isMouseBypassEnabled = true;
            root.Padding = new Thickness(0);

            // Set root size from explicit attributes or fall back to viewport size
            // so child text elements can derive wrap width from parent bounds.
            float rootWidth = GetAttributeSingle(xmlElement, "Width", 0f);
            float rootHeight = GetAttributeSingle(xmlElement, "Height", 0f);
            if (rootWidth <= 0f || rootHeight <= 0f) {
                try {
                    if (rootWidth <= 0f)
                        rootWidth = _scene.graphicsDevice.Viewport.Width;
                    if (rootHeight <= 0f)
                        rootHeight = _scene.graphicsDevice.Viewport.Height;
                } catch {
                }
            }
            if (rootWidth > 0f || rootHeight > 0f) {
                root.Size = new Vector2(rootWidth, rootHeight);
            }

            return root;
        }

        private SceneElement CreateRectangle(XmlElement xmlElement) {
            var rect = new RectangleElement(_scene);
            var bgColor = GetAttributeColor(xmlElement, "Background", new Color(50, 50, 50));
            if (xmlElement.HasAttribute("BackgroundColor")) {
                bgColor = GetAttributeColor(xmlElement, "BackgroundColor", bgColor);
            }
            rect.BackgroundColor = bgColor;
            return rect;
        }

        private SceneElement CreateCircle(XmlElement xmlElement) {
            var circle = new CircleElement(_scene);
            circle.FillColor = GetAttributeColor(xmlElement, "Fill", Color.White);
            if (xmlElement.HasAttribute("FillColor")) {
                circle.FillColor = GetAttributeColor(xmlElement, "FillColor", circle.FillColor);
            }
            return circle;
        }

        private SceneElement CreateText(XmlElement xmlElement) {
            var text = new TextElement(_scene);

            // Set wrapping parameters BEFORE text content so the initial
            // UpdateWrappedText uses the correct wrap width.
            string wrappingStr = GetAttributeString(xmlElement, "TextWrapping", "Wrap");
            text.TextWrapping = ParseTextWrapping(wrappingStr);

            // Use MaxWidth if specified, otherwise use Width as the wrapping boundary
            float maxWidth = GetAttributeSingle(xmlElement, "MaxWidth", 0f);
            if (maxWidth <= 0f) {
                maxWidth = GetAttributeSingle(xmlElement, "Width", 0f);
            }
            text.MaxWidth = maxWidth;

            text.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", Color.White);
            if (xmlElement.HasAttribute("ForegroundColor")) {
                text.ForegroundColor = GetAttributeColor(xmlElement, "ForegroundColor", text.ForegroundColor);
            }
            string fontName = GetAttributeString(xmlElement, "Font", "");
            if (!string.IsNullOrEmpty(fontName)) {
                text.Font = fontName;
            }

            // Set text last so wrapping uses the correct parameters
            text.Text = GetAttributeString(xmlElement, "Text", xmlElement.InnerText.Trim());
            return text;
        }

        private SceneElement CreateButton(XmlElement xmlElement) {
            var button = new Button(_scene);

            // Disable auto-sizing when explicit dimensions are specified in XAML,
            // otherwise DoAutoSize() overrides the explicit Width/Height.
            if (xmlElement.HasAttribute("Width") || xmlElement.HasAttribute("Height")) {
                button.AutoSize = ButtonAutoSize.None;
            }

            button.Text = GetAttributeString(xmlElement, "Content", xmlElement.InnerText.Trim());
            if (xmlElement.HasAttribute("Text")) {
                button.Text = GetAttributeString(xmlElement, "Text", button.Text);
            }
            button.BackgroundColor = GetAttributeColor(xmlElement, "Background", button.BackgroundColor);
            button.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", button.ForegroundColor);
            button.MouseOverBackgroundColor = GetAttributeColor(xmlElement, "MouseOverBackground", button.MouseOverBackgroundColor);
            button.MouseDownBackgroundColor = GetAttributeColor(xmlElement, "MouseDownBackground", button.MouseDownBackgroundColor);
            return button;
        }

        private SceneElement CreateCheckBox(XmlElement xmlElement) {
            var checkBox = new CheckBox(_scene);
            checkBox.Content = GetAttributeString(xmlElement, "Content", xmlElement.InnerText.Trim());
            checkBox.IsChecked = GetAttributeBoolean(xmlElement, "IsChecked", false);
            checkBox.IsThreeState = GetAttributeBoolean(xmlElement, "IsThreeState", false);
            checkBox.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", checkBox.ForegroundColor);
            return checkBox;
        }

        private SceneElement CreateRadioButton(XmlElement xmlElement) {
            var radioButton = new RadioButton(_scene);
            radioButton.Content = GetAttributeString(xmlElement, "Content", xmlElement.InnerText.Trim());
            radioButton.GroupName = GetAttributeString(xmlElement, "GroupName", "");
            radioButton.IsChecked = GetAttributeBoolean(xmlElement, "IsChecked", false);
            radioButton.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", radioButton.ForegroundColor);
            return radioButton;
        }

        private SceneElement CreateTextBox(XmlElement xmlElement) {
            var textBox = new Textbox(_scene);
            textBox.Text = GetAttributeString(xmlElement, "Text", "");
            textBox.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", textBox.ForegroundColor);
            textBox.BackgroundColor = GetAttributeColor(xmlElement, "Background", textBox.BackgroundColor);
            textBox.AcceptsReturn = GetAttributeBoolean(xmlElement, "AcceptsReturn", true);
            return textBox;
        }

        private SceneElement CreateSlider(XmlElement xmlElement) {
            var slider = new Trackbar(_scene);
            slider.MinimumValue = GetAttributeDouble(xmlElement, "Minimum", 0d);
            slider.MaximumValue = GetAttributeDouble(xmlElement, "Maximum", 100d);
            slider.Value = GetAttributeDouble(xmlElement, "Value", 0d);
            return slider;
        }

        private SceneElement CreateProgressBar(XmlElement xmlElement) {
            var progressBar = new ProgressBar(_scene);
            progressBar.Minimum = GetAttributeDouble(xmlElement, "Minimum", 0d);
            progressBar.Maximum = GetAttributeDouble(xmlElement, "Maximum", 100d);
            progressBar.Value = GetAttributeDouble(xmlElement, "Value", 0d);
            progressBar.IsIndeterminate = GetAttributeBoolean(xmlElement, "IsIndeterminate", false);
            progressBar.FillColor = GetAttributeColor(xmlElement, "Foreground", progressBar.FillColor);
            progressBar.TrackColor = GetAttributeColor(xmlElement, "Background", progressBar.TrackColor);
            return progressBar;
        }

        private SceneElement CreateStackPanel(XmlElement xmlElement) {
            var stackPanel = new StackPanel(_scene);
            string orientationStr = GetAttributeString(xmlElement, "Orientation", "Vertical");
            stackPanel.Orientation = orientationStr.ToLower() == "horizontal" ? Orientation.Horizontal : Orientation.Vertical;
            stackPanel.Spacing = GetAttributeSingle(xmlElement, "Spacing", 4.0f);
            stackPanel.BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent);
            return stackPanel;
        }

        private SceneElement CreateGrid(XmlElement xmlElement) {
            var grid = new Grid(_scene);
            grid.BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent);
            grid.ShowGridLines = GetAttributeBoolean(xmlElement, "ShowGridLines", false);

            // Parse row definitions
            var rowDefsNode = GetPropertyElement(xmlElement, "Grid.RowDefinitions");
            if (rowDefsNode != null) {
                foreach (XmlNode rowNode in rowDefsNode.ChildNodes) {
                    if (rowNode is XmlElement && rowNode.LocalName == "RowDefinition") {
                        XmlElement rowXml = (XmlElement)rowNode;
                        grid.RowDefinitions.Add(ParseGridDefinition(rowXml, "Height"));
                    }
                }
            }

            // Parse column definitions
            var colDefsNode = GetPropertyElement(xmlElement, "Grid.ColumnDefinitions");
            if (colDefsNode != null) {
                foreach (XmlNode colNode in colDefsNode.ChildNodes) {
                    if (colNode is XmlElement && colNode.LocalName == "ColumnDefinition") {
                        XmlElement colXml = (XmlElement)colNode;
                        grid.ColumnDefinitions.Add(ParseGridDefinition(colXml, "Width"));
                    }
                }
            }

            return grid;
        }

        private SceneElement CreatePanel(XmlElement xmlElement) {
            var panel = new RectangleElement(_scene);
            panel.BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent);
            return panel;
        }

        private SceneElement CreateBorder(XmlElement xmlElement) {
            var border = new RectangleElement(_scene);
            border.BackgroundColor = GetAttributeColor(xmlElement, "Background", new Color(50, 50, 50));
            return border;
        }

        private SceneElement CreateComboBox(XmlElement xmlElement) {
            var comboBox = new ComboBox(_scene);
            comboBox.BackgroundColor = GetAttributeColor(xmlElement, "Background", comboBox.BackgroundColor);

            // Parse items from ComboBox.Items child element
            var itemsNode = GetPropertyElement(xmlElement, "ComboBox.Items");
            if (itemsNode != null) {
                foreach (XmlNode itemNode in itemsNode.ChildNodes) {
                    if (itemNode is XmlElement) {
                        XmlElement itemXml = (XmlElement)itemNode;
                        if (itemXml.LocalName == "ComboBoxItem") {
                            comboBox.AddItem(GetAttributeString(itemXml, "Content", itemXml.InnerText.Trim()));
                        }
                    }
                }
            }

            int selectedIndex = GetAttributeInteger(xmlElement, "SelectedIndex", -1);
            if (selectedIndex >= 0) {
                comboBox.SelectedIndex = selectedIndex;
            }

            return comboBox;
        }

        private SceneElement CreateListBox(XmlElement xmlElement) {
            var listBox = new ListBox(_scene);
            listBox.BackgroundColor = GetAttributeColor(xmlElement, "Background", listBox.BackgroundColor);
            listBox.SelectionColor = GetAttributeColor(xmlElement, "SelectionBackground", listBox.SelectionColor);
            listBox.ItemHeight = GetAttributeSingle(xmlElement, "ItemHeight", listBox.ItemHeight);

            // Parse items from ListBox.Items child element
            var itemsNode = GetPropertyElement(xmlElement, "ListBox.Items");
            if (itemsNode != null) {
                foreach (XmlNode itemNode in itemsNode.ChildNodes) {
                    if (itemNode is XmlElement) {
                        XmlElement itemXml = (XmlElement)itemNode;
                        if (itemXml.LocalName == "ListBoxItem") {
                            listBox.AddItem(GetAttributeString(itemXml, "Content", itemXml.InnerText.Trim()));
                        }
                    }
                }
            }

            int selectedIndex = GetAttributeInteger(xmlElement, "SelectedIndex", -1);
            if (selectedIndex >= 0) {
                listBox.SelectedIndex = selectedIndex;
            }

            return listBox;
        }

        private SceneElement CreateTabControl(XmlElement xmlElement) {
            var tabControl = new TabControl(_scene);
            tabControl.BackgroundColor = GetAttributeColor(xmlElement, "Background", tabControl.BackgroundColor);
            tabControl.TabHeaderHeight = GetAttributeSingle(xmlElement, "TabHeaderHeight", tabControl.TabHeaderHeight);

            // Tabs are parsed as children with special handling
            return tabControl;
        }

        private SceneElement CreateScrollViewer(XmlElement xmlElement) {
            var scrollViewer = new ScrollViewer(_scene);
            scrollViewer.BackgroundColor = GetAttributeColor(xmlElement, "Background", scrollViewer.BackgroundColor);

            string vScroll = GetAttributeString(xmlElement, "VerticalScrollBarVisibility", "Auto");
            scrollViewer.CanScrollVertically = vScroll.ToLower() != "disabled";

            string hScroll = GetAttributeString(xmlElement, "HorizontalScrollBarVisibility", "Disabled");
            scrollViewer.CanScrollHorizontally = hScroll.ToLower() != "disabled";

            return scrollViewer;
        }

        private SceneElement CreateSeparator(XmlElement xmlElement) {
            var separator = new Separator(_scene);
            separator.BackgroundColor = GetAttributeColor(xmlElement, "Background", separator.BackgroundColor);

            string orientationStr = GetAttributeString(xmlElement, "Orientation", "Horizontal");
            separator.Orientation = orientationStr.ToLower() == "vertical" ? Orientation.Vertical : Orientation.Horizontal;

            return separator;
        }

        private SceneElement CreateExpander(XmlElement xmlElement) {
            var expander = new Expander(_scene);
            expander.BackgroundColor = GetAttributeColor(xmlElement, "Background", expander.BackgroundColor);
            expander.Header = GetAttributeString(xmlElement, "Header", expander.Header);
            expander.IsExpanded = GetAttributeBoolean(xmlElement, "IsExpanded", true);
            expander.HeaderHeight = GetAttributeSingle(xmlElement, "HeaderHeight", expander.HeaderHeight);

            return expander;
        }

        private SceneElement CreateToolTip(XmlElement xmlElement) {
            var tooltip = new ToolTip(_scene);
            tooltip.BackgroundColor = GetAttributeColor(xmlElement, "Background", tooltip.BackgroundColor);
            tooltip.Content = GetAttributeString(xmlElement, "Content", xmlElement.InnerText.Trim());
            tooltip.ShowDelay = GetAttributeSingle(xmlElement, "ShowDelay", tooltip.ShowDelay);
            tooltip.HideDelay = GetAttributeSingle(xmlElement, "HideDelay", tooltip.HideDelay);

            return tooltip;
        }

        private SceneElement CreateGroupBox(XmlElement xmlElement) {
            var groupBox = new GroupBox(_scene);
            groupBox.BackgroundColor = GetAttributeColor(xmlElement, "Background", groupBox.BackgroundColor);
            groupBox.Header = GetAttributeString(xmlElement, "Header", groupBox.Header);
            groupBox.BorderColor = GetAttributeColor(xmlElement, "BorderBrush", groupBox.BorderColor);

            return groupBox;
        }

        private SceneElement CreateNumericUpDown(XmlElement xmlElement) {
            var numericUpDown = new NumericUpDown(_scene);
            numericUpDown.BackgroundColor = GetAttributeColor(xmlElement, "Background", numericUpDown.BackgroundColor);
            numericUpDown.Value = GetAttributeDouble(xmlElement, "Value", numericUpDown.Value);
            numericUpDown.Minimum = GetAttributeDouble(xmlElement, "Minimum", numericUpDown.Minimum);
            numericUpDown.Maximum = GetAttributeDouble(xmlElement, "Maximum", numericUpDown.Maximum);
            numericUpDown.Increment = GetAttributeDouble(xmlElement, "Increment", numericUpDown.Increment);

            return numericUpDown;
        }

        #endregion

        #region Helper Methods

        private XmlElement GetPropertyElement(XmlElement parent, string propertyName) {
            foreach (XmlNode child in parent.ChildNodes) {
                if (child is XmlElement && (child.LocalName ?? "") == (propertyName ?? "")) {
                    return (XmlElement)child;
                }
            }
            return null;
        }

        private GridDefinition ParseGridDefinition(XmlElement xmlElement, string sizeAttribute) {
            var def = new GridDefinition();
            string sizeStr = GetAttributeString(xmlElement, sizeAttribute, "*");

            if (sizeStr.ToLower() == "auto") {
                def.Size = -1; // Auto
            } else if (sizeStr.EndsWith("*")) {
                def.Size = 0f; // Star
                string starStr = sizeStr.TrimEnd('*');
                if (!string.IsNullOrEmpty(starStr)) {
                    float argresult1 = def.Star;
                    float.TryParse(starStr, out argresult1);
                    def.Star = argresult1;
                } else {
                    def.Star = 1.0f;
                }
            } else {
                float argresult = def.Size;
                float.TryParse(sizeStr, out argresult);
                def.Size = argresult;
            }

            def.MinSize = GetAttributeSingle(xmlElement, "MinHeight", 0f);
            if (xmlElement.HasAttribute("MinWidth")) {
                def.MinSize = GetAttributeSingle(xmlElement, "MinWidth", 0f);
            }

            def.MaxSize = GetAttributeSingle(xmlElement, "MaxHeight", float.MaxValue);
            if (xmlElement.HasAttribute("MaxWidth")) {
                def.MaxSize = GetAttributeSingle(xmlElement, "MaxWidth", float.MaxValue);
            }

            return def;
        }

        private string GetAttributeString(XmlElement xmlElement, string name, string defaultValue) {
            if (xmlElement.HasAttribute(name)) {
                return xmlElement.GetAttribute(name);
            }
            return defaultValue;
        }

        private float GetAttributeSingle(XmlElement xmlElement, string name, float defaultValue) {
            string str = GetAttributeString(xmlElement, name, "");
            if (string.IsNullOrEmpty(str))
                return defaultValue;
            float result;
            if (float.TryParse(str, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result))
                return result;
            return defaultValue;
        }

        private double GetAttributeDouble(XmlElement xmlElement, string name, double defaultValue) {
            string str = GetAttributeString(xmlElement, name, "");
            if (string.IsNullOrEmpty(str))
                return defaultValue;
            double result;
            if (double.TryParse(str, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result))
                return result;
            return defaultValue;
        }

        private int GetAttributeInteger(XmlElement xmlElement, string name, int defaultValue) {
            string str = GetAttributeString(xmlElement, name, "");
            if (string.IsNullOrEmpty(str))
                return defaultValue;
            int result;
            if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                return result;
            return defaultValue;
        }

        private bool GetAttributeBoolean(XmlElement xmlElement, string name, bool defaultValue) {
            string str = GetAttributeString(xmlElement, name, "").ToLower();
            if (str == "true")
                return true;
            if (str == "false")
                return false;
            return defaultValue;
        }

        private Color GetAttributeColor(XmlElement xmlElement, string name, Color defaultValue) {
            string str = GetAttributeString(xmlElement, name, "");
            if (string.IsNullOrEmpty(str))
                return defaultValue;
            return ParseColor(str, defaultValue);
        }

        private Color ParseColor(string colorString, Color defaultValue) {
            colorString = colorString.Trim();

            // Handle named colors
            switch (colorString.ToLower() ?? "") {
                case "transparent": {
                        return Color.Transparent;
                    }
                case "white": {
                        return Color.White;
                    }
                case "black": {
                        return Color.Black;
                    }
                case "red": {
                        return Color.Red;
                    }
                case "green": {
                        return Color.Green;
                    }
                case "blue": {
                        return Color.Blue;
                    }
                case "yellow": {
                        return Color.Yellow;
                    }
                case "orange": {
                        return Color.Orange;
                    }
                case "purple": {
                        return Color.Purple;
                    }
                case "gray":
                case "grey": {
                        return Color.Gray;
                    }
                case "darkgray":
                case "darkgrey": {
                        return Color.DarkGray;
                    }
                case "lightgray":
                case "lightgrey": {
                        return Color.LightGray;
                    }
            }

            // Handle hex colors
            if (colorString.StartsWith("#")) {
                try {
                    string hex = colorString.Substring(1);
                    if (hex.Length == 6) {
                        int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                        int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                        int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                        return new Color(r, g, b);
                    } else if (hex.Length == 8) {
                        int a = Convert.ToInt32(hex.Substring(0, 2), 16);
                        int r = Convert.ToInt32(hex.Substring(2, 2), 16);
                        int g = Convert.ToInt32(hex.Substring(4, 2), 16);
                        int b = Convert.ToInt32(hex.Substring(6, 2), 16);
                        return new Color(r, g, b, a);
                    }
                } catch {
                }
            }

            // Handle RGB/RGBA format: "r,g,b" or "r,g,b,a"
            string[] parts = colorString.Split(',');
            if (parts.Length >= 3) {
                try {
                    int r = int.Parse(parts[0].Trim());
                    int g = int.Parse(parts[1].Trim());
                    int b = int.Parse(parts[2].Trim());
                    int a = parts.Length >= 4 ? int.Parse(parts[3].Trim()) : 255;
                    return new Color(r, g, b, a);
                } catch {
                }
            }

            return defaultValue;
        }

        private Thickness ParseThickness(string thicknessString) {
            string[] parts = thicknessString.Split(',');
            try {
                if (parts.Length == 1) {
                    int value = int.Parse(parts[0].Trim());
                    return new Thickness(value);
                } else if (parts.Length == 2) {
                    int h = int.Parse(parts[0].Trim());
                    int v = int.Parse(parts[1].Trim());
                    return new Thickness(h, v, h, v);
                } else if (parts.Length == 4) {
                    int l = int.Parse(parts[0].Trim());
                    int t = int.Parse(parts[1].Trim());
                    int r = int.Parse(parts[2].Trim());
                    int b = int.Parse(parts[3].Trim());
                    return new Thickness(l, t, r, b);
                }
            } catch {
            }
            return new Thickness(0);
        }

        private HorizontalAlignment ParseHorizontalAlignment(string alignmentString) {
            switch (alignmentString.ToLower() ?? "") {
                case "left": {
                        return HorizontalAlignment.Left;
                    }
                case "center": {
                        return HorizontalAlignment.Center;
                    }
                case "right": {
                        return HorizontalAlignment.Right;
                    }
                case "stretch": {
                        return HorizontalAlignment.Stretch;
                    }

                default: {
                        return HorizontalAlignment.Left;
                    }
            }
        }

        private VerticalAlignment ParseVerticalAlignment(string alignmentString) {
            switch (alignmentString.ToLower() ?? "") {
                case "top": {
                        return VerticalAlignment.Top;
                    }
                case "center": {
                        return VerticalAlignment.Center;
                    }
                case "bottom": {
                        return VerticalAlignment.Bottom;
                    }
                case "stretch": {
                        return VerticalAlignment.Stretch;
                    }

                default: {
                        return VerticalAlignment.Top;
                    }
            }
        }

        private TextWrapping ParseTextWrapping(string wrappingString) {
            switch (wrappingString.ToLower() ?? "") {
                case "wrap": {
                        return TextWrapping.Wrap;
                    }
                case "wrapwithoverflow": {
                        return TextWrapping.WrapWithOverflow;
                    }

                default: {
                        return TextWrapping.NoWrap;
                    }
            }
        }

        #endregion

    }

}