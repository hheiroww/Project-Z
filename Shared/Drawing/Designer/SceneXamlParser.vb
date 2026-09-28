Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Advanced
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Shared.Animations
Imports ProjectZ.Shared.Animations.Easing
Imports ProjectZ.Shared.Animations.Properties
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Xml

Namespace [Shared].Drawing.Designer

    ''' <summary>
    ''' Parses XAML-like markup and creates Project Z UI elements.
    ''' Supports a subset of WPF XAML syntax adapted for Project Z.
    ''' </summary>
    Public Partial Class SceneXamlParser

#Region "Constants"

        Private Const NAMESPACE_PREFIX As String = "pz"

#End Region

#Region "Fields"

        Private ReadOnly _scene As Scene
        Private ReadOnly _elementFactories As New Dictionary(Of String, Func(Of XmlElement, SceneElement))
        Private ReadOnly _namedElements As New Dictionary(Of String, SceneElement)
        Private ReadOnly _xmlElements As New Dictionary(Of XmlElement, SceneElement)()
        Private ReadOnly _storyboards As New Dictionary(Of String, XamlStoryboard)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _explicitStoryboardTriggers As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _unsupportedFeatures As New List(Of String)()
        Private _sourceDirectory As String = String.Empty

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

        Public ReadOnly Property Storyboards As IReadOnlyDictionary(Of String, XamlStoryboard)
            Get
                Return _storyboards
            End Get
        End Property

        ''' <summary>
        ''' Compatibility items that were mapped to a generic Project-Z panel instead of
        ''' aborting the whole legacy window import.
        ''' </summary>
        Public ReadOnly Property UnsupportedFeatures As IReadOnlyList(Of String)
            Get
                Return _unsupportedFeatures
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

        Public Sub RegisterFactory(name As String, factory As Func(Of XmlElement, SceneElement))
            _elementFactories(name) = factory
        End Sub

        ''' <summary>
        ''' Parses XAML markup and returns the root element.
        ''' </summary>
        Public Function Parse(xaml As String) As SceneElement
            Dim doc As New XmlDocument()
            doc.LoadXml(xaml)
            _namedElements.Clear()
            _xmlElements.Clear()
            _storyboards.Clear()
            _explicitStoryboardTriggers.Clear()
            _unsupportedFeatures.Clear()
            Dim root = ParseElement(doc.DocumentElement)
            AttachVisualMasks()
            ParseStoryboards(doc)
            AttachEventTriggers(doc)
            AttachImplicitStoryboardTriggers(root)
            Return root
        End Function

        ''' <summary>
        ''' Parses XAML from a file and returns the root element.
        ''' </summary>
        Public Function ParseFile(filePath As String) As SceneElement
            If String.IsNullOrWhiteSpace(filePath) Then Throw New ArgumentException("A XAML path is required.", NameOf(filePath))
            Dim fullPath = IO.Path.GetFullPath(filePath)
            Dim previousDirectory = _sourceDirectory
            _sourceDirectory = IO.Path.GetDirectoryName(fullPath)
            Try
                Return Parse(File.ReadAllText(fullPath))
            Finally
                _sourceDirectory = previousDirectory
            End Try
        End Function

        ''' <summary>Import a linked legacy control without losing its document resources.</summary>
        Public Function ParseNamedElement(filePath As String, name As String) As SceneElement
            Dim doc As New XmlDocument()
            doc.Load(filePath)
            For Each node As XmlElement In doc.SelectNodes("//*")
                If node.GetAttribute("Name", "http://schemas.microsoft.com/winfx/2006/xaml") = name OrElse node.GetAttribute("Name") = name Then
                    Dim previous = _sourceDirectory
                    _sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath))
                    Try
                        Return ParseElement(node)
                    Finally
                        _sourceDirectory = previous
                    End Try
                End If
            Next
            Throw New InvalidDataException("XAML element not found: " & name)
        End Function

        Public Property ImplicitStoryboardTriggersEnabled As Boolean = True

        Public Function ParseLegacyWindow(filePath As String) As SceneElement
            Dim root = ParseFile(filePath)
            Dim layout As New LegacyWindowLayout(_scene, _xmlElements)
            layout.Attach()
            Return root
        End Function

        Private Sub AttachVisualMasks()
            For Each pair In _xmlElements
                Dim visual = TryCast(pair.Key.SelectSingleNode("*[contains(local-name(), '.OpacityMask')]/*[local-name()='VisualBrush']"), XmlElement)
                If visual Is Nothing Then Continue For
                Dim match = System.Text.RegularExpressions.Regex.Match(visual.GetAttribute("Visual"), "ElementName\s*=\s*([^,}\s]+)")
                If Not match.Success Then Continue For
                Dim target As SceneElement = Nothing
                If Not _namedElements.TryGetValue(match.Groups(1).Value, target) Then Continue For
                Dim border = TryCast(target, Border)
                If border Is Nothing Then Continue For
                For Each descendant In _xmlElements
                    Dim node As XmlNode = descendant.Key
                    While node IsNot Nothing AndAlso node IsNot pair.Key
                        node = node.ParentNode
                    End While
                    If node Is pair.Key AndAlso TypeOf descendant.Value Is ImageElement Then
                        Dim image = DirectCast(descendant.Value, ImageElement)
                        image.OpacityMaskBrush = border.BackgroundBrush
                        If border.BackgroundBrush Is Nothing Then image.MaskBackgroundColor = Function() border.BackgroundColor
                        image.MaskCornerRadii = Function() border.CornerRadii
                    End If
                Next
            Next
        End Sub

        Public Function ParseItemTemplate(filePath As String, ownerName As String, values As IReadOnlyDictionary(Of String, String), width As Single,
                                          Optional container As RectangleElement = Nothing) As SceneElement
            Dim doc As New XmlDocument()
            doc.Load(filePath)
            Dim owner = doc.SelectNodes("//*").Cast(Of XmlElement)().First(Function(n) n.GetAttribute("x:Name") = ownerName)
            If container IsNot Nothing Then
                Dim item = doc.CreateElement("ListBoxItem", owner.NamespaceURI)
                If owner.HasAttribute("ItemContainerStyle") Then item.SetAttribute("Style", owner.GetAttribute("ItemContainerStyle"))
                owner.AppendChild(item)
                ApplyResourceStyle(item)
                ApplyRoundedSurface(container, item)
            End If
            Dim template = DirectCast(owner.SelectSingleNode("*[contains(local-name(), '.ItemTemplate')]/*[local-name()='DataTemplate']/*"), XmlElement)
            For Each node As XmlElement In template.SelectNodes("descendant-or-self::*")
                For Each attribute As XmlAttribute In node.Attributes
                    If attribute.Value.StartsWith("{Binding ", StringComparison.Ordinal) Then
                        Dim key = attribute.Value.Substring(9).TrimEnd("}"c).Trim()
                        Dim value As String = Nothing
                        attribute.Value = If(values.TryGetValue(key, value), value, String.Empty)
                    End If
                Next
            Next
            Dim root = ParseElement(template)
            Dim layout As New LegacyWindowLayout(_scene, _xmlElements)
            layout.Attach()
            root.Size = New Vector2(width, layout.Measure(root, width).Y)
            Return root
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

        Public Function BeginStoryboard(name As String) As IReadOnlyList(Of AnimationBase)
            Dim storyboard As XamlStoryboard = Nothing
            If Not _storyboards.TryGetValue(name, storyboard) Then
                Throw New KeyNotFoundException("Storyboard not found: " & name)
            End If
            Return storyboard.Begin(_scene.gameTime)
        End Function

#End Region

#Region "Element Factory Registration"

        Private Sub RegisterElementFactories()
            ' Root elements
            _elementFactories("Scene") = Function(e) CreateSceneRoot(e)
            _elementFactories("Window") = Function(e) CreateSceneRoot(e)
            _elementFactories("MetroWindow") = Function(e) CreateSceneRoot(e)
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
            _elementFactories("Label") = Function(e) CreateLabel(e)
            _elementFactories("Image") = Function(e) CreateImage(e)
            _elementFactories("Ellipse") = Function(e) CreateCircle(e)

            ' Input Controls
            _elementFactories("Button") = Function(e) CreateButton(e)
            _elementFactories("CheckBox") = Function(e) CreateCheckBox(e)
            _elementFactories("RadioButton") = Function(e) CreateRadioButton(e)
            _elementFactories("TextBox") = Function(e) CreateTextBox(e)
            _elementFactories("PasswordBox") = Function(e) CreatePasswordBox(e)
            _elementFactories("RichTextBox") = Function(e) CreateRichTextBox(e)
            _elementFactories("ToggleButton") = Function(e) CreateToggleButton(e)
            _elementFactories("ToggleSwitch") = Function(e) CreateToggleButton(e)
            _elementFactories("Slider") = Function(e) CreateSlider(e)
            _elementFactories("Trackbar") = Function(e) CreateSlider(e)
            _elementFactories("ProgressBar") = Function(e) CreateProgressBar(e)

            ' Advanced Controls
            _elementFactories("ComboBox") = Function(e) CreateComboBox(e)
            _elementFactories("ListBox") = Function(e) CreateListBox(e)
            _elementFactories("TabControl") = Function(e) CreateTabControl(e)
            _elementFactories("ScrollViewer") = Function(e) CreateScrollViewer(e)
            _elementFactories("wScrollViewer") = Function(e) CreateScrollViewer(e)
            _elementFactories("ClipboardHistoryScrollViewer") = Function(e) CreateScrollViewer(e)
            _elementFactories("Separator") = Function(e) CreateSeparator(e)
            _elementFactories("Expander") = Function(e) CreateExpander(e)
            _elementFactories("ToolTip") = Function(e) CreateToolTip(e)
            _elementFactories("GroupBox") = Function(e) CreateGroupBox(e)
            _elementFactories("NumericUpDown") = Function(e) CreateNumericUpDown(e)
            _elementFactories("Popup") = Function(e) CreatePopup(e)
            _elementFactories("ContextMenu") = Function(e) CreateContextMenu(e)
            _elementFactories("MenuItem") = Function(e) CreateMenuItem(e)
            _elementFactories("MediaElement") = Function(e) CreateMediaElement(e)
            _elementFactories("MediaControl") = Function(e) CreateMediaElement(e)
            _elementFactories("LoadingCircle") = Function(e) CreateLoadingIndicator(e)

            ' Layout
            _elementFactories("StackPanel") = Function(e) CreateStackPanel(e)
            _elementFactories("Canvas") = Function(e) CreateCanvas(e)
            _elementFactories("WrapPanel") = Function(e) CreateWrapPanel(e)
            _elementFactories("UniformGrid") = Function(e) CreateUniformGrid(e)
            _elementFactories("DockPanel") = Function(e) CreateDockPanel(e)
            _elementFactories("Grid") = Function(e) CreateGrid(e)
            _elementFactories("Panel") = Function(e) CreatePanel(e)
            _elementFactories("Border") = Function(e) CreateBorder(e)
        End Sub

#End Region

#Region "Element Parsing"

        Private Function ParseElement(xmlElement As XmlElement) As SceneElement
            ApplyResourceStyle(xmlElement)
            Dim elementName As String = xmlElement.LocalName

            ' Check if we have a factory for this element
            Dim element As SceneElement
            If Not _elementFactories.ContainsKey(elementName) Then
                ' Keep the remainder of the window usable.  A generic panel is the
                ' closest safe visual equivalent for an unknown legacy control and the
                ' build-time generator reports the exact item for user follow-up.
                element = CreatePanel(xmlElement)
                _unsupportedFeatures.Add($"Element {elementName} mapped to Panel")
            Else
                element = _elementFactories(elementName)(xmlElement)
            End If
            _xmlElements(xmlElement) = element

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
            ApplyXamlEffect(element, xmlElement)
            If TypeOf element Is RectangleElement Then
                ApplyBackgroundBrush(DirectCast(element, RectangleElement), xmlElement)
                ApplyRoundedSurface(DirectCast(element, RectangleElement), xmlElement)
            End If

            ' Parse children with proper z-index assignment
            ' WPF behavior: elements later in XAML appear on top (higher z-order)
            ' childIndex increases for each child, so later elements get higher z-index
            Dim childIndex As Integer = 0
            If TypeOf element Is TabControl Then
                ParseTabItems(xmlElement, DirectCast(element, TabControl))
                Return element
            End If

            For Each childNode As XmlNode In VisualChildNodes(xmlElement)
                If TypeOf childNode Is XmlElement Then
                    Dim childXml = DirectCast(childNode, XmlElement)
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
                        ElseIf TypeOf element Is DockPanel Then
                            Dim dockName = GetAttributeString(childXml, "DockPanel.Dock", "Left")
                            Dim dockSide As Dock = Dock.Left
                            [Enum].TryParse(dockName, True, dockSide)
                            DirectCast(element, DockPanel).AddChild(childElement, dockSide)
                        ElseIf TypeOf element Is ScrollViewer Then
                            Dim viewer = DirectCast(element, ScrollViewer)
                            If viewer.Content Is Nothing Then
                                viewer.Content = childElement
                            Else
                                Throw New InvalidDataException("ScrollViewer supports one content element. Wrap multiple controls in a Panel.")
                            End If
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

        Private Sub ApplyResourceStyle(element As XmlElement)
            CopyTemplateSurface(element, TryCast(element.SelectSingleNode("*[contains(local-name(), '.Template')]/*[local-name()='ControlTemplate']"), XmlElement))
            ApplyResolvedStyle(element)
        End Sub

        ' A common WPF template is a Border around a content presenter. Map its
        ' surface geometry onto the native control, not a WPF island or edited XAML.
        Private Sub CopyTemplateSurface(element As XmlElement, template As XmlElement, Optional depth As Integer = 0)
            If template Is Nothing OrElse depth > 8 Then Return
            Dim surface = TryCast(template.SelectSingleNode("*[local-name()='Border']"), XmlElement)
            If surface Is Nothing Then
                ' WPF ComboBox commonly delegates its closed surface to a
                ' ToggleButton in a Grid; exclude the Popup's dropdown border.
                Dim toggle = TryCast(template.SelectSingleNode("*[local-name()='Grid']/*[local-name()='ToggleButton']"), XmlElement)
                If toggle IsNot Nothing Then
                    Dim style = Resource(toggle, toggle.GetAttribute("Style"))
                    If style IsNot Nothing Then CopyTemplateSurface(element, TryCast(style.SelectSingleNode("*[local-name()='Setter'][@Property='Template']/*/*[local-name()='ControlTemplate']"), XmlElement), depth + 1)
                End If
                Return
            End If
            ' An explicit template brush is NOT a TemplateBinding: it wins over
            ' the control's own Background, as it does on the WPF combo surface.
            For Each brushName In {"Background", "BorderBrush"}
                Dim value = surface.GetAttribute(brushName)
                If value.Length > 0 AndAlso Not value.StartsWith("{TemplateBinding") Then element.SetAttribute(brushName, value)
            Next
            Dim effectNode = surface.SelectSingleNode("*[contains(local-name(), '.Effect')]")
            If effectNode IsNot Nothing AndAlso element.SelectSingleNode("*[contains(local-name(), '.Effect')]") Is Nothing Then
                Dim propertyNode = element.OwnerDocument.CreateElement(element.LocalName & ".Effect", element.NamespaceURI)
                For Each child In effectNode.ChildNodes.OfType(Of XmlElement)()
                    propertyNode.AppendChild(element.OwnerDocument.ImportNode(child, True))
                Next
                element.AppendChild(propertyNode)
            End If
            For Each name In {"CornerRadius", "BorderThickness", "BorderBrush"}
                If element.HasAttribute(name) Then Continue For
                If element.SelectSingleNode("*[contains(local-name(), '." & name & "')]") IsNot Nothing Then Continue For
                Dim value = surface.GetAttribute(name)
                If value.Length = 0 Then
                    Dim propertyNode = surface.SelectSingleNode("*[local-name()='Border." & name & "']")
                    If propertyNode IsNot Nothing Then value = propertyNode.InnerText.Trim()
                End If
                If value.Length > 0 AndAlso Not value.StartsWith("{TemplateBinding", StringComparison.Ordinal) Then element.SetAttribute(name, value)
            Next
        End Sub

        Private Sub ApplyRoundedSurface(target As RectangleElement, xml As XmlElement)
            Dim value = xml.GetAttribute("CornerRadius")
            If value.Length = 0 Then
                Dim node = xml.SelectSingleNode("*[contains(local-name(), '.CornerRadius')]")
                If node IsNot Nothing Then value = node.InnerText.Trim()
            End If
            If value.Length > 0 Then
                Try
                    If value.StartsWith("{StaticResource ", StringComparison.Ordinal) OrElse value.StartsWith("{DynamicResource ", StringComparison.Ordinal) Then
                        Dim key = value.Substring(value.IndexOf(" "c) + 1).TrimEnd("}"c).Trim()
                        Dim scope As XmlNode = xml
                        Dim resource As XmlElement = Nothing
                        While scope IsNot Nothing AndAlso resource Is Nothing
                            resource = scope.SelectNodes("*[contains(local-name(), '.Resources')]/*").Cast(Of XmlElement)().FirstOrDefault(Function(n) n.GetAttribute("x:Key") = key)
                            scope = scope.ParentNode
                        End While
                        If resource Is Nothing Then Throw New FormatException("CornerRadius resource not found: " & key)
                        value = resource.InnerText.Trim()
                    End If
                    target.CornerRadii = CornerRadii.Parse(value)
                Catch ex As Exception When TypeOf ex Is FormatException OrElse TypeOf ex Is ArgumentException OrElse TypeOf ex Is OverflowException
                    _unsupportedFeatures.Add(xml.LocalName & " CornerRadius: " & ex.Message)
                End Try
            End If
            If xml.HasAttribute("BorderThickness") Then target.BorderThickness = GetAttributeSingle(xml, "BorderThickness", target.BorderThickness)
            If xml.HasAttribute("BorderBrush") Then target.BorderColor = GetAttributeColor(xml, "BorderBrush", target.BorderColor)
        End Sub

        Private Sub ParseStoryboards(document As XmlDocument)
            For Each node As XmlNode In document.SelectNodes("//*[local-name()='Storyboard']")
                Dim storyboardXml = TryCast(node, XmlElement)
                If storyboardXml Is Nothing Then Continue For
                Dim name = GetAttributeString(storyboardXml, "x:Key", String.Empty)
                If String.IsNullOrWhiteSpace(name) Then name = GetAttributeString(storyboardXml, "x:Name", String.Empty)
                If String.IsNullOrWhiteSpace(name) Then name = GetAttributeString(storyboardXml, "Name", String.Empty)
                If String.IsNullOrWhiteSpace(name) Then Continue For
                _storyboards(name) = CreateStoryboard(storyboardXml, name)
            Next
        End Sub

        Private Function CreateStoryboard(storyboardXml As XmlElement, name As String) As XamlStoryboard
            Dim storyboard As New XamlStoryboard(name)
            For Each node As XmlNode In storyboardXml.ChildNodes
                Dim animationXml = TryCast(node, XmlElement)
                If animationXml Is Nothing Then Continue For
                If Not {"DoubleAnimation", "ColorAnimation", "DoubleAnimationUsingKeyFrames",
                        "ColorAnimationUsingKeyFrames", "ObjectAnimationUsingKeyFrames"}.Contains(animationXml.LocalName) Then Continue For

                Try
                    Dim targetName = GetAttributeString(animationXml, "Storyboard.TargetName", String.Empty)
                    Dim propertyPath = GetAttributeString(animationXml, "Storyboard.TargetProperty", String.Empty)
                    Dim target As SceneElement = Nothing
                    If String.IsNullOrWhiteSpace(targetName) OrElse Not _namedElements.TryGetValue(targetName, target) Then
                        Throw New InvalidDataException("Storyboard '" & name & "' has an unknown TargetName: " & targetName)
                    End If
                    Dim relative As Boolean
                    Dim targetProperty = CreateAnimationProperty(target, propertyPath, relative)
                    Dim repeatForever = GetAttributeString(animationXml, "RepeatBehavior", String.Empty).
                        Equals("Forever", StringComparison.OrdinalIgnoreCase)

                    Select Case animationXml.LocalName
                        Case "DoubleAnimation"
                            Dim duration = ParseDuration(GetAttributeString(animationXml, "Duration", "0:0:0.25"))
                            Dim fromValue As Double? = Nothing
                            Dim parsed As Double
                            If Double.TryParse(GetAttributeString(animationXml, "From", String.Empty), NumberStyles.Float,
                                               CultureInfo.InvariantCulture, parsed) Then fromValue = parsed
                            Dim toText = GetAttributeString(animationXml, "To", String.Empty)
                            If Not Double.TryParse(toText, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
                                Throw New InvalidDataException("DoubleAnimation requires a numeric To value for " & targetName & "." & propertyPath)
                            End If
                            storyboard.AddDouble(target, targetProperty, fromValue, parsed, duration,
                                                 CreateEasingFactory(animationXml), repeatForever, relative)
                        Case "ColorAnimation"
                            Dim duration = ParseDuration(GetAttributeString(animationXml, "Duration", "0:0:0.25"))
                            Dim fromColor As Color? = Nothing
                            Dim fromText = GetAttributeString(animationXml, "From", String.Empty)
                            If Not String.IsNullOrWhiteSpace(fromText) Then fromColor = ParseColor(fromText, Color.Transparent)
                            Dim toText = GetAttributeString(animationXml, "To", String.Empty)
                            If String.IsNullOrWhiteSpace(toText) Then Throw New InvalidDataException("ColorAnimation requires a To value.")
                            storyboard.AddColor(target, targetProperty, fromColor, ParseColor(toText, Color.Transparent),
                                                duration, CreateEasingFactory(animationXml), repeatForever)
                        Case "DoubleAnimationUsingKeyFrames"
                            Dim frames As New List(Of XamlDoubleKeyFrame)()
                            For Each frameNode As XmlNode In animationXml.ChildNodes
                                Dim frame = TryCast(frameNode, XmlElement)
                                If frame Is Nothing OrElse Not frame.LocalName.EndsWith("DoubleKeyFrame", StringComparison.Ordinal) Then Continue For
                                Dim value As Double
                                If Not Double.TryParse(GetAttributeString(frame, "Value", String.Empty), NumberStyles.Float,
                                                       CultureInfo.InvariantCulture, value) Then Continue For
                                frames.Add(New XamlDoubleKeyFrame With {
                                    .KeyTime = ParseDuration(GetAttributeString(frame, "KeyTime", "0:0:0")),
                                    .Value = value,
                                    .IsDiscrete = frame.LocalName.StartsWith("Discrete", StringComparison.Ordinal),
                                    .EasingFactory = CreateEasingFactory(frame)
                                })
                            Next
                            If frames.Count = 0 Then Throw New InvalidDataException("Double key-frame animation has no numeric frames.")
                            storyboard.AddDoubleKeyFrames(target, targetProperty, frames, frames.Max(Function(frame) frame.KeyTime), repeatForever)
                        Case "ColorAnimationUsingKeyFrames"
                            Dim frames As New List(Of XamlColorKeyFrame)()
                            For Each frameNode As XmlNode In animationXml.ChildNodes
                                Dim frame = TryCast(frameNode, XmlElement)
                                If frame Is Nothing OrElse Not frame.LocalName.EndsWith("ColorKeyFrame", StringComparison.Ordinal) Then Continue For
                                frames.Add(New XamlColorKeyFrame With {
                                    .KeyTime = ParseDuration(GetAttributeString(frame, "KeyTime", "0:0:0")),
                                    .Value = ParseColor(GetAttributeString(frame, "Value", String.Empty), Color.Transparent),
                                    .IsDiscrete = frame.LocalName.StartsWith("Discrete", StringComparison.Ordinal),
                                    .EasingFactory = CreateEasingFactory(frame)
                                })
                            Next
                            If frames.Count = 0 Then Throw New InvalidDataException("Color key-frame animation has no frames.")
                            storyboard.AddColorKeyFrames(target, targetProperty, frames, frames.Max(Function(frame) frame.KeyTime), repeatForever)
                        Case "ObjectAnimationUsingKeyFrames"
                            If Not TypeOf targetProperty Is CornerRadiusProperty Then Throw New NotSupportedException("Unsupported object animation property: " & propertyPath)
                            Dim frames As New List(Of XamlCornerRadiusKeyFrame)()
                            For Each frameNode As XmlNode In animationXml.ChildNodes
                                Dim frame = TryCast(frameNode, XmlElement)
                                If frame Is Nothing OrElse Not frame.LocalName.EndsWith("ObjectKeyFrame", StringComparison.Ordinal) Then Continue For
                                Dim valueText = GetAttributeString(frame, "Value", frame.InnerText).Trim()
                                frames.Add(New XamlCornerRadiusKeyFrame With {
                                    .KeyTime = ParseDuration(GetAttributeString(frame, "KeyTime", "0:0:0")),
                                    .Value = CornerRadii.Parse(valueText)
                                })
                            Next
                            If frames.Count = 0 Then Throw New NotSupportedException("Unsupported object key-frame values.")
                            storyboard.AddCornerRadiusKeyFrames(target, New CornerRadiiProperty(DirectCast(target, RectangleElement)), frames, frames.Max(Function(frame) frame.KeyTime), repeatForever)
                    End Select
                Catch ex As Exception
                    _unsupportedFeatures.Add("Storyboard " & name & ": " & ex.Message)
                End Try
            Next
            Return storyboard
        End Function

        Private Function CreateAnimationProperty(target As SceneElement, propertyPath As String,
                                                 ByRef relativeToCurrent As Boolean) As ElementProperty
            Dim normalized = propertyPath.Replace("(", String.Empty).Replace(")", String.Empty).
                Replace(" ", String.Empty).ToLowerInvariant()
            relativeToCurrent = False
            If normalized.Contains(".effect.") Then
                If target.VisualEffect Is Nothing Then Throw New NotSupportedException("Animation targets an effect without a native adapter: " & propertyPath)
                Return New XamlEffectProperty(target, normalized.Split("."c).Last())
            End If
            If normalized.Contains("gradientstops") AndAlso TypeOf target Is RectangleElement Then
                Dim match = Text.RegularExpressions.Regex.Match(normalized, "gradientstops\[(\d+)\]")
                Dim brush = DirectCast(target, RectangleElement).BackgroundBrush
                If match.Success AndAlso brush IsNot Nothing Then
                    Dim index = Integer.Parse(match.Groups(1).Value, CultureInfo.InvariantCulture)
                    If index >= 0 AndAlso index < brush.Stops.Count Then
                        Return New GradientStopProperty(target, brush.Stops(index), normalized.EndsWith("color"))
                    End If
                End If
                Throw New NotSupportedException("Missing gradient stop for " & propertyPath)
            End If
            If normalized.Contains("scaletransform.scalex") Then Return New ScaleXProperty(target)
            If normalized.Contains("scaletransform.scaley") Then Return New ScaleYProperty(target)
            If normalized.Contains("translatetransform.x") Then Return New TranslationXProperty(target)
            If normalized.Contains("translatetransform.y") Then Return New TranslationYProperty(target)
            If normalized.EndsWith("canvas.left") OrElse normalized.EndsWith("position.x") OrElse
               normalized.EndsWith("left") Then Return New LeftProperty(target)
            If normalized.EndsWith("canvas.top") OrElse normalized.EndsWith("position.y") OrElse
               normalized.EndsWith("top") Then Return New TopProperty(target)
            If normalized.EndsWith("width") Then Return New WidthProperty(target)
            If normalized.EndsWith("height") Then Return New HeightProperty(target)
            If normalized.EndsWith("opacity") Then Return New OpacityProperty(target)
            If normalized.EndsWith("background") OrElse normalized.EndsWith("backgroundcolor") OrElse
               (normalized.Contains("background") AndAlso normalized.EndsWith("gradientstop.color")) Then
                If TypeOf target Is RectangleElement Then Return New BackgroundColorProperty(target)
            End If
            If normalized.EndsWith("foreground") OrElse normalized.EndsWith("foregroundcolor") Then
                If TypeOf target Is TextElement Then Return New ForegroundColorProperty(DirectCast(target, TextElement))
            End If
            If normalized.EndsWith("fill") OrElse normalized.EndsWith("fillcolor") Then
                If TypeOf target Is PolygonElement Then Return New FillColorProperty(target)
            End If
            If (normalized.EndsWith("borderbrush") OrElse normalized.EndsWith("stroke") OrElse
                normalized.EndsWith("strokecolor")) AndAlso TypeOf target Is Border Then
                Return New BorderColorProperty(DirectCast(target, Border))
            End If
            If normalized.EndsWith("border.cornerradius") OrElse normalized.EndsWith("cornerradius") Then
                If TypeOf target Is RectangleElement Then Return New CornerRadiusProperty(DirectCast(target, RectangleElement))
            End If
            If normalized.EndsWith("value") AndAlso TypeOf target Is Trackbar Then Return New TrackbarValueProperty(target)
            Throw New NotSupportedException("Unsupported Storyboard.TargetProperty: " & propertyPath)
        End Function

        Private Sub ApplyBackgroundBrush(target As RectangleElement, xml As XmlElement)
            target.BackgroundBrush = ReadGradientBrush(xml, If(xml.LocalName = "Rectangle", "Fill", "Background"))
            If TypeOf target Is Button Then
                Dim button = DirectCast(target, Button)
                button.MouseOverBackgroundBrush = ReadGradientBrush(xml, "MouseOverBackground")
                button.MouseDownBackgroundBrush = ReadGradientBrush(xml, "MouseDownBackground")
            End If
        End Sub

        Private Function CreateEasingFactory(animationXml As XmlElement) As Func(Of EaseFunction)
            Dim easingXml As XmlElement = Nothing
            For Each descendant As XmlNode In animationXml.SelectNodes(".//*[local-name()='SineEase' or local-name()='PowerEase' or local-name()='CircleEase']")
                easingXml = TryCast(descendant, XmlElement)
                If easingXml IsNot Nothing Then Exit For
            Next
            If easingXml Is Nothing Then Return Function() New SineEase(EaseType.Ignore)

            Dim mode As EaseType = EaseType.EaseInOut
            [Enum].TryParse(GetAttributeString(easingXml, "EasingMode", "EaseInOut"), True, mode)
            Select Case easingXml.LocalName
                Case "PowerEase"
                    Dim power = GetAttributeDouble(easingXml, "Power", 2.0R)
                    Return Function() New PowerEase(mode, power)
                Case "CircleEase"
                    Return Function() New CircleEase(mode)
                Case Else
                    Return Function() New SineEase(mode)
            End Select
        End Function

        Private Shared Function ParseDuration(value As String) As TimeSpan
            If String.IsNullOrWhiteSpace(value) OrElse value.Equals("Automatic", StringComparison.OrdinalIgnoreCase) Then
                Return TimeSpan.FromMilliseconds(250)
            End If
            Dim duration As TimeSpan
            If TimeSpan.TryParse(value, CultureInfo.InvariantCulture, duration) Then Return duration
            If value.EndsWith("ms", StringComparison.OrdinalIgnoreCase) Then
                Dim milliseconds As Double
                If Double.TryParse(value.Substring(0, value.Length - 2), NumberStyles.Float,
                                   CultureInfo.InvariantCulture, milliseconds) Then Return TimeSpan.FromMilliseconds(milliseconds)
            End If
            Throw New FormatException("Unsupported XAML Duration: " & value)
        End Function

        Private Sub AttachEventTriggers(document As XmlDocument)
            For Each node As XmlNode In document.SelectNodes("//*[local-name()='EventTrigger']")
                Dim trigger = TryCast(node, XmlElement)
                If trigger Is Nothing Then Continue For
                Dim ownerNode As XmlNode = trigger.ParentNode
                Dim owner As SceneElement = Nothing
                While ownerNode IsNot Nothing AndAlso owner Is Nothing
                    Dim ownerXml = TryCast(ownerNode, XmlElement)
                    If ownerXml IsNot Nothing Then _xmlElements.TryGetValue(ownerXml, owner)
                    ownerNode = ownerNode.ParentNode
                End While
                If owner Is Nothing Then Continue For

                Dim begin = TryCast(trigger.SelectSingleNode(".//*[local-name()='BeginStoryboard']"), XmlElement)
                If begin Is Nothing Then Continue For
                Dim key = ParseStaticResourceKey(GetAttributeString(begin, "Storyboard", String.Empty))
                If String.IsNullOrWhiteSpace(key) OrElse Not _storyboards.ContainsKey(key) Then Continue For
                Dim eventName = GetAttributeString(trigger, "RoutedEvent", String.Empty).ToLowerInvariant()
                Dim storyboardKey = key
                _explicitStoryboardTriggers.Add(storyboardKey)
                Select Case eventName
                    Case "loaded"
                        AddHandler owner.Loaded, Sub() BeginStoryboard(storyboardKey)
                    Case "mouseenter"
                        AddHandler owner.MouseEnter, Sub() BeginStoryboard(storyboardKey)
                    Case "mouseleave"
                        AddHandler owner.MouseLeave, Sub() BeginStoryboard(storyboardKey)
                    Case "click", "button.click", "mouseleftbuttonup"
                        AddHandler owner.MouseLeftClick, Sub(point) BeginStoryboard(storyboardKey)
                    Case "mouseleftbuttondown"
                        AddHandler owner.MouseLeftDown, Sub(point) BeginStoryboard(storyboardKey)
                End Select
            Next
        End Sub

        ''' <summary>
        ''' Legacy wShare controls conventionally keep storyboards named after
        ''' code-behind handlers (MouseEnter/MouseLeave/Loaded/Click). Project-Z
        ''' binds those resources automatically, so linked XAML needs no copied or
        ''' hand-written hover code.
        ''' </summary>
        Private Sub AttachImplicitStoryboardTriggers(root As SceneElement)
            If root Is Nothing OrElse Not ImplicitStoryboardTriggersEnabled Then Return
            For Each pair In _storyboards
                If _explicitStoryboardTriggers.Contains(pair.Key) Then Continue For
                Dim key = pair.Key
                Select Case key.ToLowerInvariant()
                    Case "mouseenter"
                        AddHandler root.MouseEnter, Sub() BeginStoryboard(key)
                    Case "mouseleave"
                        AddHandler root.MouseLeave, Sub() BeginStoryboard(key)
                    Case "loaded"
                        AddHandler root.Loaded, Sub() BeginStoryboard(key)
                    Case "click", "mouseleftbuttonup"
                        AddHandler root.MouseLeftClick, Sub(point) BeginStoryboard(key)
                    Case "mouseleftbuttondown"
                        AddHandler root.MouseLeftDown, Sub(point) BeginStoryboard(key)
                End Select
            Next
        End Sub

        Private Shared Function ParseStaticResourceKey(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then Return String.Empty
            Dim text = value.Trim().Trim("{"c, "}"c)
            Const prefix = "StaticResource "
            If text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then Return text.Substring(prefix.Length).Trim()
            Return text
        End Function

        Private Iterator Function VisualChildNodes(parent As XmlElement) As IEnumerable(Of XmlNode)
            For Each node As XmlNode In parent.ChildNodes
                If Not TypeOf node Is XmlElement Then Continue For
                Dim child = DirectCast(node, XmlElement)
                If Not child.LocalName.Contains(".") Then
                    Yield child
                ElseIf child.LocalName.EndsWith(".Content", StringComparison.Ordinal) OrElse
                       child.LocalName.EndsWith(".Child", StringComparison.Ordinal) Then
                    For Each contentNode As XmlNode In child.ChildNodes
                        If TypeOf contentNode Is XmlElement Then Yield contentNode
                    Next
                End If
            Next
        End Function

        Private Sub ParseTabItems(xmlElement As XmlElement, control As TabControl)
            For Each node As XmlNode In VisualChildNodes(xmlElement)
                If Not TypeOf node Is XmlElement Then Continue For
                Dim tabXml = DirectCast(node, XmlElement)
                If tabXml.LocalName <> "TabItem" Then Continue For
                Dim contentNodes = VisualChildNodes(tabXml).OfType(Of XmlElement)().ToArray()
                Dim content As SceneElement = Nothing
                If contentNodes.Length = 1 Then
                    content = ParseElement(contentNodes(0))
                ElseIf contentNodes.Length > 1 Then
                    Dim stack As New StackPanel(_scene)
                    For Each contentNode In contentNodes
                        stack.Children.Add(ParseElement(contentNode))
                    Next
                    content = stack
                End If
                control.AddTab(GetAttributeString(tabXml, "Header", "Tab"), content)
            Next
            Dim selected = GetAttributeInteger(xmlElement, "SelectedIndex", 0)
            If control.Tabs.Count > 0 Then control.SelectedIndex = Math.Max(0, Math.Min(selected, control.Tabs.Count - 1))
        End Sub

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
            If xmlElement.HasAttribute("Visibility") Then
                Dim visibility = GetAttributeString(xmlElement, "Visibility", "Visible")
                element.isVisible = (visibility.ToLower() <> "collapsed" AndAlso visibility.ToLower() <> "hidden")
            End If

            ' IsEnabled
            If xmlElement.HasAttribute("IsEnabled") Then element.isEnabled = GetAttributeBoolean(xmlElement, "IsEnabled", True)

            If xmlElement.HasAttribute("Opacity") Then
                element.Opacity = Math.Clamp(GetAttributeSingle(xmlElement, "Opacity", 1.0F), 0.0F, 1.0F)
            End If

            If xmlElement.HasAttribute("IsHitTestVisible") Then
                element.isMouseBypassEnabled = Not GetAttributeBoolean(xmlElement, "IsHitTestVisible", True)
            End If

            ' WPF panels and images commonly rely on ClipToBounds for masks.
            If xmlElement.HasAttribute("ClipToBounds") Then
                element.Clip = GetAttributeBoolean(xmlElement, "ClipToBounds", False)
            End If

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
            text.UseXamlTextLayout = True
            Dim alignment As HorizontalAlignment
            If [Enum].TryParse(xmlElement.GetAttribute("TextAlignment"), True, alignment) Then text.TextAlignment = alignment

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

        Private Function CreateLabel(xmlElement As XmlElement) As SceneElement
            Dim label As New Label(_scene)
            label.Content = GetAttributeString(xmlElement, "Content", xmlElement.InnerText.Trim())
            label.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", Color.White)
            Return label
        End Function

        Private Function CreateImage(xmlElement As XmlElement) As SceneElement
            Dim image As New ImageElement(_scene)
            image.Source = GetAttributeString(xmlElement, "Source", String.Empty)
            Dim stretchName = GetAttributeString(xmlElement, "Stretch", "Uniform")
            Dim stretch As ImageStretch = ImageStretch.Uniform
            [Enum].TryParse(stretchName, True, stretch)
            image.Stretch = stretch
            Dim resolved = ResolveImageSource(image.Source)
            If Not String.IsNullOrWhiteSpace(resolved) Then
                image.Source = resolved
                Try
                    image.Load(resolved)
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ArgumentException
                    _unsupportedFeatures.Add("Image could not be loaded: " & resolved & " (" & ex.Message & ")")
                End Try
            End If
            Return image
        End Function

        Private Function ResolveImageSource(source As String) As String
            If String.IsNullOrWhiteSpace(source) Then Return String.Empty

            Dim normalized = Uri.UnescapeDataString(source.Trim()).Replace("/"c, IO.Path.DirectorySeparatorChar)
            Dim componentMarker = ";component" & IO.Path.DirectorySeparatorChar
            Dim markerIndex = normalized.IndexOf(componentMarker, StringComparison.OrdinalIgnoreCase)
            If markerIndex >= 0 Then normalized = normalized.Substring(markerIndex + componentMarker.Length)
            If normalized.StartsWith("pack:", StringComparison.OrdinalIgnoreCase) Then
                Dim commaIndex = normalized.LastIndexOf(","c)
                If commaIndex >= 0 Then normalized = normalized.Substring(commaIndex + 1)
            End If
            normalized = normalized.TrimStart(IO.Path.DirectorySeparatorChar)

            ' The old Jack/mask artwork is a compatibility identifier only.  Resolve it
            ' to the current heirowSnap artwork without touching linked legacy XAML.
            Dim fileName = IO.Path.GetFileName(normalized)
            If fileName.IndexOf("jack", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               fileName.Equals("mask_thumb.ico", StringComparison.OrdinalIgnoreCase) Then
                normalized = IO.Path.Combine("Resources", "heirowSnap.png")
            End If

            Dim candidates As New List(Of String)()
            If IO.Path.IsPathRooted(source) Then candidates.Add(source)
            If Not String.IsNullOrWhiteSpace(_sourceDirectory) Then
                candidates.Add(IO.Path.Combine(_sourceDirectory, normalized))
                candidates.Add(IO.Path.Combine(_sourceDirectory, "Resources", IO.Path.GetFileName(normalized)))
            End If
            candidates.Add(IO.Path.Combine(AppContext.BaseDirectory, normalized))
            candidates.Add(IO.Path.Combine(AppContext.BaseDirectory, "LegacyXaml", "wView", normalized))
            candidates.Add(IO.Path.Combine(AppContext.BaseDirectory, "LegacyXaml", "wView", "Resources", IO.Path.GetFileName(normalized)))

            For Each candidate In candidates
                If File.Exists(candidate) Then Return IO.Path.GetFullPath(candidate)
            Next
            Return String.Empty
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
            button.AutoSizeWidthOnTextOverflow = GetAttributeBoolean(xmlElement, "AutoSizeWidthOnTextOverflow", False)
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

        Private Function CreatePasswordBox(xmlElement As XmlElement) As SceneElement
            Dim password As New PasswordBox(_scene)
            password.Password = GetAttributeString(xmlElement, "Password", String.Empty)
            password.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", password.ForegroundColor)
            password.BackgroundColor = GetAttributeColor(xmlElement, "Background", password.BackgroundColor)
            Return password
        End Function

        Private Function CreateRichTextBox(xmlElement As XmlElement) As SceneElement
            Dim rich As New RichTextBox(_scene)
            rich.Text = GetAttributeString(xmlElement, "Text", xmlElement.InnerText.Trim())
            rich.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", rich.ForegroundColor)
            rich.BackgroundColor = GetAttributeColor(xmlElement, "Background", rich.BackgroundColor)
            rich.AcceptsReturn = GetAttributeBoolean(xmlElement, "AcceptsReturn", True)
            Return rich
        End Function

        Private Function CreateToggleButton(xmlElement As XmlElement) As SceneElement
            Dim toggle As New ToggleButton(_scene)
            If xmlElement.HasAttribute("Width") OrElse xmlElement.HasAttribute("Height") Then toggle.AutoSize = ButtonAutoSize.None
            toggle.Text = GetAttributeString(xmlElement, "Content", xmlElement.InnerText.Trim())
            toggle.IsChecked = GetAttributeBoolean(xmlElement, "IsChecked", False)
            toggle.BackgroundColor = GetAttributeColor(xmlElement, "Background", toggle.BackgroundColor)
            toggle.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", toggle.ForegroundColor)
            Return toggle
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
            progressBar.BorderThickness = GetAttributeSingle(xmlElement, "BorderThickness", 0)
            Dim brush = TryCast(xmlElement.SelectSingleNode("*[local-name()='ProgressBar.Foreground']/*[local-name()='LinearGradientBrush']"), XmlElement)
            If brush Is Nothing Then
                Dim reference = xmlElement.GetAttribute("Style").Trim("{"c, "}"c).Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
                If reference.Length = 2 AndAlso (reference(0) = "DynamicResource" OrElse reference(0) = "StaticResource") Then
                    ' Resolve the nearest resource scope first. Legacy track+mask templates
                    ' map to a fixed gradient clipped by Value, rather than a solid fill.
                    Dim scope As XmlNode = xmlElement
                    While scope IsNot Nothing AndAlso brush Is Nothing
                        For Each style As XmlElement In scope.SelectNodes("*[contains(local-name(), '.Resources')]/*[local-name()='Style']")
                            If style.GetAttribute("Key", "http://schemas.microsoft.com/winfx/2006/xaml") <> reference(1) Then Continue For
                            brush = TryCast(style.SelectSingleNode(".//*[local-name()='LinearGradientBrush']"), XmlElement)
                            Exit For
                        Next
                        scope = scope.ParentNode
                    End While
                End If
            End If
            If brush IsNot Nothing Then
                progressBar.GradientStart = ParseGradientPoint(brush.GetAttribute("StartPoint"), Vector2.Zero)
                progressBar.GradientEnd = ParseGradientPoint(brush.GetAttribute("EndPoint"), Vector2.One)
                For Each stopNode As XmlElement In brush.SelectNodes("*[local-name()='GradientStop']")
                    progressBar.FillGradientStops.Add(New ProgressGradientStop With {
                        .Offset = GetAttributeSingle(stopNode, "Offset", 0),
                        .Color = GetAttributeColor(stopNode, "Color", Color.White)})
                Next
            End If
            Return progressBar
        End Function

        Private Shared Function ParseGradientPoint(value As String, fallback As Vector2) As Vector2
            Dim parts = value.Split(","c)
            Dim x, y As Single
            If parts.Length = 2 AndAlso Single.TryParse(parts(0), NumberStyles.Float, CultureInfo.InvariantCulture, x) AndAlso
                Single.TryParse(parts(1), NumberStyles.Float, CultureInfo.InvariantCulture, y) Then Return New Vector2(x, y)
            Return fallback
        End Function

        Private Function CreateStackPanel(xmlElement As XmlElement) As SceneElement
            Dim stackPanel As New StackPanel(_scene)
            Dim orientationStr = GetAttributeString(xmlElement, "Orientation", "Vertical")
            stackPanel.Orientation = If(orientationStr.ToLower() = "horizontal", Orientation.Horizontal, Orientation.Vertical)
            stackPanel.Spacing = GetAttributeSingle(xmlElement, "Spacing", 4.0F)
            stackPanel.BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent)
            Return stackPanel
        End Function

        Private Function CreateCanvas(xmlElement As XmlElement) As SceneElement
            Dim canvas As New CanvasPanel(_scene)
            canvas.BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent)
            Return canvas
        End Function

        Private Function CreateWrapPanel(xmlElement As XmlElement) As SceneElement
            Dim panel As New WrapPanel(_scene)
            panel.Orientation = If(GetAttributeString(xmlElement, "Orientation", "Horizontal").Equals("Vertical", StringComparison.OrdinalIgnoreCase),
                                   Orientation.Vertical, Orientation.Horizontal)
            panel.ItemWidth = GetAttributeSingle(xmlElement, "ItemWidth", 0)
            panel.ItemHeight = GetAttributeSingle(xmlElement, "ItemHeight", 0)
            panel.HorizontalSpacing = GetAttributeSingle(xmlElement, "HorizontalSpacing", panel.HorizontalSpacing)
            panel.VerticalSpacing = GetAttributeSingle(xmlElement, "VerticalSpacing", panel.VerticalSpacing)
            panel.BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent)
            Return panel
        End Function

        Private Function CreateUniformGrid(xmlElement As XmlElement) As SceneElement
            Dim grid As New UniformGrid(_scene) With {
                .Rows = GetAttributeInteger(xmlElement, "Rows", 0),
                .Columns = GetAttributeInteger(xmlElement, "Columns", 0),
                .BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent)
            }
            Return grid
        End Function

        Private Function CreateDockPanel(xmlElement As XmlElement) As SceneElement
            Dim panel As New DockPanel(_scene) With {
                .LastChildFill = GetAttributeBoolean(xmlElement, "LastChildFill", True),
                .BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent)
            }
            Return panel
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
            Dim border As New Border(_scene)
            border.BackgroundColor = GetAttributeColor(xmlElement, "Background", New Color(50, 50, 50))
            border.BorderColor = GetAttributeColor(xmlElement, "BorderBrush", border.BorderColor)
            border.BorderThickness = GetAttributeSingle(xmlElement, "BorderThickness", border.BorderThickness)
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
            scrollViewer.WheelScrollAmount = GetAttributeSingle(xmlElement, "WheelScrollAmount", scrollViewer.WheelScrollAmount)
            scrollViewer.ScrollDeceleration = GetAttributeSingle(xmlElement, "ScrollDeceleration", scrollViewer.ScrollDeceleration)
            scrollViewer.MaximumScrollVelocity = GetAttributeSingle(xmlElement, "MaximumScrollVelocity", scrollViewer.MaximumScrollVelocity)

            Return scrollViewer
        End Function

        Private Function CreatePopup(xmlElement As XmlElement) As SceneElement
            Dim popup As New Popup(_scene)
            popup.BackgroundColor = GetAttributeColor(xmlElement, "Background", Color.Transparent)
            popup.IsOpen = GetAttributeBoolean(xmlElement, "IsOpen", False)
            Return popup
        End Function

        Private Function CreateContextMenu(xmlElement As XmlElement) As SceneElement
            Dim menu As New ContextMenu(_scene)
            menu.BackgroundColor = GetAttributeColor(xmlElement, "Background", menu.BackgroundColor)
            menu.IsOpen = GetAttributeBoolean(xmlElement, "IsOpen", False)
            Return menu
        End Function

        Private Function CreateMenuItem(xmlElement As XmlElement) As SceneElement
            Dim item As New MenuItem(_scene)
            item.Header = GetAttributeString(xmlElement, "Header", xmlElement.InnerText.Trim())
            item.BackgroundColor = GetAttributeColor(xmlElement, "Background", item.BackgroundColor)
            item.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", item.ForegroundColor)
            Return item
        End Function

        Private Function CreateMediaElement(xmlElement As XmlElement) As SceneElement
            Dim media As New MediaElement(_scene)
            media.Source = GetAttributeString(xmlElement, "Source", String.Empty)
            media.Volume = GetAttributeDouble(xmlElement, "Volume", 1.0R)
            Dim stretch As ImageStretch = ImageStretch.Uniform
            [Enum].TryParse(GetAttributeString(xmlElement, "Stretch", "Uniform"), True, stretch)
            media.Stretch = stretch
            If File.Exists(media.Source) Then media.Open(media.Source)
            Return media
        End Function

        Private Function CreateLoadingIndicator(xmlElement As XmlElement) As SceneElement
            Dim indicator As New LoadingIndicator(_scene)
            indicator.ForegroundColor = GetAttributeColor(xmlElement, "Foreground", Color.White)
            Return indicator
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
            Return ParseColor(ResolveColorText(xmlElement, str), defaultValue)
        End Function

        Private Function ParseColor(colorString As String, defaultValue As Color) As Color
            colorString = colorString.Trim()

            ' scRGB channels are linear light; convert RGB to the renderer's sRGB bytes.
            If colorString.StartsWith("sc#", StringComparison.OrdinalIgnoreCase) Then
                Try
                    Dim components = colorString.Substring(3).Split(","c).
                        Select(Function(value) Double.Parse(value.Trim(), CultureInfo.InvariantCulture)).ToArray()
                    If components.Length = 4 Then
                        Return XamlColorInterpolation.FromScRgb(components(0), components(1), components(2), components(3))
                    End If
                Catch
                End Try
            End If

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
