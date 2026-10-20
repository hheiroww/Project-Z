Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.UI.Input
    ''' <summary>A reusable item viewport scrollbar. Count and visible items determine thumb size; offset is row-based.</summary>
    Public Class CollectionScrollBar
        Inherits RectangleElement
        Public Property ItemCount As Integer
        Public Property VisibleItems As Integer = 1
        Public Property AccentColor As Color = New Color(255, 155, 66)
        Private offset As Integer
        Public Event OffsetChanged(firstIndex As Integer)
        Public Property FirstIndex As Integer
            Get
                Return offset
            End Get
            Set(value As Integer)
                Dim bounded = Math.Clamp(value, 0, Math.Max(0, ItemCount - VisibleItems))
                If bounded = offset Then Return
                offset = bounded : Scene.RequestRender() : RaiseEvent OffsetChanged(offset)
            End Set
        End Property
        Public Sub New(scene As Scene)
            MyBase.New(scene) : Size = New Vector2(8, 200) : BackgroundColor = New Color(41, 44, 48)
            AddHandler MouseWheel, Sub(delta, p) Scroll(delta)
            AddHandler MouseLeftDown, Sub(p) SetPosition(Position.Y + p.Y)
            AddHandler scene.OnMouseMove, AddressOf Drag
        End Sub
        Public Sub Scroll(delta As Integer)
            If isEnabled AndAlso delta <> 0 Then FirstIndex -= Math.Sign(delta)
        End Sub
        Private Sub SetPosition(y As Single)
            Dim thumb = ThumbHeight(), span = Math.Max(1, Size.Y - thumb)
            FirstIndex = CInt(Math.Round(Math.Clamp((y - Position.Y - thumb / 2) / span, 0, 1) * Math.Max(0, ItemCount - VisibleItems)))
        End Sub
        Private Sub Drag(current As Point, previous As Point)
            If isMouseDown AndAlso isEnabled Then SetPosition(current.Y)
        End Sub
        Private Function ThumbHeight() As Single
            Return CSng(Math.Min(Size.Y, Math.Max(24, Size.Y * Math.Min(1, VisibleItems / CDbl(Math.Max(1, ItemCount))))))
        End Function
        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            MyBase.Draw(gameTime)
            Dim height = ThumbHeight(), maximum = Math.Max(0, ItemCount - VisibleItems)
            Dim top = Position.Y + If(maximum > 0, offset / CDbl(maximum), 0) * (Size.Y - height)
            spriteBatch.Draw(Texture, New Rectangle(CInt(Position.X), CInt(top), CInt(Size.X), CInt(height)), ApplyOpacity(AccentColor))
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then RemoveHandler Scene.OnMouseMove, AddressOf Drag
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
