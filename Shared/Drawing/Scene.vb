#Region "Using Statements"
Imports System.Collections.Generic
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.Shared.Content
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Advanced
Imports System.Collections
Imports ProjectZ.Shared.Drawing
Imports SocketJack.Extensions

#End Region

Namespace [Shared].Drawing

    Public MustInherit Partial Class Scene
        Implements IDisposable

#Region "Properties"

        Friend renderTarget As RenderTarget2D

        Public Function sender() As Game
            Return _sender
        End Function
        Private _sender As Game
        Public ReadOnly Property MouseState As MouseState
            Get
                Return _MouseState
            End Get
        End Property

        Private _MouseState As MouseState
        Friend Property CurrentKeyboardState As KeyboardState
        Public Property SpriteSettings As XNA.SpriteBatchPropertySet
        Public isInitialized As Boolean = False
        Public Property isCursorVisible As Boolean = False
        Public Property UseRenderTarget As Boolean = False
        Public Property BackgroundColor As Color = New Color(0, 0, 0, 0)
        Public Effects As List(Of Effect)

        Public ReadOnly Property MousePosition As Point
            Get
                Return _MouseState.Position
            End Get
        End Property

        Public FPS As Integer = 60
        Public DrawFPS As Integer = 60
        Public Property EnableFrameProfiling As Boolean
        Public Property EnableDesignTimeControls As Boolean = True
        Private ReadOnly renderPaths As New Dictionary(Of SceneElement, List(Of SceneElement))
        Public ReadOnly Property LastLayoutMilliseconds As Double
        Public ReadOnly Property LastRenderMilliseconds As Double
        Public ReadOnly Property LastElementTickMilliseconds As Double
        Private ReadOnly drawCosts As New Dictionary(Of String, Double)
        Private ReadOnly drawCounts As New Dictionary(Of String, Integer)
        Public ReadOnly Property ElementDrawProfile As String
            Get
                Return String.Join(";", drawCosts.OrderByDescending(Function(p) p.Value).Take(8).Select(Function(p) p.Key & "=" & p.Value.ToString("0.000")))
            End Get
        End Property
        Public ReadOnly Property ElementDrawCounts As String
            Get
                Return String.Join(";", drawCounts.Select(Function(p) p.Key & "=" & p.Value.ToString()))
            End Get
        End Property

        Public ReadOnly Property Elements As IList(Of SceneElement)
            Get
                Return I_Elements.Values
            End Get
        End Property

#End Region

#Region "Internals"

        Protected Friend hasBegun As Boolean = False

        Protected Friend gameTime As New GameTime

#Region "Constants"

        Private Const OneSecond As Long = 10000000

#End Region

        Protected Friend RenderTargetOptions As XNA.SpriteBatchPropertySet

        Protected Friend Texture As Texture2D

        Private LastTick As Long = 0

        Private LastSelected As SceneElement
        Private KeyboardButton As UI.Input.Button
        Private KeyboardButtonKey As Keys
        Public ReadOnly Property FocusedElement As SceneElement
            Get
                Return LastSelected
            End Get
        End Property

        Private LastMouseDrag As SceneElement

        Public Cursor As PolygonElement

        Private CursorBorder As PolygonElement

        Private InternalFPS As Integer = 0
        Private Property I_Elements As New SortedList(Of Integer, SceneElement)
        Private Property GUID_INDEX As New SortedList(Of String, Integer)
        Private Property RemoveQueue As New List(Of Integer)
        Private Property AddQueue As New List(Of SceneElement)
        Private Property BringToFrontQueue As New List(Of SceneElement)
        Private ReadOnly RenderOrderCache As New List(Of SceneElement)()
        Private RenderOrderDirty As Boolean = True
        Protected Friend ReadOnly Property graphicsDevice As GraphicsDevice
            Get
                Return CType(sender.Services.GetService(GetType(GraphicsDevice)), GraphicsDevice)
            End Get
        End Property
        Protected Friend ReadOnly Property contentCollection As ContentContainer
            Get
                Return CType(sender.Services.GetService(GetType(ContentContainer)), ContentContainer)
            End Get
        End Property
        Private spriteBatch As SpriteBatch

        ' Cached RasterizerState to avoid creating new instances every frame
        Private _scissorRasterizerState As RasterizerState
#End Region

#Region "Events"

#Region "Projection Hosts"

        Private ProjectionHosts As New List(Of SceneProjectionHost)

        Public Sub AddProjectionHost(Target As SceneProjectionHost)
            ProjectionHosts.Add(Target)
        End Sub

        Public Sub RemoveProjectionHost(Target As SceneProjectionHost)
            ProjectionHosts.Remove(Target)
        End Sub

        Public Function ContainsProjectionHost(Target As SceneProjectionHost) As Boolean
            Return ProjectionHosts.Contains(Target)
        End Function

#End Region

        Public Event Initialized(gameTime As GameTime)

        Public Event PreDraw(gameTime As GameTime)
        Public Event PostDraw(gameTime As GameTime)

        Public Event OnKeyPress(Key As Keys, KeyboardState As KeyboardState)
        Public Event OnKeyDown(Key As Keys, KeyboardState As KeyboardState)
        Public Event OnKeyUp(Key As Keys, KeyboardState As KeyboardState)

        Public Event OnMouseMove(currentPoint As Point, lastPoint As Point)
        Public Event OnMouseDrag(currentPoint As Point, lastPoint As Point)
        Public Event OnMouseDragDrop(currentPoint As Point, lastPoint As Point)


        Public Event OnMouseRightClick(p As Point)
        Public Event OnMouseLeftClick(p As Point)


        Public Event OnMouseLeftDown(p As Point)
        Public Event OnMouseLeftUp(p As Point)
        Public Event OnMouseWheel(delta As Integer, p As Point)
        ''' <summary>Raised when the native window loses pointer ownership or focus.</summary>
        Public Event OnPointerCancelled()
        Public Event ViewportResized(oldSize As Point, newSize As Point)

        ''' <summary>Called after the DX backbuffer is synchronized to a resized client area.</summary>
        Public Overridable Sub ResizeViewport(oldSize As Point, newSize As Point)
            RaiseEvent ViewportResized(oldSize, newSize)
            If Cursor IsNot Nothing Then Cursor.Size = New Vector2(24)
            If CursorBorder IsNot Nothing Then CursorBorder.Size = New Vector2(24)
        End Sub

        Private MouseDownPoint As Point
        Private MouseIsDown As Boolean = False  ' Proper flag for tracking mouse state
        Private LastElement As SceneElement
        Private DragDropElement As SceneElement  ' Saved for DragDrop event after MouseUp
        Private ClickTargetElement As SceneElement  ' Element that should receive click event
        Private HoveredElement As SceneElement  ' Currently hovered element for reliable enter/leave

        Protected Friend Sub MouseLeftDown(p As Point)
            RaiseEvent OnMouseLeftDown(p)
            _MouseLeftDown(PointToElement(p), p)
        End Sub

        Private Sub _MouseLeftDown(Element As SceneElement, p As Point)
            If Element IsNot Nothing AndAlso Element.isEnabled Then
                LastElement = Element
                ClickTargetElement = Element  ' Save for click event
                DragDropElement = Element  ' Save for potential drag-drop
                MouseDownPoint = p
                MouseIsDown = True
                Element.OnMouseLeftDown(p.Subtract(Element.Position))
                If LastSelected Is Nothing Then
                    If Element.CanSelect Then
                        Element.isSelected = True
                        LastSelected = Element
                    End If
                End If
            Else
                ClickTargetElement = Nothing
                DragDropElement = Nothing
                If LastSelected IsNot Nothing AndAlso LastSelected.isEnabled Then
                    If LastSelected.CanSelect Then
                        LastSelected.isSelected = False
                    End If
                    LastSelected = Nothing
                End If
            End If
            If LastSelected IsNot Nothing AndAlso LastSelected.isEnabled Then
                If LastSelected.CanSelect AndAlso LastSelected IsNot Element Then
                    LastSelected.isSelected = False
                    If Element IsNot Nothing AndAlso Element.isEnabled Then
                        If Element.CanSelect AndAlso Not Element.isSelected Then
                            Element.isSelected = True
                            LastSelected = Element
                        End If
                    End If
                End If
            End If
            ProjectionHosts.ForEach(Sub(s) s.TargetScene.MouseLeftDown(s.Interp(p)))
        End Sub

        Protected Friend Sub MouseLeftUp(p As Point)
            RaiseEvent OnMouseLeftUp(p)

            ' Store reference before clearing
            Dim elementToRelease As SceneElement = LastElement

            ' Always notify the element that received mouse down
            If elementToRelease IsNot Nothing AndAlso elementToRelease.isEnabled Then
                elementToRelease.OnMouseLeftUp(p.Subtract(elementToRelease.Position))
                elementToRelease.OnUserInvalidated()
            End If

            ' Reset state but keep ClickTargetElement and DragDropElement for their respective events
            MouseIsDown = False
            MouseDownPoint = Point.Zero
            LastElement = Nothing
            LastMouseDrag = Nothing

            ProjectionHosts.ForEach(Sub(s) s.TargetScene.MouseLeftUp(s.Interp(p)))
        End Sub

        Protected Friend Sub MouseLeftClick(p As Point)
            RaiseEvent OnMouseLeftClick(p)

            ' Use the element that received MouseDown, not a new hit test
            ' This ensures clicks are reliable even if mouse moved slightly
            Dim Element As SceneElement = ClickTargetElement
            If Element IsNot Nothing AndAlso Element.isEnabled Then
                Element.OnMouseLeftClick(p.Subtract(Element.Position))
            End If

            ' Clear the click target after processing
            ClickTargetElement = Nothing

            ProjectionHosts.ForEach(Sub(s) s.TargetScene.MouseLeftClick(s.Interp(p)))
        End Sub

        Protected Friend Sub MouseRightClick(p As Point)
            RaiseEvent OnMouseRightClick(p)
            Dim Element As SceneElement = PointToElement(p)
            If Element IsNot Nothing AndAlso Element.isEnabled Then
                Element.OnMouseRightClick(p.Subtract(Element.Position))
                Element.OnUserInvalidated()
            End If

            ProjectionHosts.ForEach(Sub(s) s.TargetScene.MouseRightClick(s.Interp(p)))
        End Sub

        Protected Friend Sub MouseWheel(delta As Integer, p As Point)
            RaiseEvent OnMouseWheel(delta, p)
            Dim element = PointToElement(p)
            ' Wheel input bubbles so a ScrollViewer still receives it when its
            ' content (text, images, buttons, etc.) is the topmost hit target.
            Dim current = element
            While current IsNot Nothing
                If current.isEnabled Then
                    current.OnMouseWheel(delta, p.Subtract(current.Position))
                    current.OnUserInvalidated()
                End If
                current = current.Parent
            End While
            ProjectionHosts.ForEach(Sub(s) s.TargetScene.MouseWheel(delta, s.Interp(p)))
        End Sub

        Protected Friend Sub MouseDragDrop(currentPoint As Point, lastPoint As Point)
            RaiseEvent OnMouseDragDrop(currentPoint, lastPoint)

            ' Use DragDropElement (set during MouseDown) or LastMouseDrag (set during drag) as fallback
            Dim draggedElement As SceneElement = If(DragDropElement, LastMouseDrag)
            If draggedElement IsNot Nothing Then
                Dim dropTarget As SceneElement = PointToElement(currentPoint)
                draggedElement.OnMouseDragDrop(currentPoint, dropTarget)
                draggedElement.OnUserInvalidated()
            End If

            ' Reset drag state
            DragDropElement = Nothing
            LastMouseDrag = Nothing

            ProjectionHosts.ForEach(Sub(s) s.TargetScene.MouseDragDrop(s.Interp(currentPoint), s.Interp(lastPoint)))
        End Sub

        Protected Friend Sub MouseDrag(currentPoint As Point, lastPoint As Point)
            RaiseEvent OnMouseDrag(currentPoint, lastPoint)

            ' Use the element that was originally clicked (LastElement) or LastMouseDrag
            ' Don't do a new hit test - the element being dragged should be the one that received MouseDown
            Dim elementToDrag As SceneElement = If(LastMouseDrag, LastElement)

            If elementToDrag IsNot Nothing AndAlso elementToDrag.isEnabled AndAlso elementToDrag.isMouseDown Then
                Dim r_currentPoint As Point = currentPoint.Subtract(elementToDrag.Position)
                Dim r_lastPoint As Point = lastPoint.Subtract(elementToDrag.Position)

                ' Check for drag over other elements
                Dim hoverElement As SceneElement = PointToElement(currentPoint)
                If hoverElement IsNot elementToDrag Then
                    elementToDrag.OnMouseDragOver(r_currentPoint, hoverElement)
                End If

                elementToDrag.OnMouseDrag(r_currentPoint, r_lastPoint)
                LastMouseDrag = elementToDrag
            End If

            ProjectionHosts.ForEach(Sub(s) s.TargetScene.MouseDrag(s.Interp(currentPoint), s.Interp(lastPoint)))
        End Sub

        Protected Friend Sub MouseMove(currentPoint As Point, lastPoint As Point)
            RaiseEvent OnMouseMove(currentPoint, lastPoint)
            If Cursor IsNot Nothing AndAlso isCursorVisible Then
                Cursor.Position = New Vector2(currentPoint.X + 1, currentPoint.Y + 1)
                CursorBorder.Position = New Vector2(currentPoint.X, currentPoint.Y)
            End If

            Dim currentElement As SceneElement = PointToElement(currentPoint)

            ' Handle mouse leave - use tracked HoveredElement for reliable state management
            If HoveredElement IsNot Nothing AndAlso HoveredElement IsNot currentElement Then
                If HoveredElement.isMouseOver Then
                    HoveredElement.isMouseOver = False
                End If
            End If

            ' Handle mouse enter - set isMouseOver on the element we're over
            If currentElement IsNot Nothing AndAlso currentElement.isEnabled Then
                If Not currentElement.isMouseOver Then currentElement.isMouseOver = True
                currentElement.OnMouseMove(currentPoint.Subtract(currentElement.Position), lastPoint.Subtract(currentElement.Position))
            End If

            ' Track the currently hovered element
            HoveredElement = currentElement

            ProjectionHosts.ForEach(Sub(s) s.TargetScene.MouseMove(s.Interp(currentPoint), s.Interp(lastPoint)))
        End Sub

        Protected Friend Sub SetMouseState(State As MouseState)
            _MouseState = State
            ProjectionHosts.ForEach(Sub(s) s.TargetScene.SetMouseState(s.Interp(State)))
        End Sub

        Protected Friend Sub CancelPointerInput()
            KeyboardButton = Nothing
            RaiseEvent OnPointerCancelled()
            If LastElement IsNot Nothing AndAlso LastElement.isEnabled AndAlso LastElement.isMouseDown Then
                LastElement.OnMouseLeftUp(New Point(-1, -1))
            End If
            If HoveredElement IsNot Nothing AndAlso HoveredElement.isMouseOver Then
                HoveredElement.isMouseOver = False
            End If
            MouseIsDown = False
            MouseDownPoint = Point.Zero
            LastElement = Nothing
            LastMouseDrag = Nothing
            DragDropElement = Nothing
            ClickTargetElement = Nothing
            HoveredElement = Nothing
            ProjectionHosts.ForEach(Sub(s) s.TargetScene.CancelPointerInput())
        End Sub

        Protected Friend Sub KeyPress(Key As Keys, KeyboardState As KeyboardState)
            CurrentKeyboardState = KeyboardState
            If Key = Keys.Tab Then Return
            RaiseEvent OnKeyPress(Key, KeyboardState)
            If LastSelected IsNot Nothing AndAlso LastSelected.isEnabled Then
                If LastSelected.CanSelect AndAlso LastSelected.isSelected Then
                    LastSelected.KeyPress(Key, KeyboardState)
                End If
            End If

            ProjectionHosts.ForEach(Sub(s) s.TargetScene.KeyPress(Key, KeyboardState))
        End Sub

        Protected Friend Sub KeyDown(Key As Keys, KeyboardState As KeyboardState)
            CurrentKeyboardState = KeyboardState
            If Key = Keys.Tab Then
                MoveFocus(KeyboardState.IsKeyDown(Keys.LeftShift) OrElse KeyboardState.IsKeyDown(Keys.RightShift))
                Return
            End If
            If (Key = Keys.Enter OrElse Key = Keys.Space) AndAlso TypeOf LastSelected Is UI.Input.Button AndAlso CanFocus(LastSelected) Then
                KeyboardButton = DirectCast(LastSelected, UI.Input.Button)
                KeyboardButtonKey = Key
                Return
            End If
            RaiseEvent OnKeyDown(Key, KeyboardState)
            If LastSelected IsNot Nothing AndAlso LastSelected.isEnabled Then
                If LastSelected.CanSelect AndAlso LastSelected.isSelected Then
                    LastSelected.KeyDown(Key, KeyboardState)
                End If
            End If
            ProjectionHosts.ForEach(Sub(s) s.TargetScene.KeyDown(Key, KeyboardState))
        End Sub

        Protected Friend Sub KeyUp(Key As Keys, KeyboardState As KeyboardState)
            CurrentKeyboardState = KeyboardState
            If Key = Keys.Tab Then Return
            If KeyboardButton IsNot Nothing AndAlso Key = KeyboardButtonKey Then
                Dim target = KeyboardButton
                KeyboardButton = Nothing
                If target Is LastSelected AndAlso target.isSelected AndAlso CanFocus(target) Then target.OnMouseLeftClick(New Point(CInt(target.Size.X / 2), CInt(target.Size.Y / 2)))
                Return
            End If
            RaiseEvent OnKeyUp(Key, KeyboardState)
            If LastSelected IsNot Nothing AndAlso LastSelected.isEnabled Then
                If LastSelected.CanSelect AndAlso LastSelected.isSelected Then
                    LastSelected.KeyUp(Key, KeyboardState)
                    LastSelected.OnUserInvalidated()
                End If
            End If
            ProjectionHosts.ForEach(Sub(s) s.TargetScene.KeyUp(Key, KeyboardState))
        End Sub

#End Region

#Region "Drawing Methods"

        Protected Friend WhitePlain As Texture2D

        Public Function MeasureText(Font As String, Text As String) As Vector2
            Dim vectorFont As VectorFont = Nothing
            If contentCollection.VectorFonts.TryGetValue(Font, vectorFont) Then Return vectorFont.Measure(Text)
            Return contentCollection.Fonts(Font).MeasureString(SanitizeText(Font, Text))
        End Function

        Public Function TextLineSpacing(font As String) As Integer
            Dim vectorFont As VectorFont = Nothing
            If contentCollection.VectorFonts.TryGetValue(font, vectorFont) Then Return vectorFont.LineSpacing
            Return contentCollection.Fonts(font).LineSpacing
        End Function

        ''' <summary>
        ''' Replaces glyphs missing from a compiled SpriteFont instead of letting
        ''' arbitrary chat, clipboard, or filename text crash the render thread.
        ''' Newlines are retained because SpriteFont handles them as layout tokens.
        ''' </summary>
        Private ReadOnly fontCharacters As New Dictionary(Of SpriteFont, HashSet(Of Char))

        Public Function SanitizeText(Font As String, text As String) As String
            If String.IsNullOrEmpty(text) Then Return If(text, String.Empty)
            If contentCollection.VectorFonts.ContainsKey(Font) Then Return text
            Dim spriteFont = contentCollection.Fonts(Font)
            Dim supported As HashSet(Of Char) = Nothing
            If Not fontCharacters.TryGetValue(spriteFont, supported) Then
                supported = New HashSet(Of Char)(spriteFont.Characters)
                fontCharacters.Add(spriteFont, supported)
            End If
            Dim replacement As Char = If(supported.Contains("?"c), "?"c, " "c)
            Dim chars As Char() = Nothing
            For index = 0 To text.Length - 1
                Dim value = text(index)
                If value <> ControlChars.Cr AndAlso value <> ControlChars.Lf AndAlso
                   value <> ControlChars.Tab AndAlso Not supported.Contains(value) Then
                    If chars Is Nothing Then chars = text.ToCharArray()
                    chars(index) = replacement
                End If
            Next
            Return If(chars Is Nothing, text, New String(chars))
        End Function

#End Region

        ''' <summary>
        ''' Finds the topmost element at the given point, checking children hierarchically.
        ''' </summary>
        Public Function PointToElement(p As Point) As SceneElement
            ' The render list already contains every descendant.  Walking each
            ' registered item recursively tests children multiple times and can
            ' select a low-z sibling through a parent after a popup was tested.
            ' Use the exact same hierarchical ordering as drawing, in reverse.
            Dim ordered = GetRenderOrderedElements()
            For i As Integer = ordered.Count - 1 To 0 Step -1
                Dim element = ordered(i)
                If Not element.ContainsPoint(p) Then Continue For
                ' Reject off-point geometry before walking the parent chain.
                If Not IsEffectivelyVisible(element) OrElse Not IsEffectivelyEnabled(element) Then Continue For
                If Not element.isMouseBypassEnabled Then Return element

                ' Ancestors already have their own slots in this render list.
                ' A bypassed overlay must not promote its parent ahead of an
                ' interactive sibling underneath it (for example a close button).
            Next
            Return Nothing
        End Function

        ''' <summary>
        ''' Recursively hit tests an element and its children.
        ''' Children are tested first (in reverse z-order) since they render on top.
        ''' Returns the element that should receive the click, or Nothing if no hit.
        ''' </summary>
        Private Function HitTestElement(element As SceneElement, p As Point) As SceneElement
            If Not IsEffectivelyVisible(element) OrElse Not IsEffectivelyEnabled(element) Then Return Nothing

            ' First check if the point is even within this element's bounds
            Dim elementContainsPoint As Boolean = element.ContainsPoint(p)

            ' Check children (they're on top of the parent)
            ' Sort by z-index descending, then by add order descending
            If element.Children.Count > 0 Then
                Dim sortedChildren = element.Children.OrderByDescending(Function(x) x.zIndex).ThenByDescending(Function(x) element.Children.IndexOf(x)).ToList()
                For Each child In sortedChildren
                    Dim childHit = HitTestElement(child, p)
                    If childHit IsNot Nothing Then
                        ' If the child hit has isMouseBypassEnabled, return the parent instead
                        If childHit.isMouseBypassEnabled Then
                            ' Return this parent element if it doesn't also have bypass
                            If Not element.isMouseBypassEnabled Then
                                Return element
                            End If
                            ' Parent also has bypass, skip and let the parent's parent handle it
                            Continue For
                        End If
                        Return childHit
                    End If

                    ' Child returned Nothing, but check if the child itself contains the point and has bypass enabled
                    ' This handles the case where the child's own ContainsPoint check passed but it returned Nothing due to bypass
                    If child.isMouseBypassEnabled AndAlso child.ContainsPoint(p) Then
                        If Not element.isMouseBypassEnabled Then
                            Return element
                        End If
                    End If
                Next
            End If

            ' Then check this element
            If Not element.isMouseBypassEnabled AndAlso elementContainsPoint Then
                Return element
            End If

            Return Nothing
        End Function

        ''' <summary>
        ''' Gets all elements at the given point, checking children hierarchically.
        ''' </summary>
        Public Function PointToElements(p As Point) As SceneElement()
            Dim Elements As New List(Of SceneElement)
            Dim ordered = GetRenderOrderedElements()
            For i As Integer = ordered.Count - 1 To 0 Step -1
                Dim element = ordered(i)
                If Not element.ContainsPoint(p) Then Continue For
                If IsEffectivelyVisible(element) AndAlso IsEffectivelyEnabled(element) AndAlso
                   Not element.isMouseBypassEnabled Then Elements.Add(element)
            Next
            Return Elements.ToArray
        End Function

        ''' <summary>
        ''' Recursively collects all elements at the given point.
        ''' </summary>
        Private Sub HitTestElementAll(element As SceneElement, p As Point, results As List(Of SceneElement))
            If Not IsEffectivelyVisible(element) OrElse Not IsEffectivelyEnabled(element) Then Return

            Dim elementContainsPoint As Boolean = element.ContainsPoint(p)

            ' Check children first (they're on top)
            If element.Children.Count > 0 Then
                Dim sortedChildren = element.Children.OrderByDescending(Function(x) x.zIndex).ThenByDescending(Function(x) element.Children.IndexOf(x)).ToList()
                For Each child In sortedChildren
                    HitTestElementAll(child, p, results)
                Next
            End If

            ' Then check this element
            If Not element.isMouseBypassEnabled AndAlso elementContainsPoint Then
                results.Add(element)
            End If
        End Sub

        ''' <summary>
        ''' Adds an element and its existing descendants to the scene draw list.
        ''' ChildCollection only raises ChildAdded for children appended after their
        ''' parent is live, so parsed XAML trees must be walked here as well.
        ''' </summary>
        Public Sub AddElement(Element As SceneElement)
            QueueElementTree(Element)
        End Sub

        ''' <summary>Selects an input element for an embedding host.</summary>
        Public Sub FocusElement(element As SceneElement)
            If Not CanFocus(element) Then Return
            KeyboardButton = Nothing
            If LastSelected IsNot Nothing AndAlso LastSelected IsNot element AndAlso LastSelected.CanSelect Then
                LastSelected.isSelected = False
            End If
            LastSelected = element
            element.isSelected = True
            element.OnUserInvalidated()
        End Sub

        Private Function CanFocus(element As SceneElement) As Boolean
            Return element IsNot Nothing AndAlso element.CanSelect AndAlso ContainsElement(element) AndAlso IsEffectivelyVisible(element) AndAlso IsEffectivelyEnabled(element)
        End Function

        ''' <summary>Move through visible enabled tab stops, wrapping at either end.</summary>
        Public Function MoveFocus(Optional backwards As Boolean = False) As Boolean
            Dim targets = GetRenderOrderedElements().Where(Function(e) e.TabStop AndAlso CanFocus(e)).OrderBy(Function(e) e.TabIndex).ToList()
            If targets.Count = 0 Then Return False
            Dim index = targets.IndexOf(LastSelected)
            If index < 0 Then
                index = If(backwards, targets.Count - 1, 0)
            Else
                index = (index + If(backwards, -1, 1) + targets.Count) Mod targets.Count
            End If
            FocusElement(targets(index))
            Return True
        End Function

        Private Sub QueueElementTree(Element As SceneElement)
            If Element Is Nothing OrElse Element.IsDisposed Then Return
            If Not AddQueue.Contains(Element) AndAlso Not ContainsElement(Element) Then
                AddQueue.Add(Element)
            End If
            For Each child As SceneElement In Element.Children
                QueueElementTree(child)
            Next
        End Sub

        ''' <summary>
        ''' Removes a root element from the scene.
        ''' </summary>
        Public Sub RemoveElement(Element As SceneElement)
            For Each child As SceneElement In Element.Children
                RemoveElement(child)
            Next
            ' Use GUID_INDEX to find the actual z-index in case Element.zIndex is stale
            If GUID_INDEX.ContainsKey(Element.GUID) Then
                Dim actualZIndex As Integer = GUID_INDEX(Element.GUID)
                RemoveQueue.Add(actualZIndex)
            End If
            ' Also remove from AddQueue if pending
            If AddQueue.Contains(Element) Then
                AddQueue.Remove(Element)
            End If
        End Sub

        Public Overloads Function ContainsElement(Element As SceneElement) As Boolean
            Return ContainsElement(Element.GUID)
        End Function

        Public Overloads Function ContainsElement(GUID As String) As Boolean
            If GUID_INDEX.ContainsKey(GUID) Then
                Dim zIndex As Integer = GUID_INDEX(GUID)
                Return I_Elements.ContainsKey(zIndex)
            End If
            Return False
        End Function

        Public Overloads Function GetElement(GUID As String) As SceneElement
            Return I_Elements(GUID_INDEX(GUID))
        End Function

        Public Overloads Function GetElement(zIndex As Integer) As SceneElement
            Return I_Elements(zIndex)
        End Function

        Private Overloads Sub DrawElements(Elements As IEnumerable(Of SceneElement))
            DrawElements(Elements, False)
        End Sub

        ''' <summary>
        ''' Draws elements with clipping support.
        ''' </summary>
        Private Overloads Sub DrawElements(Elements As IEnumerable(Of SceneElement), ForceClip As Boolean)
            Dim profileStarted = If(EnableFrameProfiling, Diagnostics.Stopwatch.GetTimestamp(), 0L)
            ' Cache graphics device reference outside the loop
            Dim gd As GraphicsDevice = spriteBatch.GraphicsDevice
            Dim viewportRect As New Rectangle(0, 0, CInt(gd.Viewport.Width / Quality.RenderScale.X), CInt(gd.Viewport.Height / Quality.RenderScale.Y))
            Dim ordered = GetRenderOrderedElements()

            ' The scene stores descendants in a flat draw list, but layout is a
            ' tree operation. Validate each root once so large scroll viewers do
            ' not recursively revisit the same cards for every flat descendant.
            For Each root In ordered
                If root.Parent Is Nothing AndAlso IsEffectivelyVisible(root) Then
                    root.ValidationCheck()
                End If
            Next

            ' Elements are registered flat for ticking/removal, but their visual
            If EnableFrameProfiling Then
                _LastLayoutMilliseconds = Diagnostics.Stopwatch.GetElapsedTime(profileStarted).TotalMilliseconds
                profileStarted = Diagnostics.Stopwatch.GetTimestamp()
            End If
            ' order is hierarchical.  Sorting here preserves Panel.ZIndex inside
            ' each parent and promotes an IsOverlay ancestor together with all of
            ' its descendants above ordinary content added at any later time.
            If ordered.Any(Function(element) element.VisualEffect IsNot Nothing AndAlso element.VisualEffect.IsActive AndAlso IsEffectivelyVisible(element)) Then
                DrawXamlEffectCanvas(ordered, gd, viewportRect, ForceClip)
            Else
                For Each E As SceneElement In ordered
                    If Not IsEffectivelyVisible(E) Then Continue For
                    DrawElementRecursive(E, gd, viewportRect, ForceClip)
                Next
            End If
            If EnableFrameProfiling Then _LastRenderMilliseconds = Diagnostics.Stopwatch.GetElapsedTime(profileStarted).TotalMilliseconds
        End Sub

        Private Function GetRenderOrderedElements() As List(Of SceneElement)
            If RenderOrderDirty Then
                RenderOrderCache.Clear()
                RenderOrderCache.AddRange(I_Elements.Values)
                renderPaths.Clear()
                For Each element In RenderOrderCache
                    renderPaths(element) = GetAncestorPath(element)
                Next
                RenderOrderCache.Sort(AddressOf CompareRenderOrder)
                renderPaths.Clear()
                RenderOrderDirty = False
            End If
            Return RenderOrderCache
        End Function

        Friend Sub InvalidateRenderOrder()
            RenderOrderDirty = True
        End Sub

        ''' <summary>Diagnostics for importer/popup acceptance tests.</summary>
        Public Function GetRenderOrderIndex(element As SceneElement) As Integer
            If element Is Nothing Then Return -1
            Return GetRenderOrderedElements().IndexOf(element)
        End Function

        Private Function CompareRenderOrder(left As SceneElement, right As SceneElement) As Integer
            If Object.ReferenceEquals(left, right) Then Return 0

            Dim overlayOrder = IsOverlayElement(left).CompareTo(IsOverlayElement(right))
            If overlayOrder <> 0 Then Return overlayOrder

            Dim leftPath = renderPaths(left)
            Dim rightPath = renderPaths(right)
            Dim common = Math.Min(leftPath.Count, rightPath.Count)
            Dim index = 0
            While index < common AndAlso Object.ReferenceEquals(leftPath(index), rightPath(index))
                index += 1
            End While

            ' Parents paint before their children.
            If index = common Then Return leftPath.Count.CompareTo(rightPath.Count)

            Dim leftNode = leftPath(index)
            Dim rightNode = rightPath(index)
            Dim zOrder = leftNode.zIndex.CompareTo(rightNode.zIndex)
            If zOrder <> 0 Then Return zOrder

            Dim leftSibling = GetSiblingOrder(leftNode)
            Dim rightSibling = GetSiblingOrder(rightNode)
            Dim siblingOrder = leftSibling.CompareTo(rightSibling)
            If siblingOrder <> 0 Then Return siblingOrder

            Return GetRegistrationOrder(leftNode).CompareTo(GetRegistrationOrder(rightNode))
        End Function

        Private Shared Function GetAncestorPath(element As SceneElement) As List(Of SceneElement)
            Dim path As New List(Of SceneElement)()
            Dim current = element
            While current IsNot Nothing
                path.Add(current)
                current = current.Parent
            End While
            path.Reverse()
            Return path
        End Function

        Private Shared Function GetSiblingOrder(element As SceneElement) As Integer
            If element.Parent Is Nothing Then Return 0
            Return element.Parent.Children.IndexOf(element)
        End Function

        Private Function GetRegistrationOrder(element As SceneElement) As Integer
            Dim value As Integer
            If element IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(element.GUID) AndAlso
               GUID_INDEX.TryGetValue(element.GUID, value) Then Return value
            Return Integer.MinValue
        End Function

        Friend Sub BringElementToFront(Element As SceneElement)
            If Element Is Nothing OrElse BringToFrontQueue.Contains(Element) Then Return
            RemoveElement(Element)
            BringToFrontQueue.Add(Element)
            InvalidateRenderOrder()
        End Sub

        ''' <summary>
        ''' Child controls are also registered in the scene's flat render list.  A
        ''' hidden parent must therefore suppress every descendant explicitly.
        ''' </summary>
        Private Shared Function IsEffectivelyVisible(element As SceneElement) As Boolean
            Dim current = element
            While current IsNot Nothing
                If current.IsDisposed OrElse Not current.isVisible OrElse current.IsLayoutVirtualized Then Return False
                current = current.Parent
            End While
            Return True
        End Function

        Private Shared Function IsEffectivelyEnabled(element As SceneElement) As Boolean
            Dim current = element
            While current IsNot Nothing
                If Not current.isEnabled Then Return False
                current = current.Parent
            End While
            Return True
        End Function

        ''' <summary>
        ''' Draws an element with optional clipping support.
        ''' Children are already in I_Elements via _Children_ChildAdded and drawn in the main loop.
        ''' </summary>
        Private Sub DrawElementRecursive(E As SceneElement, gd As GraphicsDevice, viewportRect As Rectangle, ForceClip As Boolean)
            If Not IsEffectivelyVisible(E) Then Return
            Dim Start As Long = Diagnostics.Stopwatch.GetTimestamp
            Dim previousScissor As Rectangle = gd.ScissorRectangle
            Dim needsClipRestore As Boolean = False

            Dim clipRect As Rectangle = viewportRect
            Dim hasClip As Boolean = ForceClip
            Dim clipOwner As SceneElement = E
            While clipOwner IsNot Nothing
                If clipOwner.Clip Then
                    hasClip = True
                    clipRect = Rectangle.Intersect(clipRect, clipOwner.Rectangle)
                End If
                clipOwner = clipOwner.Parent
            End While
            If ForceClip AndAlso Not E.Clip Then
                clipRect = Rectangle.Intersect(clipRect, If(E.Parent IsNot Nothing, E.Parent.Rectangle, E.Rectangle))
            End If

            ' Descendants have their own entries in the flat draw list. Cull
            ' only this element, not its children, and retain a small AA fringe.
            Dim drawBounds = E.Rectangle
            drawBounds.Inflate(2, 2)
            If Not drawBounds.Intersects(clipRect) Then Return

            If hasClip Then
                ' A clipped ancestor constrains every descendant.  The scene
                ' stores children in a flat render list, so derive the complete
                ' ancestor mask for each draw instead of relying on recursion.
                Dim globalScissor = previousScissor
                globalScissor = New Rectangle(CInt(globalScissor.X / Quality.RenderScale.X), CInt(globalScissor.Y / Quality.RenderScale.Y),
                    CInt(globalScissor.Width / Quality.RenderScale.X), CInt(globalScissor.Height / Quality.RenderScale.Y))
                globalScissor.Offset(CInt(EffectRenderOffset.X), CInt(EffectRenderOffset.Y))
                clipRect = Rectangle.Intersect(clipRect, globalScissor)

                ' Skip drawing if the scissor rectangle is empty
                If clipRect.Width <= 0 OrElse clipRect.Height <= 0 Then
                    Return
                End If

                ' Enable scissor test if not already enabled
                Dim scissorWasEnabled As Boolean = False
                Try
                    Dim rs = gd.RasterizerState
                    scissorWasEnabled = (rs IsNot Nothing AndAlso rs.ScissorTestEnable)
                Catch
                End Try

                If Not scissorWasEnabled Then
                    If _scissorRasterizerState Is Nothing Then
                        _scissorRasterizerState = New RasterizerState() With {
                            .CullMode = CullMode.None,
                            .ScissorTestEnable = True,
                            .MultiSampleAntiAlias = True
                        }
                    End If
                    gd.RasterizerState = _scissorRasterizerState
                End If

                clipRect.Offset(-CInt(EffectRenderOffset.X), -CInt(EffectRenderOffset.Y))
                gd.ScissorRectangle = Quality.ScaleRectangle(clipRect)
                needsClipRestore = True
            End If

            ' Layout was validated once from the visual roots before this flat
            ' draw pass. Revalidating here recursively made gallery scrolling
            ' quadratic and calculated clip rectangles from stale geometry.
            E.OnPreDraw(gameTime)
            E.doDraw(gameTime)

            If EnableFrameProfiling Then
                Dim key = E.GetType().Name
                If Not drawCosts.ContainsKey(key) Then drawCosts(key) = 0
                drawCosts(key) += Diagnostics.Stopwatch.GetElapsedTime(Start).TotalMilliseconds
                If Not drawCounts.ContainsKey(key) Then drawCounts(key) = 0
                drawCounts(key) += 1
            End If

            ' Notify draw finished
            Dim elapsedTime As TimeSpan = TimeSpan.FromTicks(Diagnostics.Stopwatch.GetTimestamp - Start)
            E.OnDrawFinished(elapsedTime)

            ' Restore previous scissor rectangle if we changed it
            If needsClipRestore Then
                gd.ScissorRectangle = previousScissor
            End If
        End Sub

        Public Overridable Sub ApplyEffects()
            If Effects Is Nothing Then Return
            For Each E As Effect In Effects
                Dim inverseViewport = E.Parameters.Item("InverseViewportSize")
                If inverseViewport IsNot Nothing AndAlso
                   graphicsDevice.Viewport.Width > 0 AndAlso graphicsDevice.Viewport.Height > 0 Then
                    inverseViewport.SetValue(New Vector2(
                        1.0F / graphicsDevice.Viewport.Width,
                        1.0F / graphicsDevice.Viewport.Height))
                End If
                For Each P As EffectPass In E.CurrentTechnique.Passes
                    P.Apply()
                Next
            Next
        End Sub

        ''' <summary>
        ''' Updates the render target to match the current back buffer size.
        ''' Call this when the window is resized.
        ''' </summary>
        Public Sub UpdateRenderTargetSize()
            If Not isInitialized Then Return

            Dim newWidth As Integer = graphicsDevice.PresentationParameters.BackBufferWidth
            Dim newHeight As Integer = graphicsDevice.PresentationParameters.BackBufferHeight
            Dim samples = Quality.SupportedSamples(graphicsDevice, Quality.AntiAliasingSamples)
            If Quality.AntiAliasingTechnique = "SSAA" Then samples = 0

            ' Only recreate if size changed
            If renderTarget IsNot Nothing AndAlso
               renderTarget.Width = newWidth AndAlso
               renderTarget.Height = newHeight AndAlso renderTarget.MultiSampleCount = samples Then
                Return
            End If

            ' Dispose old render target
            renderTarget?.Dispose()

            ' Create new render target with updated size
            renderTarget = New RenderTarget2D(graphicsDevice,
                                              newWidth,
                                              newHeight,
                                              False, graphicsDevice.PresentationParameters.BackBufferFormat,
                                              DepthFormat.Depth24Stencil8, samples, RenderTargetUsage.DiscardContents)
        End Sub

        Public Function DrawToRenderTarget() As RenderTarget2D
            If UseRenderTarget Then
                Dim prior = graphicsDevice.GetRenderTargets()
                ' Ensure render target matches current back buffer size
                UpdateRenderTargetSize()

                If Quality.AntiAliasingTechnique = "SSAA" AndAlso Quality.AntiAliasingSamples > 0 Then
                    Try
                        graphicsDevice.SetRenderTarget(renderTarget)
                        DrawWithQuality(gameTime, True)
                    Finally
                        If prior.Length = 0 Then graphicsDevice.SetRenderTarget(Nothing) Else graphicsDevice.SetRenderTargets(prior)
                    End Try
                    Return renderTarget
                End If

                graphicsDevice.SetRenderTarget(renderTarget)
                graphicsDevice.Clear(BackgroundColor)

                RaiseEvent PreDraw(gameTime)

                ApplyEffects()

                DrawElements(I_Elements.Values)

                If Not hasBegun Then
                    SpriteSettings.Begin(spriteBatch)
                    hasBegun = True
                End If

                DrawToRenderTarget = renderTarget

                If hasBegun Then
                    hasBegun = False
                    spriteBatch.End()
                End If

                If prior.Length = 0 Then graphicsDevice.SetRenderTarget(Nothing) Else graphicsDevice.SetRenderTargets(prior)

                Exit Function
            End If
            Return Nothing
        End Function

        Public Overridable Sub Draw(gameTime As GameTime)
            If Not isInitialized Then Return
            DrawWithQuality(gameTime)
        End Sub

        Private Sub DrawCore(gameTime As GameTime, Optional bypassRenderTarget As Boolean = False)

            If UseRenderTarget AndAlso Not bypassRenderTarget Then
                Dim Texture As RenderTarget2D = DrawToRenderTarget()
                If Not hasBegun Then
                    hasBegun = True
                    RenderTargetOptions.Begin(spriteBatch)
                End If
                spriteBatch.Draw(Texture, New Rectangle(0, 0, renderTarget.Width, renderTarget.Height), Color.White)
                If hasBegun Then
                    hasBegun = False
                    spriteBatch.End()
                End If
                Me.Texture = Texture
            Else
                graphicsDevice.Clear(BackgroundColor)
                RaiseEvent PreDraw(gameTime)
                If Not hasBegun Then
                    hasBegun = True
                    spriteBatch.Begin()
                End If
                ApplyEffects()
                DrawElements(I_Elements.Values)
            End If
            If isCursorVisible Then
                CursorBorder.Draw(gameTime)
                Cursor.Draw(gameTime)
            End If
            If hasBegun Then
                hasBegun = False
                spriteBatch.End()
            End If

            RaiseEvent PostDraw(gameTime)
        End Sub

        Friend CursorDefault As Vector2() = {New Vector2(1, 1), New Vector2(3, 10), New Vector2(5, 5), New Vector2(9, 5)}
        Friend CursorDefaultBorder As Vector2() = {New Vector2(0, 0), New Vector2(4, 12), New Vector2(6, 8), New Vector2(12, 6)}

        Friend CursorResizeLeft As Vector2() = {New Vector2(0, 6), New Vector2(5, 11), New Vector2(6, 10), New Vector2(3, 4), New Vector2(10, 5), New Vector2(3, 6), New Vector2(5, 0), New Vector2(0, 5)}
        Friend CursorResizeLeftBorder As Vector2() = {New Vector2(0, 0), New Vector2(4, 12), New Vector2(6, 8), New Vector2(12, 6)}

        Friend CursorResizeRight As Vector2() = {New Vector2(1, 1), New Vector2(3, 10), New Vector2(5, 5), New Vector2(9, 5)}
        Friend CursorResizeRightBorder As Vector2() = {New Vector2(0, 0), New Vector2(4, 12), New Vector2(6, 8), New Vector2(12, 6)}

        Friend CursorResizeTop As Vector2() = {New Vector2(1, 1), New Vector2(3, 10), New Vector2(5, 5), New Vector2(9, 5)}
        Friend CursorResizeTopBorder As Vector2() = {New Vector2(0, 0), New Vector2(4, 12), New Vector2(6, 8), New Vector2(12, 6)}

        Friend CursorResizeBottom As Vector2() = {New Vector2(1, 1), New Vector2(3, 10), New Vector2(5, 5), New Vector2(9, 5)}
        Friend CursorResizeBottomBorder As Vector2() = {New Vector2(0, 0), New Vector2(4, 12), New Vector2(6, 8), New Vector2(12, 6)}

        Public Overloads Sub ChangeCursorType(Cursor As CursorType)
            Me.Cursor.ClearVectorPoints()
            Select Case Cursor
                Case CursorType.Default
                    Me.Cursor.AddVectorPoints(CursorDefault)
                Case CursorType.ResizeBottom
                    Me.Cursor.AddVectorPoints(CursorResizeBottom)
                Case CursorType.ResizeLeft
                    Me.Cursor.AddVectorPoints(CursorResizeLeft)
                Case CursorType.ResizeRight
                    Me.Cursor.AddVectorPoints(CursorResizeRight)
                Case CursorType.ResizeTop
                    Me.Cursor.AddVectorPoints(CursorResizeTop)
            End Select
        End Sub


        Public Overloads Sub ChangeCursorType(Cursor As Vector2())
            Me.Cursor.ClearVectorPoints()
            Me.Cursor.AddVectorPoints(Cursor)
        End Sub

        Public Function GetSpriteBatch() As SpriteBatch
            Return Me.spriteBatch
        End Function

        Public Overridable Sub Initialize(gameTime As GameTime)
            Me.gameTime = gameTime
            CheckForAddedChildren()
            ProjectionHosts.ForEach(Sub(s) s.TargetScene.Initialize(gameTime))
            WhitePlain = Textures.CreateSolidTexture(graphicsDevice, Color.White)

            renderTarget = New RenderTarget2D(graphicsDevice,
                                              graphicsDevice.PresentationParameters.BackBufferWidth,
                                              graphicsDevice.PresentationParameters.BackBufferHeight,
                                              False, graphicsDevice.PresentationParameters.BackBufferFormat,
                                              DepthFormat.Depth24Stencil8)

            If SpriteSettings Is Nothing Then
                SpriteSettings = New XNA.SpriteBatchPropertySet(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                                                            SamplerState.LinearClamp, DepthStencilState.Default,
                                                            RasterizerState.CullCounterClockwise)
            End If

            If RenderTargetOptions Is Nothing Then
                RenderTargetOptions = New XNA.SpriteBatchPropertySet(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                                                            SamplerState.LinearClamp, DepthStencilState.Default,
                                                            RasterizerState.CullNone)
            End If

            Cursor = New PolygonElement(Me, {New Vector2(1, 1), New Vector2(3, 10), New Vector2(5, 5), New Vector2(9, 5)}) With {.Size = New Vector2(4)}
            Cursor.FillColor = Color.White
            Cursor.Size = New Vector2(24)

            CursorBorder = New PolygonElement(Me, {New Vector2(0, 0), New Vector2(4, 12), New Vector2(6, 8), New Vector2(12, 6)}) With {.Size = New Vector2(4)}
            CursorBorder.FillColor = Color.Black
            CursorBorder.Size = New Vector2(24)

        End Sub

        Private Sub CheckForAddedChildren()
            If AddQueue.Count > 0 Then
                ' Process all items in the queue
                While AddQueue.Count > 0
                    Dim Element As SceneElement = AddQueue(0)
                    AddQueue.RemoveAt(0)
                    If Element.IsDisposed Then Continue While

                    ' Generate GUID if missing
                    If String.IsNullOrEmpty(Element.GUID) Then
                        Element.GUID = System.Guid.NewGuid.ToString
                    End If

                    ' Skip if element already exists
                    If GUID_INDEX.ContainsKey(Element.GUID) Then
                        Continue While
                    End If

                    ' Registration order is intentionally separate from XAML's
                    ' local Panel.ZIndex.  The previous implementation replaced
                    ' the imported value here, so z-index could never survive the
                    ' translator and late async content could cover popups.
                    Dim nextIndex = If(I_Elements.Count = 0, 0, I_Elements.Keys.Max() + 1)
                    I_Elements.Add(nextIndex, Element)
                    GUID_INDEX.Add(Element.GUID, nextIndex)
                    InvalidateRenderOrder()
                End While
            End If
        End Sub

        Private Sub CheckForRemovedChildren()
            If RemoveQueue.Count > 0 Then
                For i As Integer = RemoveQueue.Count - 1 To 0 Step -1
                    Dim x As Integer = RemoveQueue(i)
                    If Not I_Elements.ContainsKey(x) Then
                        Continue For
                    End If
                    Dim e As SceneElement = I_Elements(x)
                    If e.GetType Is GetType(SceneProjectionHost) Then
                        Dim h As SceneProjectionHost = DirectCast(e, SceneProjectionHost)
                        If ContainsProjectionHost(h) Then RemoveProjectionHost(h)
                    End If
                    GUID_INDEX.Remove(e.GUID)
                    I_Elements.Remove(x)
                    InvalidateRenderOrder()
                Next
                RemoveQueue.Clear()
            End If
            If BringToFrontQueue.Count > 0 Then
                For Each element In BringToFrontQueue.ToArray()
                    QueueElementTree(element)
                Next
                BringToFrontQueue.Clear()
            End If
        End Sub

        Private Shared Function IsOverlayElement(element As SceneElement) As Boolean
            Dim current = element
            While current IsNot Nothing
                If current.IsOverlay Then Return True
                current = current.Parent
            End While
            Return False
        End Function

        Public Overridable Sub Tick(gameTime As GameTime)
            ' Initialize's GameTime is not necessarily the object supplied on
            ' later frames (notably the native backend). Event-created clocks
            ' must start at the current scene time, not at application startup.
            Me.gameTime = gameTime
            ' Process removals FIRST, then additions
            ' This allows remove-and-readd patterns (like BringToFront) to work correctly
            CheckForRemovedChildren()
            CheckForAddedChildren()

            If Not isInitialized Then
                Initialize(gameTime)
                RaiseEvent Initialized(gameTime)
                isInitialized = True
            End If

            ' Tick all elements (children are already in I_Elements via _Children_ChildAdded)
            Dim tickStarted = If(EnableFrameProfiling, Diagnostics.Stopwatch.GetTimestamp(), 0L)
            For Each element In I_Elements.Values
                If Not element.IsDisposed Then element.Tick(gameTime)
            Next
            If tickStarted <> 0 Then _LastElementTickMilliseconds = Diagnostics.Stopwatch.GetElapsedTime(tickStarted).TotalMilliseconds

            InternalFPS += 1
            Dim MS As Integer = gameTime.ElapsedGameTime.Milliseconds
            If MS <> 0 Then
                FPS = CInt(1000 / gameTime.ElapsedGameTime.Milliseconds)
            End If
            If LastTick + OneSecond < gameTime.TotalGameTime.Ticks Then
                LastTick = gameTime.TotalGameTime.Ticks
                InternalFPS = 0
            End If
        End Sub

        Public Sub New()

        End Sub

        Public Sub New(SceneManager As SceneManager)
            InitialConstructor(SceneManager)
        End Sub

        Public Sub InitialConstructor(SceneManager As SceneManager)
            _sender = SceneManager.Sender
            spriteBatch = CType(sender.Services.GetService(GetType(SpriteBatch)), SpriteBatch)

            ' Initialize effects list
            Effects = New List(Of Effect)()

            ' Try to load the backend-specific FXAA shader effect.
            Try
                Dim FXAA As New Effect(graphicsDevice, My.Resources.FXAA)
                FXAA.Parameters.Item("EdgeThreshold").SetValue(0.125F)
                FXAA.Parameters.Item("SubPixelAliasingRemoval").SetValue(1.0F)
                Effects.Add(FXAA)
            Catch ex As Exception
                ' An incompatible shader must not prevent the scene from rendering.
                Debug.WriteLine($"FXAA shader load failed: {ex.Message}")
            End Try

        End Sub

#Region "IDisposable Support"
        Private disposedValue As Boolean

        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not disposedValue Then
                If disposing Then
                    ' Dispose managed resources
                    For Each element In I_Elements.Values
                        element.Dispose()
                    Next
                    I_Elements.Clear()
                    GUID_INDEX.Clear()

                    Cursor?.Dispose()
                    CursorBorder?.Dispose()

                    If Effects IsNot Nothing Then
                        For Each effect In Effects
                            effect?.Dispose()
                        Next
                        Effects.Clear()
                    End If

                    renderTarget?.Dispose()
                    qualityTarget?.Dispose()
                    qualityBatch?.Dispose()
                    qualityResolve?.Dispose()
                    effectCanvas?.Dispose()
                    effectCompositeBatch?.Dispose()
                    WhitePlain?.Dispose()
                    _scissorRasterizerState?.Dispose()
                End If
                disposedValue = True
            End If
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
#End Region

    End Class

    Public Enum CursorType
        [Default]
        ResizeLeft
        ResizeTop
        ResizeRight
        ResizeBottom
    End Enum

End Namespace
