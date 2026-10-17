Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports System.Collections.Generic

Namespace [Shared].Drawing.UI.Layout
    Public Enum GridSizeMode
        Auto
        Pixel
        Star
    End Enum

    ''' <summary>
    ''' Represents a row or column definition for a Grid panel.
    ''' </summary>
    <Serializable>
    Public Class GridDefinition
        ' When unset, retain the legacy Size convention used by existing native applications.
        Public Property SizeMode As GridSizeMode?
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
        Public Property ActualSize As Single = 0

        ''' <summary>
        ''' Creates an Auto-sized definition.
        ''' </summary>
        Public Shared Function Auto() As GridDefinition
            Return New GridDefinition With {.Size = -1, .SizeMode = GridSizeMode.Auto}
        End Function

        ''' <summary>
        ''' Creates a Star-sized definition.
        ''' </summary>
        Public Shared Function StarSize(Optional multiplier As Single = 1.0F) As GridDefinition
            Return New GridDefinition With {.Size = 0, .Star = multiplier, .SizeMode = GridSizeMode.Star}
        End Function

        ''' <summary>
        ''' Creates a fixed-sized definition.
        ''' </summary>
        Public Shared Function Fixed(pixels As Single) As GridDefinition
            Return New GridDefinition With {.Size = pixels, .SizeMode = GridSizeMode.Pixel}
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
            ConfigureChild(element, row, column, rowSpan, columnSpan, widthExplicit, heightExplicit)
            Children.Add(element)
        End Sub

        Public Sub ConfigureChild(element As SceneElement, row As Integer, column As Integer,
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
            Dim rows = If(_RowDefinitions.Count = 0, New List(Of GridDefinition) From {GridDefinition.StarSize()}, _RowDefinitions)
            Dim columns = If(_ColumnDefinitions.Count = 0, New List(Of GridDefinition) From {GridDefinition.StarSize()}, _ColumnDefinitions)

            ' Calculate row heights
            ComputeTrackSizes(rows, Size.Y - Padding.Top - Padding.Bottom, DesiredTracks(rows.Count, False), True)
            
            ' Calculate column widths
            ComputeTrackSizes(columns, Size.X - Padding.Left - Padding.Right, DesiredTracks(columns.Count, True), True)

            ' Position children
            For Each child As SceneElement In Children
                If Not child.isVisible Then Continue For

                Dim attached = GetAttached(child)
                Dim row = Math.Clamp(attached.Row, 0, rows.Count - 1)
                Dim col = Math.Clamp(attached.Column, 0, columns.Count - 1)
                Dim rowSpan = Math.Clamp(attached.RowSpan, 1, rows.Count - row)
                Dim colSpan = Math.Clamp(attached.ColumnSpan, 1, columns.Count - col)

                ' Calculate position
                Dim x As Single = Position.X + Padding.Left
                For i = 0 To col - 1
                    x += columns(i).ActualSize
                Next

                Dim y As Single = Position.Y + Padding.Top
                For i = 0 To row - 1
                    y += rows(i).ActualSize
                Next

                ' Calculate size based on span
                Dim width As Single = 0
                For i = col To col + colSpan - 1
                    width += columns(i).ActualSize
                Next

                Dim height As Single = 0
                For i = row To row + rowSpan - 1
                    height += rows(i).ActualSize
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

        Private Function DesiredTracks(count As Integer, horizontal As Boolean) As Single()
            Dim desired(count - 1) As Single
            For Each child In Children
                If Not child.isVisible Then Continue For
                Dim attached = GetAttached(child)
                Dim index = Math.Clamp(If(horizontal, attached.Column, attached.Row), 0, count - 1)
                Dim span = If(horizontal, attached.ColumnSpan, attached.RowSpan)
                If span <> 1 Then Continue For
                Dim size = If(horizontal, attached.DesiredWidth, attached.DesiredHeight)
                If TypeOf child Is TextElement Then
                    Dim measured = DirectCast(child, TextElement).MeasureXamlText(Math.Max(1, Me.Size.X))
                    size = Math.Max(size, If(horizontal, measured.X, measured.Y))
                End If
                desired(index) = Math.Max(desired(index), size + If(horizontal, child.Margin.Left + child.Margin.Right, child.Margin.Top + child.Margin.Bottom))
            Next
            Return desired
        End Function

        ''' <summary>Shared native and imported grid sizing, with constrained star redistribution.</summary>
        Public Shared Function ComputeTrackSizes(definitions As IList(Of GridDefinition), available As Single, desired As Single(), stretch As Boolean) As Single()
            Dim values(definitions.Count - 1) As Single
            Dim active As New List(Of Integer)
            For i = 0 To definitions.Count - 1
                Dim definition = definitions(i)
                Dim mode = definition.SizeMode.GetValueOrDefault(If(definition.Size < 0, GridSizeMode.Auto, If(definition.Size = 0, GridSizeMode.Star, GridSizeMode.Pixel)))
                Dim maximum = Math.Max(definition.MinSize, definition.MaxSize)
                If mode = GridSizeMode.Star AndAlso stretch AndAlso definition.Star > 0 Then
                    active.Add(i)
                Else
                    Dim requested = If(mode = GridSizeMode.Pixel, definition.Size, If(mode = GridSizeMode.Star AndAlso stretch, 0, desired(i)))
                    values(i) = Math.Clamp(requested, definition.MinSize, maximum)
                End If
            Next
            If active.Count > 0 Then
                Dim remaining = Math.Max(0, available - values.Sum())
                Dim lower As Double = 0
                Dim upper As Double = active.Max(Function(i) CDbl(remaining) / definitions(i).Star)
                ' Solve sum(clamp(unit * weight, min, max)) = remaining. Simultaneously
                ' clamping both extremes can otherwise overallocate a constrained grid.
                For iteration = 0 To 47
                    Dim unit = (lower + upper) / 2
                    Dim total = active.Sum(Function(i) Math.Clamp(unit * definitions(i).Star, CDbl(definitions(i).MinSize), CDbl(Math.Max(definitions(i).MinSize, definitions(i).MaxSize))))
                    If total > remaining Then
                        upper = unit
                    Else
                        lower = unit
                    End If
                Next
                For Each i In active
                    values(i) = CSng(Math.Clamp(lower * definitions(i).Star, CDbl(definitions(i).MinSize), CDbl(Math.Max(definitions(i).MinSize, definitions(i).MaxSize))))
                Next
            End If
            For i = 0 To definitions.Count - 1
                definitions(i).ActualSize = values(i)
            Next
            Return values
        End Function

#End Region

    End Class

End Namespace
