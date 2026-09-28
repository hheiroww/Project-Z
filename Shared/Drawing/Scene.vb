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

    Public MustInherit Class Scene
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

        Private LastMouseDrag As SceneElement

        Public Cursor As PolygonElement

        Private CursorBorder As PolygonElement

        Private InternalFPS As Integer = 0
        Private Property I_Elements As New SortedList(Of Integer, SceneElement)
        Private Property GUID_INDEX As New SortedList(Of String, Integer)
        Private Property RemoveQueue As New List(Of Integer)
        Private Property AddQueue As New List(Of SceneElement)
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
                If LastSelected.CanSelect Then
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

        Protected Friend Sub KeyPress(Key As Keys, KeyboardState As KeyboardState)
            RaiseEvent OnKeyPress(Key, KeyboardState)
            If LastSelected IsNot Nothing AndAlso LastSelected.isEnabled Then
                If LastSelected.CanSelect AndAlso LastSelected.isSelected Then
                    LastSelected.KeyPress(Key, KeyboardState)
                End If
            End If

            ProjectionHosts.ForEach(Sub(s) s.TargetScene.KeyPress(Key, KeyboardState))
        End Sub

        Protected Friend Sub KeyDown(Key As Keys, KeyboardState As KeyboardState)
            RaiseEvent OnKeyDown(Key, KeyboardState)
            If LastSelected IsNot Nothing AndAlso LastSelected.isEnabled Then
                If LastSelected.CanSelect AndAlso LastSelected.isSelected Then
                    LastSelected.KeyDown(Key, KeyboardState)
                End If
            End If
            ProjectionHosts.ForEach(Sub(s) s.TargetScene.KeyDown(Key, KeyboardState))
        End Sub

        Protected Friend Sub KeyUp(Key As Keys, KeyboardState As KeyboardState)
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
            Return contentCollection.Fonts(Font).MeasureString(Text)
        End Function

#End Region

        ''' <summary>
        ''' Finds the topmost element at the given point, checking children hierarchically.
        ''' </summary>
        Public Function PointToElement(p As Point) As SceneElement
            ' Iterate by actual keys in descending order (highest z-index first)
            Dim keys = I_Elements.Keys.ToList()
            For i As Integer = keys.Count - 1 To 0 Step -1
                Dim zIndex As Integer = keys(i)
                Dim Element As SceneElement = I_Elements(zIndex)
                Dim hit = HitTestElement(Element, p)
                If hit IsNot Nothing Then
                    Return hit
                End If
            Next
            Return Nothing
        End Function

        ''' <summary>
        ''' Recursively hit tests an element and its children.
        ''' Children are tested first (in reverse z-order) since they render on top.
        ''' Returns the element that should receive the click, or Nothing if no hit.
        ''' </summary>
        Private Function HitTestElement(element As SceneElement, p As Point) As SceneElement
            If Not element.isVisible Then Return Nothing

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
            ' Iterate by actual keys in descending order (highest z-index first)
            Dim keys = I_Elements.Keys.ToList()
            For i As Integer = keys.Count - 1 To 0 Step -1
                Dim zIndex As Integer = keys(i)
                Dim Element As SceneElement = I_Elements(zIndex)
                HitTestElementAll(Element, p, Elements)
            Next
            Return Elements.ToArray
        End Function

        ''' <summary>
        ''' Recursively collects all elements at the given point.
        ''' </summary>
        Private Sub HitTestElementAll(element As SceneElement, p As Point, results As List(Of SceneElement))
            If Not element.isVisible Then Return

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

        Private Sub QueueElementTree(Element As SceneElement)
            If Element Is Nothing Then Return
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
            ' Cache graphics device reference outside the loop
            Dim gd As GraphicsDevice = spriteBatch.GraphicsDevice
            Dim viewportRect As New Rectangle(0, 0, gd.Viewport.Width, gd.Viewport.Height)

            For Each E As SceneElement In Elements
                If Not E.isVisible Then Continue For
                DrawElementRecursive(E, gd, viewportRect, ForceClip)
            Next
        End Sub

        ''' <summary>
        ''' Draws an element with optional clipping support.
        ''' Children are already in I_Elements via _Children_ChildAdded and drawn in the main loop.
        ''' </summary>
        Private Sub DrawElementRecursive(E As SceneElement, gd As GraphicsDevice, viewportRect As Rectangle, ForceClip As Boolean)
            If Not E.isVisible Then Return

            Dim Start As Long = Diagnostics.Stopwatch.GetTimestamp
            Dim previousScissor As Rectangle = gd.ScissorRectangle
            Dim needsClipRestore As Boolean = False

            If E.Clip Or ForceClip Then
                ' Setup clipping
                Dim clipRect As Rectangle = If(E.Parent IsNot Nothing, E.Parent.Rectangle, E.Rectangle)
                clipRect = Rectangle.Intersect(clipRect, viewportRect)
                clipRect = Rectangle.Intersect(clipRect, previousScissor)

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
                            .ScissorTestEnable = True
                        }
                    End If
                    gd.RasterizerState = _scissorRasterizerState
                End If

                gd.ScissorRectangle = clipRect
                needsClipRestore = True
            End If

            ' Draw this element
            E.OnPreDraw(gameTime)
            E.ValidationCheck()
            E.doDraw(gameTime)

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
                For Each T As EffectTechnique In E.Techniques
                    For Each P As EffectPass In T.Passes
                        P.Apply()
                    Next
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

            ' Only recreate if size changed
            If renderTarget IsNot Nothing AndAlso
               renderTarget.Width = newWidth AndAlso
               renderTarget.Height = newHeight Then
                Return
            End If

            ' Dispose old render target
            renderTarget?.Dispose()

            ' Create new render target with updated size
            renderTarget = New RenderTarget2D(graphicsDevice,
                                              newWidth,
                                              newHeight,
                                              False, graphicsDevice.PresentationParameters.BackBufferFormat,
                                              DepthFormat.Depth24Stencil8)
        End Sub

        Public Function DrawToRenderTarget() As RenderTarget2D
            If UseRenderTarget Then
                ' Ensure render target matches current back buffer size
                UpdateRenderTargetSize()

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

                graphicsDevice.SetRenderTarget(Nothing)

                Exit Function
            End If
            Return Nothing
        End Function

        Public Overridable Sub Draw(gameTime As GameTime)
            If Not isInitialized Then Return

            If UseRenderTarget Then
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
                                                            SamplerState.PointClamp, DepthStencilState.Default,
                                                            RasterizerState.CullCounterClockwise)
            End If

            If RenderTargetOptions Is Nothing Then
                RenderTargetOptions = New XNA.SpriteBatchPropertySet(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                                                            SamplerState.PointClamp, DepthStencilState.Default,
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

                    ' Generate GUID if missing
                    If String.IsNullOrEmpty(Element.GUID) Then
                        Element.GUID = System.Guid.NewGuid.ToString
                    End If

                    ' Skip if element already exists
                    If GUID_INDEX.ContainsKey(Element.GUID) Then
                        Continue While
                    End If

                    ' Always assign the next highest Z-index to ensure new elements appear on top
                    Dim nextIndex As Integer = 0
                    If I_Elements.Count > 0 Then
                        nextIndex = I_Elements.Keys.Max() + 1
                    End If
                    Element.zIndex = nextIndex

                    I_Elements.Add(Element.zIndex, Element)
                    GUID_INDEX.Add(Element.GUID, Element.zIndex)
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
                Next
                RemoveQueue.Clear()
            End If
        End Sub

        Public Overridable Sub Tick(gameTime As GameTime)
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
            For Each element In I_Elements.Values
                element.Tick(gameTime)
            Next

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

            ' Try to load FXAA shader effect (may fail if shader needs rebuild for newer KNI version)
            Try
                Dim FXAA As New Effect(graphicsDevice, My.Resources.FXAA)
                FXAA.Parameters.Item("EdgeThreshold").SetValue(0.125F)
                FXAA.Parameters.Item("SubPixelAliasingRemoval").SetValue(1.0F)
                Effects.Add(FXAA)
            Catch ex As Exception
                ' FXAA shader not compatible with current KNI version - continue without it
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
