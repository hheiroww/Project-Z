Imports System.Diagnostics
Imports System.Globalization
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.ShaderEditor
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.Designer
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Windows.Composition

Namespace Windows.ShaderEditing
    Friend NotInheritable Class AdvancedShaderView
        Private ReadOnly scene As Scene
        Private ReadOnly graph As ShaderGraphCanvas
        Private ReadOnly inspector As StackPanel
        Private ReadOnly inspectorScroll As ScrollViewer
        Private ReadOnly inspectorEmpty As TextElement
        Private ReadOnly inspectorPanel As SceneElement
        Private ReadOnly palettePanel As SceneElement
        Private ReadOnly paletteSearch As Textbox
        Private ReadOnly paletteHint As TextElement
        Private ReadOnly paletteResults As ListBox
        Private ReadOnly contextLayer As CanvasPanel
        Private ReadOnly contextPanel As SceneElement
        Private ReadOnly contextSearch As Textbox
        Private ReadOnly contextHint As TextElement
        Private ReadOnly contextResults As ListBox
        Private ReadOnly graphStatus As TextElement
        Private ReadOnly catalog As ShaderNodeDefinition() = ShaderNodes.All.Where(Function(item) item.Kind <> "Output").
            OrderBy(Function(item) item.Category).ThenBy(Function(item) item.Label).ToArray()
        Private ReadOnly fieldCommits As New List(Of Action)
        Private ReadOnly fieldEditors As New List(Of SceneElement)
        Private settings As New WindowAppearanceSettings()
        Private document As ShaderDocument
        Private sideMatches As ShaderNodeDefinition() = Array.Empty(Of ShaderNodeDefinition)()
        Private contextMatches As ShaderNodeDefinition() = Array.Empty(Of ShaderNodeDefinition)()
        Private contextPosition As Vector2
        Private pendingWire As ShaderGraphCanvas.WireDrag
        Private selectingResult As Boolean
        Private rebuildingInspector As Boolean
        Private lastPaletteClick As Integer = -1
        Private lastPaletteClickTime As Long

        Public ReadOnly Property Root As SceneElement
        Public Event GraphChanged As EventHandler

        Public Sub New(scene As Scene)
            Me.scene = scene
            Dim parser As SceneXamlParser = Nothing
            Root = ShaderEditorChrome.LoadView(scene, "AdvancedShaderView.xaml", parser)
            Dim elements = parser.NamedElements
            inspector = DirectCast(elements("InspectorContent"), StackPanel)
            inspector.AutoSize = True
            inspectorScroll = DirectCast(elements("InspectorScroll"), ScrollViewer)
            inspectorEmpty = DirectCast(elements("InspectorEmpty"), TextElement)
            inspectorPanel = elements("InspectorPanel")
            palettePanel = elements("PalettePanel")
            paletteSearch = DirectCast(elements("PaletteSearch"), Textbox)
            paletteHint = DirectCast(elements("PaletteSearchHint"), TextElement)
            paletteResults = DirectCast(elements("PaletteResults"), ListBox)
            contextLayer = DirectCast(elements("ContextLayer"), CanvasPanel)
            contextPanel = elements("ContextPanel")
            contextSearch = DirectCast(elements("ContextSearch"), Textbox)
            contextHint = DirectCast(elements("ContextSearchHint"), TextElement)
            contextResults = DirectCast(elements("ContextResults"), ListBox)
            graphStatus = DirectCast(elements("GraphStatus"), TextElement)

            graph = New ShaderGraphCanvas(scene)
            DirectCast(elements("CanvasHost"), Grid).AddChild(graph, 0, 0)
            AddHandler graph.SelectionChanged, AddressOf RebuildInspector
            AddHandler graph.Changed, AddressOf NotifyChanged
            AddHandler graph.StatusChanged, Sub(message) graphStatus.Text = message
            AddHandler graph.PaletteRequested, AddressOf OpenContext
            AddHandler graph.MouseLeftDown, Sub(p) CloseContext()
            AddHandler graph.MouseRightDown, Sub(p) CloseContext()

            DirectCast(elements("FrameButton"), Button).SetClickAction(Sub() Frame(False))
            DirectCast(elements("OutputButton"), Button).SetClickAction(Sub() Frame(True))
            DirectCast(elements("DisconnectButton"), Button).SetClickAction(AddressOf graph.DisconnectSelection)
            DirectCast(elements("PropertiesButton"), Button).SetClickAction(Sub() TogglePanel(inspectorPanel, 0, 170))
            DirectCast(elements("PaletteButton"), Button).SetClickAction(Sub() TogglePanel(palettePanel, 2, 174))
            DirectCast(elements("AddNodeButton"), Button).SetClickAction(Sub() ChooseResult(False))
            AddHandler paletteSearch.OnTextChanged, Sub(e) FilterPalette(False)
            AddHandler contextSearch.OnTextChanged, Sub(e) FilterPalette(True)
            AddHandler paletteSearch.Submitted, Sub() ChooseResult(False)
            AddHandler contextSearch.Submitted, Sub() ChooseResult(True)
            AddHandler paletteSearch.OnKeyDown, Sub(key, state) MoveResultSelection(paletteResults, key)
            AddHandler contextSearch.OnKeyDown, AddressOf ContextKeyDown
            AddHandler paletteResults.SelectionChanged, AddressOf OnPaletteClick
            paletteResults.ItemAppearance = AddressOf StylePaletteItem
            contextResults.ItemAppearance = Sub(index, row, text) StyleListLabel(text)
            AddHandler contextResults.SelectionChanged, Sub(sender, index)
                                                            If Not selectingResult AndAlso index >= 0 Then ChooseResult(True)
                                                        End Sub
            FilterPalette(False)
            RebuildInspector()
        End Sub

        Public Sub SetDocument(value As ShaderDocument)
            CloseContext()
            document = value
            graph.SetDocument(value)
        End Sub

        Public Sub Refresh()
            graph.Refresh(fitNewNodes:=True)
            RebuildInspector()
        End Sub

        Public Sub CommitEdits()
            If fieldEditors.Any(Function(child) child.isSelected) Then scene.FocusElement(Nothing)
            For Each commit In fieldCommits.ToArray()
                commit()
            Next
        End Sub

        Public Sub ApplyAppearance(value As WindowAppearanceSettings)
            settings = value
            ShaderEditorChrome.ApplyTheme(Root, settings)
            inspectorScroll.BackgroundColor = Color.Transparent
            paletteResults.BackgroundColor = Color.Transparent
            contextResults.BackgroundColor = Color.Transparent
            contextResults.SelectionColor = Color.Lerp(settings.SurfaceColor, settings.AccentColor, 0.3F)
            paletteResults.SelectionColor = contextResults.SelectionColor
            inspectorEmpty.ForegroundColor = Color.Lerp(settings.TextColor, settings.SurfaceColor, 0.28F)
            DirectCast(contextPanel, RectangleElement).BackgroundColor = Color.Lerp(settings.BackgroundColor, settings.SurfaceColor, 0.73F)
            graph.ApplyAppearance(settings)
        End Sub

        Private Sub Frame(outputOnly As Boolean)
            graph.Frame(outputOnly)
            scene.FocusElement(graph)
        End Sub

        Private Sub TogglePanel(panel As SceneElement, column As Integer, width As Single)
            panel.isVisible = Not panel.isVisible
            Dim layout = DirectCast(Root, Grid)
            layout.ColumnDefinitions(column).Size = If(panel.isVisible, width, 0)
            layout.ArrangeChildren()
        End Sub

        Private Sub FilterPalette(context As Boolean)
            Dim search = If(context, contextSearch, paletteSearch)
            Dim list = If(context, contextResults, paletteResults)
            Dim hint = If(context, contextHint, paletteHint)
            hint.isVisible = search.Text.Length = 0
            Dim query = search.Text.Trim()
            Dim matches = catalog.Where(Function(node) (node.Label.Contains(query, StringComparison.OrdinalIgnoreCase) OrElse
                node.Category.Contains(query, StringComparison.OrdinalIgnoreCase)) AndAlso (Not context OrElse graph.CanInsert(node, pendingWire))).ToArray()
            If context Then
                contextMatches = matches
            Else
                sideMatches = matches
                lastPaletteClick = -1
            End If
            selectingResult = True
            Try
                list.Items = matches.Select(Function(node) node.Label).ToList()
                list.SelectedIndex = If(matches.Length > 0, 0, -1)
            Finally
                selectingResult = False
            End Try
            scene.RequestRender()
        End Sub

        Private Sub StyleListLabel(text As TextElement)
            text.ForegroundColor = settings.TextColor
            text.FontScale = 0.7F
            text.TextWrapping = TextWrapping.NoWrap
            text.UseXamlTextLayout = True
        End Sub

        Private Sub StylePaletteItem(index As Integer, row As RectangleElement, text As TextElement)
            StyleListLabel(text)
            Dim started As Vector2
            Dim definition As ShaderNodeDefinition = Nothing
            AddHandler row.MouseLeftDown, Sub(p)
                                              started = row.Position + p.ToVector2()
                                              definition = sideMatches.ElementAtOrDefault(index)
                                          End Sub
            AddHandler row.MouseLeftUp, Sub(p)
                                            Dim position = row.Position + p.ToVector2()
                                            If definition IsNot Nothing AndAlso graph.Rectangle.Contains(position) AndAlso
                                                Vector2.DistanceSquared(started, position) > 25 Then
                                                CloseContext()
                                                graph.DropNode(definition, position)
                                                scene.FocusElement(graph)
                                            End If
                                            definition = Nothing
                                        End Sub
        End Sub

        Private Sub OnPaletteClick(sender As ListBox, index As Integer)
            If selectingResult OrElse index < 0 Then Return
            Dim now = Stopwatch.GetTimestamp()
            If index = lastPaletteClick AndAlso Stopwatch.GetElapsedTime(lastPaletteClickTime, now).TotalMilliseconds < 400 Then
                lastPaletteClick = -1
                ChooseResult(False)
            Else
                lastPaletteClick = index
                lastPaletteClickTime = now
            End If
        End Sub

        Private Sub MoveResultSelection(list As ListBox, key As Keys)
            If (key <> Keys.Up AndAlso key <> Keys.Down) OrElse list.Items.Count = 0 Then Return
            selectingResult = True
            Try
                list.SelectedIndex = Math.Clamp(list.SelectedIndex + If(key = Keys.Up, -1, 1), 0, list.Items.Count - 1)
                list.ScrollIntoView(list.SelectedIndex)
            Finally
                selectingResult = False
            End Try
        End Sub

        Private Sub ContextKeyDown(key As Keys, state As KeyboardState)
            If key = Keys.Escape Then
                CloseContext()
                scene.FocusElement(graph)
            Else
                MoveResultSelection(contextResults, key)
            End If
        End Sub

        Private Sub OpenContext(viewport As Vector2, position As Vector2, wire As ShaderGraphCanvas.WireDrag)
            contextPosition = position
            pendingWire = wire
            contextSearch.Text = ""
            FilterPalette(True)
            Dim offset = Vector2.Clamp(viewport, Vector2.Zero, Vector2.Max(Vector2.Zero, contextLayer.Size - contextPanel.Size))
            contextLayer.SetChildPosition(contextPanel, offset)
            contextPanel.isVisible = True
            scene.FocusElement(contextSearch)
        End Sub

        Private Sub CloseContext()
            contextPanel.isVisible = False
            pendingWire = Nothing
        End Sub

        Private Sub ChooseResult(context As Boolean)
            Dim matches = If(context, contextMatches, sideMatches)
            Dim index = If(context, contextResults, paletteResults).SelectedIndex
            If index < 0 OrElse index >= matches.Length Then Return
            Dim wire = pendingWire
            CloseContext()
            graph.AddNode(matches(index), If(context, CType(contextPosition, Vector2?), Nothing), If(context, wire, Nothing))
            scene.FocusElement(graph)
        End Sub

        Private Sub NotifyChanged()
            RaiseEvent GraphChanged(Me, EventArgs.Empty)
        End Sub

        Private Sub RebuildInspector()
            If rebuildingInspector Then Return
            rebuildingInspector = True
            Try
                fieldCommits.Clear()
                fieldEditors.Clear()
                For Each child In inspector.Children.ToArray()
                    inspector.Children.Remove(child)
                    child.Dispose()
                Next
                inspectorScroll.ScrollOffset = Vector2.Zero
                Dim node = graph.InspectedNode
                inspectorEmpty.isVisible = node Is Nothing
                inspectorScroll.isVisible = node IsNot Nothing
                If node Is Nothing Then
                    inspectorEmpty.Text = If(graph.SelectionCount > 1, $"{graph.SelectionCount} nodes selected", "Select a node to edit its values.")
                    Return
                End If

                Dim definition = ShaderNodes.Get(node.Kind)
                AddCaption(definition.Label)
                AddCaption(definition.Category, True)
                If node.Kind <> "Output" Then
                    AddCaption("Name", True)
                    AddTextField(node, node.Name, False, Function() node.Name, Sub(value) node.Name = value.Trim())
                End If
                For index = 0 To definition.Parameters.Count - 1
                    AddParameter(node, index, definition.Parameters(index).Name)
                Next
                If definition.HasExpression Then
                    AddCaption("HLSL expression")
                    AddTextField(node, node.Expression, True, Function() node.Expression, Sub(value) node.Expression = value)
                    AddCaption("A, B, C, uv, Time, TexelSize", True)
                End If
                For Each port In definition.Inputs
                    Dim wired = document.Connections.Any(Function(wire) wire.Target = node.Id AndAlso wire.Input = port.Name)
                    AddCaption($"{port.Name} · {TypeName(port.Type)}{If(wired, " · Wired", "")}", True)
                Next
                If definition.Output.HasValue Then AddCaption($"Output · {TypeName(definition.Output.Value)}", True)
            Finally
                rebuildingInspector = False
                scene.RequestRender()
            End Try
        End Sub

        Private Sub AddCaption(value As String, Optional muted As Boolean = False)
            inspector.Children.Add(New TextElement(scene) With {.TextWrapping = TextWrapping.NoWrap, .UseXamlTextLayout = True,
                .FontScale = If(muted, 0.7F, 0.83F), .ForegroundColor = If(muted, Color.Lerp(settings.TextColor, settings.SurfaceColor, 0.28F), settings.TextColor),
                .Text = value, .Size = New Vector2(145, 22), .Clip = True, .isMouseBypassEnabled = True})
        End Sub

        Private Sub AddTextField(node As ShaderNode, value As String, multiline As Boolean, read As Func(Of String), write As Action(Of String))
            Dim box As New Textbox(scene) With {.Text = value, .AcceptsReturn = multiline, .Size = New Vector2(145, If(multiline, 76, 27)),
                .BackgroundColor = settings.BackgroundColor, .ForegroundColor = settings.TextColor, .BorderColor = settings.SurfaceColor, .BorderThickness = 1}
            Dim commit As Action = Sub()
                                       If rebuildingInspector OrElse document Is Nothing OrElse Not document.Nodes.Contains(node) OrElse read() = box.Text Then Return
                                       Dim previous = read()
                                       write(box.Text)
                                       If previous = read() Then Return
                                       graph.UpdateCards()
                                       NotifyChanged()
                                   End Sub
            fieldCommits.Add(commit)
            fieldEditors.Add(box)
            AddHandler box.Deselected, Sub() commit()
            AddHandler box.Submitted, Sub() commit()
            AddHandler box.OnKeyDown, Sub(key, state)
                                         If key = Keys.Escape Then box.Text = read()
                                     End Sub
            inspector.Children.Add(box)
        End Sub

        Private Sub AddParameter(node As ShaderNode, index As Integer, label As String)
            AddCaption(label, True)
            Dim box As DragNumberBox
            Dim knob As RotaryKnob = Nothing
            Dim field As SceneElement
            Dim current = node.Values(index)
            If IsNormalizedParameter(node, label) Then
                Dim parser As SceneXamlParser = Nothing
                field = ShaderEditorChrome.LoadView(scene, "AdvancedShaderParameter.xaml", parser)
                box = DirectCast(parser.NamedElements("Value"), DragNumberBox)
                box.MinimumValue = Math.Min(0, current)
                box.MaximumValue = Math.Max(1, current)
                box.DecimalPlaces = 3
                box.Value = current
                knob = DirectCast(parser.NamedElements("Knob"), RotaryKnob)
                knob.DragPixels = 100
                knob.Value = current
                fieldEditors.Add(knob)
            Else
                box = ShaderEditorChrome.CreateValue(scene, label, current)
                box.Size = New Vector2(145, 28)
                field = box
            End If
            fieldEditors.Add(box)
            ShaderEditorChrome.ApplyTheme(field, settings)
            Dim pending As Boolean
            Dim synchronizing As Boolean
            Dim commit As Action = Sub()
                                       If Not pending Then Return
                                       pending = False
                                       NotifyChanged()
                                   End Sub
            Dim updateValue As Action(Of Double, Boolean) = Sub(value, fromKnob)
                                                               If synchronizing OrElse rebuildingInspector OrElse document Is Nothing OrElse
                                                                   Not document.Nodes.Contains(node) OrElse node.Values(index) = value Then Return
                                                               synchronizing = True
                                                               Try
                                                                   If fromKnob Then
                                                                       box.Value = value
                                                                   ElseIf knob IsNot Nothing Then
                                                                       knob.Value = value
                                                                   End If
                                                               Finally
                                                                   synchronizing = False
                                                               End Try
                                                               node.Values(index) = value
                                                               graph.UpdateCards()
                                                               pending = True
                                                               If Not box.isMouseDown AndAlso (knob Is Nothing OrElse Not knob.isMouseDown) Then commit()
                                                           End Sub
            AddHandler box.ValueChanged, Sub(value) updateValue(value, False)
            AddHandler box.MouseLeftUp, Sub(p) commit()
            AddHandler box.Deselected, Sub() commit()
            If knob IsNot Nothing Then
                AddHandler knob.ValueChanged, Sub(value) updateValue(value, True)
                AddHandler knob.MouseLeftUp, Sub(p) commit()
                AddHandler knob.Deselected, Sub() commit()
            End If
            fieldCommits.Add(commit)
            inspector.Children.Add(field)
        End Sub

        Private Shared Function IsNormalizedParameter(node As ShaderNode, label As String) As Boolean
            If node.Kind = "Color" Then Return label = "R" OrElse label = "G" OrElse label = "B" OrElse label = "A"
            Return label = "Opacity" OrElse label = "Intensity" OrElse label = "Mix" OrElse label = "Smoothness" OrElse label = "Factor"
        End Function

        Friend Shared Function TypeName(type As ShaderValueType) As String
            Return type.ToString().ToLowerInvariant()
        End Function
    End Class

    Friend NotInheritable Class ShaderGraphCanvas
        Inherits RectangleElement

        Private Const NodeWidth As Single = 172
        Private Const HeaderHeight As Single = 28
        Private Const RowHeight As Single = 21
        Private Const CoordinateLimit As Double = 1000000
        Private ReadOnly selectedIds As New HashSet(Of Guid)
        Private ReadOnly zOrder As New List(Of ShaderNode)
        Private ReadOnly cards As New Dictionary(Of Guid, GraphCard)
        Private ReadOnly dragOrigins As New Dictionary(Of Guid, (X As Double, Y As Double))
        Private document As ShaderDocument
        Private settings As New WindowAppearanceSettings()
        Private zoom As Single = 1
        Private pan As New Vector2(25, 25)
        Private originalPan As Vector2
        Private start As Vector2
        Private pointer As New Vector2(-1, -1)
        Private wire As WireDrag
        Private pendingSelection As Guid?
        Private needsFrame As Boolean
        Private gesture As GraphGesture

        Private Enum GraphGesture
            Idle
            Pan
            Move
            Marquee
            Wire
        End Enum

        Friend NotInheritable Class PortHit
            Public Property Node As ShaderNode
            Public Property Input As ShaderPort
            Public Property Type As ShaderValueType
            Public Property Anchor As Vector2
        End Class

        Friend NotInheritable Class WireDrag
            Public Property Port As PortHit
            Public Property Original As ShaderConnection
        End Class

        Public Event Changed()
        Public Event SelectionChanged()
        Public Event StatusChanged(message As String)
        Public Event PaletteRequested(viewport As Vector2, position As Vector2, wire As WireDrag)

        Public ReadOnly Property SelectionCount As Integer
            Get
                Return selectedIds.Count
            End Get
        End Property

        Public ReadOnly Property InspectedNode As ShaderNode
            Get
                Return If(selectedIds.Count = 1, zOrder.FirstOrDefault(Function(node) selectedIds.Contains(node.Id)), Nothing)
            End Get
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            Clip = True
            CanSelect = True
            BackgroundColor = Color.Transparent
            AddHandler RectangleChanged, AddressOf ViewportChanged
            AddHandler MouseLeftDown, AddressOf StartLeft
            AddHandler MouseLeftUp, AddressOf EndLeft
            AddHandler MouseRightDown, AddressOf StartPan
            AddHandler MouseRightUp, AddressOf EndPan
            AddHandler MouseWheel, AddressOf ZoomAtPointer
            AddHandler OnKeyDown, AddressOf HandleKey
            AddHandler scene.OnMouseMove, AddressOf FollowPointer
            AddHandler scene.OnPointerCancelled, AddressOf CancelGesture
        End Sub

        Public Sub SetDocument(value As ShaderDocument)
            CancelGesture()
            document = value
            selectedIds.Clear()
            zOrder.Clear()
            For Each card In cards.Values
                Children.Remove(card)
                card.Dispose()
            Next
            cards.Clear()
            Refresh()
            Frame()
            RaiseEvent SelectionChanged()
        End Sub

        Public Sub Refresh(Optional fitNewNodes As Boolean = False)
            If document Is Nothing Then Return
            Dim existing = document.Nodes.ToDictionary(Function(node) node.Id)
            Dim added = existing.Keys.Any(Function(id) Not cards.ContainsKey(id))
            For Each id In cards.Keys.Where(Function(key) Not existing.ContainsKey(key)).ToArray()
                Dim card = cards(id)
                Children.Remove(card)
                card.Dispose()
                cards.Remove(id)
                selectedIds.Remove(id)
            Next
            zOrder.RemoveAll(Function(node) Not existing.ContainsKey(node.Id))
            For Each node In document.Nodes
                If cards.ContainsKey(node.Id) Then Continue For
                Dim card As New GraphCard(Scene, node)
                cards.Add(node.Id, card)
                zOrder.Add(node)
                Children.Add(card)
            Next
            If fitNewNodes AndAlso added Then Frame(fitAll:=True) Else UpdateCards()
        End Sub

        Public Sub ApplyAppearance(value As WindowAppearanceSettings)
            settings = value
            UpdateCards()
        End Sub

        Public Sub UpdateCards()
            For index = 0 To zOrder.Count - 1
                Dim node = zOrder(index)
                Dim card = cards(node.Id)
                Dim at = Position + ToViewport(NodeLocation(node))
                Dim dimensions = NodeDimensions(node) * zoom
                card.zIndex = index
                card.isVisible = at.X + dimensions.X >= Position.X AndAlso at.Y + dimensions.Y >= Position.Y AndAlso
                    at.X <= Position.X + Size.X AndAlso at.Y <= Position.Y + Size.Y
                If card.isVisible Then card.UpdateLayout(at, dimensions, zoom, selectedIds.Contains(node.Id), settings)
            Next
            Scene.RequestRender()
        End Sub

        Private Sub ViewportChanged()
            If needsFrame AndAlso Size.X > 1 AndAlso Size.Y > 1 Then
                Frame()
            Else
                UpdateCards()
            End If
        End Sub

        Protected Overrides Sub AlignChildren()
            ' Card positions are determined by the graph camera.
        End Sub

        Public Sub Frame(Optional outputOnly As Boolean = False, Optional fitAll As Boolean = False)
            If document Is Nothing Then Return
            If Size.X <= 1 OrElse Size.Y <= 1 Then
                needsFrame = True
                Return
            End If
            Dim nodes = zOrder.Where(Function(node) If(outputOnly, node.Kind = "Output", fitAll OrElse selectedIds.Count = 0 OrElse selectedIds.Contains(node.Id))).ToArray()
            If nodes.Length = 0 Then Return
            Dim minimum = New Vector2(nodes.Min(Function(node) NodeLocation(node).X), nodes.Min(Function(node) NodeLocation(node).Y))
            Dim maximum = New Vector2(nodes.Max(Function(node) NodeLocation(node).X + NodeDimensions(node).X),
                                      nodes.Max(Function(node) NodeLocation(node).Y + NodeDimensions(node).Y))
            Dim span = maximum - minimum
            zoom = Math.Clamp(Math.Min((Size.X - 55) / span.X, (Size.Y - 55) / span.Y), 0.16F, 1.25F)
            pan = (Size - span * zoom) / 2 - minimum * zoom
            needsFrame = False
            RaiseEvent StatusChanged($"{zoom:P0} · Drag ports to connect. Alt-click to disconnect.")
            UpdateCards()
        End Sub

        Public Function CanInsert(definition As ShaderNodeDefinition, drag As WireDrag) As Boolean
            If drag Is Nothing Then Return True
            Dim port = drag.Port
            If port.Input Is Nothing Then Return definition.Inputs.Any(Function(input) Compatible(port.Type, input.Type))
            Return definition.Output.HasValue AndAlso Compatible(definition.Output.Value, port.Type)
        End Function

        Private Shared Function Compatible(source As ShaderValueType, target As ShaderValueType) As Boolean
            Return source = target OrElse source = ShaderValueType.Float
        End Function

        Public Sub AddNode(definition As ShaderNodeDefinition, Optional position As Vector2? = Nothing, Optional drag As WireDrag = Nothing)
            If document Is Nothing Then Return
            Dim at = If(position.HasValue, position.Value, FindOpenPosition(definition))
            Dim node = ShaderNodes.Create(definition.Kind, ClampCoordinate(at.X), ClampCoordinate(at.Y))
            document.Nodes.Add(node)
            If drag IsNot Nothing Then
                Dim port = drag.Port
                If port.Input Is Nothing Then
                    Dim input = definition.Inputs.FirstOrDefault(Function(candidate) ShaderNodes.CanConnect(document, port.Node.Id, node.Id, candidate.Name))
                    If input IsNot Nothing Then Connect(port.Node.Id, node.Id, input.Name, drag.Original)
                ElseIf ShaderNodes.CanConnect(document, node.Id, port.Node.Id, port.Input.Name) Then
                    Connect(node.Id, port.Node.Id, port.Input.Name, Nothing)
                End If
            End If
            selectedIds.Clear()
            selectedIds.Add(node.Id)
            Refresh()
            Dim screen = ToViewport(NodeLocation(node))
            If Not InViewport(screen) OrElse Not InViewport(screen + NodeDimensions(node) * zoom) Then Frame(fitAll:=True)
            NotifyGraphChanged()
        End Sub

        Private Function FindOpenPosition(definition As ShaderNodeDefinition) As Vector2
            Dim dimensions = NodeDimensions(definition)
            Dim right = Size.X - dimensions.X * zoom - 12
            Dim bottom = Size.Y - dimensions.Y * zoom - 12
            If right > 12 AndAlso bottom > 12 Then
                For Each vertical In {0.48F, 0.75F, 0.15F, 0.95F, 0.0F}
                    For Each horizontal In {0.4F, 0.1F, 0.65F, 0.95F, 0.0F}
                        Dim candidate = ToGraph(New Vector2(12 + (right - 12) * horizontal, 12 + (bottom - 12) * vertical))
                        If zOrder.All(Function(node)
                                          Dim start = NodeLocation(node), finish = start + NodeDimensions(node)
                                          Return candidate.X + dimensions.X + 16 <= start.X OrElse finish.X + 16 <= candidate.X OrElse
                                              candidate.Y + dimensions.Y + 16 <= start.Y OrElse finish.Y + 16 <= candidate.Y
                                      End Function) Then Return candidate
                    Next
                Next
            End If
            Return New Vector2(zOrder.Max(Function(node) NodeLocation(node).X + NodeDimensions(node).X) + 32, ToGraph(Size / 2).Y)
        End Function

        Public Sub DropNode(definition As ShaderNodeDefinition, position As Vector2)
            AddNode(definition, ToGraph(position - Me.Position))
        End Sub

        Public Sub DisconnectSelection()
            If document IsNot Nothing AndAlso document.Connections.RemoveAll(Function(item) selectedIds.Contains(item.Source) OrElse selectedIds.Contains(item.Target)) > 0 Then
                NotifyGraphChanged()
            End If
            Scene.FocusElement(Me)
        End Sub

        Private Sub StartLeft(local As Point)
            If document Is Nothing OrElse gesture <> GraphGesture.Idle Then Return
            start = local.ToVector2()
            pointer = start
            Dim at = ToGraph(pointer)
            Dim port = HitPort(at)
            Dim state = Scene.InputKeyboardState
            If port IsNot Nothing Then
                If state.IsKeyDown(Keys.LeftAlt) OrElse state.IsKeyDown(Keys.RightAlt) Then
                    If document.Connections.RemoveAll(Function(item) If(port.Input Is Nothing, item.Source = port.Node.Id,
                        item.Target = port.Node.Id AndAlso item.Input = port.Input.Name)) > 0 Then NotifyGraphChanged()
                    Return
                End If
                wire = StartWire(port)
                gesture = GraphGesture.Wire
                RaiseEvent StatusChanged($"Connect {AdvancedShaderView.TypeName(wire.Port.Type)} or release on empty canvas to add a node.")
            Else
                Dim node = HitNode(at)
                Dim shift = state.IsKeyDown(Keys.LeftShift) OrElse state.IsKeyDown(Keys.RightShift)
                If node Is Nothing Then
                    If Not shift Then SelectOnly(Nothing)
                    gesture = GraphGesture.Marquee
                Else
                    pendingSelection = Nothing
                    If shift Then
                        If Not selectedIds.Add(node.Id) Then selectedIds.Remove(node.Id)
                        RaiseEvent SelectionChanged()
                    ElseIf Not selectedIds.Contains(node.Id) Then
                        SelectOnly(node)
                    ElseIf selectedIds.Count > 1 Then
                        pendingSelection = node.Id
                    End If
                    zOrder.Remove(node)
                    zOrder.Add(node)
                    dragOrigins.Clear()
                    For Each item In zOrder.Where(Function(candidate) selectedIds.Contains(candidate.Id))
                        dragOrigins.Add(item.Id, (item.X, item.Y))
                    Next
                    gesture = GraphGesture.Move
                End If
            End If
            Scene.CapturePointer(Me)
            UpdateCards()
        End Sub

        Private Function StartWire(port As PortHit) As WireDrag
            If port.Input IsNot Nothing Then
                Dim incoming = document.Connections.FirstOrDefault(Function(item) item.Target = port.Node.Id AndAlso item.Input = port.Input.Name)
                If incoming IsNot Nothing Then
                    Dim source = zOrder.First(Function(node) node.Id = incoming.Source)
                    Return New WireDrag With {.Port = Ports(source).First(Function(item) item.Input Is Nothing), .Original = incoming}
                End If
            End If
            Return New WireDrag With {.Port = port}
        End Function

        Private Sub EndLeft(local As Point)
            If gesture = GraphGesture.Idle OrElse gesture = GraphGesture.Pan Then Return
            pointer = local.ToVector2()
            Dim delta = pointer - start
            Dim previous = gesture
            Dim pending = wire
            gesture = GraphGesture.Idle
            wire = Nothing
            Scene.ReleasePointerCapture(Me)
            Select Case previous
                Case GraphGesture.Move
                    MoveNodes(If(delta.LengthSquared() > 1, delta, Vector2.Zero))
                    If delta.LengthSquared() > 1 AndAlso dragOrigins.Count > 0 Then
                        RaiseEvent Changed()
                    ElseIf pendingSelection.HasValue Then
                        SelectOnly(zOrder.FirstOrDefault(Function(node) node.Id = pendingSelection.Value))
                    End If
                    dragOrigins.Clear()
                    pendingSelection = Nothing
                Case GraphGesture.Marquee
                    If delta.LengthSquared() > 9 Then
                        Dim first = Vector2.Min(ToGraph(start), ToGraph(pointer))
                        Dim last = Vector2.Max(ToGraph(start), ToGraph(pointer))
                        For Each node In zOrder
                            Dim at = NodeLocation(node), edge = at + NodeDimensions(node)
                            If edge.X >= first.X AndAlso edge.Y >= first.Y AndAlso at.X <= last.X AndAlso at.Y <= last.Y Then selectedIds.Add(node.Id)
                        Next
                        RaiseEvent SelectionChanged()
                    End If
                Case GraphGesture.Wire
                    FinishWire(pending, delta)
            End Select
            UpdateCards()
        End Sub

        Private Sub FinishWire(drag As WireDrag, delta As Vector2)
            Dim at = ToGraph(pointer)
            Dim target = HitPort(at)
            If target IsNot Nothing Then
                If CanConnect(drag.Port, target) Then
                    Dim source = If(drag.Port.Input Is Nothing, drag.Port, target)
                    Dim input = If(drag.Port.Input Is Nothing, target, drag.Port)
                    If Connect(source.Node.Id, input.Node.Id, input.Input.Name, drag.Original) Then NotifyGraphChanged()
                ElseIf delta.LengthSquared() > 4 Then
                    RaiseEvent StatusChanged("Those ports cannot connect. Check direction, type, and graph cycles.")
                End If
            ElseIf delta.LengthSquared() > 25 AndAlso InViewport(pointer) AndAlso HitNode(at) Is Nothing Then
                RaiseEvent PaletteRequested(pointer, at, drag)
            End If
        End Sub

        Private Function CanConnect(first As PortHit, second As PortHit) As Boolean
            If (first.Input Is Nothing) = (second.Input Is Nothing) Then Return False
            Dim source = If(first.Input Is Nothing, first, second)
            Dim target = If(first.Input Is Nothing, second, first)
            Return ShaderNodes.CanConnect(document, source.Node.Id, target.Node.Id, target.Input.Name)
        End Function

        Private Function Connect(source As Guid, target As Guid, input As String, original As ShaderConnection) As Boolean
            Dim removed = original IsNot Nothing AndAlso (original.Target <> target OrElse original.Input <> input) AndAlso document.Connections.Remove(original)
            If document.Connections.Any(Function(item) item.Source = source AndAlso item.Target = target AndAlso item.Input = input) Then Return removed
            document.Connections.RemoveAll(Function(item) item.Target = target AndAlso item.Input = input)
            document.Connections.Add(New ShaderConnection With {.Source = source, .Target = target, .Input = input})
            RaiseEvent StatusChanged($"Connected to {input}.")
            Return True
        End Function

        Private Sub StartPan(local As Point)
            If document Is Nothing OrElse gesture <> GraphGesture.Idle Then Return
            Scene.FocusElement(Me)
            start = local.ToVector2()
            pointer = start
            originalPan = pan
            gesture = GraphGesture.Pan
            Scene.CapturePointer(Me)
        End Sub

        Private Sub EndPan(local As Point)
            If gesture <> GraphGesture.Pan Then Return
            Dim at = local.ToVector2()
            Dim delta = at - start
            gesture = GraphGesture.Idle
            Scene.ReleasePointerCapture(Me)
            If delta.LengthSquared() <= 25 Then
                pan = originalPan
                RaiseEvent PaletteRequested(at, ToGraph(at), Nothing)
            Else
                pan = originalPan + delta
            End If
            UpdateCards()
        End Sub

        Private Sub FollowPointer(current As Point, previous As Point)
            pointer = current.ToVector2() - Position
            If gesture = GraphGesture.Idle Then Return
            Dim delta = pointer - start
            Select Case gesture
                Case GraphGesture.Pan
                    pan = originalPan + delta
                    UpdateCards()
                Case GraphGesture.Move
                    MoveNodes(If(delta.LengthSquared() > 1, delta, Vector2.Zero))
                    UpdateCards()
                Case Else
                    Scene.RequestRender()
            End Select
        End Sub

        Private Sub MoveNodes(delta As Vector2)
            For Each node In zOrder
                Dim original As (X As Double, Y As Double)
                If dragOrigins.TryGetValue(node.Id, original) Then
                    node.X = ClampCoordinate(original.X + delta.X / zoom)
                    node.Y = ClampCoordinate(original.Y + delta.Y / zoom)
                End If
            Next
        End Sub

        Private Sub CancelGesture()
            If gesture = GraphGesture.Move Then MoveNodes(Vector2.Zero)
            If gesture = GraphGesture.Pan Then pan = originalPan
            gesture = GraphGesture.Idle
            wire = Nothing
            dragOrigins.Clear()
            pendingSelection = Nothing
            Scene.ReleasePointerCapture(Me)
            UpdateCards()
        End Sub

        Private Sub ZoomAtPointer(delta As Integer, local As Point)
            If gesture <> GraphGesture.Idle Then Return
            Dim anchor = local.ToVector2()
            Dim world = ToGraph(anchor)
            zoom = Math.Clamp(zoom * MathF.Pow(1.16F, delta / 120.0F), 0.16F, 2.75F)
            pan = anchor - world * zoom
            RaiseEvent StatusChanged($"Zoom {zoom:P0}")
            UpdateCards()
        End Sub

        Private Sub HandleKey(key As Keys, state As KeyboardState)
            If document Is Nothing Then Return
            If key = Keys.Escape Then
                CancelGesture()
                SelectOnly(Nothing)
                Return
            End If
            If gesture <> GraphGesture.Idle Then Return
            Dim control = state.IsKeyDown(Keys.LeftControl) OrElse state.IsKeyDown(Keys.RightControl)
            If key = Keys.Space Then
                Dim at = If(InViewport(pointer), pointer, Size / 2)
                RaiseEvent PaletteRequested(at, ToGraph(at), Nothing)
            ElseIf key = Keys.F AndAlso Not control Then
                Frame()
            ElseIf key = Keys.Delete OrElse key = Keys.Back Then
                DeleteSelection()
            ElseIf key = Keys.A AndAlso control Then
                selectedIds.UnionWith(zOrder.Select(Function(node) node.Id))
                RaiseEvent SelectionChanged()
                UpdateCards()
            ElseIf key = Keys.D AndAlso control Then
                DuplicateSelection()
            End If
        End Sub

        Private Sub DeleteSelection()
            Dim removable = zOrder.Where(Function(node) node.Kind <> "Output" AndAlso selectedIds.Contains(node.Id)).Select(Function(node) node.Id).ToHashSet()
            If removable.Count = 0 Then Return
            document.Nodes.RemoveAll(Function(node) removable.Contains(node.Id))
            document.Connections.RemoveAll(Function(item) removable.Contains(item.Source) OrElse removable.Contains(item.Target))
            Refresh()
            NotifyGraphChanged()
        End Sub

        Private Sub DuplicateSelection()
            Dim copies As New Dictionary(Of Guid, ShaderNode)
            For Each node In zOrder.Where(Function(item) item.Kind <> "Output" AndAlso selectedIds.Contains(item.Id))
                Dim copy = ShaderNodes.Create(node.Kind, ClampCoordinate(node.X + 26), ClampCoordinate(node.Y + 26))
                copy.Values = DirectCast(node.Values.Clone(), Double())
                copy.Name = node.Name
                copy.Expression = node.Expression
                copies.Add(node.Id, copy)
            Next
            If copies.Count = 0 Then Return
            Dim wires = document.Connections.Where(Function(item) copies.ContainsKey(item.Source) AndAlso copies.ContainsKey(item.Target)).
                Select(Function(item) New ShaderConnection With {.Source = copies(item.Source).Id, .Target = copies(item.Target).Id, .Input = item.Input}).ToArray()
            document.Nodes.AddRange(copies.Values)
            document.Connections.AddRange(wires)
            selectedIds.Clear()
            selectedIds.UnionWith(copies.Values.Select(Function(node) node.Id))
            Refresh()
            NotifyGraphChanged()
        End Sub

        Private Sub SelectOnly(node As ShaderNode)
            selectedIds.Clear()
            If node IsNot Nothing Then selectedIds.Add(node.Id)
            RaiseEvent SelectionChanged()
            UpdateCards()
        End Sub

        Private Sub NotifyGraphChanged()
            RaiseEvent SelectionChanged()
            RaiseEvent Changed()
            Scene.RequestRender()
        End Sub

        Private Function InViewport(at As Vector2) As Boolean
            Return at.X >= 0 AndAlso at.Y >= 0 AndAlso at.X < Size.X AndAlso at.Y < Size.Y
        End Function

        Private Function ToGraph(at As Vector2) As Vector2
            Return (at - pan) / zoom
        End Function

        Private Function ToViewport(at As Vector2) As Vector2
            Return at * zoom + pan
        End Function

        Private Shared Function ClampCoordinate(value As Double) As Double
            Return Math.Clamp(value, -CoordinateLimit, CoordinateLimit)
        End Function

        Private Shared Function NodeLocation(node As ShaderNode) As Vector2
            Return New Vector2(CSng(ClampCoordinate(node.X)), CSng(ClampCoordinate(node.Y)))
        End Function

        Private Shared Function NodeDimensions(node As ShaderNode) As Vector2
            Return NodeDimensions(ShaderNodes.Get(node.Kind))
        End Function

        Private Shared Function NodeDimensions(definition As ShaderNodeDefinition) As Vector2
            Dim rows = Math.Max(definition.Inputs.Count, If(definition.Output.HasValue, 1, 0))
            Return New Vector2(NodeWidth, HeaderHeight + rows * RowHeight + If(definition.Parameters.Count > 0, 24, 6))
        End Function

        Private Shared Iterator Function Ports(node As ShaderNode) As IEnumerable(Of PortHit)
            Dim definition = ShaderNodes.Get(node.Kind)
            Dim at = NodeLocation(node)
            Dim top = at.Y + HeaderHeight + RowHeight / 2 + 1
            For index = 0 To definition.Inputs.Count - 1
                Dim input = definition.Inputs(index)
                Yield New PortHit With {.Node = node, .Input = input, .Type = input.Type, .Anchor = New Vector2(at.X, top + index * RowHeight)}
            Next
            If definition.Output.HasValue Then Yield New PortHit With {.Node = node, .Type = definition.Output.Value, .Anchor = New Vector2(at.X + NodeWidth, top)}
        End Function

        Private Shared Function ContainsNode(node As ShaderNode, at As Vector2) As Boolean
            Dim minimum = NodeLocation(node), maximum = minimum + NodeDimensions(node)
            Return at.X >= minimum.X AndAlso at.Y >= minimum.Y AndAlso at.X <= maximum.X AndAlso at.Y <= maximum.Y
        End Function

        Private Function HitNode(at As Vector2) As ShaderNode
            For index = zOrder.Count - 1 To 0 Step -1
                If ContainsNode(zOrder(index), at) Then Return zOrder(index)
            Next
            Return Nothing
        End Function

        Private Function HitPort(at As Vector2) As PortHit
            Dim tolerance = Math.Clamp(8 / zoom, 6, 17)
            For index = zOrder.Count - 1 To 0 Step -1
                Dim node = zOrder(index)
                For Each port In Ports(node)
                    Dim minimumX = port.Anchor.X - If(port.Input Is Nothing, 49, tolerance)
                    Dim maximumX = port.Anchor.X + If(port.Input Is Nothing, tolerance, 70)
                    If at.X >= minimumX AndAlso at.X <= maximumX AndAlso Math.Abs(at.Y - port.Anchor.Y) <= tolerance / 2 Then Return port
                Next
                If ContainsNode(node, at) Then Exit For
            Next
            Return Nothing
        End Function

        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            If Size.X <= 0 OrElse Size.Y <= 0 Then Return
            If Texture Is Nothing Then Texture = Global.ProjectZ.Shared.Content.Textures.GetSharedWhiteTexture(Scene.graphicsDevice)
            Dim faint = Color.Lerp(settings.BackgroundColor, settings.TextColor, 0.26F) * 0.58F
            Dim stepSize = If(zoom >= 0.55F, 24.0F, 96.0F) * zoom
            Dim x = pan.X Mod stepSize
            While x < Size.X
                spriteBatch.Draw(Texture, New Rectangle(CInt(Position.X + x), CInt(Position.Y), 1, CInt(Size.Y)), faint)
                x += stepSize
            End While
            Dim y = pan.Y Mod stepSize
            While y < Size.Y
                spriteBatch.Draw(Texture, New Rectangle(CInt(Position.X), CInt(Position.Y + y), CInt(Size.X), 1), faint)
                y += stepSize
            End While
            If document Is Nothing Then Return
            For Each item In document.Connections
                If item Is wire?.Original OrElse Not cards.ContainsKey(item.Source) OrElse Not cards.ContainsKey(item.Target) Then Continue For
                Dim source = Ports(cards(item.Source).Node).FirstOrDefault(Function(port) port.Input Is Nothing)
                Dim target = Ports(cards(item.Target).Node).FirstOrDefault(Function(port) port.Input?.Name = item.Input)
                If source Is Nothing OrElse target Is Nothing Then Continue For
                Dim highlighted = selectedIds.Contains(item.Source) OrElse selectedIds.Contains(item.Target)
                DrawWire(source.Anchor, target.Anchor, PortColor(source.Type), If(highlighted, 3.1F, 2.0F))
            Next
            If wire IsNot Nothing Then
                Dim from = wire.Port
                Dim at = ToGraph(pointer)
                Dim target = HitPort(at)
                If target IsNot Nothing AndAlso CanConnect(from, target) Then at = target.Anchor
                DrawWire(If(from.Input Is Nothing, from.Anchor, at), If(from.Input Is Nothing, at, from.Anchor), PortColor(from.Type), 3)
            End If
            If gesture = GraphGesture.Marquee Then
                Dim origin = Position + Vector2.Min(start, pointer)
                Dim extent = Vector2.Max(start, pointer) - Vector2.Min(start, pointer)
                Dim bounds As New Rectangle(CInt(origin.X), CInt(origin.Y), CInt(extent.X), CInt(extent.Y))
                spriteBatch.Draw(Texture, bounds, settings.AccentColor * 0.22F)
                DrawSegment(origin, origin + New Vector2(extent.X, 0), settings.AccentColor, 1)
                DrawSegment(origin, origin + New Vector2(0, extent.Y), settings.AccentColor, 1)
                DrawSegment(origin + extent, origin + New Vector2(extent.X, 0), settings.AccentColor, 1)
                DrawSegment(origin + extent, origin + New Vector2(0, extent.Y), settings.AccentColor, 1)
            End If
        End Sub

        Private Sub DrawWire(from As Vector2, [to] As Vector2, color As Color, thickness As Single)
            Dim a = Position + ToViewport(from), d = Position + ToViewport([to])
            Dim bend = Math.Clamp(Math.Abs(d.X - a.X) * 0.46F + Math.Abs(d.Y - a.Y) * 0.16F, 20.0F, 190.0F)
            Dim b = a + New Vector2(bend, 0), c = d - New Vector2(bend, 0)
            Dim previous = a
            For index = 1 To 20
                Dim t = index / 20.0F, u = 1 - t
                Dim nextPoint = a * (u * u * u) + b * (3 * u * u * t) + c * (3 * u * t * t) + d * (t * t * t)
                DrawSegment(previous, nextPoint, color, thickness)
                previous = nextPoint
            Next
        End Sub

        Private Sub DrawSegment(a As Vector2, b As Vector2, color As Color, thickness As Single)
            Dim delta = b - a
            spriteBatch.Draw(Texture, a, Nothing, color, MathF.Atan2(delta.Y, delta.X), Vector2.Zero,
                             New Vector2(delta.Length(), thickness), SpriteEffects.None, 0)
        End Sub

        Private Shared Function PortColor(type As ShaderValueType) As Color
            Select Case type
                Case ShaderValueType.Float : Return New Color(130, 217, 179)
                Case ShaderValueType.Float2 : Return New Color(128, 200, 255)
                Case ShaderValueType.Float3 : Return New Color(255, 213, 148)
                Case Else : Return New Color(195, 173, 255)
            End Select
        End Function

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                RemoveHandler Scene.OnMouseMove, AddressOf FollowPointer
                RemoveHandler Scene.OnPointerCancelled, AddressOf CancelGesture
            End If
            MyBase.Dispose(disposing)
        End Sub

        Private NotInheritable Class GraphCard
            Inherits RectangleElement

            Public ReadOnly Property Node As ShaderNode
            Private ReadOnly definition As ShaderNodeDefinition
            Private ReadOnly title As TextElement
            Private ReadOnly portLabels As TextElement()
            Private ReadOnly values As TextElement
            Private appearance As New WindowAppearanceSettings()
            Private highlighted As Boolean
            Private scale As Single

            Public Sub New(scene As Scene, node As ShaderNode)
                MyBase.New(scene)
                Me.Node = node
                definition = ShaderNodes.Get(node.Kind)
                isMouseBypassEnabled = True
                title = Label(scene, definition.Label)
                Children.Add(title)
                portLabels = Ports(node).Select(Function(port) Label(scene, If(port.Input?.Name, AdvancedShaderView.TypeName(port.Type)))).ToArray()
                For Each portLabel In portLabels
                    Children.Add(portLabel)
                Next
                If definition.Parameters.Count > 0 Then
                    values = Label(scene, "")
                    Children.Add(values)
                End If
            End Sub

            Private Shared Function Label(scene As Scene, text As String) As TextElement
                Return New TextElement(scene) With {.TextWrapping = TextWrapping.NoWrap, .UseXamlTextLayout = True,
                    .Text = text, .Clip = True, .isMouseBypassEnabled = True}
            End Function

            Public Sub UpdateLayout(at As Vector2, dimensions As Vector2, zoom As Single, selected As Boolean, settings As WindowAppearanceSettings)
                Position = at
                Size = dimensions
                highlighted = selected
                appearance = settings
                scale = zoom
                Dim titleText = If(String.IsNullOrWhiteSpace(Node.Name), definition.Label, Node.Name)
                If title.Text <> titleText Then title.Text = titleText
                Place(title, 10, 5, NodeWidth - 20, 0.76F, If(definition.Output.HasValue, settings.TextColor, settings.AccentColor))
                Dim labelColor = Color.Lerp(settings.TextColor, settings.SurfaceColor, 0.24F)
                Dim index = 0
                For Each port In Ports(Node)
                    Dim label = portLabels(index)
                    Dim fromRight = port.Input Is Nothing
                    label.TextAlignment = If(fromRight, HorizontalAlignment.Right, HorizontalAlignment.Left)
                    Place(label, If(fromRight, NodeWidth - 64, 10), port.Anchor.Y - NodeLocation(Node).Y - 8, If(fromRight, 54, 96), 0.68F, labelColor)
                    index += 1
                Next
                If values IsNot Nothing Then
                    Dim text = String.Join(", ", Node.Values.Select(Function(value) value.ToString("G4", CultureInfo.InvariantCulture)))
                    If values.Text <> text Then values.Text = text
                    Dim swatch = If(Node.Kind = "Color", 25, 10)
                    Place(values, swatch, dimensions.Y / scale - 20, NodeWidth - swatch - 10, 0.66F, labelColor)
                End If
            End Sub

            Private Sub Place(text As TextElement, x As Single, y As Single, width As Single, fontScale As Single, color As Color)
                text.Position = Position + New Vector2(x, y) * scale
                text.FontScale = fontScale * scale
                text.Size = New Vector2(width, 23) * scale
                text.ForegroundColor = color
                text.isVisible = scale >= 0.34F
            End Sub

            Protected Overrides Sub AlignChildren()
                ' Labels use the same graph zoom and local port positions as the card.
            End Sub

            Protected Friend Overrides Sub Draw(gameTime As GameTime)
                If Texture Is Nothing Then Texture = Global.ProjectZ.Shared.Content.Textures.GetSharedWhiteTexture(Scene.graphicsDevice)
                Dim bounds = Rectangle
                Dim border = If(highlighted, appearance.AccentColor, Color.Lerp(appearance.SurfaceColor, appearance.TextColor, 0.35F))
                spriteBatch.Draw(Texture, bounds, border)
                Dim inset = If(highlighted, 2, 1)
                Dim inner As New Rectangle(bounds.X + inset, bounds.Y + inset, Math.Max(0, bounds.Width - inset * 2), Math.Max(0, bounds.Height - inset * 2))
                spriteBatch.Draw(Texture, inner, appearance.SurfaceColor)
                inner.Height = Math.Max(0, CInt(HeaderHeight * scale) - inset)
                spriteBatch.Draw(Texture, inner, Color.Lerp(appearance.SurfaceColor, appearance.BackgroundColor, 0.42F))
                Dim nodePosition = NodeLocation(Node)
                For Each port In Ports(Node)
                    Dim at = Position + (port.Anchor - nodePosition) * scale
                    Dim size = Math.Max(5, CInt(scale * 9))
                    spriteBatch.Draw(Texture, New Rectangle(CInt(at.X) - size \ 2, CInt(at.Y) - size \ 2, size, size), PortColor(port.Type))
                Next
                If Node.Kind = "Color" Then
                    Dim channels = Node.Values.Select(Function(value) CInt(Math.Round(Math.Clamp(value, 0, 1) * 255))).ToArray()
                    spriteBatch.Draw(Texture, New Rectangle(bounds.X + CInt(10 * scale), bounds.Bottom - CInt(19 * scale), CInt(12 * scale), CInt(12 * scale)),
                                     New Color(channels(0), channels(1), channels(2), channels(3)))
                End If
            End Sub
        End Class
    End Class
End Namespace
