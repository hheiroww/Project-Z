Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Xml

Namespace [Shared].Drawing.Designer

    ''' <summary>
    ''' Parses XAML-like markup and creates Project Z UI elements.
    ''' Supports a subset of WPF XAML syntax adapted for Project Z.
    ''' </summary>
    Public Class SceneXamlParser

#Region "Constants"

        Private Const NAMESPACE_PREFIX As String = "pz"

#End Region

#Region "Fields"

        Private ReadOnly _scene As Scene
        Private ReadOnly _elementFactories As New Dictionary(Of String, Func(Of XmlElement, SceneElement))
        Private ReadOnly _namedElements As New Dictionary(Of String, SceneElement)

#End Region

#Region "Properties"

        ''' <summary>
        ''' Gets the dictionary of named elements (elements with x:Name attribute).
        ''' </summary>
        Public ReadOnly Property NamedElements As Dictionary(Of String, SceneElement)
            Get
                Return _namedElements
            End Get
        End Property

#End Region

#Region "Constructors"

        ''' <summary>
        ''' Creates a new XAML parser for the specified scene.
        ''' </summary>
        Public Sub New(scene As Scene)
            _scene = scene
            RegisterElementFactories()
        End Sub

#End Region

#Region "Public Methods"

        ''' <summary>
        ''' Parses XAML markup and returns the root element.
        ''' </summary>
        Public Function Parse(xaml As String) As SceneElement
            Dim doc As New XmlDocument()
            doc.LoadXml(xaml)
            Return ParseElement(doc.DocumentElement)
        End Function

        ''' <summary>
        ''' Parses XAML from a file and returns the root element.
        ''' </summary>
        Public Function ParseFile(filePath As String) As SceneElement
            Dim xaml As String = File.ReadAllText(filePath)
            Return Parse(xaml)
        End Function

        ''' <summary>
        ''' Gets a named element by its x:Name.
        ''' </summary>
        Public Function FindName(Of T As SceneElement)(name As String) As T
            If _namedElements.ContainsKey(name) Then
                Return DirectCast(_namedElements(name), T)
            End If
            Return Nothing
        End Function

#End Region

#Region "Element Factory Registration"

        Private Sub RegisterElementFactories()
            ' Root elements
            _elementFactories("Scene") = Function(e) CreateSceneRoot(e)
            _elementFactories("Window") = Function(e) CreateSceneRoot(e)
            _elementFactories("UserControl") = Function(e) CreateSceneRoot(e)
            _elementFactories("Page") = Function(e) CreateSceneRoot(e)

            ' Primitives
            _elementFactories("Rectangle") = Function(e) CreateRectangle(e)
            _elementFactories("RectangleElement") = Function(e) CreateRectangle(e)
            _elementFactories("Circle") = Function(e) CreateCircle(e)
            _elementFactories("CircleElement") = Function(e) CreateCircle(e)
            _elementFactories("Text") = Function(e) CreateText(e)
            _elementFactories("TextElement") = Function(e) CreateText(e)
            _elementFactories("TextBlock") = Function(e) CreateText(e)

            ' Input Controls
            _elementFactories("Button") = Function(e) CreateButton(e)
            _elementFactories("CheckBox") = Function(e) CreateCheckBox(e)
            _elementFactories("RadioButton") = Function(e) CreateRadioButton(e)
            _elementFactories("TextBox") = Function(e) CreateTextBox(e)
            _elementFactories("Slider") = Function(e) CreateSlider(e)
            _elementFactories("Trackbar") = Function(e) CreateSlider(e)
            _elementFactories("ProgressBar") = Function(e) CreateProgressBar(e)

            ' Advanced Controls
            _elementFactories("ComboBox") = Function(e) CreateComboBox(e)
            _elementFactories("ListBox") = Function(e) CreateListBox(e)
            _elementFactories("TabControl") = Function(e) CreateTabControl(e)
            _elementFactories("ScrollViewer") = Function(e) CreateScrollViewer(e)
            _elementFactories("Separator") = Function(e) CreateSeparator(e)
            _elementFactories("Expander") = Function(e) CreateExpander(e)
            _elementFactories("ToolTip") = Function(e) CreateToolTip(e)
            _elementFactories("GroupBox") = Function(e) CreateGroupBox(e)
            _elementFactories("NumericUpDown") = Function(e) CreateNumericUpDown(e)

            ' Layout
            _elementFactories("StackPanel") = Function(e) CreateStackPanel(e)
            _elementFactories("Grid") = Function(e) CreateGrid(e)
            _elementFactories("Panel") = Function(e) CreatePanel(e)
            _elementFactories("Border") = Function(e) CreateBorder(e)
        End Sub

#End Region

#Region "Element Parsing"

        Private Function ParseElement(xmlElement As XmlElement) As SceneElement
            Dim elementName As String = xmlElement.LocalName

            ' Check if we have a factory for this element
            If Not _elementFactories.ContainsKey(elementName) Then
                Throw New NotSupportedException($"Unknown element type: {elementName}")
            End If

            Dim element As SceneElement = _elementFactories(elementName)(xmlElement)

            ' Handle x:Name attribute
            Dim nameAttr = xmlElement.GetAttribute("x:Name")
            If String.IsNullOrEmpty(nameAttr) Then
                nameAttr = xmlElement.GetAttribute("Name")
            End If
            If Not String.IsNullOrEmpty(nameAttr) Then
                _namedElements(nameAttr) = element
            End If

            ' Parse common properties
            ApplyCommonProperties(element, xmlElement)

            ' Parse children with proper z-index assignment
            ' WPF behavior: elements later in XAML appear on top (higher z-order)
            ' childIndex increases for each child, so later elements get higher z-index
            Dim childIndex As Integer = 0
            For Each childNode As XmlNode In xmlElement.ChildNodes
                If TypeOf childNode Is XmlElement Then
                    Dim childXml = DirectCast(childNode, XmlElement)
                    ' Skip property elements (e.g., Grid.RowDefinitions)
                    If Not childXml.LocalName.Contains(".") Then
                        Dim childElement = ParseElement(childXml)
                        ' Assign z-index based on document order if not explicitly set via Panel.ZIndex
                        ' This matches WPF behavior where later elements appear on top
                        If Not childXml.HasAttribute("ZIndex") AndAlso Not childXml.HasAttribute("Panel.ZIndex") Then
                            childElement.zIndex = childIndex
                        End If
                        childElement.Parent = element
                        ' For Grid parents, use AddChild to set attached properties
                        If TypeOf element Is Grid Then
                            Dim grid = DirectCast(element, Grid)
                            Dim gridRow = GetAttributeInteger(childXml, "Grid.Row", 0)
                            Dim gridCol = GetAttributeInteger(childXml, "Grid.Column", 0)
                            Dim gridRowSpan = GetAttributeInteger(childXml, "Grid.RowSpan", 1)
                            Dim gridColSpan = GetAttributeInteger(childXml, "Grid.ColumnSpan", 1)
                            grid.AddChild(childElement, gridRow, gridCol, gridRowSpan, gridColSpan,
                                          childXml.HasAttribute("Width"), childXml.HasAttribute("Height"))
                        Else
                            element.Children.Add(childElement)
                        End If
                        ' Re-trigger text wrapping now that parent is assigned,
                        ' so wrap width can be derived from parent bounds.
                        If TypeOf childElement Is TextElement Then
                            Dim textEl = DirectCast(childElement, TextElement)
                            textEl.Text = textEl.Text
                        End If
                        childIndex += 1
                    End If
                End If
            Next

            Return element
        End Function

        Private Sub ApplyCommonProperties(element As SceneElement, xmlElement As XmlElement)
            ' --- Position: Canvas.Left/Top take precedence over X/Y ---
            ' These values are incorporated into the element's Margin so the
            ' layout system (AlignChild) uses them as positional offsets.
            Dim x As Single = 0, y As Single = 0
            If xmlElement.HasAttribute("Canvas.Left") Then
                x = GetAttributeSingle(xmlElement, "Canvas.Left", 0)
            ElseIf xmlElement.HasAttribute("X") Then
                x = GetAttributeSingle(xmlElement, "X", 0)
            End If
            If xmlElement.HasAttribute("Canvas.Top") Then
                y = GetAttributeSingle(xmlElement, "Canvas.Top", 0)
            ElseIf xmlElement.HasAttribute("Y") Then
                y = GetAttributeSingle(xmlElement, "Y", 0)
            End If

            ' Set initial position for immediate use before layout runs
            element.Position = New Vector2(x, y)

            ' --- Size: Always set if present, even if zero ---
            Dim widthSet = xmlElement.HasAttribute("Width")
            Dim heightSet = xmlElement.HasAttribute("Height")
            Dim width = GetAttributeSingle(xmlElement, "Width", element.Size.X)
            Dim height = GetAttributeSingle(xmlElement, "Height", element.Size.Y)
            If widthSet Or heightSet Then
                element.Size = New Vector2(width, height)
            End If

            ' --- Min/Max size (optional, for stricter WPF compatibility) ---
            If xmlElement.HasAttribute("MinWidth") Then
                element.MinSize = New Vector2(GetAttributeSingle(xmlElement, "MinWidth", element.MinSize.X), element.MinSize.Y)
            End If
            If xmlElement.HasAttribute("MinHeight") Then
                element.MinSize = New Vector2(element.MinSize.X, GetAttributeSingle(xmlElement, "MinHeight", element.MinSize.Y))
            End If
            If xmlElement.HasAttribute("MaxWidth") Then
                element.MaxSize = New Vector2(GetAttributeSingle(xmlElement, "MaxWidth", element.MaxSize.X), element.MaxSize.Y)
            End If
            If xmlElement.HasAttribute("MaxHeight") Then
                element.MaxSize = New Vector2(element.MaxSize.X, GetAttributeSingle(xmlElement, "MaxHeight", element.MaxSize.Y))
            End If

            ' Visibility
            Dim visibility = GetAttributeString(xmlElement, "Visibility", "Visible")
            element.isVisible = (visibility.ToLower() <> "collapsed" AndAlso visibility.ToLower() <> "hidden")

            ' IsEnabled
            element.isEnabled = GetAttributeBoolean(xmlElement, "IsEnabled", True)

            ' Margin - combine XAML Margin with X/Y position offsets
            ' The layout system (AlignChild) uses Margin.Left/Top for positioning,
            ' so X/Y/Canvas.Left/Canvas.Top must be incorporated here.
            Dim marginStr = GetAttributeString(xmlElement, "Margin", "")
            Dim margin As Thickness
            If Not String.IsNullOrEmpty(marginStr) Then
                margin = ParseThickness(marginStr)
            Else
                margin = New Thickness(0)
            End If
            If x <> 0 OrElse y <> 0 Then
                margin = New Thickness(margin.Left + CInt(x), margin.Top + CInt(y), margin.Right, margin.Bottom)
            End If
            element.Margin = margin

            ' Padding
            Dim paddingStr = GetAttributeString(xmlElement, "Padding", "")
            If Not String.IsNullOrEmpty(paddingStr) Then
                element.Padding = ParseThickness(paddingStr)
            End If

            ' Alignment
            Dim hAlign = GetAttributeString(xmlElement, "HorizontalAlignment", "")
            If Not String.IsNullOrEmpty(hAlign) Then
                element.HorizontalAlign = ParseHorizontalAlignment(hAlign)
            End If

            Dim vAlign = GetAttributeString(xmlElement, "VerticalAlignment", "")
            If Not String.IsNullOrEmpty(vAlign) Then
                element.VerticalAlign = ParseVerticalAlignment(vAlign)
            End If

            ' --- Stretch support: if alignment is Stretch and no explicit size, fill parent ---
            If (hAlign.ToLower() = "stretch" Or vAlign.ToLower() = "stretch") And Not (widthSet Or heightSet) Then
                ' For Canvas, stretching means fill available space (parent size minus margin)
                ' This requires parent size, so you may need to handle this after tree is built.
                element.StretchToParent = True
            End If

            ' ZIndex
            element.zIndex = GetAttributeInteger(xmlElement, "ZIndex", element.zIndex)
            If xmlElement.HasAttribute("Panel.ZIndex") Then
                element.zIndex = GetAttributeInteger(xmlElement, "Panel.ZIndex", element.zIndex)
            End If
        End Sub

#End Region

#Region "Element Creators"

        ''' <summary>
        ''' Creates a root container element for the Scene.
        ''' This is a virtual element that holds all child elements.
        ''' </summary>
        Private Function CreateSceneRoot(xmlElement As XmlElement) As SceneElement
            ' Create a transparent container to hold scene children
            Dim root As New RectangleElement(_scene)
            root.BackgroundColor = Color.Transparent
            root.isMouseBypassEnabled = True
            root.Padding = New Thickness(0)

            ' Set root size from explicit attributes or fall back to viewport size
            ' so child text elements can derive wrap width from parent bounds.
            Dim rootWidth = GetAttributeSingle(xmlElement, "Width", 0)
            Dim rootHeight = GetAttributeSingle(xmlElement, "Height", 0)
            If rootWidth <= 0 OrElse rootHeight <= 0 Then
                Try
                    If rootWidth <= 0 Then rootWidth = _scene.graphicsDevice.Viewport.Width
                    If rootHeight <= 0 Then rootHeight = _scene.graphicsDevice.Viewport.Height
                Catch
                End Try
            End If
            If rootWidth > 0 OrElse rootHeight > 0 Then
                root.Size = New Vector2(rootWidth, rootHeight)
            End If

            Return root
        End Function

        Private Function CreateRectangle(xmlElement As XmlElement) As SceneElement
            Dim rect As New RectangleElement(_scene)
            Dim bgColor = GetAttributeColor(xmlElement, "Background", New Color(50, 50, 50))
            If xmlElement.HasAttribute("BackgroundColor") Then
                bgColor = GetAttributeColor(xmlElement, "BackgroundColor", bgColor)
            End If
            rect.BackgroundColor = bgColor
            Return rect
        End Function

        Private Function CreateCircle(xmlElement As XmlElement) As SceneElement
            Dim circle As New CircleElement(_scene)
            circle.FillColor = GetAttributeColor(xmlElement, "Fill", Color.White)
            If xmlElement.HasAttribute("FillColor") Then
                circle.FillColor = GetAttributeColor(xmlElement, "FillColor", circle.FillColor)
            End If
            Return circle
        End Function

        Private Function CreateText(xmlElement As XmlElement) As SceneElement
            Dim text As New TextElement(_scene)

            ' Set wrapping parameters BEFORE text content so the initial
            ' UpdateWrappedText uses the correct wrap width.
            Dim wrappingStr = GetAttributeString(xmlElement, "TextWrapping", "Wrap")
            text.TextWrapping = ParseTextWrapping(wrappingStr)

            ' Use MaxWidth if specified, otherwise use Width as the wrapping boundary
            Dim maxWidth = GetAttributeSingle(xmlElement, "MaxWidth", 0)
            If maxWidth <= 0 Then
                maxWidth = GetAttributeSingle(xmlElement, "Width", 0)
            End If
            text.MaxWidth = maxWidth

            text.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", Color.White)
            If xmlElement.HasAttribute("ForegroundColor") Then
                text.ForegroundColor = GetAttributeColor(xmlElement, "ForegroundColor", text.ForegroundColor)
            End If
            Dim fontName = GetAttributeString(xmlElement, "Font", "")
            If Not String.IsNullOrEmpty(fontName) Then
                text.Font = fontName
            End If

            ' Set text last so wrapping uses the correct parameters
            text.Text = GetAttributeString(xmlElement, "Text", xmlElement.InnerText.Trim())
            Return text
        End Function

        Private Function CreateButton(xmlElement As XmlElement) As SceneElement
            Dim button As New Button(_scene)

            ' Disable auto-sizing when explicit dimensions are specified in XAML,
            ' otherwise DoAutoSize() overrides the explicit Width/Height.
            If xmlElement.HasAttribute("Width") OrElse xmlElement.HasAttribute("Height") Then
                button.AutoSize = ButtonAutoSize.None
            End If

            button.Text = GetAttributeString(xmlElement, "Content", xmlElement.InnerText.Trim())
            If xmlElement.HasAttribute("Text") Then
                button.Text = GetAttributeString(xmlElement, "Text", button.Text)
            End If
            button.BackgroundColor = GetAttributeColor(xmlElement, "Background", button.BackgroundColor)
            button.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", button.ForegroundColor)
            button.MouseOverBackgroundColor = GetAttributeColor(xmlElement, "MouseOverBackground", button.MouseOverBackgroundColor)
            button.MouseDownBackgroundColor = GetAttributeColor(xmlElement, "MouseDownBackground", button.MouseDownBackgroundColor)
            Return button
        End Function

        Private Function CreateCheckBox(xmlElement As XmlElement) As SceneElement
            Dim checkBox As New CheckBox(_scene)
            checkBox.Content = GetAttributeString(xmlElement, "Content", xmlElement.InnerText.Trim())
            checkBox.IsChecked = GetAttributeBoolean(xmlElement, "IsChecked", False)
            checkBox.IsThreeState = GetAttributeBoolean(xmlElement, "IsThreeState", False)
            checkBox.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", checkBox.ForegroundColor)
            Return checkBox
        End Function

        Private Function CreateRadioButton(xmlElement As XmlElement) As SceneElement
            Dim radioButton As New RadioButton(_scene)
            radioButton.Content = GetAttributeString(xmlElement, "Content", xmlElement.InnerText.Trim())
            radioButton.GroupName = GetAttributeString(xmlElement, "GroupName", "")
            radioButton.IsChecked = GetAttributeBoolean(xmlElement, "IsChecked", False)
            radioButton.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", radioButton.ForegroundColor)
            Return radioButton
        End Function

        Private Function CreateTextBox(xmlElement As XmlElement) As SceneElement
            Dim textBox As New Textbox(_scene)
            textBox.Text = GetAttributeString(xmlElement, "Text", "")
            textBox.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", textBox.ForegroundColor)
            textBox.BackgroundColor = GetAttributeColor(xmlElement, "Background", textBox.BackgroundColor)
            textBox.AcceptsReturn = GetAttributeBoolean(xmlElement, "AcceptsReturn", True)
            Return textBox
        End Function

        Private Function CreateSlider(xmlElement As XmlElement) As SceneElement
            Dim slider As New Trackbar(_scene)
            slider.MinimumValue = GetAttributeDouble(xmlElement, "Minimum", 0)
            slider.MaximumValue = GetAttributeDouble(xmlElement, "Maximum", 100)
            slider.Value = GetAttributeDouble(xmlElement, "Value", 0)
            Return slider
        End Function

        Private Function CreateProgressBar(xmlElement As XmlElement) As SceneElement
            Dim progressBar As New ProgressBar(_scene)
            progressBar.Minimum = GetAttributeDouble(xmlElement, "Minimum", 0)
            progressBar.Maximum = GetAttributeDouble(xmlElement, "Maximum", 100)
            progressBar.Value = GetAttributeDouble(xmlElement, "Value", 0)
            progressBar.IsIndeterminate = GetAttributeBoolean(xmlElement, "IsIndeterminate", False)
            progressBar.FillColor = GetAttributeColor(xmlElement, "Foreground", progressBar.FillColor)
            progressBar.TrackColor = GetAttributeColor(xmlElement, "Background", progressBar.TrackColor)
            Return progressBar
        End Function

        Private Function CreateStackPanel(xmlElement As XmlElement) As SceneElement
            Dim stackPanel As New StackPanel(_scene)
            Dim orientationStr = GetAttributeString(xmlElement, "Orientation", "Vertical")
            stackPanel.Orientation = If(orientationStr.ToLower() = "horizontal", Orientation.Horizontal, Orientation.Vertical)
            stackPanel.Spacing = GetAttributeSingle(xmlElement, "Spacing", 4.0F)
            stackPanel.BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent)
            Return stackPanel
        End Function

        Private Function CreateGrid(xmlElement As XmlElement) As SceneElement
            Dim grid As New Grid(_scene)
            grid.BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent)
            grid.ShowGridLines = GetAttributeBoolean(xmlElement, "ShowGridLines", False)

            ' Parse row definitions
            Dim rowDefsNode = GetPropertyElement(xmlElement, "Grid.RowDefinitions")
            If rowDefsNode IsNot Nothing Then
                For Each rowNode As XmlNode In rowDefsNode.ChildNodes
                    If TypeOf rowNode Is XmlElement AndAlso rowNode.LocalName = "RowDefinition" Then
                        Dim rowXml = DirectCast(rowNode, XmlElement)
                        grid.RowDefinitions.Add(ParseGridDefinition(rowXml, "Height"))
                    End If
                Next
            End If

            ' Parse column definitions
            Dim colDefsNode = GetPropertyElement(xmlElement, "Grid.ColumnDefinitions")
            If colDefsNode IsNot Nothing Then
                For Each colNode As XmlNode In colDefsNode.ChildNodes
                    If TypeOf colNode Is XmlElement AndAlso colNode.LocalName = "ColumnDefinition" Then
                        Dim colXml = DirectCast(colNode, XmlElement)
                        grid.ColumnDefinitions.Add(ParseGridDefinition(colXml, "Width"))
                    End If
                Next
            End If

            Return grid
        End Function

        Private Function CreatePanel(xmlElement As XmlElement) As SceneElement
            Dim panel As New RectangleElement(_scene)
            panel.BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent)
            Return panel
        End Function

        Private Function CreateBorder(xmlElement As XmlElement) As SceneElement
            Dim border As New RectangleElement(_scene)
            border.BackgroundColor = GetAttributeColor(xmlElement, "Background", New Color(50, 50, 50))
            Return border
        End Function

        Private Function CreateComboBox(xmlElement As XmlElement) As SceneElement
            Dim comboBox As New ComboBox(_scene)
            comboBox.BackgroundColor = GetAttributeColor(xmlElement, "Background", comboBox.BackgroundColor)

            ' Parse items from ComboBox.Items child element
            Dim itemsNode = GetPropertyElement(xmlElement, "ComboBox.Items")
            If itemsNode IsNot Nothing Then
                For Each itemNode As XmlNode In itemsNode.ChildNodes
                    If TypeOf itemNode Is XmlElement Then
                        Dim itemXml = DirectCast(itemNode, XmlElement)
                        If itemXml.LocalName = "ComboBoxItem" Then
                            comboBox.AddItem(GetAttributeString(itemXml, "Content", itemXml.InnerText.Trim()))
                        End If
                    End If
                Next
            End If

            Dim selectedIndex = GetAttributeInteger(xmlElement, "SelectedIndex", -1)
            If selectedIndex >= 0 Then
                comboBox.SelectedIndex = selectedIndex
            End If

            Return comboBox
        End Function

        Private Function CreateListBox(xmlElement As XmlElement) As SceneElement
            Dim listBox As New ListBox(_scene)
            listBox.BackgroundColor = GetAttributeColor(xmlElement, "Background", listBox.BackgroundColor)
            listBox.SelectionColor = GetAttributeColor(xmlElement, "SelectionBackground", listBox.SelectionColor)
            listBox.ItemHeight = GetAttributeSingle(xmlElement, "ItemHeight", listBox.ItemHeight)

            ' Parse items from ListBox.Items child element
            Dim itemsNode = GetPropertyElement(xmlElement, "ListBox.Items")
            If itemsNode IsNot Nothing Then
                For Each itemNode As XmlNode In itemsNode.ChildNodes
                    If TypeOf itemNode Is XmlElement Then
                        Dim itemXml = DirectCast(itemNode, XmlElement)
                        If itemXml.LocalName = "ListBoxItem" Then
                            listBox.AddItem(GetAttributeString(itemXml, "Content", itemXml.InnerText.Trim()))
                        End If
                    End If
                Next
            End If

            Dim selectedIndex = GetAttributeInteger(xmlElement, "SelectedIndex", -1)
            If selectedIndex >= 0 Then
                listBox.SelectedIndex = selectedIndex
            End If

            Return listBox
        End Function

        Private Function CreateTabControl(xmlElement As XmlElement) As SceneElement
            Dim tabControl As New TabControl(_scene)
            tabControl.BackgroundColor = GetAttributeColor(xmlElement, "Background", tabControl.BackgroundColor)
            tabControl.TabHeaderHeight = GetAttributeSingle(xmlElement, "TabHeaderHeight", tabControl.TabHeaderHeight)

            ' Tabs are parsed as children with special handling
            Return tabControl
        End Function

        Private Function CreateScrollViewer(xmlElement As XmlElement) As SceneElement
            Dim scrollViewer As New ScrollViewer(_scene)
            scrollViewer.BackgroundColor = GetAttributeColor(xmlElement, "Background", scrollViewer.BackgroundColor)

            Dim vScroll = GetAttributeString(xmlElement, "VerticalScrollBarVisibility", "Auto")
            scrollViewer.CanScrollVertically = (vScroll.ToLower() <> "disabled")

            Dim hScroll = GetAttributeString(xmlElement, "HorizontalScrollBarVisibility", "Disabled")
            scrollViewer.CanScrollHorizontally = (hScroll.ToLower() <> "disabled")

            Return scrollViewer
        End Function

        Private Function CreateSeparator(xmlElement As XmlElement) As SceneElement
            Dim separator As New Separator(_scene)
            separator.BackgroundColor = GetAttributeColor(xmlElement, "Background", separator.BackgroundColor)

            Dim orientationStr = GetAttributeString(xmlElement, "Orientation", "Horizontal")
            separator.Orientation = If(orientationStr.ToLower() = "vertical", Orientation.Vertical, Orientation.Horizontal)

            Return separator
        End Function

        Private Function CreateExpander(xmlElement As XmlElement) As SceneElement
            Dim expander As New Expander(_scene)
            expander.BackgroundColor = GetAttributeColor(xmlElement, "Background", expander.BackgroundColor)
            expander.Header = GetAttributeString(xmlElement, "Header", expander.Header)
            expander.IsExpanded = GetAttributeBoolean(xmlElement, "IsExpanded", True)
            expander.HeaderHeight = GetAttributeSingle(xmlElement, "HeaderHeight", expander.HeaderHeight)

            Return expander
        End Function

        Private Function CreateToolTip(xmlElement As XmlElement) As SceneElement
            Dim tooltip As New ToolTip(_scene)
            tooltip.BackgroundColor = GetAttributeColor(xmlElement, "Background", tooltip.BackgroundColor)
            tooltip.Content = GetAttributeString(xmlElement, "Content", xmlElement.InnerText.Trim())
            tooltip.ShowDelay = GetAttributeSingle(xmlElement, "ShowDelay", tooltip.ShowDelay)
            tooltip.HideDelay = GetAttributeSingle(xmlElement, "HideDelay", tooltip.HideDelay)

            Return tooltip
        End Function

        Private Function CreateGroupBox(xmlElement As XmlElement) As SceneElement
            Dim groupBox As New GroupBox(_scene)
            groupBox.BackgroundColor = GetAttributeColor(xmlElement, "Background", groupBox.BackgroundColor)
            groupBox.Header = GetAttributeString(xmlElement, "Header", groupBox.Header)
            groupBox.BorderColor = GetAttributeColor(xmlElement, "BorderBrush", groupBox.BorderColor)

            Return groupBox
        End Function

        Private Function CreateNumericUpDown(xmlElement As XmlElement) As SceneElement
            Dim numericUpDown As New NumericUpDown(_scene)
            numericUpDown.BackgroundColor = GetAttributeColor(xmlElement, "Background", numericUpDown.BackgroundColor)
            numericUpDown.Value = GetAttributeDouble(xmlElement, "Value", numericUpDown.Value)
            numericUpDown.Minimum = GetAttributeDouble(xmlElement, "Minimum", numericUpDown.Minimum)
            numericUpDown.Maximum = GetAttributeDouble(xmlElement, "Maximum", numericUpDown.Maximum)
            numericUpDown.Increment = GetAttributeDouble(xmlElement, "Increment", numericUpDown.Increment)

            Return numericUpDown
        End Function

#End Region

#Region "Helper Methods"

        Private Function GetPropertyElement(parent As XmlElement, propertyName As String) As XmlElement
            For Each child As XmlNode In parent.ChildNodes
                If TypeOf child Is XmlElement AndAlso child.LocalName = propertyName Then
                    Return DirectCast(child, XmlElement)
                End If
            Next
            Return Nothing
        End Function

        Private Function ParseGridDefinition(xmlElement As XmlElement, sizeAttribute As String) As GridDefinition
            Dim def As New GridDefinition()
            Dim sizeStr = GetAttributeString(xmlElement, sizeAttribute, "*")

            If sizeStr.ToLower() = "auto" Then
                def.Size = -1 ' Auto
            ElseIf sizeStr.EndsWith("*") Then
                def.Size = 0 ' Star
                Dim starStr = sizeStr.TrimEnd("*"c)
                If Not String.IsNullOrEmpty(starStr) Then
                    Single.TryParse(starStr, def.Star)
                Else
                    def.Star = 1.0F
                End If
            Else
                Single.TryParse(sizeStr, def.Size)
            End If

            def.MinSize = GetAttributeSingle(xmlElement, "MinHeight", 0)
            If xmlElement.HasAttribute("MinWidth") Then
                def.MinSize = GetAttributeSingle(xmlElement, "MinWidth", 0)
            End If

            def.MaxSize = GetAttributeSingle(xmlElement, "MaxHeight", Single.MaxValue)
            If xmlElement.HasAttribute("MaxWidth") Then
                def.MaxSize = GetAttributeSingle(xmlElement, "MaxWidth", Single.MaxValue)
            End If

            Return def
        End Function

        Private Function GetAttributeString(xmlElement As XmlElement, name As String, defaultValue As String) As String
            If xmlElement.HasAttribute(name) Then
                Return xmlElement.GetAttribute(name)
            End If
            Return defaultValue
        End Function

        Private Function GetAttributeSingle(xmlElement As XmlElement, name As String, defaultValue As Single) As Single
            Dim str = GetAttributeString(xmlElement, name, "")
            If String.IsNullOrEmpty(str) Then Return defaultValue
            Dim result As Single
            If Single.TryParse(str, NumberStyles.Float Or NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, result) Then Return result
            Return defaultValue
        End Function

        Private Function GetAttributeDouble(xmlElement As XmlElement, name As String, defaultValue As Double) As Double
            Dim str = GetAttributeString(xmlElement, name, "")
            If String.IsNullOrEmpty(str) Then Return defaultValue
            Dim result As Double
            If Double.TryParse(str, NumberStyles.Float Or NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, result) Then Return result
            Return defaultValue
        End Function

        Private Function GetAttributeInteger(xmlElement As XmlElement, name As String, defaultValue As Integer) As Integer
            Dim str = GetAttributeString(xmlElement, name, "")
            If String.IsNullOrEmpty(str) Then Return defaultValue
            Dim result As Integer
            If Integer.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, result) Then Return result
            Return defaultValue
        End Function

        Private Function GetAttributeBoolean(xmlElement As XmlElement, name As String, defaultValue As Boolean) As Boolean
            Dim str = GetAttributeString(xmlElement, name, "").ToLower()
            If str = "true" Then Return True
            If str = "false" Then Return False
            Return defaultValue
        End Function

        Private Function GetAttributeColor(xmlElement As XmlElement, name As String, defaultValue As Color) As Color
            Dim str = GetAttributeString(xmlElement, name, "")
            If String.IsNullOrEmpty(str) Then Return defaultValue
            Return ParseColor(str, defaultValue)
        End Function

        Private Function ParseColor(colorString As String, defaultValue As Color) As Color
            colorString = colorString.Trim()

            ' Handle named colors
            Select Case colorString.ToLower()
                Case "transparent" : Return Color.Transparent
                Case "white" : Return Color.White
                Case "black" : Return Color.Black
                Case "red" : Return Color.Red
                Case "green" : Return Color.Green
                Case "blue" : Return Color.Blue
                Case "yellow" : Return Color.Yellow
                Case "orange" : Return Color.Orange
                Case "purple" : Return Color.Purple
                Case "gray", "grey" : Return Color.Gray
                Case "darkgray", "darkgrey" : Return Color.DarkGray
                Case "lightgray", "lightgrey" : Return Color.LightGray
            End Select

            ' Handle hex colors
            If colorString.StartsWith("#") Then
                Try
                    Dim hex = colorString.Substring(1)
                    If hex.Length = 6 Then
                        Dim r = Convert.ToInt32(hex.Substring(0, 2), 16)
                        Dim g = Convert.ToInt32(hex.Substring(2, 2), 16)
                        Dim b = Convert.ToInt32(hex.Substring(4, 2), 16)
                        Return New Color(r, g, b)
                    ElseIf hex.Length = 8 Then
                        Dim a = Convert.ToInt32(hex.Substring(0, 2), 16)
                        Dim r = Convert.ToInt32(hex.Substring(2, 2), 16)
                        Dim g = Convert.ToInt32(hex.Substring(4, 2), 16)
                        Dim b = Convert.ToInt32(hex.Substring(6, 2), 16)
                        Return New Color(r, g, b, a)
                    End If
                Catch
                End Try
            End If

            ' Handle RGB/RGBA format: "r,g,b" or "r,g,b,a"
            Dim parts = colorString.Split(","c)
            If parts.Length >= 3 Then
                Try
                    Dim r = Integer.Parse(parts(0).Trim())
                    Dim g = Integer.Parse(parts(1).Trim())
                    Dim b = Integer.Parse(parts(2).Trim())
                    Dim a = If(parts.Length >= 4, Integer.Parse(parts(3).Trim()), 255)
                    Return New Color(r, g, b, a)
                Catch
                End Try
            End If

            Return defaultValue
        End Function

        Private Function ParseThickness(thicknessString As String) As Thickness
            Dim parts = thicknessString.Split(","c)
            Try
                If parts.Length = 1 Then
                    Dim value = Integer.Parse(parts(0).Trim())
                    Return New Thickness(value)
                ElseIf parts.Length = 2 Then
                    Dim h = Integer.Parse(parts(0).Trim())
                    Dim v = Integer.Parse(parts(1).Trim())
                    Return New Thickness(h, v, h, v)
                ElseIf parts.Length = 4 Then
                    Dim l = Integer.Parse(parts(0).Trim())
                    Dim t = Integer.Parse(parts(1).Trim())
                    Dim r = Integer.Parse(parts(2).Trim())
                    Dim b = Integer.Parse(parts(3).Trim())
                    Return New Thickness(l, t, r, b)
                End If
            Catch
            End Try
            Return New Thickness(0)
        End Function

        Private Function ParseHorizontalAlignment(alignmentString As String) As HorizontalAlignment
            Select Case alignmentString.ToLower()
                Case "left" : Return HorizontalAlignment.Left
                Case "center" : Return HorizontalAlignment.Center
                Case "right" : Return HorizontalAlignment.Right
                Case "stretch" : Return HorizontalAlignment.Stretch
                Case Else : Return HorizontalAlignment.Left
            End Select
        End Function

        Private Function ParseVerticalAlignment(alignmentString As String) As VerticalAlignment
            Select Case alignmentString.ToLower()
                Case "top" : Return VerticalAlignment.Top
                Case "center" : Return VerticalAlignment.Center
                Case "bottom" : Return VerticalAlignment.Bottom
                Case "stretch" : Return VerticalAlignment.Stretch
                Case Else : Return VerticalAlignment.Top
            End Select
        End Function

        Private Function ParseTextWrapping(wrappingString As String) As Primitives.TextWrapping
            Select Case wrappingString.ToLower()
                Case "wrap" : Return Primitives.TextWrapping.Wrap
                Case "wrapwithoverflow" : Return Primitives.TextWrapping.WrapWithOverflow
                Case Else : Return Primitives.TextWrapping.NoWrap
            End Select
        End Function

#End Region

    End Class

End Namespace
