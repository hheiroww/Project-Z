Imports System.Globalization
Imports Microsoft.Xna.Framework
Imports ProjectZ.ShaderEditor
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.Designer
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Windows.Composition

Namespace Windows.ShaderEditing
    Friend NotInheritable Class SimpleShaderView
        Private Shared ReadOnly blendModes As String() = {"Normal", "Add", "Multiply", "Screen", "Overlay", "Subtract"}
        Private Shared ReadOnly shaders As String() = {"", "Color", "Source", "Gradient", "Checker", "Noise"}
        Private Const MaximumRows As Integer = 6
        Private Const RowPitch As Integer = 39

        Private ReadOnly scene As Scene
        Private ReadOnly colorChannel, alphaChannel, upButton, downButton, removeButton As Button
        Private ReadOnly addShader As ComboBox
        Private ReadOnly scroll As CollectionScrollBar
        Private ReadOnly channelPanel, layersPanel, rowParent, inspector, propertiesPanel As SceneElement
        Private ReadOnly shaderName, layerNameLabel, valueLabel, status As TextElement
        Private ReadOnly layerNameInput As Textbox
        Private ReadOnly valueInput As DragNumberBox
        Private ReadOnly valueKnob As RotaryKnob
        Private ReadOnly rows As New List(Of LayerRow)
        Private ReadOnly colorFields As New List(Of ColorField)
        Private ReadOnly layers As New List(Of LayerEntry)
        Private document As ShaderDocument
        Private output As ShaderNode
        Private baseNode As ShaderNode
        Private valueNode As ShaderNode
        Private valueScale As Double = 1
        Private appearance As WindowAppearanceSettings
        Private alpha As Boolean
        Private binding As Boolean
        Private selected As Guid?

        Public ReadOnly Property Root As SceneElement
        Public Event GraphChanged As EventHandler

        Public Sub New(scene As Scene)
            Me.scene = scene
            Dim parser As SceneXamlParser = Nothing
            Root = ShaderEditorChrome.LoadView(scene, "SimpleShaderView.xaml", parser)
            colorChannel = parser.FindName(Of Button)("ColorChannel")
            alphaChannel = parser.FindName(Of Button)("AlphaChannel")
            upButton = parser.FindName(Of Button)("Up")
            downButton = parser.FindName(Of Button)("Down")
            removeButton = parser.FindName(Of Button)("Remove")
            addShader = parser.FindName(Of ComboBox)("AddShader")
            scroll = parser.FindName(Of CollectionScrollBar)("LayerScroll")
            shaderName = parser.FindName(Of TextElement)("ShaderName")
            layerNameLabel = parser.FindName(Of TextElement)("LayerNameLabel")
            layerNameInput = parser.FindName(Of Textbox)("LayerNameInput")
            valueLabel = parser.FindName(Of TextElement)("ValueLabel")
            valueInput = parser.FindName(Of DragNumberBox)("ValueInput")
            valueKnob = parser.FindName(Of RotaryKnob)("ValueKnob")
            status = parser.FindName(Of TextElement)("Status")

            channelPanel = parser.FindName(Of SceneElement)("ChannelPanel")
            layersPanel = parser.FindName(Of SceneElement)("LayersPanel")
            rowParent = parser.FindName(Of SceneElement)("LayerRows")
            inspector = parser.FindName(Of SceneElement)("Inspector")
            propertiesPanel = parser.FindName(Of SceneElement)("PropertiesPanel")
            For index = 0 To MaximumRows - 1
                Dim row = New LayerRow(Me, index * RowPitch)
                rows.Add(row)
                rowParent.Children.Add(row.Root)
            Next
            Dim colorParent = parser.FindName(Of SceneElement)("ColorFields")
            For index = 0 To 1
                Dim field = New ColorField(Me, index * 46)
                colorFields.Add(field)
                colorParent.Children.Add(field.Root)
            Next
            scroll.VisibleItems = MaximumRows
            scroll.FirstIndex = 0
            AddHandler scroll.OffsetChanged, Sub(first) RenderRows()
            AddHandler scene.OnMouseWheel, Sub(delta, point)
                                               If Root.isVisible AndAlso rowParent.Rectangle.Contains(point) Then scroll.Scroll(delta)
                                           End Sub
            AddHandler colorChannel.MouseLeftClick, Sub(point) SelectChannel(False)
            AddHandler alphaChannel.MouseLeftClick, Sub(point) SelectChannel(True)
            AddHandler addShader.SelectionChanged, Sub(sender, index) AddLayer(index)
            AddHandler upButton.MouseLeftClick, Sub(point) MoveLayer(-1)
            AddHandler downButton.MouseLeftClick, Sub(point) MoveLayer(1)
            AddHandler removeButton.MouseLeftClick, Sub(point) RemoveLayer()
            AddHandler layerNameInput.Submitted, AddressOf CommitName
            AddHandler layerNameInput.Deselected, AddressOf CommitName
            AddHandler valueInput.ValueChanged, Sub(value) ChangeValue(value, False)
            AddHandler valueKnob.ValueChanged, Sub(value) ChangeValue(value, True)
            appearance = WindowAppearance.Default.Settings
            SelectChannel(False)
            AddHandler Root.SizeChanged, Sub(previous, current) Layout(current)
            AddHandler Root.VisibilityChanged, Sub()
                                                   If Root.isVisible Then Return
                                                   addShader.IsDropDownOpen = False
                                                   For Each row In rows
                                                       row.CloseMenu()
                                                   Next
                                               End Sub
            Layout(Root.Size)
        End Sub

        Public Sub SetDocument(value As ShaderDocument)
            document = value
            selected = Nothing
            scroll.FirstIndex = 0
            status.Text = ""
            Refresh()
        End Sub

        Public Sub Refresh()
            output = document?.Nodes.Find(Function(node) node.Kind = "Output")
            layers.Clear()
            baseNode = If(output Is Nothing, Nothing, InputNode(output, ChannelPort))
            Dim visited As New HashSet(Of Guid)
            While baseNode IsNot Nothing AndAlso baseNode.Kind = BlendKind AndAlso visited.Add(baseNode.Id)
                Dim source = InputNode(baseNode, "Layer")
                If alpha AndAlso source IsNot Nothing AndAlso (source.Kind = "Alpha" OrElse source.Kind = "Red") Then
                    Dim componentSource = InputNode(source, "Input")
                    If componentSource IsNot Nothing AndAlso ((source.Kind = "Alpha" AndAlso componentSource.Kind = "Source") OrElse
                       (source.Kind = "Red" AndAlso IsPattern(componentSource.Kind))) Then source = componentSource
                End If
                layers.Add(New LayerEntry(baseNode, source))
                baseNode = InputNode(baseNode, "Base")
            End While
            If SelectedIndex() < 0 Then selected = Nothing
            scroll.ItemCount = If(output Is Nothing, 0, layers.Count + 1)
            Dim index = If(selected.HasValue, SelectedIndex(), layers.Count)
            If index < scroll.FirstIndex Then scroll.FirstIndex = index
            If selected.HasValue AndAlso index >= scroll.FirstIndex + scroll.VisibleItems Then scroll.FirstIndex = index - scroll.VisibleItems + 1
            scroll.FirstIndex = Math.Min(scroll.FirstIndex, Math.Max(0, scroll.ItemCount - scroll.VisibleItems))
            scroll.isVisible = scroll.ItemCount > scroll.VisibleItems
            RenderRows()
            ShowAttributes()
        End Sub

        Public Sub ApplyAppearance(settings As WindowAppearanceSettings)
            appearance = settings
            ShaderEditorChrome.ApplyTheme(Root, settings)
            status.ForegroundColor = settings.AccentColor
            scroll.AccentColor = settings.AccentColor
            ApplyMenuTheme(addShader)
            For Each row In rows
                row.ApplyAppearance()
            Next
            For Each field In colorFields
                field.ApplyAppearance()
            Next
            StyleSelection()
        End Sub

        Private Sub Layout(size As Vector2)
            Dim rootCanvas = DirectCast(Root, CanvasPanel)
            Dim extraWidth = Math.Max(0, size.X - 650)
            Dim count = Math.Clamp(CInt(Math.Floor((size.Y - 284) / RowPitch)), 3, MaximumRows)
            scroll.VisibleItems = count
            Dim rowHeight = count * RowPitch
            channelPanel.Size = New Vector2(channelPanel.Size.X, size.Y)
            layersPanel.Size = New Vector2(533 + extraWidth, rowHeight + 10)
            rowParent.Size = New Vector2(509 + extraWidth, rowHeight)
            scroll.Size = New Vector2(scroll.Size.X, rowHeight)
            rootCanvas.SetChildPosition(scroll, New Vector2(635 + extraWidth, 49))
            rootCanvas.SetChildPosition(addShader, New Vector2(273 + extraWidth, 4))
            rootCanvas.SetChildPosition(upButton, New Vector2(419 + extraWidth, 4))
            rootCanvas.SetChildPosition(downButton, New Vector2(480 + extraWidth, 4))
            rootCanvas.SetChildPosition(removeButton, New Vector2(555 + extraWidth, 4))
            rootCanvas.SetChildPosition(inspector, New Vector2(117, 64 + rowHeight))
            inspector.Size = New Vector2(533 + extraWidth, inspector.Size.Y)
            propertiesPanel.Size = New Vector2(533 + extraWidth, propertiesPanel.Size.Y)
            layerNameInput.Size = New Vector2(403 + extraWidth, layerNameInput.Size.Y)
            For Each row In rows
                row.Resize(extraWidth)
            Next
            Refresh()
        End Sub

        Private Sub ApplyMenuTheme(menu As ComboBox)
            menu.ItemAppearance = Sub(index, background, label)
                                      background.BackgroundColor = If(index = menu.SelectedIndex,
                                          Color.Lerp(appearance.SurfaceColor, appearance.AccentColor, 0.2F), appearance.SurfaceColor)
                                      background.BorderColor = Color.Lerp(appearance.SurfaceColor, appearance.TextColor, 0.2F)
                                      label.ForegroundColor = appearance.TextColor
                                  End Sub
        End Sub

        Private Sub SelectChannel(useAlpha As Boolean)
            addShader.IsDropDownOpen = False
            alpha = useAlpha
            selected = Nothing
            binding = True
            addShader.Items = New List(Of String) From {
                "Add shader…", If(alpha, "Value", "Color"), If(alpha, "Input Image Alpha", "Input Image"),
                "Gradient", "Checkerboard", "Noise"
            }
            addShader.SelectedIndex = 0
            binding = False
            scroll.FirstIndex = 0
            Refresh()
        End Sub

        Private ReadOnly Property ChannelPort As String
            Get
                Return If(alpha, "Alpha", "Color")
            End Get
        End Property

        Private ReadOnly Property BlendKind As String
            Get
                Return If(alpha, "AlphaBlend", "LayerBlend")
            End Get
        End Property

        Private Sub RenderRows()
            binding = True
            For index = 0 To rows.Count - 1
                Dim item = scroll.FirstIndex + index
                If index >= scroll.VisibleItems Then
                    rows(index).Bind(Nothing)
                ElseIf item < layers.Count Then
                    rows(index).Bind(layers(item))
                ElseIf item = layers.Count AndAlso output IsNot Nothing Then
                    Dim label = If(baseNode IsNot Nothing, NodeName(baseNode),
                        If(alpha, If(layers.Count = 0, "Color alpha", "Input image alpha"), "Input image"))
                    rows(index).Bind(Nothing, "Base · " & label)
                Else
                    rows(index).Bind(Nothing)
                End If
            Next
            binding = False
            StyleSelection()
            scene.RequestRender()
        End Sub

        Private Sub StyleSelection()
            Dim chosen = Color.Lerp(appearance.SurfaceColor, appearance.AccentColor, 0.2F)
            colorChannel.BackgroundColor = If(alpha, appearance.SurfaceColor, chosen)
            alphaChannel.BackgroundColor = If(alpha, chosen, appearance.SurfaceColor)
            For Each row In rows
                Dim isSelected = If(row.Entry Is Nothing, row.Root.isVisible AndAlso Not selected.HasValue, row.Entry.Blend.Id = selected.GetValueOrDefault())
                row.SelectButton.BackgroundColor = If(isSelected, chosen, appearance.SurfaceColor)
            Next
        End Sub

        Private Sub SelectLayer(entry As LayerEntry)
            CommitName()
            selected = If(entry Is Nothing, CType(Nothing, Guid?), entry.Blend.Id)
            StyleSelection()
            ShowAttributes()
        End Sub

        Private Sub ShowAttributes()
            Dim index = SelectedIndex()
            addShader.isEnabled = output IsNot Nothing
            upButton.isEnabled = index > 0
            downButton.isEnabled = index >= 0 AndAlso index < layers.Count - 1
            removeButton.isEnabled = index >= 0

            binding = True
            Dim entry = If(index >= 0, layers(index), Nothing)
            Dim source = If(entry Is Nothing, baseNode, entry.Source)
            shaderName.Text = If(source Is Nothing, "Channel default", NodeName(source))
            layerNameLabel.isVisible = entry IsNot Nothing
            layerNameInput.isVisible = entry IsNot Nothing
            layerNameInput.Text = If(entry?.Blend.Name, "")
            valueInput.isVisible = False
            valueKnob.isVisible = False
            valueLabel.isVisible = False
            valueNode = Nothing
            For Each field In colorFields
                field.Bind(Nothing)
            Next

            If source IsNot Nothing Then
                Select Case source.Kind
                    Case "Float"
                        BindValue(source, "Value", 0, 1, 0.01, 1)
                    Case "Color"
                        colorFields(0).Bind(source, "Color")
                    Case "Gradient"
                        BindValue(source, "Angle (deg)", -3600, 3600, 1, 360)
                    Case "Checker", "Noise", "RadialGradient", "Voronoi"
                        BindValue(source, "Scale", 0.01, 100000, 0.01, 1)
                    Case "Source"
                        Dim uv = InputNode(source, "UV")
                        If uv IsNot Nothing Then shaderName.Text &= "  ·  UV from " & NodeName(uv)
                    Case Else
                        shaderName.Text &= "  ·  Edit connections in Advanced"
                End Select
                If IsPattern(source.Kind) Then
                    colorFields(0).Bind(source, If(alpha, "Value A", "Color A"), "A", alpha)
                    colorFields(1).Bind(source, If(alpha, "Value B", "Color B"), "B", alpha)
                End If
            End If
            binding = False
            scene.RequestRender()
        End Sub

        Private Sub BindValue(node As ShaderNode, label As String, minimum As Double, maximum As Double, stepSize As Double, scale As Double)
            valueNode = node
            valueScale = scale
            valueLabel.Text = label
            valueInput.MinimumValue = minimum
            valueInput.MaximumValue = maximum
            valueInput.StepSize = stepSize
            valueInput.DecimalPlaces = If(stepSize < 0.1, 3, 1)
            valueInput.Value = node.Values(0) * scale
            valueInput.isVisible = True
            valueKnob.MinimumValue = Math.Max(minimum, -180)
            valueKnob.MaximumValue = Math.Min(maximum, If(label = "Scale", Math.Max(64, valueInput.Value), 180))
            valueKnob.StepSize = stepSize
            valueKnob.Value = valueInput.Value
            valueKnob.isVisible = True
            valueLabel.isVisible = True
        End Sub

        Private Sub ChangeValue(value As Double, fromKnob As Boolean)
            If binding OrElse valueNode Is Nothing Then Return
            binding = True
            Try
                If fromKnob Then valueInput.Value = value Else valueKnob.Value = value
            Finally
                binding = False
            End Try
            SetValue(valueNode, 0, valueInput.Value / valueScale)
        End Sub

        Private Sub CommitName()
            Dim index = SelectedIndex()
            If binding OrElse index < 0 Then Return
            Dim entry = layers(index)
            Dim updated = layerNameInput.Text.Trim()
            If entry.Blend.Name = updated Then Return
            entry.Blend.Name = updated
            Dim row = rows.Find(Function(item) item.Entry Is entry)
            If row IsNot Nothing Then row.SelectButton.Text = LayerName(entry)
            Changed()
        End Sub

        Private Sub AddLayer(index As Integer)
            If binding OrElse output Is Nothing OrElse index <= 0 OrElse index >= shaders.Length Then Return
            binding = True
            addShader.SelectedIndex = 0
            binding = False

            Dim kind = If(alpha AndAlso index = 1, "Float", shaders(index))
            Dim nodes = document.Nodes.ToList()
            Dim links = document.Connections.ToList()
            Dim blend = AddNode(nodes, BlendKind, output.X - 225, output.Y + If(alpha, 180, 0))
            Dim source = AddNode(nodes, kind, blend.X - If(alpha AndAlso kind <> "Float", 450, 225), blend.Y)
            Dim layerInput = source.Id
            If alpha AndAlso kind <> "Float" Then
                Dim component = AddNode(nodes, If(kind = "Source", "Alpha", "Red"), blend.X - 225, blend.Y)
                SetInput(links, component.Id, "Input", source.Id)
                layerInput = component.Id
            End If
            SetInput(links, blend.Id, "Layer", layerInput)
            Dim updated = layers.Prepend(New LayerEntry(blend, source)).ToList()
            ReplaceStack(updated, blend.Id, nodes, links)
        End Sub

        Private Sub MoveLayer(direction As Integer)
            Dim index = SelectedIndex()
            MoveLayer(index, index + direction)
        End Sub

        Private Function DropLayer(entry As LayerEntry, pointerY As Single) As Boolean
            Dim index = layers.IndexOf(entry)
            Dim destination = Math.Clamp(scroll.FirstIndex + CInt(Math.Floor((pointerY - rowParent.Position.Y) / RowPitch)),
                scroll.FirstIndex, Math.Min(layers.Count - 1, scroll.FirstIndex + scroll.VisibleItems - 1))
            If index < 0 OrElse index = destination Then Return False
            MoveLayer(index, destination)
            Return True
        End Function

        Private Sub MoveLayer(index As Integer, destination As Integer)
            If index < 0 OrElse destination < 0 OrElse destination >= layers.Count Then Return
            Dim updated = layers.ToList()
            Dim moved = updated(index)
            updated.RemoveAt(index)
            updated.Insert(destination, moved)
            ReplaceStack(updated, moved.Blend.Id)
        End Sub

        Private Sub RemoveLayer()
            Dim index = SelectedIndex()
            If index < 0 Then Return
            Dim updated = layers.ToList()
            Dim removed = updated(index).Blend
            updated.RemoveAt(index)
            Dim nextId = If(updated.Count = 0, CType(Nothing, Guid?), updated(Math.Min(index, updated.Count - 1)).Blend.Id)
            ReplaceStack(updated, nextId, removed:=removed)
        End Sub

        Private Sub ReplaceStack(updated As List(Of LayerEntry), nextId As Guid?, Optional nodes As List(Of ShaderNode) = Nothing,
                                 Optional links As List(Of ShaderConnection) = Nothing, Optional removed As ShaderNode = Nothing)
            nodes = If(nodes, document.Nodes.ToList())
            links = If(links, document.Connections.ToList())
            Dim candidate As New ShaderDocument With {.Nodes = nodes, .Connections = links}
            For Each item In updated
                SetInput(links, item.Blend.Id, "Base", Nothing)
            Next
            SetInput(links, output.Id, ChannelPort, Nothing)

            Dim below = baseNode?.Id
            For index = updated.Count - 1 To 0 Step -1
                Dim blend = updated(index).Blend
                If below.HasValue AndAlso Not ShaderNodes.CanConnect(candidate, below.Value, blend.Id, "Base") Then
                    status.Text = "This layer order would create a circular dependency."
                    Return
                End If
                SetInput(links, blend.Id, "Base", below)
                below = blend.Id
            Next
            SetInput(links, output.Id, ChannelPort, below)
            If removed IsNot Nothing AndAlso links.All(Function(link) link.Source <> removed.Id) Then
                links.RemoveAll(Function(link) link.Target = removed.Id)
                nodes.Remove(removed)
            End If
            document.Nodes = nodes
            document.Connections = links
            selected = nextId
            Refresh()
            Changed()
        End Sub

        Private Shared Function AddNode(nodes As List(Of ShaderNode), kind As String, x As Double, y As Double) As ShaderNode
            Dim row = y
            While nodes.Any(Function(node) Math.Abs(node.X - x) < 185 AndAlso Math.Abs(node.Y - row) < 115)
                row += 145
            End While
            Dim created = ShaderNodes.Create(kind, x, row)
            nodes.Add(created)
            Return created
        End Function

        Private Function InputNode(target As ShaderNode, port As String) As ShaderNode
            Dim source = document?.Connections.Find(Function(link) link.Target = target.Id AndAlso link.Input = port)
            Return If(source Is Nothing, Nothing, document.Nodes.Find(Function(node) node.Id = source.Source))
        End Function

        Private Shared Sub SetInput(links As List(Of ShaderConnection), target As Guid, port As String, source As Guid?)
            links.RemoveAll(Function(link) link.Target = target AndAlso link.Input = port)
            If source.HasValue Then links.Add(New ShaderConnection With {.Source = source.Value, .Target = target, .Input = port})
        End Sub

        Private Function SelectedIndex() As Integer
            Return If(selected.HasValue, layers.FindIndex(Function(layer) layer.Blend.Id = selected.Value), -1)
        End Function

        Private Shared Function IsPattern(kind As String) As Boolean
            Return kind = "Gradient" OrElse kind = "Checker" OrElse kind = "Noise" OrElse kind = "RadialGradient" OrElse kind = "Voronoi"
        End Function

        Private Shared Function NodeName(node As ShaderNode) As String
            If Not String.IsNullOrWhiteSpace(node.Name) Then Return node.Name
            Select Case node.Kind
                Case "Source" : Return "Input Image"
                Case "Float" : Return "Value"
                Case "Checker" : Return "Checkerboard"
                Case Else : Return ShaderNodes.Get(node.Kind).Label
            End Select
        End Function

        Private Shared Function LayerName(entry As LayerEntry) As String
            If Not String.IsNullOrWhiteSpace(entry.Blend.Name) Then Return entry.Blend.Name
            Return If(entry.Source IsNot Nothing, NodeName(entry.Source), "Unconnected Shader")
        End Function

        Private Sub SetValue(node As ShaderNode, index As Integer, value As Double)
            If node.Values(index) = value Then Return
            node.Values(index) = value
            Changed()
        End Sub

        Private Sub Changed()
            status.Text = ""
            scene.RequestRender()
            RaiseEvent GraphChanged(Me, EventArgs.Empty)
        End Sub

        Private NotInheritable Class LayerEntry
            Public ReadOnly Blend As ShaderNode
            Public ReadOnly Source As ShaderNode
            Public Sub New(blend As ShaderNode, source As ShaderNode)
                Me.Blend = blend
                Me.Source = source
            End Sub
        End Class

        Private NotInheritable Class LayerRow
            Private ReadOnly owner As SimpleShaderView
            Private ReadOnly enabled As CheckBox
            Private ReadOnly blend As ComboBox
            Private ReadOnly opacity As Trackbar
            Private ReadOnly opacityLabel As TextElement
            Private dragStart As Single
            Private moved As Boolean
            Public ReadOnly Root As SceneElement
            Public ReadOnly SelectButton As Button
            Public Entry As LayerEntry

            Public Sub New(owner As SimpleShaderView, y As Integer)
                Me.owner = owner
                Dim parser As SceneXamlParser = Nothing
                Root = ShaderEditorChrome.LoadView(owner.scene, "SimpleShaderLayer.xaml", parser)
                Root.Margin = New Thickness(0, y, 0, 0)
                Root.Position = New Vector2(0, y)
                enabled = parser.FindName(Of CheckBox)("Enabled")
                SelectButton = parser.FindName(Of Button)("SelectLayer")
                blend = parser.FindName(Of ComboBox)("BlendMode")
                opacity = parser.FindName(Of Trackbar)("Opacity")
                opacityLabel = parser.FindName(Of TextElement)("OpacityLabel")
                blend.Items = blendModes.ToList()
                opacity.ShowTooltip = False
                opacity.ShowValue = False
                AddHandler SelectButton.MouseLeftDown, Sub(point)
                                                          dragStart = SelectButton.Position.Y + point.Y
                                                          moved = False
                                                      End Sub
                AddHandler SelectButton.MouseLeftUp, Sub(point)
                                                        Dim pointerY = SelectButton.Position.Y + point.Y
                                                        If Entry IsNot Nothing AndAlso Math.Abs(pointerY - dragStart) >= 12 Then
                                                            moved = owner.DropLayer(Entry, pointerY)
                                                        End If
                                                    End Sub
                AddHandler SelectButton.MouseLeftClick, Sub(point)
                                                            If Not moved Then owner.SelectLayer(Entry)
                                                        End Sub
                AddHandler enabled.CheckedChanged, Sub(oldValue, newValue)
                                                       If Not owner.binding AndAlso Entry IsNot Nothing Then
                                                           owner.SetValue(Entry.Blend, 2, If(newValue.GetValueOrDefault(), 1, 0))
                                                       End If
                                                   End Sub
                AddHandler blend.SelectionChanged, Sub(sender, index)
                                                       If Not owner.binding AndAlso Entry IsNot Nothing AndAlso index >= 0 Then
                                                           owner.SetValue(Entry.Blend, 1, index)
                                                       End If
                                                   End Sub
                AddHandler opacity.ValueChanged, Sub(value)
                                                     opacityLabel.Text = value.ToString("0", CultureInfo.InvariantCulture) & "%"
                                                     If Not owner.binding AndAlso Entry IsNot Nothing Then owner.SetValue(Entry.Blend, 0, value / 100)
                                                 End Sub
            End Sub

            Public Sub Bind(item As LayerEntry, Optional baseTitle As String = Nothing)
                blend.IsDropDownOpen = False
                Entry = item
                Root.isVisible = item IsNot Nothing OrElse baseTitle IsNot Nothing
                enabled.isVisible = item IsNot Nothing
                blend.isVisible = item IsNot Nothing
                opacity.isVisible = item IsNot Nothing
                opacityLabel.isVisible = item IsNot Nothing
                If item Is Nothing Then
                    SelectButton.Text = If(baseTitle, "")
                    Return
                End If
                SelectButton.Text = LayerName(item)
                enabled.IsChecked = item.Blend.Values(2) >= 0.5
                blend.SelectedIndex = CInt(Math.Truncate(Math.Clamp(item.Blend.Values(1), 0, blendModes.Length - 1)))
                opacity.Value = item.Blend.Values(0) * 100
                opacityLabel.Text = opacity.Value.ToString("0", CultureInfo.InvariantCulture) & "%"
            End Sub

            Public Sub ApplyAppearance()
                owner.ApplyMenuTheme(blend)
                opacity.TrackColor = Color.Lerp(owner.appearance.SurfaceColor, owner.appearance.AccentColor, 0.24F)
                opacity.ThumbColor = owner.appearance.AccentColor
            End Sub

            Public Sub CloseMenu()
                blend.IsDropDownOpen = False
            End Sub

            Public Sub Resize(extraWidth As Single)
                Dim canvas = DirectCast(Root, CanvasPanel)
                Dim nameWidth = extraWidth * 0.65F
                Root.Size = New Vector2(509 + extraWidth, Root.Size.Y)
                SelectButton.Size = New Vector2(169 + nameWidth, SelectButton.Size.Y)
                canvas.SetChildPosition(blend, New Vector2(209 + nameWidth, 3))
                canvas.SetChildPosition(opacity, New Vector2(341 + nameWidth, 8))
                opacity.Size = New Vector2(112 + extraWidth - nameWidth, opacity.Size.Y)
                canvas.SetChildPosition(opacityLabel, New Vector2(457 + extraWidth, 8))
            End Sub
        End Class

        Private NotInheritable Class ColorField
            Private ReadOnly owner As SimpleShaderView
            Private ReadOnly caption As TextElement
            Private ReadOnly swatch As RectangleElement
            Private ReadOnly hex As Textbox
            Private ReadOnly components As DragNumberBox()
            Private ReadOnly componentLabels As TextElement()
            Private target As ShaderNode
            Private input As String
            Private changing As Boolean
            Private grayscale As Boolean
            Public ReadOnly Root As SceneElement

            Public Sub New(owner As SimpleShaderView, y As Integer)
                Me.owner = owner
                Dim parser As SceneXamlParser = Nothing
                Root = ShaderEditorChrome.LoadView(owner.scene, "SimpleShaderColor.xaml", parser)
                Root.Margin = New Thickness(0, y, 0, 0)
                Root.Position = New Vector2(0, y)
                caption = parser.FindName(Of TextElement)("Caption")
                swatch = parser.FindName(Of RectangleElement)("Swatch")
                hex = parser.FindName(Of Textbox)("Hex")
                Dim names = {"Red", "Green", "Blue", "Alpha"}
                components = names.Select(Function(name) parser.FindName(Of DragNumberBox)(name)).ToArray()
                componentLabels = names.Select(Function(name) parser.FindName(Of TextElement)(name & "Label")).ToArray()
                For index = 0 To components.Length - 1
                    Dim channel = index
                    Dim component = components(channel)
                    component.MinimumValue = 0
                    component.MaximumValue = 255
                    component.StepSize = 1
                    component.DecimalPlaces = 0
                    component.ShowSpinButtons = False
                    AddHandler component.ValueChanged, Sub(value) SetComponent(channel, value)
                Next
                AddHandler hex.Submitted, AddressOf CommitHex
                AddHandler hex.Deselected, AddressOf CommitHex
            End Sub

            Public Sub Bind(node As ShaderNode, Optional label As String = "", Optional port As String = Nothing, Optional scalar As Boolean = False)
                target = node
                input = port
                grayscale = scalar
                caption.Text = label
                Root.isVisible = node IsNot Nothing
                If node Is Nothing Then Return
                Dim linked = If(port Is Nothing, Nothing, owner.InputNode(node, port))
                Dim editable = linked Is Nothing OrElse linked.Kind = "Color"
                If Not editable Then caption.Text = label & " · linked"
                swatch.isVisible = editable AndAlso Not scalar
                hex.isVisible = editable AndAlso Not scalar
                For index = 0 To components.Length - 1
                    components(index).isVisible = editable AndAlso (Not scalar OrElse index = 0)
                    componentLabels(index).isVisible = editable AndAlso Not scalar
                Next
                Dim left = If(scalar, 105, 262)
                DirectCast(Root, CanvasPanel).SetChildPosition(components(0), New Vector2(left, 7))
                components(0).MaximumValue = If(scalar, 1, 255)
                components(0).StepSize = If(scalar, 0.01, 1)
                components(0).DecimalPlaces = If(scalar, 3, 0)
                If editable Then UpdateDisplay()
            End Sub

            Private Function Read() As Double()
                Dim colorNode = If(input Is Nothing, target, owner.InputNode(target, input))
                Dim fallback = If(input = "B", 1.0, 0.0)
                Return If(colorNode IsNot Nothing, colorNode.Values.ToArray(), {fallback, fallback, fallback, 1.0})
            End Function

            Private Sub Write(values As Double())
                If target Is Nothing OrElse Read().SequenceEqual(values) Then Return
                Dim colorNode = If(input Is Nothing, target, owner.InputNode(target, input))
                If colorNode Is Nothing Then
                    colorNode = AddNode(owner.document.Nodes, "Color", target.X - 225, target.Y + If(input = "A", -100, 100))
                    SetInput(owner.document.Connections, target.Id, input, colorNode.Id)
                End If
                colorNode.Values = values
                UpdateDisplay()
                owner.Changed()
            End Sub

            Private Sub SetComponent(index As Integer, value As Double)
                If changing OrElse owner.binding OrElse target Is Nothing Then Return
                Dim values = Read()
                values(index) = If(grayscale, value, Math.Round(value) / 255)
                Write(values)
            End Sub

            Private Sub CommitHex()
                If changing OrElse owner.binding OrElse target Is Nothing OrElse Not hex.isVisible Then Return
                Dim text = hex.Text.Trim().TrimStart("#"c)
                If text.Length = 6 Then text &= "FF"
                Dim rgba As UInteger
                If text.Length = 8 AndAlso UInteger.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, rgba) Then
                    If text.Equals(CurrentHex(), StringComparison.OrdinalIgnoreCase) Then Return
                    Write({((rgba >> 24) And 255UI) / 255.0, ((rgba >> 16) And 255UI) / 255.0,
                          ((rgba >> 8) And 255UI) / 255.0, (rgba And 255UI) / 255.0})
                Else
                    owner.status.Text = "Enter a color as #RRGGBB or #RRGGBBAA."
                    UpdateDisplay()
                End If
            End Sub

            Private Function CurrentHex() As String
                Return Convert.ToHexString(Read().Select(Function(value) CByte(Math.Round(Math.Clamp(value, 0, 1) * 255))).ToArray())
            End Function

            Private Sub UpdateDisplay()
                changing = True
                Dim values = Read()
                Dim rgba = values.Select(Function(value) CByte(Math.Round(Math.Clamp(value, 0, 1) * 255))).ToArray()
                swatch.BackgroundColor = New Color(rgba(0), rgba(1), rgba(2), rgba(3))
                hex.Text = "#" & Convert.ToHexString(rgba)
                For index = 0 To rgba.Length - 1
                    components(index).Value = If(grayscale AndAlso index = 0, values(0), rgba(index))
                Next
                changing = False
            End Sub

            Public Sub ApplyAppearance()
                swatch.BorderColor = owner.appearance.TextColor * 0.5F
                swatch.BorderThickness = 1
                If target IsNot Nothing AndAlso components(0).isVisible Then UpdateDisplay()
            End Sub
        End Class
    End Class
End Namespace
