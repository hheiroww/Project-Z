Imports Microsoft.Xna.Framework
Imports ProjectZ.ShaderEditor
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Windows.Composition

Namespace Windows.ShaderEditing
    Friend NotInheritable Class SimpleMaterialView
        Private ReadOnly controls As New List(Of MaterialRow)
        Private document As ShaderDocument
        Private refreshing As Boolean

        Public ReadOnly Property Root As CanvasPanel
        Public Event GraphChanged As EventHandler

        Public Sub New(scene As Scene)
            Root = New CanvasPanel(scene)
            Root.Children.Add(New TextElement(scene) With {.Position = New Vector2(20, 14), .Size = New Vector2(340, 23),
                .Text = "Material channels", .UseXamlTextLayout = True})
            For Each channel In SurfaceMaterials.Channels
                Dim y As Single = 46 + controls.Count * 39
                Root.Children.Add(New TextElement(scene) With {.Position = New Vector2(20, y + 6), .Size = New Vector2(175, 26),
                    .Text = channel.Label, .UseXamlTextLayout = True})
                Dim status As New TextElement(scene) With {.Position = New Vector2(202, y + 6), .Size = New Vector2(340, 24),
                    .Text = "Driven by Advanced graph", .UseXamlTextLayout = True}
                Root.Children.Add(status)
                Dim fields As New List(Of DragNumberBox)
                Dim count = If(channel.Type = ShaderValueType.Float, 1, 3)
                For index = 0 To count - 1
                    Dim component = index
                    Dim field As New DragNumberBox(scene) With {
                        .Position = New Vector2(202 + component * 125, y), .Size = New Vector2(112, 29),
                        .MinimumValue = channel.Minimum, .MaximumValue = channel.Maximum, .StepSize = channel.Step,
                        .DecimalPlaces = 3, .ShowSpinButtons = False}
                    AddHandler field.ValueChanged, Sub(value) ChangeValue(channel, component, value)
                    fields.Add(field)
                    Root.Children.Add(field)
                Next
                controls.Add(New MaterialRow(channel, status, fields.ToArray()))
            Next
        End Sub

        Public Sub SetDocument(value As ShaderDocument)
            document = value
            Refresh()
        End Sub

        Public Sub Refresh()
            If document?.IsSurface <> True Then Return
            refreshing = True
            Try
                For Each row In controls
                    Dim node = SurfaceMaterials.FindConstant(document, row.Channel)
                    row.Status.isVisible = node Is Nothing
                    For index = 0 To row.Fields.Length - 1
                        row.Fields(index).isVisible = node IsNot Nothing
                        If node IsNot Nothing Then row.Fields(index).Value = node.Values(index)
                    Next
                Next
            Finally
                refreshing = False
            End Try
        End Sub

        Public Sub ApplyAppearance(settings As WindowAppearanceSettings)
            ShaderEditorChrome.ApplyTheme(Root, settings)
        End Sub

        Private Sub ChangeValue(channel As SurfaceMaterialChannel, component As Integer, value As Double)
            If refreshing OrElse document?.IsSurface <> True Then Return
            Dim node = SurfaceMaterials.FindConstant(document, channel)
            If node Is Nothing OrElse node.Values(component) = value Then Return
            node.Values(component) = value
            RaiseEvent GraphChanged(Me, EventArgs.Empty)
        End Sub

        Private NotInheritable Class MaterialRow
            Public ReadOnly Channel As SurfaceMaterialChannel
            Public ReadOnly Status As TextElement
            Public ReadOnly Fields As DragNumberBox()

            Public Sub New(channel As SurfaceMaterialChannel, status As TextElement, fields As DragNumberBox())
                Me.Channel = channel
                Me.Status = status
                Me.Fields = fields
            End Sub
        End Class
    End Class
End Namespace
