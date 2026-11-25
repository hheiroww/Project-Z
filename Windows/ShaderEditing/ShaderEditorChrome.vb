Imports System.Globalization
Imports System.IO
Imports System.Xml
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.Designer
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Advanced
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Windows.Composition

Namespace Windows.ShaderEditing
    Friend NotInheritable Class ShaderEditorChrome
        Private Sub New()
        End Sub

        Friend Shared Function LoadView(scene As Scene, name As String, ByRef parser As SceneXamlParser) As SceneElement
            parser = New SceneXamlParser(scene)
            parser.RegisterFactory("DragNumberBox", Function(xml) New DragNumberBox(scene) With {
                .MinimumValue = Number(xml, "Minimum", -10000), .MaximumValue = Number(xml, "Maximum", 10000),
                .StepSize = Number(xml, "Step", 0.01), .Value = Number(xml, "Value", 0)})
            parser.RegisterFactory("RotaryKnob", Function(xml) New RotaryKnob(scene) With {
                .MinimumValue = Number(xml, "Minimum", 0), .MaximumValue = Number(xml, "Maximum", 1),
                .StepSize = Number(xml, "Step", 0.01), .Value = Number(xml, "Value", 0)})
            parser.RegisterFactory("VerticalSlider", Function(xml) New VerticalSlider(scene) With {
                .MinimumValue = Number(xml, "Minimum", 0), .MaximumValue = Number(xml, "Maximum", 1),
                .StepSize = Number(xml, "Step", 0.01), .Value = Number(xml, "Value", 0)})
            parser.RegisterFactory("CollectionScrollBar", Function(xml) New CollectionScrollBar(scene))
            parser.RegisterFactory("AccentButton", Function(xml) New AccentButton(scene) With {
                .Text = xml.GetAttribute("Content"), .AutoSize = ButtonAutoSize.None})
            Using stream = GetType(ShaderEditorChrome).Assembly.GetManifestResourceStream("ProjectZ.ShaderEditing." & name)
                If stream Is Nothing Then Throw New InvalidDataException("Missing shader editor XAML resource: " & name)
                Using reader As New StreamReader(stream)
                    Return parser.Parse(reader.ReadToEnd())
                End Using
            End Using
        End Function

        Private Shared Function Number(xml As XmlElement, name As String, fallback As Double) As Double
            Dim value As Double
            Return If(Double.TryParse(xml.GetAttribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, value), value, fallback)
        End Function

        Friend Shared Function CreateValue(scene As Scene, label As String, value As Double) As DragNumberBox
            Dim low = -10000.0, high = 10000.0, stepSize = 0.01
            Select Case label.ToUpperInvariant()
                Case "R", "G", "B", "A", "RED", "GREEN", "BLUE", "ALPHA", "OPACITY", "INTENSITY", "MIX", "SMOOTHNESS"
                    low = 0 : high = 1
                Case "SCALE", "FREQUENCY", "DENSITY"
                    low = 0.01 : high = 1024
            End Select
            Return New DragNumberBox(scene) With {.MinimumValue = Math.Min(low, value), .MaximumValue = Math.Max(high, value),
                .StepSize = stepSize, .DecimalPlaces = 3, .Value = value}
        End Function

        Friend Shared Sub ApplyTheme(root As SceneElement, theme As WindowAppearanceSettings)
            Dim border = Color.Lerp(theme.SurfaceColor, theme.TextColor, 0.25F)
            Dim hover = Color.Lerp(theme.SurfaceColor, theme.AccentColor, 0.22F)
            If TypeOf root Is CollectionScrollBar Then
                Dim scroll = DirectCast(root, CollectionScrollBar)
                scroll.BackgroundColor = theme.BackgroundColor
                scroll.AccentColor = theme.AccentColor
            ElseIf TypeOf root Is CanvasPanel Then
                Dim panel = DirectCast(root, CanvasPanel)
                If panel.BackgroundColor.A > 0 Then panel.BackgroundColor = theme.SurfaceColor * (panel.BackgroundColor.A / 255.0F)
            ElseIf TypeOf root Is Button Then
                Dim button = DirectCast(root, Button)
                button.BackgroundColor = theme.SurfaceColor
                button.ForegroundColor = theme.TextColor
                button.BorderColor = border
                button.BorderThickness = 1
                button.CornerRadius = 3
                button.MouseOverBackgroundColor = hover
                button.MouseDownBackgroundColor = Color.Lerp(theme.SurfaceColor, theme.AccentColor, 0.35F)
                If TypeOf button Is AccentButton Then DirectCast(button, AccentButton).AccentColor = theme.AccentColor
            ElseIf TypeOf root Is Textbox Then
                Dim box = DirectCast(root, Textbox)
                box.BackgroundColor = theme.BackgroundColor * 0.8F
                box.ForegroundColor = theme.TextColor
                box.BorderColor = border
                box.BorderThickness = 1
                box.CornerRadius = 3
                If TypeOf box Is DragNumberBox Then DirectCast(box, DragNumberBox).AccentColor = theme.AccentColor
            ElseIf TypeOf root Is StudioValueControl Then
                Dim control = DirectCast(root, StudioValueControl)
                control.AccentColor = theme.AccentColor
                control.TrackColor = border
            ElseIf TypeOf root Is CheckBox Then
                Dim box = DirectCast(root, CheckBox)
                box.ForegroundColor = theme.TextColor
                box.CheckColor = theme.AccentColor
                box.BoxBorderColor = border
                box.CheckedBackgroundColor = theme.SurfaceColor
            ElseIf TypeOf root Is TextElement Then
                DirectCast(root, TextElement).ForegroundColor = theme.TextColor
            ElseIf TypeOf root Is RectangleElement Then
                Dim surface = DirectCast(root, RectangleElement)
                If surface.BackgroundColor.A > 0 Then surface.BackgroundColor = theme.SurfaceColor * (surface.BackgroundColor.A / 255.0F)
                If surface.BorderThickness > 0 Then surface.BorderColor = border
            End If
            For Each child In root.Children
                ApplyTheme(child, theme)
            Next
        End Sub

        Friend Shared Sub DrawCommandSymbol(scene As Scene, button As Button, command As String, ink As Color)
            Dim paths As Vector2()()
            Select Case command
                Case "New"
                    paths = {New Vector2() {New Vector2(8, 3), New Vector2(8, 13)}, New Vector2() {New Vector2(3, 8), New Vector2(13, 8)}}
                Case "Open"
                    paths = {New Vector2() {New Vector2(2, 13), New Vector2(2, 4), New Vector2(7, 4), New Vector2(9, 6), New Vector2(14, 6), New Vector2(14, 13), New Vector2(2, 13)}}
                Case "Save", "ExportSource"
                    paths = {New Vector2() {New Vector2(3, 10), New Vector2(3, 14), New Vector2(13, 14), New Vector2(13, 10)},
                        New Vector2() {New Vector2(8, 2), New Vector2(8, 10), New Vector2(5, 7)}, New Vector2() {New Vector2(8, 10), New Vector2(11, 7)}}
                Case Else
                    Dim flip = command = "Redo"
                    Dim stem As Vector2() = {New Vector2(4, 7), New Vector2(10, 7), New Vector2(13, 10), New Vector2(13, 13)}
                    Dim head As Vector2() = {New Vector2(7, 4), New Vector2(4, 7), New Vector2(7, 10)}
                    paths = {stem, head}
                    If flip Then paths = paths.Select(Function(path) path.Select(Function(point) New Vector2(16 - point.X, point.Y)).ToArray()).ToArray()
            End Select
            For Each path In paths
                For index = 1 To path.Length - 1
                    Dim a = path(index - 1) + New Vector2(8, 7)
                    Dim b = path(index) + New Vector2(8, 7)
                    Dim delta = b - a
                    Dim perpendicular = New Vector2(-delta.Y, delta.X) / delta.Length()
                    button.Children.Add(New PolygonElement(scene, {a + perpendicular, b + perpendicular, b - perpendicular, a - perpendicular}) With {
                        .Size = button.Size, .FillColor = ink, .isMouseBypassEnabled = True})
                Next
            Next
        End Sub
    End Class
End Namespace
