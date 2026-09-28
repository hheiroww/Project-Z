Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.UI.Layout
    <Serializable>
    Public Class UniformGrid
        Inherits RectangleElement
        Public Property Rows As Integer
        Public Property Columns As Integer
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = Color.Transparent
        End Sub
        Protected Overrides Sub AlignChildren()
            Dim count = Children.Count
            If count = 0 Then Return
            Dim columnsToUse = If(Columns > 0, Columns, CInt(Math.Ceiling(Math.Sqrt(count))))
            Dim rowsToUse = If(Rows > 0, Rows, CInt(Math.Ceiling(count / CDbl(columnsToUse))))
            Dim cellWidth = Size.X / Math.Max(1, columnsToUse)
            Dim cellHeight = Size.Y / Math.Max(1, rowsToUse)
            For index = 0 To count - 1
                Dim column = index Mod columnsToUse
                Dim row = index \ columnsToUse
                Children(index).Position = New Vector2(Position.X + column * cellWidth, Position.Y + row * cellHeight)
                Children(index).Size = New Vector2(cellWidth, cellHeight)
            Next
        End Sub
    End Class

    Public Enum Dock
        Left
        Top
        Right
        Bottom
    End Enum

    <Serializable>
    Public Class DockPanel
        Inherits RectangleElement
        Private ReadOnly docks As New Dictionary(Of SceneElement, Dock)()
        Public Property LastChildFill As Boolean = True
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = Color.Transparent
        End Sub
        Public Sub AddChild(element As SceneElement, dock As Dock)
            docks(element) = dock
            Children.Add(element)
        End Sub
        Protected Overrides Sub AlignChildren()
            Dim left = Position.X + Padding.Left
            Dim top = Position.Y + Padding.Top
            Dim right = Position.X + Size.X - Padding.Right
            Dim bottom = Position.Y + Size.Y - Padding.Bottom
            For index = 0 To Children.Count - 1
                Dim child = Children(index)
                If LastChildFill AndAlso index = Children.Count - 1 Then
                    child.Position = New Vector2(left, top)
                    child.Size = New Vector2(Math.Max(0, right - left), Math.Max(0, bottom - top))
                    Exit For
                End If
                Dim dockSide As Dock = If(docks.ContainsKey(child), docks(child), Dock.Left)
                Select Case dockSide
                    Case Dock.Left
                        child.Position = New Vector2(left, top) : child.Size = New Vector2(child.Size.X, bottom - top) : left += child.Size.X
                    Case Dock.Right
                        right -= child.Size.X : child.Position = New Vector2(right, top) : child.Size = New Vector2(child.Size.X, bottom - top)
                    Case Dock.Top
                        child.Position = New Vector2(left, top) : child.Size = New Vector2(right - left, child.Size.Y) : top += child.Size.Y
                    Case Dock.Bottom
                        bottom -= child.Size.Y : child.Position = New Vector2(left, bottom) : child.Size = New Vector2(right - left, child.Size.Y)
                End Select
            Next
        End Sub
    End Class
End Namespace
