Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.UI.Layout
    ''' <summary>
    ''' Absolute-position panel equivalent to WPF Canvas. Child coordinates are
    ''' relative to the panel and are not changed by layout.
    ''' </summary>
    <Serializable>
    Public Class CanvasPanel
        Inherits RectangleElement

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = Color.Transparent
        End Sub
    End Class
End Namespace
