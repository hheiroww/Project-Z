Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports System.Collections.Generic

Namespace [Shared].Drawing.UI.Layout

    ''' <summary>
    ''' Orientation for layout panels.
    ''' </summary>
    Public Enum Orientation
        Horizontal
        Vertical
    End Enum

    ''' <summary>
    ''' A stack panel control similar to WPF StackPanel.
    ''' Arranges child elements in a single line (horizontal or vertical).
    ''' </summary>
    <Serializable>
    Public Class StackPanel
        Inherits RectangleElement

#Region "Properties"

        ''' <summary>
        ''' Gets or sets the orientation of the stack panel.
        ''' </summary>
        Public Property Orientation As Orientation
            Get
                Return _Orientation
            End Get
            Set(value As Orientation)
                _Orientation = value
                ArrangeChildren()
            End Set
        End Property
        Private _Orientation As Orientation = Orientation.Vertical

        ''' <summary>
        ''' Gets or sets the spacing between child elements.
        ''' </summary>
        Public Property Spacing As Single
            Get
                Return _Spacing
            End Get
            Set(value As Single)
                _Spacing = value
                ArrangeChildren()
            End Set
        End Property
        Private _Spacing As Single = 4.0F

        ''' <summary>
        ''' Gets or sets whether to automatically resize to fit children.
        ''' </summary>
        Public Property AutoSize As Boolean = False

#End Region

#Region "Events"

        ''' <summary>
        ''' Raised when the layout is updated.
        ''' </summary>
        Public Event LayoutUpdated()

#End Region

#Region "Constructors"

        Public Sub New(Scene As Scene)
            MyBase.New(Scene)
            Init()
        End Sub

        Public Sub New(Scene As Scene, spriteBatch As SpriteBatch)
            MyBase.New(Scene, spriteBatch)
            Init()
        End Sub

        Public Sub New(Scene As Scene, newSpritebatch As Boolean)
            MyBase.New(Scene, newSpritebatch)
            Init()
        End Sub

        Private Sub Init()
            BackgroundColor = Color.Transparent
            AddHandler Children.ChildAdded, AddressOf OnChildAdded
            AddHandler Children.ChildRemoved, AddressOf OnChildRemoved
        End Sub

#End Region

#Region "Methods"

        Private Sub OnChildAdded(c As SceneElement)
            ArrangeChildren()
        End Sub

        Private Sub OnChildRemoved(c As SceneElement)
            ArrangeChildren()
        End Sub

        Private Sub OnLayoutChanged() Handles Me.RectangleChanged
            ArrangeChildren()
        End Sub

        ''' <summary>
        ''' Arranges child elements according to the orientation.
        ''' </summary>
        Public Sub ArrangeChildren()
            Dim currentOffset As Single = 0
            Dim maxCrossSize As Single = 0

            For Each child As SceneElement In Children
                If Not child.isVisible Then Continue For

                If _Orientation = Orientation.Vertical Then
                    child.Position = New Vector2(Position.X + Padding.Left, Position.Y + Padding.Top + currentOffset)
                    currentOffset += child.Size.Y + _Spacing
                    maxCrossSize = Math.Max(maxCrossSize, child.Size.X)
                Else
                    child.Position = New Vector2(Position.X + Padding.Left + currentOffset, Position.Y + Padding.Top)
                    currentOffset += child.Size.X + _Spacing
                    maxCrossSize = Math.Max(maxCrossSize, child.Size.Y)
                End If
            Next

            ' Remove the last spacing
            If currentOffset > _Spacing Then
                currentOffset -= _Spacing
            End If

            ' Auto-size the panel to fit children
            If AutoSize Then
                If _Orientation = Orientation.Vertical Then
                    Size = New Vector2(maxCrossSize + Padding.Left + Padding.Right, 
                                       currentOffset + Padding.Top + Padding.Bottom)
                Else
                    Size = New Vector2(currentOffset + Padding.Left + Padding.Right, 
                                       maxCrossSize + Padding.Top + Padding.Bottom)
                End If
            End If

            RaiseEvent LayoutUpdated()
        End Sub

        Protected Overrides Sub AlignChildren()
            ArrangeChildren()
        End Sub

        ''' <summary>
        ''' Adds a child element to the stack panel.
        ''' </summary>
        Public Sub AddChild(element As SceneElement)
            Children.Add(element)
        End Sub

        ''' <summary>
        ''' Removes a child element from the stack panel.
        ''' </summary>
        Public Sub RemoveChild(element As SceneElement)
            Children.Remove(element)
        End Sub

        ''' <summary>
        ''' Clears all child elements.
        ''' </summary>
        Public Sub ClearChildren()
            Children.Clear()
            ArrangeChildren()
        End Sub

#End Region

    End Class

End Namespace
