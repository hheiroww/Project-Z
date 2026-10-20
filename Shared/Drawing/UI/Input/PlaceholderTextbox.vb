Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.UI.Input
    ''' <summary>Native text input with a noninteractive placeholder child; no overlapping scene root or focus target.</summary>
    Public Class PlaceholderTextbox
        Inherits Textbox
        Private ReadOnly hint As TextElement
        Public Property PlaceholderText As String = ""
        Public Property PlaceholderColor As Color = New Color(150, 158, 169)
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            hint = New TextElement(scene) With {.isMouseBypassEnabled = True, .CanSelect = False, .TextWrapping = TextWrapping.NoWrap}
            Children.Add(hint)
            AddHandler PreDraw, Sub(time)
                                    hint.Text = PlaceholderText : hint.Font = Font : hint.ForegroundColor = PlaceholderColor
                                    hint.isVisible = Text.Length = 0 AndAlso PlaceholderText.Length > 0
                                    hint.Position = Position + TextPadding
                                    hint.MaxWidth = Math.Max(1, Size.X - TextPadding.X * 2)
                                    hint.Size = New Vector2(hint.MaxWidth, Math.Max(1, Size.Y - TextPadding.Y * 2))
                                End Sub
        End Sub
    End Class
End Namespace
