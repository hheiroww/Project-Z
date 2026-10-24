Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports ProjectZ.Shared.Drawing.UI.Advanced
Imports System.Collections.Generic
Imports Microsoft.Xna.Framework.Input

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
        Private _dropDownButton As PolygonElement
        Private _itemHeight As Single = 28.0F
        Private Shadows ReadOnly ownerScene As Scene
        Private _highlightedIndex As Integer = -1

        Public ReadOnly Property HighlightedIndex As Integer
            Get
                Return _highlightedIndex
            End Get
        End Property
        Public ReadOnly Property DropDownElements As IReadOnlyList(Of RectangleElement)
            Get
                Return _dropDownItems.AsReadOnly()
            End Get
        End Property
        Public Property DropDownBackgroundColor As Color = New Color(16, 35, 30, 242)
        Public Property DropDownHighlightColor As Color = New Color(36, 84, 68, 246)
        Public Property DropDownIndicatorColor As Color = New Color(122, 210, 184)

        Public Property DismissDistance As Single = 28.0F
        Public Property ItemIsEnabled As Func(Of Integer, Boolean)
        Public Property ItemAppearance As Action(Of Integer, RectangleElement, TextElement)

        Private editor As Textbox
        Public Event TextChanged()
        Private _isEditable As Boolean
        Public Property IsEditable As Boolean
            Get
                Return _isEditable
            End Get
            Set(value As Boolean)
                _isEditable = value
                editor.isVisible = value
                _headerText.isVisible = Not value
                AlignHeader()
            End Set
        End Property
        Public Property EditableText As String
            Get
                Return editor.Text
            End Get
            Set(value As String)
                editor.Text = value
            End Set
        End Property

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
                If _isDropDownOpen = value Then Return
                _isDropDownOpen = value
                If value Then
                    Dim ancestor = Parent
                    While ancestor IsNot Nothing
                        Dim scroll = TryCast(ancestor, ScrollViewer)
                        If scroll IsNot Nothing Then scroll.StopScrolling()
                        ancestor = ancestor.Parent
                    End While
                    _highlightedIndex = If(IsItemEnabled(_selectedIndex), _selectedIndex, FindEnabled(-1, 1))
                    Scene?.FocusElement(Me)
                End If
                UpdateDropDown()
                OnUserInvalidated()
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
            CanSelect = True
            BackgroundColor = New Color(45, 45, 45)
            Size = New Vector2(180, 32)

            _headerText = New TextElement(scene) With {
                .Text = "",
                .ForegroundColor = Color.White,
                .isMouseBypassEnabled = True
            }
            Children.Add(_headerText)

            _dropDownButton = New PolygonElement(scene, {New Vector2(0, 0), New Vector2(5, 5), New Vector2(10, 0), New Vector2(10, 3), New Vector2(5, 8), New Vector2(0, 3)}) With {
                .Size = New Vector2(10, 8), .FillColor = New Color(210, 232, 224),
                .isMouseBypassEnabled = True
            }
            Children.Add(_dropDownButton)
            editor = New Textbox(scene) With {.isVisible = False, .BackgroundColor = Color.Transparent, .ForegroundColor = Color.White}
            Children.Add(editor)
            AddHandler editor.OnTextChanged, Sub() RaiseEvent TextChanged()
            AddHandler MouseLeftClick, Sub(point) IsDropDownOpen = Not IsDropDownOpen
            AddHandler Deselected, Sub() IsDropDownOpen = False
            If ownerScene IsNot Nothing Then
                AddHandler ownerScene.OnMouseMove, AddressOf HandleSceneMouseMove
                AddHandler ownerScene.OnMouseLeftDown, AddressOf HandleSceneMouseLeftDown
                AddHandler ownerScene.OnPointerCancelled, AddressOf HandlePointerCancelled
            End If
        End Sub

        Private Function ContainsDropDownWithMargin(point As Point, margin As Single) As Boolean
            Dim bounds = Rectangle
            For Each item In _dropDownItems
                bounds = Microsoft.Xna.Framework.Rectangle.Union(bounds, item.Rectangle)
            Next
            bounds.Inflate(CInt(margin), CInt(margin))
            Return bounds.Contains(point)
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
                If editor IsNot Nothing Then editor.Text = _items(_selectedIndex)
            Else
                _headerText.Text = ""
            End If

            AlignHeader()
        End Sub

        Private Sub AlignHeader()
            If editor IsNot Nothing Then
                editor.Position = Position + New Vector2(4, 2)
                editor.Size = New Vector2(Math.Max(0, Size.X - 30), Math.Max(0, Size.Y - 4))
            End If
            _headerText.Position = New Vector2(Position.X + 8, Position.Y + (Size.Y - _headerText.Size.Y) / 2)
            _dropDownButton.Position = New Vector2(Position.X + Size.X - 24, Position.Y + (Size.Y - _dropDownButton.Size.Y) / 2)
            _dropDownButton.RenderRotationOrigin = New Vector2(0.5F)
            _dropDownButton.RenderRotation = If(_isDropDownOpen, 180, 0)
        End Sub

        Protected Overrides Sub AlignChildren()
            AlignHeader()
            For index = 0 To _dropDownItems.Count - 1
                _dropDownItems(index).Position = New Vector2(Rectangle.Left, Rectangle.Bottom + index * _itemHeight)
                _dropDownItems(index).Size = New Vector2(Rectangle.Width, _itemHeight)
            Next
        End Sub

        Private Function IsItemEnabled(index As Integer) As Boolean
            Return index >= 0 AndAlso index < _items.Count AndAlso (ItemIsEnabled Is Nothing OrElse ItemIsEnabled(index))
        End Function
        Private Function FindEnabled(start As Integer, direction As Integer) As Integer
            Dim index = start + direction
            While index >= 0 AndAlso index < _items.Count
                If IsItemEnabled(index) Then Return index
                index += direction
            End While
            Return -1
        End Function
        Private Sub Highlight(index As Integer)
            If Not IsItemEnabled(index) Then Return
            Dim changed = _highlightedIndex <> index
            _highlightedIndex = index
            For row = 0 To _dropDownItems.Count - 1
                Dim item = _dropDownItems(row)
                Dim fill = If(row = index, DropDownHighlightColor, DropDownBackgroundColor)
                Dim border = If(row = index, DropDownIndicatorColor, New Color(122, 180, 161, 40))
                changed = changed OrElse item.BackgroundColor <> fill OrElse item.BorderColor <> border
                item.BackgroundColor = fill
                item.BorderColor = border
                item.BorderThickness = 1
            Next
            If changed Then OnUserInvalidated()
        End Sub
        Public Sub RefreshDropDownAppearance()
            Highlight(_highlightedIndex)
        End Sub
        Private Sub CommitHighlight()
            If IsItemEnabled(_highlightedIndex) Then SelectedIndex = _highlightedIndex
            IsDropDownOpen = False
            Scene?.FocusElement(Me)
        End Sub
        Protected Friend Overrides Function HandleNavigationKey(key As Keys, state As KeyboardState, keyPress As Boolean) As Boolean
            If key = Keys.Tab Then
                If Not keyPress Then IsDropDownOpen = False
                Return False
            End If
            Select Case key
                Case Keys.Down, Keys.Up, Keys.Home, Keys.End, Keys.PageDown, Keys.PageUp
                    If Not keyPress Then Return True
                    Dim wasOpen = IsDropDownOpen
                    IsDropDownOpen = True
                    If key = Keys.Home OrElse key = Keys.End Then
                        Highlight(If(key = Keys.Home, FindEnabled(-1, 1), FindEnabled(_items.Count, -1)))
                    ElseIf wasOpen OrElse _selectedIndex >= 0 Then
                        Dim direction = If(key = Keys.Up OrElse key = Keys.PageUp, -1, 1)
                        Dim steps = If(key = Keys.PageUp OrElse key = Keys.PageDown, 5, 1)
                        For stepIndex = 1 To steps
                            Dim nextIndex = FindEnabled(_highlightedIndex, direction)
                            If nextIndex < 0 Then Exit For
                            Highlight(nextIndex)
                        Next
                    End If
                    Return True
                Case Keys.Enter, Keys.Space, Keys.F4
                    If keyPress Then Return True
                    If IsEditable AndAlso key = Keys.Space AndAlso Not IsDropDownOpen Then Return False
                    If IsDropDownOpen Then CommitHighlight() Else IsDropDownOpen = True
                    Return True
                Case Keys.Escape
                    If Not IsDropDownOpen Then Return False
                    If Not keyPress Then IsDropDownOpen = False
                    Return True
            End Select
            Return False
        End Function

        Private Sub UpdateDropDown()
            AlignHeader()
            ' Remove existing dropdown items
            For Each item In _dropDownItems
                If Scene IsNot Nothing Then
                    Scene.RemoveElement(item)
                End If
                item.Dispose()
            Next
            _dropDownItems.Clear()

            If _isDropDownOpen AndAlso Scene IsNot Nothing Then
                Dim yOffset As Single = Rectangle.Bottom
                For i As Integer = 0 To _items.Count - 1
                    Dim index As Integer = i
                    Dim itemBg As New RectangleElement(Scene) With {
                        .Position = New Vector2(Rectangle.Left, yOffset),
                        .Size = New Vector2(Rectangle.Width, _itemHeight),
                        .BackgroundColor = If(i = _highlightedIndex, DropDownHighlightColor, DropDownBackgroundColor),
                        .CornerRadius = 4, .BorderColor = New Color(122, 180, 161, 40), .BorderThickness = 1,
                        .KeyboardFocusTarget = Me,
                        .IsOverlay = True
                    }
                    itemBg.isEnabled = ItemIsEnabled Is Nothing OrElse ItemIsEnabled(i)

                    Dim itemText As New TextElement(Scene) With {
                        .Text = _items(i),
                        .ForegroundColor = Color.White,
                        .Margin = New Thickness(8, 4, 0, 0),
                        .isMouseBypassEnabled = True
                    }

                    itemBg.Children.Add(itemText)
                    If ItemAppearance IsNot Nothing Then ItemAppearance.Invoke(i, itemBg, itemText)
                    _dropDownItems.Add(itemBg)
                    Scene.AddElement(itemBg)
                    AddHandler itemBg.MouseEnter, Sub() Highlight(index)

                    AddHandler itemBg.MouseLeftClick, Sub(p)
                                                          If Not itemBg.isEnabled Then Return
                                                          _highlightedIndex = index
                                                          CommitHighlight()
                                                      End Sub

                    yOffset += _itemHeight
                Next
                Highlight(_highlightedIndex)
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

        ''' <summary>Optional native item visuals, used by imported live DataTemplates.</summary>
        Public Property ItemVisualFactory As Func(Of Integer, SceneElement)
        Public Property ReleaseItemVisual As Action(Of Integer, SceneElement)
        Public Property VirtualizeItemTemplates As Boolean = True
        Public Property ItemIsEnabled As Func(Of Integer, Boolean)
        Public Property ItemAppearance As Action(Of Integer, RectangleElement, TextElement)
        Private _headerVisual As SceneElement
        Public Property HeaderVisual As SceneElement
            Get
                Return _headerVisual
            End Get
            Set(value As SceneElement)
                If _headerVisual IsNot Nothing Then
                    Children.Remove(_headerVisual)
                    _headerVisual.Dispose()
                End If
                _headerVisual = value
                If value IsNot Nothing Then Children.Add(value)
                Invalidate()
            End Set
        End Property
        Public Property HeaderHeight As Single = 24.0F
        Public Property ItemsOrientation As Orientation = Orientation.Vertical
        Public Property WrapItems As Boolean
        Public Property ItemWidth As Single = 100.0F
        Private ReadOnly Property HeaderOffset As Single
            Get
                Return If(_headerVisual Is Nothing, 0.0F, HeaderHeight)
            End Get
        End Property
        Private Function ItemLocation(index As Integer) As Vector2
            If WrapItems Then
                Dim columns = Math.Max(1, CInt(Math.Floor(Size.X / Math.Max(1.0F, ItemWidth))))
                Return New Vector2((index Mod columns) * ItemWidth, (index \ columns) * _itemHeight)
            End If
            If ItemsOrientation = Orientation.Horizontal Then Return New Vector2(index * ItemWidth, 0)
            Dim top As Single
            For row = 0 To Math.Min(index, _itemElements.Count) - 1
                top += _itemElements(row).Size.Y
            Next
            Return New Vector2(0, top)
        End Function
        Private ReadOnly Property ContentHeight As Single
            Get
                If _items.Count = 0 Then Return 0
                Return ItemLocation(_items.Count - 1).Y + If(WrapItems OrElse ItemsOrientation = Orientation.Horizontal, _itemHeight, If(_itemElements.Count = 0, _itemHeight, _itemElements.Last().Size.Y))
            End Get
        End Property

        Private _items As New List(Of String)
        Private _selectedIndex As Integer = -1
        Private _itemElements As New List(Of RectangleElement)
        Private deferredItemLayout As Integer
        Private estimatedTemplateHeight As Single = 24
        Private ReadOnly Property HasVirtualTemplates As Boolean
            Get
                Return VirtualizeItemTemplates AndAlso ItemVisualFactory IsNot Nothing AndAlso _items.Count > 64 AndAlso Not WrapItems AndAlso ItemsOrientation = Orientation.Vertical
            End Get
        End Property
        Private ReadOnly selectedIndices As New HashSet(Of Integer)
        ''' <summary>0 = single, 1 = multiple, 2 = extended selection.</summary>
        Public Property SelectionMode As Integer
        Public ReadOnly Property SelectedIndexes As Integer()
            Get
                Return selectedIndices.OrderBy(Function(index) index).ToArray()
            End Get
        End Property

        Public Sub SetSelection(indexes As IEnumerable(Of Integer))
            Dim values = indexes.Distinct().ToArray()
            If values.Any(Function(index) index < 0 OrElse index >= _items.Count) Then Throw New ArgumentOutOfRangeException(NameOf(indexes))
            If SelectionMode = 0 AndAlso values.Length > 1 Then Throw New InvalidOperationException("Single selection permits one item.")
            selectedIndices.Clear()
            selectedIndices.UnionWith(values)
            _selectedIndex = values.DefaultIfEmpty(-1).First()
            UpdateItemColors()
            RaiseEvent SelectionChanged(Me, _selectedIndex)
        End Sub

        Public Sub SelectIndex(index As Integer, control As Boolean, shift As Boolean)
            If SelectionMode = 0 OrElse (SelectionMode = 2 AndAlso Not control AndAlso Not shift) Then
                SelectedIndex = index
            ElseIf SelectionMode = 2 AndAlso shift AndAlso _selectedIndex >= 0 Then
                Dim anchor = _selectedIndex
                Dim range = Enumerable.Range(Math.Min(anchor, index), Math.Abs(anchor - index) + 1)
                SetSelection(If(control, selectedIndices.Concat(range), range).ToArray())
                _selectedIndex = anchor
            Else
                Dim nextSelection = New HashSet(Of Integer)(selectedIndices)
                If Not nextSelection.Add(index) Then nextSelection.Remove(index)
                SetSelection(nextSelection)
            End If
        End Sub
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
                    selectedIndices.Clear()
                    If value >= 0 Then selectedIndices.Add(value)
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
            VirtualizationViewport = Me
            AddHandler MouseWheel, AddressOf HandleMouseWheel
        End Sub

        Private Sub HandleMouseWheel(delta As Integer, point As Point)
            If delta = 0 Then Return
            _scrollOffset -= (delta / 120.0F) * _itemHeight * 3
            Dim maxOffset = Math.Max(0.0F, ContentHeight - (Size.Y - HeaderOffset))
            _scrollOffset = Math.Max(0.0F, Math.Min(_scrollOffset, maxOffset))
            Invalidate()
        End Sub

        Public Sub AddItem(item As String)
            _items.Add(item)
            AppendItemVisual(_items.Count - 1)
            ' Child insertion invalidates the list. Resolve the whole batch on
            ' the next scene validation instead of remeasuring every prior row.
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
            selectedIndices.RemoveWhere(Function(index) index >= _items.Count)
            deferredItemLayout += 1
            Try
            ' Clear existing elements
            For Each elem In _itemElements
                Children.Remove(elem)
                elem.Dispose()
            Next
            _itemElements.Clear()

            ' Create new item elements
            For i As Integer = 0 To _items.Count - 1
                AppendItemVisual(i)
            Next
            Finally
                deferredItemLayout -= 1
            End Try
            Invalidate()
        End Sub

        Private Sub AppendItemVisual(index As Integer)
            deferredItemLayout += 1
            Try
                Dim itemBg As New RectangleElement(Scene) With {
                    .Position = New Vector2(0, index * _itemHeight),
                    .Size = New Vector2(Size.X, _itemHeight),
                    .BackgroundColor = If(selectedIndices.Contains(index), _selectionColor, Color.Transparent)
                }
                itemBg.isEnabled = ItemIsEnabled Is Nothing OrElse ItemIsEnabled(index)

                Dim itemText As New TextElement(Scene) With {
                    .Text = _items(index),
                    .ForegroundColor = Color.White,
                    .Margin = New Thickness(6, 2, 0, 0),
                    .isMouseBypassEnabled = True
                }

                If ItemVisualFactory IsNot Nothing Then
                    If Not HasVirtualTemplates Then itemBg.Children.Add(ItemVisualFactory(index))
                    itemText.Dispose()
                Else
                    itemBg.Children.Add(itemText)
                    If ItemAppearance IsNot Nothing Then ItemAppearance.Invoke(index, itemBg, itemText)
                End If
                _itemElements.Add(itemBg)
                Children.Add(itemBg)

                AddHandler itemBg.MouseLeftClick, Sub(p)
                                                      If Not itemBg.isEnabled Then Return
                                                      Dim keys = Microsoft.Xna.Framework.Input.Keyboard.GetState()
                                                      SelectIndex(index, keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) OrElse keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl), keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) OrElse keys.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift))
                                                  End Sub
            Finally
                deferredItemLayout -= 1
            End Try
        End Sub

        Protected Overrides Sub AlignChildren()
            If deferredItemLayout > 0 Then Return
            If _headerVisual IsNot Nothing Then
                _headerVisual.Position = Position
                _headerVisual.Size = New Vector2(Size.X, HeaderHeight)
            End If
            Dim top As Single
            Dim rowWidth = If(WrapItems OrElse ItemsOrientation = Orientation.Horizontal, ItemWidth, Size.X)
            Dim virtualTemplates = HasVirtualTemplates
            Dim attached = Not virtualTemplates OrElse (Scene IsNot Nothing AndAlso Scene.Elements.Contains(Me))
            Dim ancestor = Parent
            While attached AndAlso ancestor IsNot Nothing
                attached = ancestor.isVisible
                ancestor = ancestor.Parent
            End While
            For index = 0 To _itemElements.Count - 1
                Dim item = _itemElements(index)
                Dim height = If(virtualTemplates, Math.Max(_itemHeight, estimatedTemplateHeight), _itemHeight)
                Dim nearViewport = attached AndAlso top + height >= _scrollOffset - 64 AndAlso top <= _scrollOffset + Size.Y + 64
                If virtualTemplates AndAlso nearViewport AndAlso item.Children.Count = 0 Then
                    item.Children.Add(ItemVisualFactory(index))
                ElseIf virtualTemplates AndAlso Not nearViewport AndAlso item.Children.Count > 0 Then
                    Dim previousView = item.Children(0)
                    item.Children.Remove(previousView)
                    If ReleaseItemVisual IsNot Nothing Then ReleaseItemVisual.Invoke(index, previousView)
                    previousView.Dispose()
                End If
                If ItemVisualFactory IsNot Nothing AndAlso Not WrapItems AndAlso ItemsOrientation = Orientation.Vertical AndAlso item.Children.Count > 0 Then
                    Dim view = item.Children(0)
                    Dim desired = If(view.ImportedMeasure Is Nothing, view.Size, view.ImportedMeasure.Invoke(Math.Max(1, rowWidth)))
                    If Single.IsFinite(desired.Y) Then height = Math.Max(_itemHeight, desired.Y)
                    If virtualTemplates Then estimatedTemplateHeight = height
                    If view.ImportedMeasure IsNot Nothing Then view.Size = New Vector2(rowWidth, height)
                End If
                Dim location = If(WrapItems OrElse ItemsOrientation = Orientation.Horizontal, ItemLocation(index), New Vector2(0, top))
                item.Position = Position + location + New Vector2(0, HeaderOffset - _scrollOffset)
                item.Size = New Vector2(rowWidth, height)
                top += height
            Next
        End Sub

        Public Sub ScrollIntoView(index As Integer)
            If index < 0 OrElse index >= _items.Count Then Throw New ArgumentOutOfRangeException(NameOf(index))
            AlignChildren()
            Dim top = ItemLocation(index).Y
            If top < _scrollOffset Then _scrollOffset = top
            Dim viewportHeight = Math.Max(0.0F, Size.Y - HeaderOffset)
            Dim height = If(index < _itemElements.Count, _itemElements(index).Size.Y, _itemHeight)
            If top + height > _scrollOffset + viewportHeight Then _scrollOffset = top + height - viewportHeight
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, Math.Max(0, ContentHeight - viewportHeight)))
            AlignChildren()
        End Sub

        Private Sub UpdateItemColors()
            For i As Integer = 0 To _itemElements.Count - 1
                _itemElements(i).BackgroundColor = If(selectedIndices.Contains(i), _selectionColor, Color.Transparent)
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
                If value = _selectedIndex Then Return
                If value >= -1 AndAlso value < _tabs.Count Then
                      _selectedIndex = value
                      UpdateTabs(False)
                    RaiseEvent SelectionChanged(Me, _selectedIndex)
                End If
            End Set
        End Property

        Public Sub SetTabs(tabs As IEnumerable(Of TabItem))
            Dim previousContent = If(_selectedIndex >= 0 AndAlso _selectedIndex < _tabs.Count, _tabs(_selectedIndex).Content, Nothing)
            _tabs = tabs.ToList()
            If _selectedIndex < 0 OrElse _selectedIndex >= _tabs.Count OrElse Not _tabs(_selectedIndex).IsVisible Then _selectedIndex = _tabs.FindIndex(Function(tab) tab.IsVisible)
            UpdateTabs()
            Dim selectedContent = If(_selectedIndex >= 0 AndAlso _selectedIndex < _tabs.Count, _tabs(_selectedIndex).Content, Nothing)
            If previousContent IsNot selectedContent Then RaiseEvent SelectionChanged(Me, _selectedIndex)
        End Sub
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

        Private Sub UpdateTabs(Optional rebuildHeaders As Boolean = True)
            If Not rebuildHeaders Then
                Dim visibleIndex = 0
                For index = 0 To _tabs.Count - 1
                    If Not _tabs(index).IsVisible Then Continue For
                    _tabHeaders(visibleIndex).BackgroundColor = If(index = _selectedIndex, New Color(50, 50, 50), New Color(35, 35, 35))
                    visibleIndex += 1
                Next
            Else
            ' Clear existing headers
            For Each header In _tabHeaders
                For Each child In header.Children.ToArray()
                    If child IsNot header.TextElement Then header.Children.Remove(child)
                Next
                Children.Remove(header)
                header.Dispose()
            Next
            _tabHeaders.Clear()

            ' Create tab headers
            Dim tabWidth As Single = Size.X / Math.Max(1, _tabs.Where(Function(tab) tab.IsVisible).Count())
            Dim visibleIndex = 0
            For i As Integer = 0 To _tabs.Count - 1
                If Not _tabs(i).IsVisible Then Continue For
                Dim index As Integer = i
                Dim header As New Button(Scene)
                header.CornerRadii = New CornerRadii(4, 4, 0, 0)
                header.AutoSize = ButtonAutoSize.None
                header.Text = If(_tabs(i).HeaderContent Is Nothing, _tabs(i).Header, String.Empty)
                If _tabs(i).HeaderContent IsNot Nothing Then header.Children.Add(_tabs(i).HeaderContent)
                header.Position = New Vector2(Position.X + visibleIndex * tabWidth, Position.Y)
                header.Size = New Vector2(tabWidth, _tabHeaderHeight)
                visibleIndex += 1
                header.isEnabled = _tabs(i).IsEnabled
                header.BackgroundColor = If(i = _selectedIndex, New Color(50, 50, 50), New Color(35, 35, 35))

                AddHandler header.MouseLeftClick, Sub(p)
                                                      SelectedIndex = index
                                                  End Sub

                _tabHeaders.Add(header)
                Children.Add(header)
            Next
            End If

            ' Update content area
            _contentArea.Position = New Vector2(Position.X, Position.Y + _tabHeaderHeight)
            _contentArea.Size = New Vector2(Size.X, Size.Y - _tabHeaderHeight)

            ' Preserve the active subtree when only a header or selection style
            ' changes. Detaching every refresh also discards focus and input.
            Dim content = If(_selectedIndex >= 0 AndAlso _selectedIndex < _tabs.Count, _tabs(_selectedIndex).Content, Nothing)
            If _contentArea.Children.Count <> If(content Is Nothing, 0, 1) OrElse (content IsNot Nothing AndAlso Not _contentArea.Children.Contains(content)) Then
                _contentArea.Children.Clear()
                If content IsNot Nothing Then _contentArea.Children.Add(content)
            End If
        End Sub

        Protected Overrides Sub AlignChildren()
            Dim tabWidth As Single = If(_tabHeaders.Count = 0, Size.X, Size.X / _tabHeaders.Count)
            For index = 0 To _tabHeaders.Count - 1
                _tabHeaders(index).Position = New Vector2(Position.X + index * tabWidth, Position.Y)
                _tabHeaders(index).Size = New Vector2(tabWidth, _tabHeaderHeight)
                Dim header = _tabHeaders(index)
                For Each child In header.Children.Where(Function(element) element IsNot header.TextElement)
                    child.Position = header.Position + New Vector2(6, 2)
                Next
            Next
            _contentArea.Position = New Vector2(Position.X, Position.Y + _tabHeaderHeight)
            _contentArea.Size = New Vector2(Size.X, Math.Max(0, Size.Y - _tabHeaderHeight))
            For Each child In _contentArea.Children
                child.Position = _contentArea.Position
                child.Size = _contentArea.Size
            Next
        End Sub
    End Class

    ''' <summary>
    ''' Represents a single tab in a TabControl.
    ''' </summary>
    Public Class TabItem
        Public Property Header As String
        Public Property HeaderContent As SceneElement
        Public Property Content As SceneElement
        Public Property IsVisible As Boolean = True
        Public Property IsEnabled As Boolean = True
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
        Private _verticalVisibility As Integer = 1
        Private _horizontalVisibility As Integer = 0
        Private _scrollVelocity As Vector2 = Vector2.Zero

        ' Values match Disabled, Auto, Hidden, Visible. Hidden retains scrolling.
        Public Property VerticalScrollBarVisibility As Integer
            Get
                Return _verticalVisibility
            End Get
            Set(value As Integer)
                If value < 0 OrElse value > 3 Then Throw New ArgumentOutOfRangeException(NameOf(value))
                _verticalVisibility = value
                _canScrollVertically = value <> 0
                ClampScrollOffset()
                UpdateContentPosition()
                UpdateScrollbars()
            End Set
        End Property

        Public Property HorizontalScrollBarVisibility As Integer
            Get
                Return _horizontalVisibility
            End Get
            Set(value As Integer)
                If value < 0 OrElse value > 3 Then Throw New ArgumentOutOfRangeException(NameOf(value))
                _horizontalVisibility = value
                _canScrollHorizontally = value <> 0
                ClampScrollOffset()
                UpdateContentPosition()
                UpdateScrollbars()
            End Set
        End Property

        Public ReadOnly Property VerticalScrollBarIsVisible As Boolean
            Get
                Return _verticalScrollbar IsNot Nothing AndAlso _verticalScrollbar.isVisible
            End Get
        End Property

        Public ReadOnly Property HorizontalScrollBarIsVisible As Boolean
            Get
                Return _horizontalScrollbar IsNot Nothing AndAlso _horizontalScrollbar.isVisible
            End Get
        End Property

        Private Function Viewport() As Vector2
            Dim showVertical = _verticalVisibility = 3
            Dim showHorizontal = _horizontalVisibility = 3
            For pass = 0 To 2
                If _content IsNot Nothing Then
                    If _verticalVisibility = 1 Then showVertical = _content.Size.Y > Math.Max(0, Size.Y - If(showHorizontal, _scrollbarWidth, 0))
                    If _horizontalVisibility = 1 Then showHorizontal = _content.Size.X > Math.Max(0, Size.X - If(showVertical, _scrollbarWidth, 0))
                End If
            Next
            Return New Vector2(Math.Max(0, Size.X - If(showVertical, _scrollbarWidth, 0)), Math.Max(0, Size.Y - If(showHorizontal, _scrollbarWidth, 0)))
        End Function

        Public ReadOnly Property ScrollableSize As Vector2
            Get
                If _content Is Nothing Then Return Vector2.Zero
                Dim available = Viewport()
                Return New Vector2(If(_canScrollHorizontally, Math.Max(0, _content.Size.X - available.X), 0), If(_canScrollVertically, Math.Max(0, _content.Size.Y - available.Y), 0))
            End Get
        End Property

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
                _verticalVisibility = If(value, 1, 0)
                UpdateScrollbars()
            End Set
        End Property

        Public Property CanScrollHorizontally As Boolean
            Get
                Return _canScrollHorizontally
            End Get
            Set(value As Boolean)
                _canScrollHorizontally = value
                _horizontalVisibility = If(value, 1, 0)
                UpdateScrollbars()
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = New Color(30, 30, 30)
            Clip = True
            _verticalTrack = New RectangleElement(scene) With {.BackgroundColor = New Color(38, 55, 57), .zIndex = Integer.MaxValue - 1, .isVisible = False}
            _horizontalTrack = New RectangleElement(scene) With {.BackgroundColor = New Color(38, 55, 57), .zIndex = Integer.MaxValue - 1, .isVisible = False}
            Children.Add(_verticalTrack)
            Children.Add(_horizontalTrack)

            _verticalScrollbar = New RectangleElement(scene) With {
                .BackgroundColor = New Color(126, 167, 172),
                .zIndex = Integer.MaxValue,
                .isVisible = False
            }
            Children.Add(_verticalScrollbar)

            _horizontalScrollbar = New RectangleElement(scene) With {
                .BackgroundColor = New Color(126, 167, 172),
                .zIndex = Integer.MaxValue,
                .isVisible = False
            }
            Children.Add(_horizontalScrollbar)
            AddHandler _verticalScrollbar.MouseLeftDown, Sub(p)
                                                            StopScrolling()
                                                            _verticalDragOffset = _scrollOffset.Y
                                                            scene.CapturePointer(_verticalScrollbar)
                                                        End Sub
            AddHandler _horizontalScrollbar.MouseLeftDown, Sub(p)
                                                              StopScrolling()
                                                              _horizontalDragOffset = _scrollOffset.X
                                                              scene.CapturePointer(_horizontalScrollbar)
                                                          End Sub
            AddHandler _verticalScrollbar.MouseLeftUp, Sub(p) scene.ReleasePointerCapture(_verticalScrollbar)
            AddHandler _horizontalScrollbar.MouseLeftUp, Sub(p) scene.ReleasePointerCapture(_horizontalScrollbar)
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
            If elapsed <= 0 OrElse _scrollVelocity = Vector2.Zero Then Return

            Dim before = _scrollOffset
            Dim rate = Math.Max(0.0F, ScrollDeceleration)
            Dim nextVelocity As New Vector2(ApproachZero(_scrollVelocity.X, rate * elapsed), ApproachZero(_scrollVelocity.Y, rate * elapsed))
            ' Integrate deceleration over real frame time, including a slow image
            ' frame. Dropping long frames or using initial velocity for the whole
            ' interval makes wheel travel depend on rendering speed.
            Dim timeX = If(rate > 0, Math.Min(elapsed, Math.Abs(_scrollVelocity.X) / rate), elapsed)
            Dim timeY = If(rate > 0, Math.Min(elapsed, Math.Abs(_scrollVelocity.Y) / rate), elapsed)
            ScrollBy(New Vector2((_scrollVelocity.X + nextVelocity.X) * 0.5F * timeX, (_scrollVelocity.Y + nextVelocity.Y) * 0.5F * timeY))
            If _scrollOffset.X = before.X Then nextVelocity.X = 0
            If _scrollOffset.Y = before.Y Then nextVelocity.Y = 0
            _scrollVelocity = nextVelocity
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

            Dim available = ScrollableSize
            Dim maxScrollX = available.X
            Dim maxScrollY = available.Y

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

            Dim available = Viewport()
            Dim viewportWidth = available.X
            Dim viewportHeight = available.Y

            ' Vertical scrollbar
            If _verticalVisibility = 3 OrElse _verticalVisibility = 1 AndAlso _content.Size.Y > viewportHeight Then
                _verticalScrollbar.isVisible = True
                _verticalTrack.isVisible = True
                _verticalTrack.Position = New Vector2(Position.X + Size.X - _scrollbarWidth, Position.Y)
                _verticalTrack.Size = New Vector2(_scrollbarWidth, Math.Max(0, viewportHeight))
                Dim scrollRatio = If(_content.Size.Y > 0, Math.Min(1, viewportHeight / _content.Size.Y), 1)
                Dim thumbHeight = Math.Min(viewportHeight, Math.Max(20, viewportHeight * scrollRatio))
                Dim thumbOffset = If(_content.Size.Y > viewportHeight, (_scrollOffset.Y / (_content.Size.Y - viewportHeight)) * (viewportHeight - thumbHeight), 0)

                _verticalScrollbar.Position = New Vector2(Position.X + Size.X - _scrollbarWidth, Position.Y + thumbOffset)
                _verticalScrollbar.Size = New Vector2(_scrollbarWidth, thumbHeight)
            Else
                _verticalScrollbar.isVisible = False
                _verticalTrack.isVisible = False
            End If

            ' Horizontal scrollbar
            If _horizontalVisibility = 3 OrElse _horizontalVisibility = 1 AndAlso _content.Size.X > viewportWidth Then
                _horizontalScrollbar.isVisible = True
                _horizontalTrack.isVisible = True
                _horizontalTrack.Position = New Vector2(Position.X, Position.Y + Size.Y - _scrollbarWidth)
                _horizontalTrack.Size = New Vector2(Math.Max(0, viewportWidth), _scrollbarWidth)
                Dim scrollRatio = If(_content.Size.X > 0, Math.Min(1, viewportWidth / _content.Size.X), 1)
                Dim thumbWidth = Math.Min(viewportWidth, Math.Max(20, viewportWidth * scrollRatio))
                Dim thumbOffset = If(_content.Size.X > viewportWidth, (_scrollOffset.X / (_content.Size.X - viewportWidth)) * (viewportWidth - thumbWidth), 0)

                _horizontalScrollbar.Position = New Vector2(Position.X + thumbOffset, Position.Y + Size.Y - _scrollbarWidth)
                _horizontalScrollbar.Size = New Vector2(thumbWidth, _scrollbarWidth)
            Else
                _horizontalScrollbar.isVisible = False
                _horizontalTrack.isVisible = False
            End If
        End Sub

        Protected Overrides Sub AlignChildren()
            If _content IsNot Nothing AndAlso _content.ImportedMeasure IsNot Nothing Then
                ' Imported stacks keep their natural scroll extent, while the
                ' non-scrolling axis follows the viewport instead of a stale
                ' parser default. Re-measure if automatic scrollbars consume space.
                For pass = 0 To 2
                    Dim available = Viewport()
                    Dim desired = _content.ImportedMeasure.Invoke(Math.Max(1, available.X))
                    Dim arranged As New Vector2(If(_canScrollHorizontally, Math.Max(available.X, desired.X), available.X),
                                               If(_canScrollVertically, Math.Max(available.Y, desired.Y), available.Y))
                    If arranged = _content.Size Then Exit For
                    _content.Size = arranged
                Next
            End If
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
        Private _expandedHeight As Single = 150.0F

        Public Event ExpandedChanged(sender As Expander, isExpanded As Boolean)
        Public ReadOnly Property HeaderElement As Button
            Get
                Return _headerButton
            End Get
        End Property

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
                If _isExpanded = value Then Return
                If Not value Then _expandedHeight = Math.Max(Size.Y, _headerHeight)
                _isExpanded = value
                If value Then Size = New Vector2(Size.X, _expandedHeight)
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

        Protected Overrides Sub AlignChildren()
            UpdateLayout()
        End Sub

        Private Sub UpdateLayout()
            If _headerButton IsNot Nothing Then
                _headerButton.AutoSize = ButtonAutoSize.None
                _headerButton.Position = Position
                _headerButton.Size = New Vector2(Size.X, _headerHeight)
            End If

            If _contentContainer IsNot Nothing Then
                _contentContainer.Position = New Vector2(Position.X, Position.Y + _headerHeight)
                _contentContainer.Size = New Vector2(Size.X, Math.Max(0, Size.Y - _headerHeight))
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
            _textElement.Position = position + New Vector2(6, 4)
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

        Public ReadOnly Property HeaderHeight As Single
            Get
                Return _headerHeight
            End Get
        End Property

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
        Protected Overrides Sub AlignChildren()
            _headerText.Position = New Vector2(Position.X + 10, Position.Y + 2)
            _contentArea.Position = New Vector2(Position.X + 4 + Padding.Left, Position.Y + _headerHeight + Padding.Top)
            _contentArea.Size = New Vector2(Math.Max(0, Size.X - 8 - Padding.Left - Padding.Right), Math.Max(0, Size.Y - _headerHeight - 4 - Padding.Top - Padding.Bottom))
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
        Protected Overrides Sub AlignChildren()
            Dim buttonWidth = Math.Min(24.0F, Size.X)
            _valueText.Position = New Vector2(Position.X + 4, Position.Y + 4)
            _upButton.AutoSize = ButtonAutoSize.None
            _downButton.AutoSize = ButtonAutoSize.None
            _upButton.Position = New Vector2(Position.X + Size.X - buttonWidth, Position.Y)
            _downButton.Position = New Vector2(Position.X + Size.X - buttonWidth, Position.Y + Size.Y / 2)
            _upButton.Size = New Vector2(buttonWidth, Size.Y / 2)
            _downButton.Size = _upButton.Size
        End Sub
    End Class

End Namespace
