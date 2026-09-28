Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports System.Collections.Generic

Namespace [Shared].Drawing.UI.Layout

    ''' <summary>
    ''' Represents a row or column definition for a Grid panel.
    ''' </summary>
    <Serializable>
    Public Class GridDefinition
        ''' <summary>
        ''' Gets or sets the size of this row/column.
        ''' Use -1 for Auto (fit to content), 0 for Star (proportional), positive for fixed pixels.
        ''' </summary>
        Public Property Size As Single = 0 ' Star by default

        ''' <summary>
        ''' Gets or sets the star multiplier when Size = 0 (Star mode).
        ''' </summary>
        Public Property Star As Single = 1.0F

        ''' <summary>
        ''' Gets or sets the minimum size.
        ''' </summary>
        Public Property MinSize As Single = 0

        ''' <summary>
        ''' Gets or sets the maximum size.
        ''' </summary>
        Public Property MaxSize As Single = Single.MaxValue

        ''' <summary>
        ''' The calculated actual size after layout.
        ''' </summary>
        Friend ActualSize As Single = 0

        ''' <summary>
        ''' Creates an Auto-sized definition.
        ''' </summary>
        Public Shared Function Auto() As GridDefinition
            Return New GridDefinition With {.Size = -1}
        End Function

        ''' <summary>
        ''' Creates a Star-sized definition.
        ''' </summary>
        Public Shared Function StarSize(Optional multiplier As Single = 1.0F) As GridDefinition
            Return New GridDefinition With {.Size = 0, .Star = multiplier}
        End Function

        ''' <summary>
        ''' Creates a fixed-sized definition.
        ''' </summary>
        Public Shared Function Fixed(pixels As Single) As GridDefinition
            Return New GridDefinition With {.Size = pixels}
        End Function
    End Class

    ''' <summary>
    ''' Attached properties for Grid children.
    ''' </summary>
    <Serializable>
    Public Class GridAttached
        Public Property Row As Integer = 0
        Public Property Column As Integer = 0
        Public Property RowSpan As Integer = 1
        Public Property ColumnSpan As Integer = 1
        Public Property WidthExplicit As Boolean
        Public Property HeightExplicit As Boolean
        Public Property DesiredWidth As Single
        Public Property DesiredHeight As Single
    End Class

    ''' <summary>
    ''' A grid panel control similar to WPF Grid.
    ''' Arranges child elements in rows and columns.
    ''' </summary>
    <Serializable>
    Public Class Grid
        Inherits RectangleElement

#Region "Properties"

        ''' <summary>
        ''' Gets the row definitions.
        ''' </summary>
        Public ReadOnly Property RowDefinitions As List(Of GridDefinition)
            Get
                Return _RowDefinitions
            End Get
        End Property
        Private ReadOnly _RowDefinitions As New List(Of GridDefinition)

        ''' <summary>
        ''' Gets the column definitions.
        ''' </summary>
        Public ReadOnly Property ColumnDefinitions As List(Of GridDefinition)
            Get
                Return _ColumnDefinitions
            End Get
        End Property
        Private ReadOnly _ColumnDefinitions As New List(Of GridDefinition)

        ''' <summary>
        ''' Gets or sets whether to show grid lines (for debugging).
        ''' </summary>
        Public Property ShowGridLines As Boolean = False

        ''' <summary>
        ''' Gets or sets the grid line color.
        ''' </summary>
        Public Property GridLineColor As Color = New Color(100, 100, 100)

        ''' <summary>
        ''' Attached properties for each child.
        ''' </summary>
        Private ReadOnly _attachedProperties As New Dictionary(Of SceneElement, GridAttached)

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

#Region "Attached Property Methods"

        ''' <summary>
        ''' Sets the row for a child element.
        ''' </summary>
        Public Sub SetRow(element As SceneElement, row As Integer)
            EnsureAttached(element).Row = row
            ArrangeChildren()
        End Sub

        ''' <summary>
        ''' Gets the row for a child element.
        ''' </summary>
        Public Function GetRow(element As SceneElement) As Integer
            Return GetAttached(element).Row
        End Function

        ''' <summary>
        ''' Sets the column for a child element.
        ''' </summary>
        Public Sub SetColumn(element As SceneElement, column As Integer)
            EnsureAttached(element).Column = column
            ArrangeChildren()
        End Sub

        ''' <summary>
        ''' Gets the column for a child element.
        ''' </summary>
        Public Function GetColumn(element As SceneElement) As Integer
            Return GetAttached(element).Column
        End Function

        ''' <summary>
        ''' Sets the row span for a child element.
        ''' </summary>
        Public Sub SetRowSpan(element As SceneElement, rowSpan As Integer)
            EnsureAttached(element).RowSpan = Math.Max(1, rowSpan)
            ArrangeChildren()
        End Sub

        ''' <summary>
        ''' Gets the row span for a child element.
        ''' </summary>
        Public Function GetRowSpan(element As SceneElement) As Integer
            Return GetAttached(element).RowSpan
        End Function

        ''' <summary>
        ''' Sets the column span for a child element.
        ''' </summary>
        Public Sub SetColumnSpan(element As SceneElement, columnSpan As Integer)
            EnsureAttached(element).ColumnSpan = Math.Max(1, columnSpan)
            ArrangeChildren()
        End Sub

        ''' <summary>
        ''' Gets the column span for a child element.
        ''' </summary>
        Public Function GetColumnSpan(element As SceneElement) As Integer
            Return GetAttached(element).ColumnSpan
        End Function

        Private Function EnsureAttached(element As SceneElement) As GridAttached
            If Not _attachedProperties.ContainsKey(element) Then
                _attachedProperties(element) = New GridAttached()
            End If
            Return _attachedProperties(element)
        End Function

        Private Function GetAttached(element As SceneElement) As GridAttached
            If _attachedProperties.ContainsKey(element) Then
                Return _attachedProperties(element)
            End If
            Return New GridAttached()
        End Function

#End Region

#Region "Methods"

        Private Sub OnChildAdded(c As SceneElement)
            EnsureAttached(c)
            ArrangeChildren()
        End Sub

        Private Sub OnChildRemoved(c As SceneElement)
            _attachedProperties.Remove(c)
            ArrangeChildren()
        End Sub

        Private Sub OnLayoutChanged() Handles Me.RectangleChanged
            ArrangeChildren()
        End Sub

        ''' <summary>
        ''' Adds a child element at the specified grid position.
        ''' </summary>
        Public Sub AddChild(element As SceneElement, row As Integer, column As Integer,
                           Optional rowSpan As Integer = 1, Optional columnSpan As Integer = 1,
                           Optional widthExplicit As Boolean = False, Optional heightExplicit As Boolean = False)
            Dim attached = EnsureAttached(element)
            attached.Row = row
            attached.Column = column
            attached.RowSpan = rowSpan
            attached.ColumnSpan = columnSpan
            attached.WidthExplicit = widthExplicit
            attached.HeightExplicit = heightExplicit
            attached.DesiredWidth = element.Size.X
            attached.DesiredHeight = element.Size.Y
            Children.Add(element)
        End Sub

        Protected Overrides Sub AlignChildren()
            ArrangeChildren()
        End Sub

        ''' <summary>
        ''' Arranges child elements in the grid.
        ''' </summary>
        Public Sub ArrangeChildren()
            ' The legacy adapter owns imported grid layout. Running the native
            ' grid algorithm from RectangleChanged fights that arrangement and
            ' remeasures every descendant on each scroll translation.
            If ImportedLayout IsNot Nothing Then Return
            ' Ensure at least one row and column
            If _RowDefinitions.Count = 0 Then
                _RowDefinitions.Add(GridDefinition.StarSize(1))
            End If
            If _ColumnDefinitions.Count = 0 Then
                _ColumnDefinitions.Add(GridDefinition.StarSize(1))
            End If

            ' Calculate row heights
            CalculateSizes(_RowDefinitions, Size.Y - Padding.Top - Padding.Bottom)
            
            ' Calculate column widths
            CalculateSizes(_ColumnDefinitions, Size.X - Padding.Left - Padding.Right)

            ' Position children
            For Each child As SceneElement In Children
                If Not child.isVisible Then Continue For

                Dim attached = GetAttached(child)
                Dim row = Math.Min(attached.Row, _RowDefinitions.Count - 1)
                Dim col = Math.Min(attached.Column, _ColumnDefinitions.Count - 1)
                Dim rowSpan = Math.Min(attached.RowSpan, _RowDefinitions.Count - row)
                Dim colSpan = Math.Min(attached.ColumnSpan, _ColumnDefinitions.Count - col)

                ' Calculate position
                Dim x As Single = Position.X + Padding.Left
                For i = 0 To col - 1
                    x += _ColumnDefinitions(i).ActualSize
                Next

                Dim y As Single = Position.Y + Padding.Top
                For i = 0 To row - 1
                    y += _RowDefinitions(i).ActualSize
                Next

                ' Calculate size based on span
                Dim width As Single = 0
                For i = col To col + colSpan - 1
                    width += _ColumnDefinitions(i).ActualSize
                Next

                Dim height As Single = 0
                For i = row To row + rowSpan - 1
                    height += _RowDefinitions(i).ActualSize
                Next

                Dim preserveDesiredWidth = attached.WidthExplicit OrElse
                                           child.HorizontalAlign = HorizontalAlignment.Right OrElse
                                           child.HorizontalAlign = HorizontalAlignment.Center
                Dim preserveDesiredHeight = attached.HeightExplicit OrElse
                                            child.VerticalAlign = VerticalAlignment.Bottom OrElse
                                            child.VerticalAlign = VerticalAlignment.Center
                Dim childWidth = If(preserveDesiredWidth,
                                    attached.DesiredWidth,
                                    Math.Max(0.0F, width - child.Margin.Left - child.Margin.Right))
                Dim childHeight = If(preserveDesiredHeight,
                                     attached.DesiredHeight,
                                     Math.Max(0.0F, height - child.Margin.Top - child.Margin.Bottom))

                Dim childX As Single
                Select Case child.HorizontalAlign
                    Case HorizontalAlignment.Right
                        childX = x + width - child.Margin.Right - childWidth
                    Case HorizontalAlignment.Center
                        childX = x + (width - childWidth + child.Margin.Left - child.Margin.Right) / 2.0F
                    Case Else
                        childX = x + child.Margin.Left
                End Select

                Dim childY As Single
                Select Case child.VerticalAlign
                    Case VerticalAlignment.Bottom
                        childY = y + height - child.Margin.Bottom - childHeight
                    Case VerticalAlignment.Center
                        childY = y + (height - childHeight + child.Margin.Top - child.Margin.Bottom) / 2.0F
                    Case Else
                        childY = y + child.Margin.Top
                End Select

                child.Position = New Vector2(childX, childY)
                child.Size = New Vector2(childWidth, childHeight)
            Next

            RaiseEvent LayoutUpdated()
        End Sub

        Private Sub CalculateSizes(definitions As List(Of GridDefinition), availableSize As Single)
            Dim fixedTotal As Single = 0
            Dim starTotal As Single = 0

            ' First pass: calculate fixed and auto sizes
            For Each def In definitions
                If def.Size > 0 Then
                    ' Fixed size
                    def.ActualSize = Math.Min(def.MaxSize, Math.Max(def.MinSize, def.Size))
                    fixedTotal += def.ActualSize
                ElseIf def.Size < 0 Then
                    ' Auto size - for now, treat as minimum size
                    def.ActualSize = def.MinSize
                    fixedTotal += def.ActualSize
                Else
                    ' Star size
                    starTotal += def.Star
                End If
            Next

            ' Second pass: distribute remaining space to star sizes
            Dim remaining As Single = availableSize - fixedTotal
            If remaining > 0 AndAlso starTotal > 0 Then
                Dim starUnit As Single = remaining / starTotal
                For Each def In definitions
                    If def.Size = 0 Then
                        def.ActualSize = Math.Min(def.MaxSize, Math.Max(def.MinSize, starUnit * def.Star))
                    End If
                Next
            End If
        End Sub

#End Region

    End Class

End Namespace
