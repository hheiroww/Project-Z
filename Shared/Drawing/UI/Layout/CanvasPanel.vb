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

        Private ReadOnly offsets As New Dictionary(Of SceneElement, Vector2)

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = Color.Transparent
            AddHandler Children.ChildAdded, Sub(child)
                                                If Not offsets.ContainsKey(child) Then offsets(child) = child.Position
                                            End Sub
            AddHandler Children.ChildRemoved, Sub(child) offsets.Remove(child)
        End Sub

        Public Sub SetChildPosition(child As SceneElement, offset As Vector2)
            offsets(child) = offset
            Invalidate()
        End Sub

        Protected Overrides Sub AlignChildren()
            For Each child In Children
                Dim offset As Vector2
                If Not offsets.TryGetValue(child, offset) Then offset = Vector2.Zero
                child.Position = Position + offset
            Next
        End Sub
    End Class
End Namespace
