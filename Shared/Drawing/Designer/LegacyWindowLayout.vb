Imports System.Xml
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Shared.Drawing.UI.Advanced

Namespace [Shared].Drawing.Designer
    ' Opt-in WPF measure/arrange for linked windows. No app-specific coordinates.
    Friend NotInheritable Class LegacyWindowLayout
        Private ReadOnly scene As Scene
        Private ReadOnly nodes As Dictionary(Of SceneElement, XmlElement)
        Private revision As Integer
        Public Sub New(scene As Scene, map As Dictionary(Of XmlElement, SceneElement))
            Me.scene = scene
            nodes = map.ToDictionary(Function(p) p.Value, Function(p) p.Key)
        End Sub
        Public Sub Invalidate()
            revision += 1
        End Sub

        Public Sub Attach()
            For Each pair In nodes
                Dim item = pair.Key, xml = pair.Value
                If xml.HasAttribute("Type", "urn:projectz:import") AndAlso item.ImportedMeasure IsNot Nothing Then Continue For
                item.ImportedMeasure = Function(width) Measure(item, width)
                AddHandler item.VisibilityChanged, Sub() revision += 1
                item.Padding = ThicknessValue(xml.GetAttribute("Padding"))
                If TypeOf item Is TextElement Then
                    Dim text = DirectCast(item, TextElement)
                    text.isMouseBypassEnabled = True
                    Dim size = Number(xml, "FontSize", 12)
                    text.FontScale = size * 0.75F / 12
                    AddHandler text.TextChanged, Sub() revision += 1
                End If
                If TypeOf item Is Button Then
                    Dim button = DirectCast(item, Button)
                    button.AutoSize = ButtonAutoSize.None
                End If
                If TypeOf item Is StackPanel Then DirectCast(item, StackPanel).AutoSize = False
                If TypeOf item Is WrapPanel Then
                    Dim wrap = DirectCast(item, WrapPanel)
                    wrap.HorizontalSpacing = 0 : wrap.VerticalSpacing = 0
                End If
                If {"Window", "UserControl", "ContentControl", "Border", "Grid", "StackPanel", "WrapPanel", "TabItem"}.Contains(xml.LocalName) Then
                    Dim lastPosition As New Vector2(Single.NaN)
                    Dim lastSize As New Vector2(Single.NaN)
                    Dim lastRevision = -1
                    item.ImportedLayout = Sub()
                                              If lastRevision = revision AndAlso lastPosition = item.Position AndAlso lastSize = item.Size Then Return
                                              If lastRevision = revision AndAlso lastSize = item.Size Then
                                                  ' A scroll is translation, not a new measure pass. Keep
                                                  ' the established XAML grid sizes and translate children.
                                                  Dim delta = item.Position - lastPosition
                                                  For Each child In item.Children
                                                      If nodes.ContainsKey(child) AndAlso child.isVisible Then child.Position += delta
                                                  Next
                                              Else
                                                  Arrange(item)
                                              End If
                                              lastPosition = item.Position
                                              lastSize = item.Size
                                              lastRevision = revision
                                          End Sub
                End If
            Next
        End Sub

        Private Function VisualChildren(item As SceneElement) As SceneElement()
            Return item.Children.Where(Function(c) nodes.ContainsKey(c) AndAlso c.isVisible).ToArray()
        End Function

        Public Function Measure(item As SceneElement, width As Single) As Vector2
            Dim xml = nodes(item)
            Dim available = Math.Max(1, width - item.Margin.Left - item.Margin.Right)
            available = Math.Min(available, Number(xml, "Width", available))
            available = Math.Min(available, Number(xml, "MaxWidth", Single.MaxValue))
            Dim desired As Vector2
            If xml.HasAttribute("Type", "urn:projectz:import") AndAlso item.ImportedMeasure IsNot Nothing Then
                desired = item.ImportedMeasure.Invoke(available)
            ElseIf TypeOf item Is TextElement Then
                Dim text = DirectCast(item, TextElement)
                desired = text.MeasureXamlText(available)
            ElseIf TypeOf item Is ImageElement Then
                Dim image = DirectCast(item, ImageElement)
                If image.Texture IsNot Nothing Then
                    Dim ratio = Math.Min(available / image.Texture.Width, Number(xml, "MaxHeight", image.Texture.Height) / image.Texture.Height)
                    desired = New Vector2(image.Texture.Width * ratio, image.Texture.Height * ratio)
                End If
            ElseIf TypeOf item Is Button Then
                Dim button = DirectCast(item, Button)
                Dim textSize = scene.MeasureText(button.Font, button.Text)
                desired = ControlDesired(item, available, textSize.X + 10, Math.Max(28, textSize.Y))
            ElseIf TypeOf item Is RadioButton Then
                Dim radio = DirectCast(item, RadioButton)
                Dim textSize = scene.MeasureText(radio.Font, radio.Content)
                desired = ControlDesired(item, available, radio.CircleSize + 8 + textSize.X, Math.Max(28, textSize.Y))
            ElseIf TypeOf item Is CheckBox Then
                Dim check = DirectCast(item, CheckBox)
                Dim textSize = scene.MeasureText(check.Font, check.Content)
                desired = ControlDesired(item, available, check.BoxSize + 8 + textSize.X, Math.Max(28, textSize.Y))
            ElseIf TypeOf item Is Separator Then
                desired = ControlDesired(item, available, 0, 1)
            ElseIf TypeOf item Is Trackbar OrElse TypeOf item Is ProgressBar Then
                desired = ControlDesired(item, available, 100, 28)
            ElseIf TypeOf item Is Textbox Then
                Dim input = DirectCast(item, Textbox)
                desired = ControlDesired(item, available, Math.Max(80, scene.MeasureText(input.Font, input.Text).X), 30)
            ElseIf TypeOf item Is ComboBox Then
                Dim label = item.Children.OfType(Of TextElement)().FirstOrDefault()
                Dim naturalWidth = If(label Is Nothing, 80, Math.Max(80, scene.MeasureText(label.Font, label.Text).X + 30))
                desired = ControlDesired(item, available, naturalWidth, 30)
            ElseIf TypeOf item Is ScrollViewer OrElse TypeOf item Is ListBox Then
                Dim naturalWidth As Single
                For Each child In VisualChildren(item)
                    naturalWidth = Math.Max(naturalWidth, Measure(child, available).X)
                Next
                desired = ControlDesired(item, available, naturalWidth, 0)
            ElseIf TypeOf item Is GroupBox Then
                Dim group = DirectCast(item, GroupBox)
                Dim contentSize As Vector2
                If group.Content IsNot Nothing AndAlso nodes.ContainsKey(group.Content) Then contentSize = Measure(group.Content, Math.Max(1, available - item.Padding.Left - item.Padding.Right - 8))
                desired = contentSize + New Vector2(8 + item.Padding.Left + item.Padding.Right, group.HeaderHeight + 4 + item.Padding.Top + item.Padding.Bottom)
            ElseIf TypeOf item Is TabControl Then
                Dim tabs = DirectCast(item, TabControl)
                Dim contentSize As Vector2
                If tabs.SelectedIndex >= 0 AndAlso tabs.SelectedIndex < tabs.Tabs.Count Then
                    Dim content = tabs.Tabs(tabs.SelectedIndex).Content
                    If content IsNot Nothing AndAlso nodes.ContainsKey(content) Then contentSize = Measure(content, available)
                End If
                desired = contentSize + New Vector2(0, tabs.TabHeaderHeight)
            ElseIf TypeOf item Is UniformGrid Then
                Dim uniform = DirectCast(item, UniformGrid)
                Dim children = VisualChildren(item)
                Dim columns = If(uniform.Columns > 0, uniform.Columns, If(uniform.Rows > 0, CInt(Math.Ceiling(children.Length / CDbl(uniform.Rows))), CInt(Math.Ceiling(Math.Sqrt(children.Length)))))
                columns = Math.Max(1, columns)
                Dim rows = If(uniform.Rows > 0, uniform.Rows, CInt(Math.Ceiling(children.Length / CDbl(columns))))
                Dim cellSize As Vector2
                Dim childWidth = If(IsUnconstrained(available), Single.MaxValue, Math.Max(1, available - item.Padding.Left - item.Padding.Right) / columns)
                For Each child In children
                    cellSize = Vector2.Max(cellSize, Measure(child, childWidth))
                Next
                desired = New Vector2(cellSize.X * columns + item.Padding.Left + item.Padding.Right, cellSize.Y * rows + item.Padding.Top + item.Padding.Bottom)
            ElseIf TypeOf item Is WrapPanel Then
                Dim wrap = DirectCast(item, WrapPanel)
                desired = LayoutWrap(wrap, available, Number(xml, "Height", Single.MaxValue), False)
            ElseIf TypeOf item Is Grid Then
                Dim innerWidth = Math.Max(1, available - item.Padding.Left - item.Padding.Right)
                If IsUnconstrained(innerWidth) Then innerWidth = TrackSizes(item, True, innerWidth, 0, False).Sum()
                Dim rows = TrackSizes(item, False, innerWidth, 0, False)
                desired = New Vector2(innerWidth + item.Padding.Left + item.Padding.Right, rows.Sum() + item.Padding.Top + item.Padding.Bottom)
            Else
                Dim horizontalStack = TypeOf item Is StackPanel AndAlso DirectCast(item, StackPanel).Orientation = Orientation.Horizontal
                For Each child In VisualChildren(item)
                    Dim childSize = Measure(child, If(horizontalStack, Single.MaxValue, available - item.Padding.Left - item.Padding.Right))
                    If horizontalStack Then
                        desired.X += childSize.X
                        desired.Y = Math.Max(desired.Y, childSize.Y)
                    ElseIf TypeOf item Is StackPanel Then
                        desired.X = Math.Max(desired.X, childSize.X)
                        desired.Y += childSize.Y
                    Else
                        desired.X = Math.Max(desired.X, childSize.X)
                        desired.Y = Math.Max(desired.Y, childSize.Y)
                    End If
                Next
                desired += New Vector2(item.Padding.Left + item.Padding.Right, item.Padding.Top + item.Padding.Bottom)
            End If
            desired.X = Number(xml, "Width", desired.X)
            desired.Y = Number(xml, "Height", desired.Y)
            desired = Vector2.Max(desired, New Vector2(Number(xml, "MinWidth", 0), Number(xml, "MinHeight", 0)))
            desired = Vector2.Min(desired, New Vector2(Number(xml, "MaxWidth", Single.MaxValue), Number(xml, "MaxHeight", Single.MaxValue)))
            Return desired + New Vector2(item.Margin.Left + item.Margin.Right, item.Margin.Top + item.Margin.Bottom)
        End Function

        Private Shared Function IsUnconstrained(width As Single) As Boolean
            Return Single.IsPositiveInfinity(width) OrElse width = Single.MaxValue
        End Function

        Private Shared Function ControlDesired(item As SceneElement, available As Single, naturalWidth As Single, naturalHeight As Single) As Vector2
            ' Horizontal stacks measure their children without a width limit.
            ' That limit is a constraint, never a requested pixel width.
            Return New Vector2(If(IsUnconstrained(available), naturalWidth + item.Padding.Left + item.Padding.Right, available), naturalHeight + item.Padding.Top + item.Padding.Bottom)
        End Function

        Private Function TrackSizes(item As SceneElement, horizontal As Boolean, width As Single, height As Single, stretch As Boolean) As Single()
            Dim grid = DirectCast(item, Grid)
            Dim definitions = If(horizontal, grid.ColumnDefinitions, grid.RowDefinitions)
            If definitions.Count = 0 Then definitions = New List(Of GridDefinition) From {GridDefinition.StarSize()}
            Dim desired(definitions.Count - 1) As Single
            Dim columns = If(horizontal, Nothing, TrackSizes(item, True, width, height, Not IsUnconstrained(width)))
            For Each child In VisualChildren(item)
                Dim xml = nodes(child)
                Dim index = Math.Clamp(CInt(Number(xml, If(horizontal, "Grid.Column", "Grid.Row"), 0)), 0, definitions.Count - 1)
                Dim span = Math.Clamp(CInt(Number(xml, If(horizontal, "Grid.ColumnSpan", "Grid.RowSpan"), 1)), 1, definitions.Count - index)
                ' Spanning content can use the star track's remaining space;
                ' it must not force that entire width into an adjacent Auto track.
                If span > 1 AndAlso stretch AndAlso Enumerable.Range(index, span).Any(Function(i) definitions(i).SizeMode.GetValueOrDefault(If(definitions(i).Size < 0, GridSizeMode.Auto, If(definitions(i).Size = 0, GridSizeMode.Star, GridSizeMode.Pixel))) = GridSizeMode.Star) Then Continue For
                Dim flexible = Enumerable.Range(index, span).Where(Function(i)
                                                                      Dim definition = definitions(i)
                                                                      Dim mode = definition.SizeMode.GetValueOrDefault(If(definition.Size < 0, GridSizeMode.Auto, If(definition.Size = 0, GridSizeMode.Star, GridSizeMode.Pixel)))
                                                                      Return mode <> GridSizeMode.Pixel AndAlso (Not stretch OrElse mode <> GridSizeMode.Star)
                                                                  End Function).ToArray()
                If flexible.Length = 0 Then Continue For
                Dim childWidth = Single.MaxValue
                If Not horizontal Then
                    Dim col = Math.Clamp(CInt(Number(xml, "Grid.Column", 0)), 0, columns.Length - 1)
                    childWidth = columns.Skip(col).Take(CInt(Number(xml, "Grid.ColumnSpan", 1))).Sum()
                End If
                Dim size = Measure(child, childWidth)
                Dim requested = If(horizontal, size.X, size.Y)
                Dim established = Enumerable.Range(index, span).Sum(Function(i) If(definitions(i).SizeMode = GridSizeMode.Pixel OrElse (definitions(i).SizeMode Is Nothing AndAlso definitions(i).Size > 0), definitions(i).Size, desired(i)))
                Dim addition = Math.Max(0, requested - established) / flexible.Length
                For Each track In flexible
                    desired(track) += addition
                Next
            Next
            Return Grid.ComputeTrackSizes(definitions, If(horizontal, width, height), desired, stretch)
        End Function

        Private Sub Arrange(item As SceneElement)
            If TypeOf item Is WrapPanel Then
                LayoutWrap(DirectCast(item, WrapPanel), item.Size.X, item.Size.Y, True)
                Return
            End If
            If TypeOf item Is DockPanel Then
                ArrangeDock(DirectCast(item, DockPanel))
                Return
            End If
            Dim width = Math.Max(1, item.Size.X - item.Padding.Left - item.Padding.Right)
            Dim height = Math.Max(0, item.Size.Y - item.Padding.Top - item.Padding.Bottom)
            Dim start = item.Position + New Vector2(item.Padding.Left, item.Padding.Top)
            Dim stackOffset As Single
            Dim horizontalStack = TypeOf item Is StackPanel AndAlso DirectCast(item, StackPanel).Orientation = Orientation.Horizontal
            Dim columns = If(TypeOf item Is Grid, TrackSizes(item, True, width, height, True), {width})
            Dim rows = If(TypeOf item Is Grid, TrackSizes(item, False, width, height, True), {height})
            For Each child In VisualChildren(item)
                Dim xml = nodes(child)
                Dim col = Math.Clamp(CInt(Number(xml, "Grid.Column", 0)), 0, columns.Length - 1)
                Dim row = Math.Clamp(CInt(Number(xml, "Grid.Row", 0)), 0, rows.Length - 1)
                Dim slotWidth = columns.Skip(col).Take(CInt(Number(xml, "Grid.ColumnSpan", 1))).Sum()
                Dim slotHeight = rows.Skip(row).Take(CInt(Number(xml, "Grid.RowSpan", 1))).Sum()
                Dim desired = Measure(child, If(horizontalStack, Single.MaxValue, slotWidth))
                Dim location = start + New Vector2(columns.Take(col).Sum(), rows.Take(row).Sum())
                If TypeOf item Is StackPanel Then
                    If horizontalStack Then
                        location.X += stackOffset
                        slotWidth = desired.X
                        stackOffset += slotWidth
                    Else
                        location.Y += stackOffset
                        slotHeight = desired.Y
                        stackOffset += slotHeight
                    End If
                End If
                Dim margin = child.Margin
                Dim childWidth = Math.Max(0, slotWidth - margin.Left - margin.Right)
                Dim childHeight = Math.Max(0, slotHeight - margin.Top - margin.Bottom)
                Dim ha = xml.GetAttribute("HorizontalAlignment"), va = xml.GetAttribute("VerticalAlignment")
                If xml.HasAttribute("Width") OrElse (ha <> "" AndAlso ha <> "Stretch") Then childWidth = Math.Max(0, desired.X - margin.Left - margin.Right)
                If xml.HasAttribute("Height") OrElse (va <> "" AndAlso va <> "Stretch") Then childHeight = Math.Max(0, desired.Y - margin.Top - margin.Bottom)
                location += New Vector2(margin.Left, margin.Top)
                If ha = "Center" Then location.X += (slotWidth - margin.Left - margin.Right - childWidth) / 2
                If ha = "Right" Then location.X += slotWidth - margin.Left - margin.Right - childWidth
                If va = "Center" Then location.Y += (slotHeight - margin.Top - margin.Bottom - childHeight) / 2
                If va = "Bottom" Then location.Y += slotHeight - margin.Top - margin.Bottom - childHeight
                If child.Position <> location Then child.Position = location
                Dim finalSize As New Vector2(childWidth, childHeight)
                If child.Size <> finalSize Then child.Size = finalSize
            Next
        End Sub

        Private Sub ArrangeDock(panel As DockPanel)
            Dim left = panel.Position.X + panel.Padding.Left, top = panel.Position.Y + panel.Padding.Top
            Dim right = panel.Position.X + panel.Size.X - panel.Padding.Right, bottom = panel.Position.Y + panel.Size.Y - panel.Padding.Bottom
            Dim children = panel.Children.Where(Function(child) nodes.ContainsKey(child) AndAlso nodes(child).GetAttribute("Visibility") <> "Collapsed").ToArray()
            For index = 0 To children.Length - 1
                Dim child = children(index), xml = nodes(child)
                Dim desired = Measure(child, Math.Max(0, right - left))
                Dim x = left, y = top, width = Math.Max(0, right - left), height = Math.Max(0, bottom - top)
                If Not panel.LastChildFill OrElse index <> children.Length - 1 Then
                    Select Case xml.GetAttribute("DockPanel.Dock")
                        Case "Top" : height = Math.Min(height, desired.Y) : top += height
                        Case "Bottom" : height = Math.Min(height, desired.Y) : bottom -= height : y = bottom
                        Case "Right" : width = Math.Min(width, desired.X) : right -= width : x = right
                        Case Else : width = Math.Min(width, desired.X) : left += width
                    End Select
                End If
                Dim margin = child.Margin
                Dim childWidth = Math.Max(0, width - margin.Left - margin.Right), childHeight = Math.Max(0, height - margin.Top - margin.Bottom)
                If xml.HasAttribute("Width") Then childWidth = Math.Min(childWidth, Math.Max(0, desired.X - margin.Left - margin.Right))
                If xml.HasAttribute("Height") Then childHeight = Math.Min(childHeight, Math.Max(0, desired.Y - margin.Top - margin.Bottom))
                x += margin.Left : y += margin.Top
                If xml.GetAttribute("HorizontalAlignment") = "Center" Then x += (width - margin.Left - margin.Right - childWidth) / 2
                If xml.GetAttribute("HorizontalAlignment") = "Right" Then x += width - margin.Left - margin.Right - childWidth
                If xml.GetAttribute("VerticalAlignment") = "Center" Then y += (height - margin.Top - margin.Bottom - childHeight) / 2
                If xml.GetAttribute("VerticalAlignment") = "Bottom" Then y += height - margin.Top - margin.Bottom - childHeight
                child.Position = New Vector2(x, y) : child.Size = New Vector2(childWidth, childHeight)
            Next
        End Sub

        Private Function LayoutWrap(panel As WrapPanel, width As Single, height As Single, arrange As Boolean) As Vector2
            Dim horizontal = panel.Orientation = Orientation.Horizontal
            Dim available = Math.Max(1, If(horizontal, width - panel.Padding.Left - panel.Padding.Right, height - panel.Padding.Top - panel.Padding.Bottom))
            Dim cursor As Single, crossOffset As Single, lineExtent As Single, maximumExtent As Single
            For Each child In VisualChildren(panel)
                Dim size = Measure(child, Single.MaxValue)
                If panel.ItemWidth > 0 Then size.X = panel.ItemWidth
                If panel.ItemHeight > 0 Then size.Y = panel.ItemHeight
                Dim extent = If(horizontal, size.X, size.Y), cross = If(horizontal, size.Y, size.X)
                If cursor > 0 AndAlso cursor + extent > available Then
                    maximumExtent = Math.Max(maximumExtent, cursor)
                    crossOffset += lineExtent
                    cursor = 0 : lineExtent = 0
                End If
                If arrange Then
                    Dim margin = child.Margin
                    child.Position = panel.Position + New Vector2(panel.Padding.Left + margin.Left + If(horizontal, cursor, crossOffset), panel.Padding.Top + margin.Top + If(horizontal, crossOffset, cursor))
                    child.Size = New Vector2(Math.Max(0, size.X - margin.Left - margin.Right), Math.Max(0, size.Y - margin.Top - margin.Bottom))
                End If
                cursor += extent
                lineExtent = Math.Max(lineExtent, cross)
            Next
            maximumExtent = Math.Max(maximumExtent, cursor)
            Dim result = If(horizontal, New Vector2(maximumExtent, crossOffset + lineExtent), New Vector2(crossOffset + lineExtent, maximumExtent))
            Return result + New Vector2(panel.Padding.Left + panel.Padding.Right, panel.Padding.Top + panel.Padding.Bottom)
        End Function

        Private Shared Function Number(xml As XmlElement, name As String, fallback As Single) As Single
            Dim value As Single
            Return If(Single.TryParse(xml.GetAttribute(name), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, value), value, fallback)
        End Function

        Private Shared Function ThicknessValue(value As String) As Thickness
            Dim parts = value.Split(","c)
            Dim values As New List(Of Integer)
            For Each part In parts
                Dim result As Integer
                Integer.TryParse(part, result)
                values.Add(result)
            Next
            If values.Count = 4 Then Return New Thickness(values(0), values(1), values(2), values(3))
            If values.Count = 2 Then Return New Thickness(values(0), values(1), values(0), values(1))
            Return New Thickness(values(0))
        End Function
    End Class
End Namespace
