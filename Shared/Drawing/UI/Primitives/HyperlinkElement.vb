Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].Drawing.UI.Primitives
    Public Class HyperlinkElement
        Inherits TextElement
        Public Shared Property DefaultLinkColor As Color = New Color(70, 155, 255)
        Public Property LinkColor As Color = DefaultLinkColor
        Public Shadows Property Underline As Boolean = True
        Private pixel As Texture2D
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            isMouseBypassEnabled = False
            TextWrapping = TextWrapping.WrapWithOverflow
        End Sub
        Protected Friend Overrides Sub Draw(time As GameTime)
            ForegroundColor = LinkColor
            MyBase.Draw(time)
            If Not Underline Then Return
            If pixel Is Nothing Then
                pixel = New Texture2D(Scene.graphicsDevice, 1, 1)
                pixel.SetData({Color.White})
            End If
            Dim y = Position.Y
            For Each line In If(WrappedText, Text).Replace(vbCr, "").Split(ChrW(10))
                Dim measured = Scene.MeasureText(Font, line)
                spriteBatch.Draw(pixel, New Rectangle(CInt(Position.X), CInt(y + Scene.TextLineSpacing(Font) - 2), CInt(measured.X), 1), ApplyOpacity(LinkColor))
                y += Scene.TextLineSpacing(Font)
            Next
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then pixel?.Dispose()
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
