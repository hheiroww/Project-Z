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

        Public Sub Attach()
            For Each pair In nodes
                Dim item = pair.Key, xml = pair.Value
                AddHandler item.VisibilityChanged, Sub() revision += 1
                item.Padding = ThicknessValue(xml.GetAttribute("Padding"))
                If TypeOf item Is TextElement Then
                    Dim text = DirectCast(item, TextElement)
                    text.isMouseBypassEnabled = True
                    Dim size = Number(xml, "FontSize", 12)
                    text.Font = "SegoeUI_" & If(size <= 10, "10", If(size <= 12, "12", "14"))
                    text.Text = text.Text.Replace("✕", "x").Replace("▾", "v").Replace("…", "...")
                    AddHandler text.TextChanged, Sub() revision += 1
                End If
                If TypeOf item Is Button Then
                    Dim button = DirectCast(item, Button)
                    button.AutoSize = ButtonAutoSize.None
                    button.Text = button.Text.Replace("▾", "v")
                End If
                If TypeOf item Is StackPanel Then DirectCast(item, StackPanel).AutoSize = False
                If {"Window", "UserControl", "Border", "Grid", "StackPanel"}.Contains(xml.LocalName) Then
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
            Dim desired As Vector2
            If TypeOf item Is TextElement Then
                Dim text = DirectCast(item, TextElement)
                text.MaxWidth = Math.Max(1, available - text.Padding.Left - text.Padding.Right)
                desired = text.XamlDesiredSize
            ElseIf TypeOf item Is ImageElement Then
                Dim image = DirectCast(item, ImageElement)
                If image.Texture IsNot Nothing Then
                    Dim ratio = Math.Min(available / image.Texture.Width, Number(xml, "MaxHeight", image.Texture.Height) / image.Texture.Height)
                    desired = New Vector2(image.Texture.Width * ratio, image.Texture.Height * ratio)
                End If
            ElseIf TypeOf item Is Button Then
                desired = New Vector2(available, 28)
            ElseIf TypeOf item Is Textbox Then
                desired = New Vector2(available, 30)
            ElseIf TypeOf item Is ScrollViewer OrElse TypeOf item Is ListBox Then
                desired = New Vector2(available, 0)
            ElseIf TypeOf item Is Grid Then
                Dim rows = TrackSizes(item, False, available, 0, False)
                desired = New Vector2(available, rows.Sum())
            Else
                For Each child In VisualChildren(item)
                    Dim childSize = Measure(child, available - item.Padding.Left - item.Padding.Right)
                    desired.X = Math.Max(desired.X, childSize.X)
                    If TypeOf item Is StackPanel Then
                        desired.Y += childSize.Y
                    Else
                        desired.Y = Math.Max(desired.Y, childSize.Y)
                    End If
                Next
                desired += New Vector2(item.Padding.Left + item.Padding.Right, item.Padding.Top + item.Padding.Bottom)
            End If
            desired.X = Number(xml, "Width", desired.X)
            desired.Y = Number(xml, "Height", desired.Y)
            desired = Vector2.Min(desired, New Vector2(Number(xml, "MaxWidth", Single.MaxValue), Number(xml, "MaxHeight", Single.MaxValue)))
            Return desired + New Vector2(item.Margin.Left + item.Margin.Right, item.Margin.Top + item.Margin.Bottom)
        End Function

        Private Function TrackSizes(item As SceneElement, horizontal As Boolean, width As Single, height As Single, stretch As Boolean) As Single()
            Dim xml = nodes(item)
            Dim definitions = xml.SelectNodes(If(horizontal, "*[local-name()='Grid.ColumnDefinitions']/*", "*[local-name()='Grid.RowDefinitions']/*")).Cast(Of XmlElement)().ToArray()
            Dim values(Math.Max(1, definitions.Length) - 1) As Single
            Dim stars(values.Length - 1) As Single
            For i = 0 To values.Length - 1
                Dim spec = If(definitions.Length = 0, "*", definitions(i).GetAttribute(If(horizontal, "Width", "Height")))
                If spec = "Auto" Then
                    For Each child In VisualChildren(item)
                        Dim index = CInt(Number(nodes(child), If(horizontal, "Grid.Column", "Grid.Row"), 0))
                        If index = i Then
                            Dim size = Measure(child, width)
                            values(i) = Math.Max(values(i), If(horizontal, size.X, size.Y))
                        End If
                    Next
                ElseIf spec.EndsWith("*") OrElse spec = "" Then
                    Dim weight As Single = 1
                    If spec.Length > 1 Then Single.TryParse(spec.TrimEnd("*"c), weight)
                    stars(i) = Math.Max(1, weight)
                    If Not stretch Then
                        For Each child In VisualChildren(item)
                            If CInt(Number(nodes(child), If(horizontal, "Grid.Column", "Grid.Row"), 0)) = i Then
                                Dim size = Measure(child, width)
                                values(i) = Math.Max(values(i), If(horizontal, size.X, size.Y))
                            End If
                        Next
                    End If
                Else
                    Single.TryParse(spec, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, values(i))
                End If
            Next
            If stretch AndAlso stars.Sum() > 0 Then
                Dim remaining = Math.Max(0, If(horizontal, width, height) - values.Sum())
                For i = 0 To values.Length - 1
                    values(i) += remaining * stars(i) / stars.Sum()
                Next
            End If
            Return values
        End Function

        Private Sub Arrange(item As SceneElement)
            Dim width = Math.Max(1, item.Size.X - item.Padding.Left - item.Padding.Right)
            Dim height = Math.Max(0, item.Size.Y - item.Padding.Top - item.Padding.Bottom)
            Dim start = item.Position + New Vector2(item.Padding.Left, item.Padding.Top)
            Dim stackOffset As Single
            Dim columns = If(TypeOf item Is Grid, TrackSizes(item, True, width, height, True), {width})
            Dim rows = If(TypeOf item Is Grid, TrackSizes(item, False, width, height, True), {height})
            For Each child In VisualChildren(item)
                Dim xml = nodes(child)
                Dim col = Math.Clamp(CInt(Number(xml, "Grid.Column", 0)), 0, columns.Length - 1)
                Dim row = Math.Clamp(CInt(Number(xml, "Grid.Row", 0)), 0, rows.Length - 1)
                Dim slotWidth = columns.Skip(col).Take(CInt(Number(xml, "Grid.ColumnSpan", 1))).Sum()
                Dim slotHeight = rows.Skip(row).Take(CInt(Number(xml, "Grid.RowSpan", 1))).Sum()
                Dim desired = Measure(child, slotWidth)
                Dim location = start + New Vector2(columns.Take(col).Sum(), rows.Take(row).Sum())
                If TypeOf item Is StackPanel Then
                    location.Y += stackOffset
                    slotHeight = desired.Y
                    stackOffset += slotHeight
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
