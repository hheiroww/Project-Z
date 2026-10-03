Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Input
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Animations.Properties
Imports System.Collections.Generic
Imports ProjectZ.Shared.XNA
Imports ProjectZ.Shared.Animations
Imports ProjectZ.Shared.Drawing.UI.Advanced
Imports ProjectZ.Shared.Drawing.UI.Input
Imports SocketJack.Serialization

Namespace [Shared].Drawing.UI

    <Serializable>
    Public MustInherit Class SceneElement
        Implements IDisposable

#Region "Properties"

        Public isUserInvalidated As Boolean = False
        ' Optional declarative layout adapter; native controls retain their own layout.
        Public Property ImportedLayout As Action
        ''' <summary>Optional compatibility routing before native input handlers. True consumes the event.</summary>
        Public Property ImportedInputFilter As Func(Of String, Object(), Boolean)
        Private _importedPointerSuppressed As Boolean

        Public Function FilterImportedInput(name As String, ParamArray values As Object()) As Boolean
            Return ImportedInputFilter IsNot Nothing AndAlso ImportedInputFilter(name, values)
        End Function
        ' Opt-in virtualization for fixed-size items such as a file gallery.
        ' Keep item positions current, but arrange descendants only near view.
        Public Property VirtualizationViewport As SceneElement
        Friend Property IsLayoutVirtualized As Boolean

        Public Property isLoaded As Boolean = False

        Public Property isPrototype As Boolean = False

        Public ReadOnly Property isManagedSpritebatch As Boolean
            Get
                Return _isManagedSpritebatch
            End Get
        End Property
        Private _isManagedSpritebatch As Boolean = False

        Public Property ForceSpriteBatchBegin As Boolean = False

        Protected Friend spriteBatch As SpriteBatchWrapper

        Public Property Position As Vector2
            Get
                Return _Position
            End Get
            Set(value As Vector2)
                If value = _Position Then Return
                Dim OldPosition As Vector2 = _Position
                _Position = value
                RaiseEvent PositionChanged(OldPosition, _Position)
            End Set
        End Property
        Private _Position As Vector2

        Public Property Size As Vector2
            Get
                Return _Size
            End Get
            Set(value As Vector2)
                If value = _Size Then Return
                Dim oldSize As Vector2 = _Size
                _Size = value
                RaiseEvent SizeChanged(oldSize, _Size)
            End Set
        End Property
        Private _Size As Vector2

        ''' <summary>
        ''' WPF RenderTransform compatibility values. They affect the rendered
        ''' and hit-test rectangle without changing layout Size/Position, so a
        ''' hover storyboard cannot make the parent panel reflow.
        ''' </summary>
        Public Property RenderScale As Vector2
            Get
                Return _RenderScale
            End Get
            Set(value As Vector2)
                _RenderScale = New Vector2(Math.Max(0.001F, value.X), Math.Max(0.001F, value.Y))
                SetRectangleProperty()
            End Set
        End Property
        Private _RenderScale As Vector2 = Vector2.One

        Public Property RenderTranslation As Vector2
            Get
                Return _RenderTranslation
            End Get
            Set(value As Vector2)
                _RenderTranslation = value
                SetRectangleProperty()
            End Set
        End Property
        Private _RenderTranslation As Vector2 = Vector2.Zero

        Public Property Padding As Thickness
            Get
                Return _Padding
            End Get
            Set(value As Thickness)
                _Padding = value
                _Valid = False
            End Set
        End Property
        Private _Padding As New Thickness(3)

        ' WPF-like minimum/maximum size
        Public Property MinSize As Vector2
            Get
                Return _MinSize
            End Get
            Set(value As Vector2)
                _MinSize = value
            End Set
        End Property
        Private _MinSize As Vector2 = Vector2.Zero

        Public Property MaxSize As Vector2
            Get
                Return _MaxSize
            End Get
            Set(value As Vector2)
                _MaxSize = value
            End Set
        End Property
        Private _MaxSize As Vector2 = New Vector2(Single.MaxValue, Single.MaxValue)

        ' For stretch alignment support
        Public Property StretchToParent As Boolean = False

        Public Property Margin As Thickness
            Get
                Return _Margin
            End Get
            Set(value As Thickness)
                _Margin = value
                _Valid = False
            End Set
        End Property
        Private _Margin As New Thickness(0)

        Public Property zIndex As Integer
            Get
                Return _zIndex
            End Get
            Set(value As Integer)
                If _zIndex = value Then Return
                _zIndex = value
                Scene?.InvalidateRenderOrder()
                RaiseEvent IndexChanged()
            End Set
        End Property
        Private _zIndex As Integer = 0

        ''' <summary>
        ''' Renders this element and its descendants in Project-Z's popup layer.
        ''' The layer stays above ordinary controls even when ordinary content is
        ''' added later (for example, asynchronously loaded gallery thumbnails).
        ''' </summary>
        Public Property IsOverlay As Boolean
            Get
                Return _IsOverlay
            End Get
            Set(value As Boolean)
                If _IsOverlay = value Then Return
                _IsOverlay = value
                Scene?.InvalidateRenderOrder()
            End Set
        End Property
        Private _IsOverlay As Boolean

        Public Property isEnabled As Boolean = True

        Public Property isSelected As Boolean
            Get
                Return _isSelected
            End Get
            Set(value As Boolean)
                If CanSelect Then
                    _isSelected = value
                    Select Case _isSelected
                        Case True
                            RaiseEvent Selected()
                        Case False
                            RaiseEvent Deselected()
                    End Select
                End If
            End Set
        End Property
        Private _isSelected As Boolean = False

        Public Property CanSelect As Boolean = False
        ''' <summary>Include this selectable control in keyboard navigation.</summary>
        Public Property TabStop As Boolean = True
        ''' <summary>Lower values come first; ties follow scene render order.</summary>
        Public Property TabIndex As Integer = 0
        Public Property isMouseOver As Boolean
            Get
                Return _MouseOver
            End Get
            Set(value As Boolean)
                _MouseOver = value
                Select Case _MouseOver
                    Case True
                        RaiseEvent MouseEnter()
                    Case False
                        RaiseEvent MouseLeave()
                End Select
            End Set
        End Property
        Private _MouseOver As Boolean = False

        Public ReadOnly Property isMouseDown As Boolean
            Get
                Return _MouseDown
            End Get
        End Property
        Private _MouseDown As Boolean = False
        Private _MouseDownPosition As Vector2

        Public Property isMovable As Boolean
            Get
                Return _isMovable
            End Get
            Set(value As Boolean)
                _isMovable = value
            End Set
        End Property
        Private _isMovable As Boolean = False

        Public Property isMouseBypassEnabled As Boolean
            Get
                Return _isMouseBypassEnabled
            End Get
            Set(value As Boolean)
                _isMouseBypassEnabled = value
            End Set
        End Property
        Private _isMouseBypassEnabled As Boolean = False

        Protected Property isElementSpriteBatch As Boolean
            Get
                Return _isElementSpriteBatch
            End Get
            Set(value As Boolean)
                _isElementSpriteBatch = value
            End Set
        End Property
        Private _isElementSpriteBatch As Boolean


        Friend WithEvents _Children As New ChildCollection(Me)

        Public ReadOnly Property Children As ChildCollection
            Get
                Return _Children
            End Get
        End Property


        Public Property HorizontalAlign As HorizontalAlignment
            Get
                Return _HorizontalAlign
            End Get
            Set(value As HorizontalAlignment)
                _HorizontalAlign = value
                RaiseEvent AlignmentChanged(_HorizontalAlign, _VerticalAlign)
            End Set
        End Property
        Private _HorizontalAlign As HorizontalAlignment = HorizontalAlignment.Left

        Public Property VerticalAlign As VerticalAlignment
            Get
                Return _VerticalAlign
            End Get
            Set(value As VerticalAlignment)
                _VerticalAlign = value
                RaiseEvent AlignmentChanged(_HorizontalAlign, _VerticalAlign)
            End Set
        End Property
        Private _VerticalAlign As VerticalAlignment = VerticalAlignment.Top

        Public Property OrientationReserve As DisplayReservation = DisplayReservation.FloatBoth

        Private Property Valid As Boolean
            Get
                Return _Valid
            End Get
            Set(value As Boolean)
                _Valid = value
                If Not _Valid Then RaiseEvent Invalidated(Me)
            End Set
        End Property
        Private _Valid As Boolean = False
        Private _isValidating As Boolean = False
        Private _isCheckingValidation As Boolean = False

        Public Function isValidated() As Boolean
            Return Valid
        End Function

        Public Property Rectangle As Rectangle
            Get
                Return _Rectangle
            End Get
            Set(value As Rectangle)
                _Rectangle = value
            End Set
        End Property
        Private _Rectangle As New Rectangle(0, 0, 0, 0)

        Public Property isVisible As Boolean
            Get
                Return visibleValue
            End Get
            Set(value As Boolean)
                If value = visibleValue Then Return
                visibleValue = value
                RaiseEvent VisibilityChanged()
            End Set
        End Property
        Private visibleValue As Boolean = True
        Public Event VisibilityChanged()

        ''' <summary>WPF-compatible opacity, inherited multiplicatively from visual parents.</summary>
        Public Property Opacity As Single = 1.0F

        Protected Friend Function ApplyOpacity(color As Color) As Color
            Dim effective = Math.Clamp(Opacity, 0.0F, 1.0F)
            Dim ancestor = Parent
            While ancestor IsNot Nothing
                effective *= Math.Clamp(ancestor.Opacity, 0.0F, 1.0F)
                ancestor = ancestor.Parent
            End While
            Return New Color(color.R, color.G, color.B, CByte(Math.Clamp(CInt(color.A * effective), 0, 255)))
        End Function

        Public Property GUID As String = ""


        Friend Scene As Scene

        Protected Friend Parent As SceneElement

        Public Property Clip As Boolean = False
        Public Property VisualEffect As Primitives.XamlNativeEffect

#End Region

#Region "Events"

        Public Event RectangleChanged()
        Public Event Selected()
        Public Event Deselected()
        Public Event PositionChanged(oldPosition As Vector2, newPosition As Vector2)
        Public Event SizeChanged(oldSize As Vector2, newSize As Vector2)
        Public Event IndexChanged()
        Public Event DrawFinished(DrawTime As TimeSpan)
        Public Event PreDraw(gameTime As GameTime)
        Public Event AlignmentChanged(HorizontalAlignment As HorizontalAlignment, VerticalAlignment As VerticalAlignment)
        Public Event PreviewInvalidated(sender As SceneElement)
        Public Event Invalidated(sender As SceneElement)
        Public Event UserInvalidated(sender As SceneElement)
        Public Event Loaded()

#Region "Child Events"

        Private Sub _Children_ChildAdded(c As SceneElement) Handles _Children.ChildAdded
            AddHandler c.Invalidated, AddressOf OnPreviewInvalidated
            Scene?.InvalidateRenderOrder()
            If Scene.Elements.Contains(Me) Then
                Scene.AddElement(c)
            End If
        End Sub

        Private Sub _Children_ChildRemoved(c As SceneElement) Handles _Children.ChildRemoved
            RemoveHandler c.Invalidated, AddressOf OnPreviewInvalidated
            Scene?.InvalidateRenderOrder()
            Scene?.RemoveElement(c)
        End Sub

        Protected Friend Sub OnPreviewInvalidated(sender As SceneElement)
            RaiseEvent PreviewInvalidated(sender)
            ' Coalesce mutations until the scene's root layout pass. Immediate
            ' recursive validation here turns a scroll into repeated tree walks
            ' for every moved child, before drawing has even started.
            _Valid = False
        End Sub

        Public Sub OnUserInvalidated()
            RaiseEvent UserInvalidated(Me)
        End Sub

#End Region

#Region "Keyboard Events"

        Public Event OnKeyPress(Key As Keys, KeyboardState As KeyboardState)
        Public Event OnKeyDown(Key As Keys, KeyboardState As KeyboardState)
        Public Event OnKeyUp(Key As Keys, KeyboardState As KeyboardState)

        Protected Friend Sub KeyPress(Key As Keys, KeyboardState As KeyboardState)
            RaiseEvent OnKeyPress(Key, KeyboardState)
        End Sub

        Protected Friend Sub KeyDown(Key As Keys, KeyboardState As KeyboardState)
            RaiseEvent OnKeyDown(Key, KeyboardState)
        End Sub

        Protected Friend Sub KeyUp(Key As Keys, KeyboardState As KeyboardState)
            RaiseEvent OnKeyUp(Key, KeyboardState)
        End Sub

#End Region

#Region "Mouse Events"
        Public Event MouseMove(currentPoint As Point, lastPoint As Point)
        Public Event MouseDrag(currentPoint As Point, startPoint As Point)
        Public Event MouseEnter()
        Public Event MouseLeave()
        Public Event MouseRightClick(p As Point)
        Public Event MouseLeftClick(p As Point)
        Public Event MouseLeftDown(p As Point)
        Public Event MouseLeftUp(p As Point)
        Public Event MouseWheel(delta As Integer, p As Point)
        Public Event DragDrop(p As Point, Element As SceneElement)
        Public Event DragOver(p As Point, Element As SceneElement)

        Protected Friend Sub OnMouseLeftDown(p As Point)
            _importedPointerSuppressed = FilterImportedInput("MouseLeftButtonDown", p)
            If _importedPointerSuppressed Then Return
            _MouseDown = True
            _MouseDownPosition = Position
            RaiseEvent MouseLeftDown(p)
        End Sub

        Protected Friend Sub OnMouseLeftUp(p As Point)
            _MouseDown = False
            If FilterImportedInput("MouseLeftButtonUp", p) Then _importedPointerSuppressed = True
            If _importedPointerSuppressed Then Return
            RaiseEvent MouseLeftUp(p)
        End Sub

        Protected Friend Sub OnMouseLeftClick(p As Point)
            If _importedPointerSuppressed Then Return
            RaiseEvent MouseLeftClick(p)
        End Sub

        Protected Friend Sub OnMouseRightClick(p As Point)
            RaiseEvent MouseRightClick(p)
        End Sub

        Protected Friend Sub OnMouseWheel(delta As Integer, p As Point)
            If FilterImportedInput("MouseWheel", delta, p) Then Return
            RaiseEvent MouseWheel(delta, p)
        End Sub

        Protected Friend Sub OnMouseMove(currentPoint As Point, lastPoint As Point)
            RaiseEvent MouseMove(currentPoint, lastPoint)
        End Sub

        Protected Friend Sub OnMouseDrag(currentPoint As Point, startPoint As Point)
            RaiseEvent MouseDrag(currentPoint, startPoint)
            If isMovable Then Position = _MouseDownPosition.Subtract(startPoint.Subtract(currentPoint))
        End Sub

        Protected Friend Sub OnMouseEnter()
            RaiseEvent MouseEnter()
        End Sub

        Protected Friend Sub OnMouseLeave()
            RaiseEvent MouseLeave()
        End Sub

        Protected Friend Sub OnMouseDragOver(p As Point, ByRef Element As SceneElement)
            RaiseEvent DragOver(p, Element)
        End Sub

        Protected Friend Sub OnMouseDragDrop(p As Point, ByRef Element As SceneElement)
            RaiseEvent DragDrop(p, Element)
        End Sub

#End Region

        Protected Friend Sub OnLoaded()
            RaiseEvent Loaded()
        End Sub

        Protected Friend Sub OnDrawFinished(TimeSpan As TimeSpan)
            RaiseEvent DrawFinished(TimeSpan)
        End Sub

        Protected Friend Sub OnPreDraw(gameTime As GameTime)
            RaiseEvent PreDraw(gameTime)
        End Sub

        Protected Friend Sub OnSizeChanged(oldSize As Vector2, newSize As Vector2)
            RaiseEvent SizeChanged(oldSize, newSize)
        End Sub

        Protected Friend Sub OnPositionChanged(oldPosition As Vector2, newPosition As Vector2)
            RaiseEvent PositionChanged(oldPosition, newPosition)
        End Sub

        Protected Friend Sub OnInvalidated(sender As SceneElement)
            RaiseEvent Invalidated(sender)
        End Sub

#End Region

#Region "Animation Properties"

        Friend LeftProperty As New LeftProperty(Me)
        Friend TopProperty As New TopProperty(Me)


        Friend WidthProperty As New LeftProperty(Me)
        Friend HeightProperty As New TopProperty(Me)

#End Region

#Region "Container Functionality"

        Public Sub ValidationCheck()
            If _isCheckingValidation OrElse disposedValue Then Return
            _isCheckingValidation = True
            Try
                ' Composite controls update internal child geometry from events such
                ' as RectangleChanged and ValueChanged. Re-run the parent layout so
                ' those local coordinates are resolved into scene coordinates before
                ' every draw, even when the parent was previously marked valid.
                Validate()
                Dim viewport = If(VirtualizationViewport Is Nothing, Rectangle.Empty, VirtualizationViewport.Rectangle)
                viewport.Inflate(64, 64)
                For Each child In Children
                    If child Is Nothing Then Continue For
                    child.IsLayoutVirtualized = VirtualizationViewport IsNot Nothing AndAlso Not child.Rectangle.Intersects(viewport)
                    If child.isVisible AndAlso Not child.IsLayoutVirtualized Then child.ValidationCheck()
                Next
            Finally
                _isCheckingValidation = False
            End Try
        End Sub

        Public Sub Invalidate()
            Valid = False
            Validate()
        End Sub

        Private Sub Validate()
            If _isValidating Then Return
            _isValidating = True
            Try
                If ImportedLayout IsNot Nothing Then
                    ImportedLayout.Invoke()
                Else
                    AlignChildren()
                End If
                Valid = True
            Finally
                _isValidating = False
            End Try
        End Sub

        Private Function HorizontalReserved() As Boolean
            Return (OrientationReserve = DisplayReservation.ReserveBoth) Or
                   (OrientationReserve = DisplayReservation.ReserveX)
        End Function

        Private Function VerticalReserved() As Boolean
            Return (OrientationReserve = DisplayReservation.ReserveBoth) Or
                   (OrientationReserve = DisplayReservation.ReserveY)
        End Function

        Private Function isFloat() As Boolean
            Return (OrientationReserve = DisplayReservation.FloatBoth) Or
                   (OrientationReserve = DisplayReservation.FloatX) Or
                   (OrientationReserve = DisplayReservation.FloatY)
        End Function

        Protected Overridable Sub AlignChildren()
            ' Order, Size, and Position Children
            Dim CurrentPosition As New Vector2(Padding.Left, Padding.Top)
            Children.ForEach(Sub(c) AlignChild(c, CurrentPosition))
        End Sub

        Private Sub AlignChild(c As SceneElement, ByRef CurrentPosition As Vector2)
            If c Is Nothing Then Return
            If (c.Parent Is Nothing) Then Return

            Dim newPosition As New Vector2(c.Parent.Position.X, c.Parent.Position.Y)
            Dim newSize As New Vector2(c.Size.X, c.Size.Y)

            Dim hReserved As Boolean = c.HorizontalReserved
            Dim vReserved As Boolean = c.VerticalReserved

            newPosition.X += CurrentPosition.X
            newPosition.Y += CurrentPosition.Y

            If hReserved Then CurrentPosition.X += c.Size.X '+ (c.Parent.Padding.Left + c.Parent.Padding.Right)
            If vReserved Then CurrentPosition.Y += c.Size.Y '+ (c.Parent.Padding.Top + c.Parent.Padding.Bottom)
            Select Case c.HorizontalAlign
                Case HorizontalAlignment.Stretch
                    newSize.X = c.Parent.Size.X - (c.Parent.Padding.Left + c.Parent.Padding.Right)
                Case HorizontalAlignment.Right
                    newPosition.X += Size.X - (c.Size.X + c.Parent.Padding.Right)
                Case HorizontalAlignment.Center
                    newPosition.X += c.Parent.Size.X / 2 - c.Size.X / 2
                    newPosition.X -= CSng((c.Parent.Padding.Left + c.Parent.Padding.Right) / 2)
                Case Else
                    newPosition.X += c.Margin.Left
            End Select


            Select Case c.VerticalAlign
                Case VerticalAlignment.Stretch
                    newSize.Y = c.Parent.Size.Y - (c.Parent.Padding.Top + Padding.Bottom)
                Case VerticalAlignment.Bottom
                    newPosition.Y += c.Parent.Size.Y - (c.Size.Y + c.Parent.Padding.Bottom)
                Case VerticalAlignment.Center
                    newPosition.Y += c.Parent.Size.Y / 2 - c.Size.Y / 2
                    newPosition.Y -= CSng((c.Parent.Padding.Top + c.Parent.Padding.Bottom) / 2)
                Case Else
                    newPosition.Y += c.Margin.Top
            End Select

            If c.Position <> newPosition Then
                c.Position = newPosition
            End If

            If c.Size <> newSize Then
                c.Size = newSize
            End If

            c.Valid = True

        End Sub

        Protected Friend ClickActionInstance As Action

        Public Sub SetClickAction(action As Action)
            ClickActionInstance = action
        End Sub

        Public Function clickActionExists() As Boolean
            Return ClickActionInstance IsNot Nothing
        End Function

        ''' <summary>
        ''' Brings this element to the front of the scene's rendering order.
        ''' The element will be removed and re-added to the scene with the highest z-index.
        ''' </summary>
        Public Sub BringToFront()
            If Scene Is Nothing Then Return
            If Not Scene.ContainsElement(Me) Then Return
            Scene.BringElementToFront(Me)
        End Sub

        ''' <summary>
        ''' Sends this element to the back of the scene's rendering order.
        ''' Note: This is implemented by removing and re-adding with the lowest z-index.
        ''' </summary>
        Public Sub SendToBack()
            If Scene Is Nothing Then Return
            If Not Scene.ContainsElement(Me) Then Return
            ' Set zIndex to minimum before removing so it gets placed at back when re-added
            ' The Scene.AddElement will assign a new index, but we set it negative first
            zIndex = Integer.MinValue
            Scene.RemoveElement(Me)
            Scene.AddElement(Me)
        End Sub
#End Region

#Region "Prototyping"

        Private ProtoButton As PrototypeElement

        Public Sub UpdateElement(newElement As SceneElement)
            Dim PropertyList As List(Of PropertyReference) = Wrapper.GetPropertyReferences(Me)
            Dim NewPropertyList As List(Of PropertyReference) = Wrapper.GetPropertyReferences(newElement)
            For i As Integer = 0 To PropertyList.Count - 1
                Dim p As PropertyReference = PropertyList(i)
                Dim np As PropertyReference = NewPropertyList(i)
                If p.Info.CanWrite Then
                    If p.Info.Name = np.Info.Name Then
                        Dim v As Object = np.Info.GetValue(newElement)
                        Dim ov As Object = np.Info.GetValue(newElement)
                        Dim oldElement As SceneElement = DirectCast(Me, SceneElement)
                        'If v.GetType = GetType(Integer) Then
                        '    Dim ianim As New DoubleAnimation(New Easing.CircleEase(Easing.EaseType.EaseInOut), CDbl(ov), CDbl(v), TimeSpan.FromMilliseconds(16.7), Scene.gameTime)
                        '    oldElement.BindAnimation(oldElement.HeightProperty, ianim)
                        '    oldElement.BindAnimation(oldElement.WidthProperty, ianim)
                        'ElseIf v.GetType = GetType(Double) Then
                        'End If
                        p.Info.SetValue(Me, v)

                    End If
                End If

            Next
        End Sub

#End Region

        Private Timeline As Timeline

        Public ReadOnly Property HasActiveAnimations As Boolean
            Get
                Return Timeline IsNot Nothing AndAlso Timeline.HasActiveAnimations
            End Get
        End Property

        Protected Friend MustOverride Sub Draw(gameTime As GameTime)


        Protected Friend Overridable Function ContainsPoint(p As Point) As Boolean
            Return Rectangle.Contains(p)
        End Function

        Public Shared RenderExceptionCount As Long
        Public Shared FirstRenderException As String
        Protected Friend Overridable Overloads Sub doDraw(gameTime As GameTime)
            If disposedValue Then Return
            Try
                spriteBatch.RenderOffset = If(Scene Is Nothing, Vector2.Zero, Scene.EffectRenderOffset)
                Dim rasterizer = spriteBatch.GraphicsDevice.RasterizerState
                If rasterizer IsNot Nothing AndAlso rasterizer.ScissorTestEnable Then
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                                      Scene.Quality.Sampler, DepthStencilState.None, rasterizer)
                Else
                    spriteBatch.Begin()
                End If
                Draw(gameTime)
                spriteBatch.End()
            Catch ex As Exception
                RenderExceptionCount += 1
                If FirstRenderException Is Nothing Then FirstRenderException = ex.ToString()
            End Try
        End Sub

        Public Overridable Sub Tick(gameTime As GameTime)
            If disposedValue Then Return
            Try
                Timeline?.Tick(gameTime)
            Catch ex As Exception

            End Try
        End Sub

        Public Sub updateScene(Scene As Scene)
            If Me.Scene Is Nothing Then
                Me.Scene = Scene
                Timeline = New Animations.Timeline(Scene.gameTime)
                If spriteBatch Is Nothing Then
                    spriteBatch = New SpriteBatchWrapper(Scene.graphicsDevice)
                    isElementSpriteBatch = True
                End If
            End If
        End Sub

        Public Function isBindedTo(Animation As AnimationBase) As Boolean
            Return Timeline.isChild(Animation)
        End Function

        Public Overloads Sub BindAnimation(TargetProperty As ElementProperty, Animation As AnimationBase)
            ' A rebuilt scene can retire the element currently under the pointer.
            ' Its synthetic MouseLeave arrives after RemoveElement has detached and
            ' disposed the timeline; ignore that stale animation request.
            If Timeline Is Nothing OrElse TargetProperty Is Nothing OrElse Animation Is Nothing Then Return
            Timeline.AddChild(Animation, TargetProperty)
        End Sub

        Public Overloads Sub UnbindAnimation(Animation As AnimationBase)
            Timeline.RemoveChild(Animation)
        End Sub

        Public Sub StopBoundAnimation(Animation As AnimationBase)
            Timeline?.StopChild(Animation)
        End Sub

        Private Sub LayoutGeometryChanged() Handles Me.SizeChanged, Me.PositionChanged, Me.AlignmentChanged
            ' Child positions are absolute and will be arranged separately.
            ' Moving an unscaled parent does not change their transform; walking
            ' its entire subtree here repeats the work at every tree level.
            SetRectangleProperty(RenderScale <> Vector2.One)
        End Sub

        Private Overloads Sub SetRectangleProperty(Optional updateDescendants As Boolean = True)
            Dim oldRect As Rectangle = Rectangle
            Dim width = Math.Max(0.0F, Size.X * RenderScale.X)
            Dim height = Math.Max(0.0F, Size.Y * RenderScale.Y)
            Dim x = Position.X + RenderTranslation.X - ((width - Size.X) / 2.0F)
            Dim y = Position.Y + RenderTranslation.Y - ((height - Size.Y) / 2.0F)
            Dim ancestor = Parent
            While ancestor IsNot Nothing
                Dim center = ancestor.Position + ancestor.Size / 2.0F
                x = center.X + (x - center.X) * ancestor.RenderScale.X + ancestor.RenderTranslation.X
                y = center.Y + (y - center.Y) * ancestor.RenderScale.Y + ancestor.RenderTranslation.Y
                width *= ancestor.RenderScale.X
                height *= ancestor.RenderScale.Y
                ancestor = ancestor.Parent
            End While
            _Rectangle = New Rectangle(CInt(Math.Round(x)), CInt(Math.Round(y)),
                                       CInt(Math.Round(width)), CInt(Math.Round(height)))
            If oldRect <> _Rectangle Then
                RaiseEvent RectangleChanged()
                Valid = False
                If updateDescendants AndAlso Children IsNot Nothing Then
                    For Each child In Children
                        child.SetRectangleProperty()
                    Next
                End If
            End If
        End Sub

        Public Function RelativeTo(Element As SceneElement) As Point
            Return New Point(CInt(Position.X + (Position.X - Element.Position.X)),
                             CInt(Position.Y + (Position.Y - Element.Position.Y)))
        End Function

        Public Sub New()

        End Sub

        Public Sub New(Scene As Scene)
            Me.Scene = Scene
            Me.GUID = System.Guid.NewGuid.ToString
            Timeline = New Animations.Timeline(Scene.gameTime)
            If spriteBatch Is Nothing Then
                spriteBatch = New SpriteBatchWrapper(Scene.graphicsDevice)
                isElementSpriteBatch = True
            End If
        End Sub

        Public Sub New(Scene As Scene, newSpritebatch As Boolean)
            Me.Scene = Scene
            Me.GUID = System.Guid.NewGuid.ToString
            Timeline = New Animations.Timeline(Scene.gameTime)
            If newSpritebatch Then
                spriteBatch = New SpriteBatchWrapper(Scene.graphicsDevice)
                isElementSpriteBatch = True
            Else
                spriteBatch = New SpriteBatchWrapper(Scene.GetSpriteBatch)
                _isManagedSpritebatch = True
                isElementSpriteBatch = False
            End If
        End Sub

        Public Sub New(Scene As Scene, ByRef spriteBatch As SpriteBatch)
            Me.Scene = Scene
            Me.GUID = System.Guid.NewGuid.ToString
            Timeline = New Animations.Timeline(Scene.gameTime)
            Me.spriteBatch = New SpriteBatchWrapper(spriteBatch)
            isElementSpriteBatch = False
            _isManagedSpritebatch = True
        End Sub

        Private Sub init()
            If Scene Is Nothing OrElse Not Scene.EnableDesignTimeControls Then Return
            If Not isPrototype AndAlso Not Me.GetType.IsSubclassOf(GetType(PrototypeElement)) Then
                ProtoButton = New PrototypeElement(Scene, Me)
                With ProtoButton
                    .AutoSize = ButtonAutoSize.X
                    .Size = New Vector2(.Size.X, 18)
                    .HorizontalAlign = HorizontalAlignment.Right
                    .VerticalAlign = VerticalAlignment.Top
                    .Text = "-"
                End With
                Children.Add(ProtoButton)
            End If
        End Sub

        Private Sub SceneElement_DrawFinished(DrawTime As TimeSpan) Handles Me.DrawFinished
            If Not isLoaded Then
                isLoaded = True
                RaiseEvent Loaded()
            Else
                'ProtoButton.doDraw(Scene.gameTime)
                ' ValidationCheck()
            End If
        End Sub

        Private Sub SceneElement_Loaded() Handles Me.Loaded
            init()
        End Sub

#Region "IDisposable Support"
        Private disposedValue As Boolean ' To detect redundant calls
        Public ReadOnly Property IsDisposed As Boolean
            Get
                Return disposedValue
            End Get
        End Property

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not Me.disposedValue Then
                Me.disposedValue = True
                If disposing Then
                    VisualEffect?.Dispose()
                    ' Detach the complete registered tree BEFORE clearing its
                    ' children. Otherwise their parent links disappear while
                    ' stale flat-list entries keep ticking/drawing every frame.
                    Scene?.RemoveElement(Me)
                    ' Dispose managed resources
                    ' Dispose children first
                    For Each child In _Children
                        child?.Dispose()
                    Next
                    _Children.Clear()

                    ' Dispose the spriteBatch wrapper if we own it
                    If isElementSpriteBatch AndAlso spriteBatch IsNot Nothing Then
                        spriteBatch.Dispose()
                        spriteBatch = Nothing
                    End If

                    ' Clear references to prevent memory leaks
                    Timeline = Nothing
                    ClickActionInstance = Nothing
                End If
            End If
            Me.disposedValue = True
        End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
#End Region

    End Class

    Public Enum AlignmentType
        Horizontal
        Vertical
        None
    End Enum

    Public Enum HorizontalAlignment
        Left
        Center
        Right
        Stretch
    End Enum

    Public Enum VerticalAlignment
        Top
        Center
        Bottom
        Stretch
    End Enum

    Public Enum DisplayReservation
        FloatBoth
        FloatX
        FloatY
        ReserveBoth
        ReserveX
        ReserveY
    End Enum

End Namespace
