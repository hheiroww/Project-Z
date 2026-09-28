Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports System.Collections.Generic

Namespace [Shared].Drawing.UI.Input

    ''' <summary>
    ''' A ComboBox control that displays a dropdown list of items.
    ''' </summary>
    Public Class ComboBox
        Inherits RectangleElement

        Private _items As New List(Of String)
        Private _selectedIndex As Integer = -1
        Private _isDropDownOpen As Boolean = False
        Private _dropDownItems As New List(Of RectangleElement)
        Private _headerText As TextElement
        Private _dropDownButton As TextElement
        Private _itemHeight As Single = 28.0F
        Private ReadOnly ownerScene As Scene

        Public Property DismissDistance As Single = 28.0F

        Public Event SelectionChanged(sender As ComboBox, selectedIndex As Integer)

        Public Property Items As List(Of String)
            Get
                Return _items
            End Get
            Set(value As List(Of String))
                _items = value
                UpdateVisual()
            End Set
        End Property

        Public Property SelectedIndex As Integer
            Get
                Return _selectedIndex
            End Get
            Set(value As Integer)
                If value >= -1 AndAlso value < _items.Count Then
                    _selectedIndex = value
                    UpdateVisual()
                    RaiseEvent SelectionChanged(Me, _selectedIndex)
                End If
            End Set
        End Property

        Public ReadOnly Property SelectedItem As String
            Get
                If _selectedIndex >= 0 AndAlso _selectedIndex < _items.Count Then
                    Return _items(_selectedIndex)
                End If
                Return Nothing
            End Get
        End Property

        Public Property IsDropDownOpen As Boolean
            Get
                Return _isDropDownOpen
            End Get
            Set(value As Boolean)
                _isDropDownOpen = value
                UpdateDropDown()
            End Set
        End Property

        Public Property ItemHeight As Single
            Get
                Return _itemHeight
            End Get
            Set(value As Single)
                _itemHeight = value
                UpdateVisual()
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            ownerScene = scene
            BackgroundColor = New Color(45, 45, 45)
            Size = New Vector2(180, 32)

            _headerText = New TextElement(scene) With {
                .Text = "",
                .ForegroundColor = Color.White,
                .isMouseBypassEnabled = True
            }
            Children.Add(_headerText)

            _dropDownButton = New TextElement(scene) With {
                .Text = "v",
                .ForegroundColor = Color.White,
                .isMouseBypassEnabled = True
            }
            Children.Add(_dropDownButton)
            AddHandler MouseLeftClick, Sub(point) IsDropDownOpen = Not IsDropDownOpen
            If ownerScene IsNot Nothing Then
                AddHandler ownerScene.OnMouseMove, AddressOf HandleSceneMouseMove
                AddHandler ownerScene.OnMouseLeftDown, AddressOf HandleSceneMouseLeftDown
                AddHandler ownerScene.OnPointerCancelled, AddressOf HandlePointerCancelled
            End If
        End Sub

        Private Function ContainsDropDownWithMargin(point As Point, margin As Single) As Boolean
            Dim bottom = Position.Y + Size.Y
            If _isDropDownOpen Then bottom += _items.Count * _itemHeight
            Return point.X >= Position.X - margin AndAlso
                   point.Y >= Position.Y - margin AndAlso
                   point.X <= Position.X + Size.X + margin AndAlso
                   point.Y <= bottom + margin
        End Function

        Private Sub HandleSceneMouseMove(currentPoint As Point, lastPoint As Point)
            If _isDropDownOpen AndAlso Not ContainsDropDownWithMargin(currentPoint, DismissDistance) Then
                IsDropDownOpen = False
            End If
        End Sub

        Private Sub HandleSceneMouseLeftDown(point As Point)
            If _isDropDownOpen AndAlso Not ContainsDropDownWithMargin(point, 0) Then IsDropDownOpen = False
        End Sub

        Private Sub HandlePointerCancelled()
            If _isDropDownOpen Then IsDropDownOpen = False
        End Sub

        Private Sub UpdateVisual()
            If _selectedIndex >= 0 AndAlso _selectedIndex < _items.Count Then
                _headerText.Text = _items(_selectedIndex)
            Else
                _headerText.Text = ""
            End If

            AlignHeader()
        End Sub

        Private Sub AlignHeader()
            _headerText.Position = New Vector2(Position.X + 8, Position.Y + (Size.Y - _headerText.Size.Y) / 2)
            _dropDownButton.Position = New Vector2(Position.X + Size.X - 24, Position.Y + (Size.Y - _dropDownButton.Size.Y) / 2)
        End Sub

        Protected Overrides Sub AlignChildren()
            AlignHeader()
        End Sub

        Private Sub UpdateDropDown()
            ' Remove existing dropdown items
            For Each item In _dropDownItems
                If Scene IsNot Nothing Then
                    Scene.RemoveElement(item)
                End If
            Next
            _dropDownItems.Clear()

            If _isDropDownOpen AndAlso Scene IsNot Nothing Then
                Dim yOffset As Single = Position.Y + Size.Y
                For i As Integer = 0 To _items.Count - 1
                    Dim index As Integer = i
                    Dim itemBg As New RectangleElement(Scene) With {
                        .Position = New Vector2(Position.X, yOffset),
                        .Size = New Vector2(Size.X, _itemHeight),
                        .BackgroundColor = If(i = _selectedIndex, New Color(60, 100, 60), New Color(55, 55, 55)),
                        .IsOverlay = True
                    }

                    Dim itemText As New TextElement(Scene) With {
                        .Text = _items(i),
                        .ForegroundColor = Color.White,
                        .Margin = New Thickness(8, 4, 0, 0),
                        .isMouseBypassEnabled = True
                    }

                    itemBg.Children.Add(itemText)
                    _dropDownItems.Add(itemBg)
                    Scene.AddElement(itemBg)

                    AddHandler itemBg.MouseLeftClick, Sub(p)
                                                          SelectedIndex = index
                                                          IsDropDownOpen = False
                                                      End Sub

                    yOffset += _itemHeight
                Next
            End If
        End Sub

        Public Sub AddItem(item As String)
            _items.Add(item)
            UpdateVisual()
        End Sub

        Public Sub RemoveItem(item As String)
            _items.Remove(item)
            If _selectedIndex >= _items.Count Then
                _selectedIndex = _items.Count - 1
            End If
            UpdateVisual()
        End Sub

        Public Sub ClearItems()
            _items.Clear()
            _selectedIndex = -1
            UpdateVisual()
        End Sub

        Protected Friend Overrides Sub doDraw(gameTime As GameTime)
            MyBase.doDraw(gameTime)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                If ownerScene IsNot Nothing Then
                    RemoveHandler ownerScene.OnMouseMove, AddressOf HandleSceneMouseMove
                    RemoveHandler ownerScene.OnMouseLeftDown, AddressOf HandleSceneMouseLeftDown
                    RemoveHandler ownerScene.OnPointerCancelled, AddressOf HandlePointerCancelled
                End If
                If _isDropDownOpen OrElse _dropDownItems.Count > 0 Then
                    _isDropDownOpen = False
                    UpdateDropDown()
                End If
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class

    ''' <summary>
    ''' A ListBox control that displays a scrollable list of items.
    ''' </summary>
    Public Class ListBox
        Inherits RectangleElement

        Private _items As New List(Of String)
        Private _selectedIndex As Integer = -1
        Private _itemElements As New List(Of RectangleElement)
        Private _itemHeight As Single = 24.0F
        Private _scrollOffset As Single = 0
        Private _selectionColor As Color = New Color(60, 100, 60)

        Public Event SelectionChanged(sender As ListBox, selectedIndex As Integer)

        Public Property Items As List(Of String)
            Get
                Return _items
            End Get
            Set(value As List(Of String))
                _items = value
                RebuildItems()
            End Set
        End Property

        Public Property SelectedIndex As Integer
            Get
                Return _selectedIndex
            End Get
            Set(value As Integer)
                If value >= -1 AndAlso value < _items.Count Then
                    _selectedIndex = value
                    UpdateItemColors()
                    RaiseEvent SelectionChanged(Me, _selectedIndex)
                End If
            End Set
        End Property

        Public ReadOnly Property SelectedItem As String
            Get
                If _selectedIndex >= 0 AndAlso _selectedIndex < _items.Count Then
                    Return _items(_selectedIndex)
                End If
                Return Nothing
            End Get
        End Property

        Public Property ItemHeight As Single
            Get
                Return _itemHeight
            End Get
            Set(value As Single)
                _itemHeight = value
                RebuildItems()
            End Set
        End Property

        Public Property SelectionColor As Color
            Get
                Return _selectionColor
            End Get
            Set(value As Color)
                _selectionColor = value
                UpdateItemColors()
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = New Color(35, 35, 35)
            Size = New Vector2(200, 150)
            Clip = True
            AddHandler MouseWheel, AddressOf HandleMouseWheel
        End Sub

        Private Sub HandleMouseWheel(delta As Integer, point As Point)
            If delta = 0 Then Return
            _scrollOffset -= (delta / 120.0F) * _itemHeight * 3
            Dim maxOffset = Math.Max(0.0F, _items.Count * _itemHeight - Size.Y)
            _scrollOffset = Math.Max(0.0F, Math.Min(_scrollOffset, maxOffset))
            Invalidate()
        End Sub

        Public Sub AddItem(item As String)
            _items.Add(item)
            RebuildItems()
        End Sub

        Public Sub RemoveItem(item As String)
            Dim index = _items.IndexOf(item)
            If index >= 0 Then
                _items.RemoveAt(index)
                If _selectedIndex >= _items.Count Then
                    _selectedIndex = _items.Count - 1
                End If
                RebuildItems()
            End If
        End Sub

        Public Sub ClearItems()
            _items.Clear()
            _selectedIndex = -1
            RebuildItems()
        End Sub

        Private Sub RebuildItems()
            ' Clear existing elements
            For Each elem In _itemElements
                Children.Remove(elem)
            Next
            _itemElements.Clear()

            ' Create new item elements
            Dim yOffset As Single = 0
            For i As Integer = 0 To _items.Count - 1
                Dim index As Integer = i
                Dim itemBg As New RectangleElement(Scene) With {
                    .Position = New Vector2(0, yOffset),
                    .Size = New Vector2(Size.X, _itemHeight),
                    .BackgroundColor = If(i = _selectedIndex, _selectionColor, Color.Transparent)
                }

                Dim itemText As New TextElement(Scene) With {
                    .Text = _items(i),
                    .ForegroundColor = Color.White,
                    .Margin = New Thickness(6, 2, 0, 0),
                    .isMouseBypassEnabled = True
                }

                itemBg.Children.Add(itemText)
                _itemElements.Add(itemBg)
                Children.Add(itemBg)

                AddHandler itemBg.MouseLeftClick, Sub(p)
                                                      SelectedIndex = index
                                                  End Sub

                yOffset += _itemHeight
            Next
            Invalidate()
        End Sub

        Protected Overrides Sub AlignChildren()
            For index = 0 To _itemElements.Count - 1
                Dim item = _itemElements(index)
                item.Position = New Vector2(Position.X, Position.Y + index * _itemHeight - _scrollOffset)
                item.Size = New Vector2(Size.X, _itemHeight)
            Next
        End Sub

        Private Sub UpdateItemColors()
            For i As Integer = 0 To _itemElements.Count - 1
                _itemElements(i).BackgroundColor = If(i = _selectedIndex, _selectionColor, Color.Transparent)
            Next
        End Sub
    End Class

    ''' <summary>
    ''' A TabControl with multiple tabs.
    ''' </summary>
    Public Class TabControl
        Inherits RectangleElement

        Private _tabs As New List(Of TabItem)
        Private _selectedIndex As Integer = -1
        Private _tabHeaderHeight As Single = 32.0F
        Private _tabHeaders As New List(Of Button)
        Private _contentArea As RectangleElement

        Public Event SelectionChanged(sender As TabControl, selectedIndex As Integer)

        Public ReadOnly Property Tabs As List(Of TabItem)
            Get
                Return _tabs
            End Get
        End Property

        Public Property SelectedIndex As Integer
            Get
                Return _selectedIndex
            End Get
            Set(value As Integer)
                If value >= 0 AndAlso value < _tabs.Count Then
                    _selectedIndex = value
                    UpdateTabs()
                    RaiseEvent SelectionChanged(Me, _selectedIndex)
                End If
            End Set
        End Property

        Public Property TabHeaderHeight As Single
            Get
                Return _tabHeaderHeight
            End Get
            Set(value As Single)
                _tabHeaderHeight = value
                UpdateTabs()
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = New Color(40, 40, 40)
            Size = New Vector2(400, 300)

            _contentArea = New RectangleElement(scene) With {
                .BackgroundColor = New Color(30, 30, 30)
            }
            Children.Add(_contentArea)
        End Sub

        Public Sub AddTab(header As String, content As SceneElement)
            Dim tab As New TabItem With {
                .Header = header,
                .Content = content
            }
            _tabs.Add(tab)

            If _selectedIndex < 0 Then
                _selectedIndex = 0
            End If

            UpdateTabs()
        End Sub

        Public Sub RemoveTab(index As Integer)
            If index >= 0 AndAlso index < _tabs.Count Then
                _tabs.RemoveAt(index)
                If _selectedIndex >= _tabs.Count Then
                    _selectedIndex = _tabs.Count - 1
                End If
                UpdateTabs()
            End If
        End Sub

        Private Sub UpdateTabs()
            ' Clear existing headers
            For Each header In _tabHeaders
                Children.Remove(header)
            Next
            _tabHeaders.Clear()

            ' Clear content area children
            _contentArea.Children.Clear()

            If _tabs.Count = 0 Then Return

            ' Create tab headers
            Dim tabWidth As Single = Size.X / _tabs.Count
            For i As Integer = 0 To _tabs.Count - 1
                Dim index As Integer = i
                Dim header As New Button(Scene)
                header.AutoSize = ButtonAutoSize.None
                header.Text = _tabs(i).Header
                header.Position = New Vector2(Position.X + i * tabWidth, Position.Y)
                header.Size = New Vector2(tabWidth, _tabHeaderHeight)
                header.BackgroundColor = If(i = _selectedIndex, New Color(50, 50, 50), New Color(35, 35, 35))

                AddHandler header.MouseLeftClick, Sub(p)
                                                      SelectedIndex = index
                                                  End Sub

                _tabHeaders.Add(header)
                Children.Add(header)
            Next

            ' Update content area
            _contentArea.Position = New Vector2(Position.X, Position.Y + _tabHeaderHeight)
            _contentArea.Size = New Vector2(Size.X, Size.Y - _tabHeaderHeight)

            ' Add selected tab content
            If _selectedIndex >= 0 AndAlso _selectedIndex < _tabs.Count Then
                Dim content = _tabs(_selectedIndex).Content
                If content IsNot Nothing Then
                    _contentArea.Children.Add(content)
                End If
            End If
        End Sub

        Protected Overrides Sub AlignChildren()
            Dim tabWidth As Single = If(_tabHeaders.Count = 0, Size.X, Size.X / _tabHeaders.Count)
            For index = 0 To _tabHeaders.Count - 1
                _tabHeaders(index).Position = New Vector2(Position.X + index * tabWidth, Position.Y)
                _tabHeaders(index).Size = New Vector2(tabWidth, _tabHeaderHeight)
            Next
            _contentArea.Position = New Vector2(Position.X, Position.Y + _tabHeaderHeight)
            _contentArea.Size = New Vector2(Size.X, Math.Max(0, Size.Y - _tabHeaderHeight))
        End Sub
    End Class

    ''' <summary>
    ''' Represents a single tab in a TabControl.
    ''' </summary>
    Public Class TabItem
        Public Property Header As String
        Public Property Content As SceneElement
    End Class

    ''' <summary>
    ''' A ScrollViewer control that provides scrolling for content larger than its viewport.
    ''' </summary>
    Public Class ScrollViewer
        Inherits RectangleElement

        Private _content As SceneElement
        Private _scrollOffset As Vector2 = Vector2.Zero
        Private _verticalScrollbar As RectangleElement
        Private _horizontalScrollbar As RectangleElement
        Private _verticalTrack, _horizontalTrack As RectangleElement
        Private _verticalDragOffset, _horizontalDragOffset As Single
        Private _scrollbarWidth As Single = 12.0F
        Private _canScrollVertically As Boolean = True
        Private _canScrollHorizontally As Boolean = False
        Private _scrollVelocity As Vector2 = Vector2.Zero

        Public Property Content As SceneElement
            Get
                Return _content
            End Get
            Set(value As SceneElement)
                If _content IsNot Nothing Then
                    Children.Remove(_content)
                End If
                _content = value
                If _content IsNot Nothing Then
                    Children.Add(_content)
                End If
                UpdateScrollbars()
            End Set
        End Property

        Public Property ScrollOffset As Vector2
            Get
                Return _scrollOffset
            End Get
            Set(value As Vector2)
                ScrollTo(value)
            End Set
        End Property

        Public Property CanScrollVertically As Boolean
            Get
                Return _canScrollVertically
            End Get
            Set(value As Boolean)
                _canScrollVertically = value
                UpdateScrollbars()
            End Set
        End Property

        Public Property CanScrollHorizontally As Boolean
            Get
                Return _canScrollHorizontally
            End Get
            Set(value As Boolean)
                _canScrollHorizontally = value
                UpdateScrollbars()
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = New Color(30, 30, 30)
            Clip = True
            _verticalTrack = New RectangleElement(scene) With {.BackgroundColor = New Color(22, 28, 26), .zIndex = Integer.MaxValue - 1, .isVisible = False}
            _horizontalTrack = New RectangleElement(scene) With {.BackgroundColor = New Color(22, 28, 26), .zIndex = Integer.MaxValue - 1, .isVisible = False}
            Children.Add(_verticalTrack)
            Children.Add(_horizontalTrack)

            _verticalScrollbar = New RectangleElement(scene) With {
                .BackgroundColor = New Color(60, 60, 60),
                .zIndex = Integer.MaxValue,
                .isVisible = False
            }
            Children.Add(_verticalScrollbar)

            _horizontalScrollbar = New RectangleElement(scene) With {
                .BackgroundColor = New Color(60, 60, 60),
                .zIndex = Integer.MaxValue,
                .isVisible = False
            }
            Children.Add(_horizontalScrollbar)
            AddHandler _verticalScrollbar.MouseLeftDown, Sub(p)
                                                            StopScrolling()
                                                            _verticalDragOffset = _scrollOffset.Y
                                                        End Sub
            AddHandler _horizontalScrollbar.MouseLeftDown, Sub(p)
                                                              StopScrolling()
                                                              _horizontalDragOffset = _scrollOffset.X
                                                          End Sub
            AddHandler _verticalScrollbar.MouseDrag, Sub(current, start)
                                                        Dim travel = _verticalTrack.Size.Y - _verticalScrollbar.Size.Y
                                                        If travel > 0 Then ScrollTo(New Vector2(_scrollOffset.X, _verticalDragOffset + (current.Y - start.Y) * Math.Max(0, _content.Size.Y - _verticalTrack.Size.Y) / travel))
                                                    End Sub
            AddHandler _horizontalScrollbar.MouseDrag, Sub(current, start)
                                                          Dim travel = _horizontalTrack.Size.X - _horizontalScrollbar.Size.X
                                                          If travel > 0 Then ScrollTo(New Vector2(_horizontalDragOffset + (current.X - start.X) * Math.Max(0, _content.Size.X - _horizontalTrack.Size.X) / travel, _scrollOffset.Y))
                                                      End Sub
            AddHandler _verticalTrack.MouseLeftDown, Sub(p)
                                                        StopScrolling()
                                                        ScrollBy(New Vector2(0, If(p.Y + _verticalTrack.Position.Y < _verticalScrollbar.Position.Y, -1, 1) * _verticalTrack.Size.Y))
                                                    End Sub
            AddHandler _horizontalTrack.MouseLeftDown, Sub(p)
                                                          StopScrolling()
                                                          ScrollBy(New Vector2(If(p.X + _horizontalTrack.Position.X < _horizontalScrollbar.Position.X, -1, 1) * _horizontalTrack.Size.X, 0))
                                                      End Sub
            AddHandler Me.MouseWheel, AddressOf HandleMouseWheel
        End Sub

        Public ReadOnly Property VerticalThumbBounds As Rectangle
            Get
                Return _verticalScrollbar.Rectangle
            End Get
        End Property

        Public ReadOnly Property HorizontalThumbBounds As Rectangle
            Get
                Return _horizontalScrollbar.Rectangle
            End Get
        End Property

        ''' <summary>Velocity added by one conventional mouse-wheel notch, in pixels per second.</summary>
        Public Property WheelScrollAmount As Single = 900.0F

        ''' <summary>Velocity removed per second while coasting.</summary>
        Public Property ScrollDeceleration As Single = 2600.0F

        ''' <summary>Caps kinetic scrolling so trackpads and free-spin wheels remain controllable.</summary>
        Public Property MaximumScrollVelocity As Single = 3200.0F

        Public ReadOnly Property ScrollVelocity As Vector2
            Get
                Return _scrollVelocity
            End Get
        End Property

        Private Sub HandleMouseWheel(delta As Integer, p As Point)
            ApplyWheelImpulse(delta)
        End Sub

        Public Sub ApplyWheelImpulse(delta As Integer)
            If delta = 0 Then Return
            Dim notches As Single = delta / 120.0F
            If _canScrollVertically Then
                _scrollVelocity.Y = Math.Max(-MaximumScrollVelocity,
                                             Math.Min(MaximumScrollVelocity, _scrollVelocity.Y - notches * WheelScrollAmount))
            ElseIf _canScrollHorizontally Then
                _scrollVelocity.X = Math.Max(-MaximumScrollVelocity,
                                             Math.Min(MaximumScrollVelocity, _scrollVelocity.X - notches * WheelScrollAmount))
            End If
        End Sub

        Public Sub StopScrolling()
            _scrollVelocity = Vector2.Zero
        End Sub

        Public Overrides Sub Tick(gameTime As GameTime)
            MyBase.Tick(gameTime)
            Dim elapsed = CSng(gameTime.ElapsedGameTime.TotalSeconds)
            If elapsed <= 0 OrElse elapsed > 0.25F OrElse _scrollVelocity = Vector2.Zero Then Return

            Dim before = _scrollOffset
            ScrollBy(_scrollVelocity * elapsed)
            If _scrollOffset.X = before.X AndAlso _scrollVelocity.X <> 0 Then _scrollVelocity.X = 0
            If _scrollOffset.Y = before.Y AndAlso _scrollVelocity.Y <> 0 Then _scrollVelocity.Y = 0

            Dim deceleration = Math.Max(0.0F, ScrollDeceleration) * elapsed
            _scrollVelocity.X = ApproachZero(_scrollVelocity.X, deceleration)
            _scrollVelocity.Y = ApproachZero(_scrollVelocity.Y, deceleration)
        End Sub

        Private Shared Function ApproachZero(value As Single, amount As Single) As Single
            If value > 0 Then Return Math.Max(0.0F, value - amount)
            If value < 0 Then Return Math.Min(0.0F, value + amount)
            Return 0.0F
        End Function

        Public Sub ScrollTo(offset As Vector2)
            _scrollOffset = offset
            ClampScrollOffset()
            UpdateContentPosition()
            UpdateScrollbars()
        End Sub

        Public Sub ScrollBy(delta As Vector2)
            _scrollOffset += delta
            ClampScrollOffset()
            UpdateContentPosition()
            UpdateScrollbars()
        End Sub

        Private Sub ClampScrollOffset()
            If _content Is Nothing Then
                _scrollOffset = Vector2.Zero
                Return
            End If

            Dim maxScrollX = Math.Max(0, _content.Size.X - Size.X + If(_canScrollVertically, _scrollbarWidth, 0))
            Dim maxScrollY = Math.Max(0, _content.Size.Y - Size.Y + If(_canScrollHorizontally, _scrollbarWidth, 0))

            _scrollOffset.X = Math.Max(0, Math.Min(_scrollOffset.X, maxScrollX))
            _scrollOffset.Y = Math.Max(0, Math.Min(_scrollOffset.Y, maxScrollY))
        End Sub

        Private Sub UpdateContentPosition()
            If _content IsNot Nothing Then
                _content.Position = Position - _scrollOffset
            End If
        End Sub

        Private Sub UpdateScrollbars()
            If _content Is Nothing Then
                _verticalScrollbar.isVisible = False
                _horizontalScrollbar.isVisible = False
                _verticalTrack.isVisible = False
                _horizontalTrack.isVisible = False
                Return
            End If

            Dim viewportWidth = Size.X - If(_canScrollVertically, _scrollbarWidth, 0)
            Dim viewportHeight = Size.Y - If(_canScrollHorizontally, _scrollbarWidth, 0)

            ' Vertical scrollbar
            If _canScrollVertically AndAlso _content.Size.Y > viewportHeight Then
                _verticalScrollbar.isVisible = True
                _verticalTrack.isVisible = True
                _verticalTrack.Position = New Vector2(Position.X + Size.X - _scrollbarWidth, Position.Y)
                _verticalTrack.Size = New Vector2(_scrollbarWidth, Math.Max(0, viewportHeight))
                Dim scrollRatio = viewportHeight / _content.Size.Y
                Dim thumbHeight = Math.Min(viewportHeight, Math.Max(20, viewportHeight * scrollRatio))
                Dim thumbOffset = (_scrollOffset.Y / (_content.Size.Y - viewportHeight)) * (viewportHeight - thumbHeight)

                _verticalScrollbar.Position = New Vector2(Position.X + Size.X - _scrollbarWidth, Position.Y + thumbOffset)
                _verticalScrollbar.Size = New Vector2(_scrollbarWidth, thumbHeight)
            Else
                _verticalScrollbar.isVisible = False
                _verticalTrack.isVisible = False
            End If

            ' Horizontal scrollbar
            If _canScrollHorizontally AndAlso _content.Size.X > viewportWidth Then
                _horizontalScrollbar.isVisible = True
                _horizontalTrack.isVisible = True
                _horizontalTrack.Position = New Vector2(Position.X, Position.Y + Size.Y - _scrollbarWidth)
                _horizontalTrack.Size = New Vector2(Math.Max(0, viewportWidth), _scrollbarWidth)
                Dim scrollRatio = viewportWidth / _content.Size.X
                Dim thumbWidth = Math.Min(viewportWidth, Math.Max(20, viewportWidth * scrollRatio))
                Dim thumbOffset = (_scrollOffset.X / (_content.Size.X - viewportWidth)) * (viewportWidth - thumbWidth)

                _horizontalScrollbar.Position = New Vector2(Position.X + thumbOffset, Position.Y + Size.Y - _scrollbarWidth)
                _horizontalScrollbar.Size = New Vector2(thumbWidth, _scrollbarWidth)
            Else
                _horizontalScrollbar.isVisible = False
                _horizontalTrack.isVisible = False
            End If
        End Sub

        Protected Overrides Sub AlignChildren()
            ClampScrollOffset()
            UpdateContentPosition()
            UpdateScrollbars()
        End Sub
    End Class

    ''' <summary>
    ''' A Separator control - a simple horizontal or vertical line.
    ''' </summary>
    Public Class Separator
        Inherits RectangleElement

        Private _orientation As Orientation = Orientation.Horizontal

        Public Property Orientation As Orientation
            Get
                Return _orientation
            End Get
            Set(value As Orientation)
                _orientation = value
                UpdateSize()
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = New Color(80, 80, 80)
            UpdateSize()
        End Sub

        Private Sub UpdateSize()
            If _orientation = Orientation.Horizontal Then
                Size = New Vector2(Size.X, 1)
            Else
                Size = New Vector2(1, Size.Y)
            End If
        End Sub
    End Class

    ''' <summary>
    ''' An Expander control that can show/hide content.
    ''' </summary>
    Public Class Expander
        Inherits RectangleElement

        Private _header As String = "Expander"
        Private _isExpanded As Boolean = True
        Private _content As SceneElement
        Private _headerButton As Button
        Private _contentContainer As RectangleElement
        Private _headerHeight As Single = 28.0F

        Public Event ExpandedChanged(sender As Expander, isExpanded As Boolean)

        Public Property Header As String
            Get
                Return _header
            End Get
            Set(value As String)
                _header = value
                If _headerButton IsNot Nothing Then
                    _headerButton.Text = If(_isExpanded, "▼ ", "▶ ") & _header
                End If
            End Set
        End Property

        Public Property IsExpanded As Boolean
            Get
                Return _isExpanded
            End Get
            Set(value As Boolean)
                _isExpanded = value
                UpdateExpansion()
                RaiseEvent ExpandedChanged(Me, _isExpanded)
            End Set
        End Property

        Public Property Content As SceneElement
            Get
                Return _content
            End Get
            Set(value As SceneElement)
                If _content IsNot Nothing Then
                    _contentContainer.Children.Remove(_content)
                End If
                _content = value
                If _content IsNot Nothing Then
                    _contentContainer.Children.Add(_content)
                End If
                UpdateExpansion()
            End Set
        End Property

        Public Property HeaderHeight As Single
            Get
                Return _headerHeight
            End Get
            Set(value As Single)
                _headerHeight = value
                UpdateLayout()
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = New Color(40, 40, 40)
            Size = New Vector2(200, 150)

            _headerButton = New Button(scene) With {
                .Text = "▼ " & _header,
                .Position = New Vector2(0, 0),
                .Size = New Vector2(Size.X, _headerHeight),
                .BackgroundColor = New Color(50, 50, 50)
            }
            Children.Add(_headerButton)

            AddHandler _headerButton.MouseLeftClick, Sub(p) IsExpanded = Not IsExpanded

            _contentContainer = New RectangleElement(scene) With {
                .BackgroundColor = Color.Transparent,
                .Position = New Vector2(0, _headerHeight)
            }
            Children.Add(_contentContainer)

            UpdateExpansion()
        End Sub

        Private Sub UpdateExpansion()
            If _headerButton IsNot Nothing Then
                _headerButton.Text = If(_isExpanded, "▼ ", "▶ ") & _header
            End If

            If _contentContainer IsNot Nothing Then
                _contentContainer.isVisible = _isExpanded
            End If

            UpdateLayout()
        End Sub

        Private Sub UpdateLayout()
            If _headerButton IsNot Nothing Then
                _headerButton.Size = New Vector2(Size.X, _headerHeight)
            End If

            If _contentContainer IsNot Nothing Then
                _contentContainer.Position = New Vector2(0, _headerHeight)
                _contentContainer.Size = New Vector2(Size.X, Size.Y - _headerHeight)
            End If

            If Not _isExpanded Then
                Size = New Vector2(Size.X, _headerHeight)
            End If
        End Sub
    End Class

    ''' <summary>
    ''' A ToolTip control that can be attached to elements.
    ''' </summary>
    Public Class ToolTip
        Inherits RectangleElement

        Private _content As String = ""
        Private _textElement As TextElement
        Private _showDelay As Single = 0.5F
        Private _hideDelay As Single = 5.0F

        Public Property Content As String
            Get
                Return _content
            End Get
            Set(value As String)
                _content = value
                If _textElement IsNot Nothing Then
                    _textElement.Text = _content
                End If
                UpdateSize()
            End Set
        End Property

        Public Property ShowDelay As Single
            Get
                Return _showDelay
            End Get
            Set(value As Single)
                _showDelay = value
            End Set
        End Property

        Public Property HideDelay As Single
            Get
                Return _hideDelay
            End Get
            Set(value As Single)
                _hideDelay = value
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = New Color(30, 30, 30, 240)
            isVisible = False
            isMouseBypassEnabled = True

            _textElement = New TextElement(scene) With {
                .Text = "",
                .ForegroundColor = Color.White,
                .Position = New Vector2(6, 4),
                .isMouseBypassEnabled = True
            }
            Children.Add(_textElement)
        End Sub

        Public Sub Show(position As Vector2)
            Me.Position = position
            isVisible = True
        End Sub

        Public Sub Hide()
            isVisible = False
        End Sub

        Private Sub UpdateSize()
            If _textElement IsNot Nothing Then
                Size = New Vector2(_textElement.Size.X + 12, _textElement.Size.Y + 8)
            End If
        End Sub
    End Class

    ''' <summary>
    ''' A GroupBox control with a header border.
    ''' </summary>
    Public Class GroupBox
        Inherits RectangleElement

        Private _header As String = "Group"
        Private _headerText As TextElement
        Private _contentArea As RectangleElement
        Private _headerHeight As Single = 20.0F
        Private _borderColor As Color = New Color(80, 80, 80)

        Public Property Header As String
            Get
                Return _header
            End Get
            Set(value As String)
                _header = value
                If _headerText IsNot Nothing Then
                    _headerText.Text = _header
                End If
            End Set
        End Property

        Public Property Content As SceneElement
            Get
                If _contentArea.Children.Count > 0 Then
                    Return _contentArea.Children(0)
                End If
                Return Nothing
            End Get
            Set(value As SceneElement)
                _contentArea.Children.Clear()
                If value IsNot Nothing Then
                    _contentArea.Children.Add(value)
                End If
            End Set
        End Property

        Public Shadows Property BorderColor As Color
            Get
                Return _borderColor
            End Get
            Set(value As Color)
                _borderColor = value
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = New Color(35, 35, 35)
            Size = New Vector2(200, 150)

            _headerText = New TextElement(scene) With {
                .Text = _header,
                .ForegroundColor = Color.LightGray,
                .Position = New Vector2(10, 2),
                .isMouseBypassEnabled = True
            }
            Children.Add(_headerText)

            _contentArea = New RectangleElement(scene) With {
                .BackgroundColor = Color.Transparent,
                .Position = New Vector2(4, _headerHeight),
                .Size = New Vector2(Size.X - 8, Size.Y - _headerHeight - 4)
            }
            Children.Add(_contentArea)
        End Sub
    End Class

    ''' <summary>
    ''' A NumericUpDown control for number input with increment/decrement buttons.
    ''' </summary>
    Public Class NumericUpDown
        Inherits RectangleElement

        Private _value As Double = 0
        Private _minimum As Double = 0
        Private _maximum As Double = 100
        Private _increment As Double = 1
        Private _valueText As TextElement
        Private _upButton As Button
        Private _downButton As Button

        Public Event ValueChanged(sender As NumericUpDown, value As Double)

        Public Property Value As Double
            Get
                Return _value
            End Get
            Set(value As Double)
                Dim newValue = Math.Max(_minimum, Math.Min(_maximum, value))
                If _value <> newValue Then
                    _value = newValue
                    UpdateDisplay()
                    RaiseEvent ValueChanged(Me, _value)
                End If
            End Set
        End Property

        Public Property Minimum As Double
            Get
                Return _minimum
            End Get
            Set(value As Double)
                _minimum = value
                If _value < _minimum Then Me.Value = _minimum
            End Set
        End Property

        Public Property Maximum As Double
            Get
                Return _maximum
            End Get
            Set(value As Double)
                _maximum = value
                If _value > _maximum Then Me.Value = _maximum
            End Set
        End Property

        Public Property Increment As Double
            Get
                Return _increment
            End Get
            Set(value As Double)
                _increment = value
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = New Color(45, 45, 45)
            Size = New Vector2(100, 28)

            Dim buttonWidth As Single = 24

            _valueText = New TextElement(scene) With {
                .Text = "0",
                .ForegroundColor = Color.White,
                .Position = New Vector2(4, 4),
                .isMouseBypassEnabled = True
            }
            Children.Add(_valueText)

            _upButton = New Button(scene) With {
                .Text = "▲",
                .Position = New Vector2(Size.X - buttonWidth, 0),
                .Size = New Vector2(buttonWidth, Size.Y / 2),
                .BackgroundColor = New Color(60, 60, 60)
            }
            Children.Add(_upButton)

            _downButton = New Button(scene) With {
                .Text = "▼",
                .Position = New Vector2(Size.X - buttonWidth, Size.Y / 2),
                .Size = New Vector2(buttonWidth, Size.Y / 2),
                .BackgroundColor = New Color(60, 60, 60)
            }
            Children.Add(_downButton)

            AddHandler _upButton.MouseLeftClick, Sub(p) Value += _increment
            AddHandler _downButton.MouseLeftClick, Sub(p) Value -= _increment

            UpdateDisplay()
        End Sub

        Private Sub UpdateDisplay()
            If _valueText IsNot Nothing Then
                _valueText.Text = _value.ToString("F2")
            End If
        End Sub
    End Class

End Namespace
