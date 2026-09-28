Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.UI.Layout
    ''' <summary>
    ''' Arranges children in rows or columns and wraps at the available extent.
    ''' This is used by file galleries, tool palettes, and thumbnail grids.
    ''' </summary>
    <Serializable>
    Public Class WrapPanel
        Inherits RectangleElement

        Public Property Orientation As Orientation = Orientation.Horizontal
        Public Property ItemWidth As Single
        Public Property ItemHeight As Single
        Public Property HorizontalSpacing As Single = 8.0F
        Public Property VerticalSpacing As Single = 8.0F
        Public Property AutoSizeExtent As Boolean = True

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = Color.Transparent
            AddHandler Children.ChildAdded, AddressOf ChildChanged
            AddHandler Children.ChildRemoved, AddressOf ChildChanged
        End Sub

        Private Sub ChildChanged(child As SceneElement)
            ArrangeChildren()
        End Sub

        Private Sub LayoutChanged() Handles Me.RectangleChanged
            ArrangeChildren()
        End Sub

        Public Sub ArrangeChildren()
            If Size.X <= 0 OrElse Size.Y <= 0 Then Return
            Dim cursorX As Single = Position.X + Padding.Left
            Dim cursorY As Single = Position.Y + Padding.Top
            Dim lineExtent As Single
            Dim maxRight As Single
            Dim maxBottom As Single

            For Each child In Children
                If Not child.isVisible Then Continue For
                Dim width = If(ItemWidth > 0, ItemWidth, child.Size.X)
                Dim height = If(ItemHeight > 0, ItemHeight, child.Size.Y)
                If ItemWidth > 0 OrElse ItemHeight > 0 Then
                    child.Size = New Vector2(If(ItemWidth > 0, ItemWidth, child.Size.X),
                                             If(ItemHeight > 0, ItemHeight, child.Size.Y))
                End If

                If Orientation = Orientation.Horizontal Then
                    If cursorX > Position.X + Padding.Left AndAlso cursorX + width + Padding.Right > Position.X + Size.X Then
                        cursorX = Position.X + Padding.Left
                        cursorY = CSng(cursorY + lineExtent + VerticalSpacing)
                        lineExtent = 0
                    End If
                    child.Position = New Vector2(cursorX, cursorY)
                    cursorX = CSng(cursorX + width + HorizontalSpacing)
                    lineExtent = Math.Max(lineExtent, height)
                Else
                    If cursorY > Position.Y + Padding.Top AndAlso cursorY + height + Padding.Bottom > Position.Y + Size.Y Then
                        cursorY = Position.Y + Padding.Top
                        cursorX = CSng(cursorX + lineExtent + HorizontalSpacing)
                        lineExtent = 0
                    End If
                    child.Position = New Vector2(cursorX, cursorY)
                    cursorY = CSng(cursorY + height + VerticalSpacing)
                    lineExtent = Math.Max(lineExtent, width)
                End If
                maxRight = Math.Max(maxRight, child.Position.X - Position.X + width + Padding.Right)
                maxBottom = Math.Max(maxBottom, child.Position.Y - Position.Y + height + Padding.Bottom)
            Next

            If AutoSizeExtent Then
                If Orientation = Orientation.Horizontal Then
                    Dim desired = New Vector2(Size.X, Math.Max(Size.Y, maxBottom))
                    If Size <> desired Then Size = desired
                Else
                    Dim desired = New Vector2(Math.Max(Size.X, maxRight), Size.Y)
                    If Size <> desired Then Size = desired
                End If
            End If
        End Sub

        Protected Overrides Sub AlignChildren()
            ArrangeChildren()
        End Sub
    End Class
End Namespace
