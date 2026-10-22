Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.UI.Input
    ''' <summary>A native two-state slider with button keyboard semantics and an optional label.</summary>
    Public Class ToggleSwitch
        Inherits Button

        Private ReadOnly track As RectangleElement
        Private ReadOnly thumb As RectangleElement
        Private pointerDown, dragged, dragOnTrack, suppressClick As Boolean
        Private checkedValue As Boolean
        Private pressX, pressValue, displayValue, transitionFrom As Single
        Private transitionAt As Double
        Private transitioning As Boolean

        Public Event CheckedChanged(isChecked As Boolean)
        Public Property IsChecked As Boolean
            Get
                Return checkedValue
            End Get
            Set(value As Boolean)
                If checkedValue = value Then Return
                checkedValue = value
                BeginTransition()
                RaiseEvent CheckedChanged(value)
            End Set
        End Property

        Public Property OnTrackColor As Color = New Color(31, 163, 136)
        Public Property OffTrackColor As Color = New Color(49, 68, 72)
        Public Property ThumbColor As Color = New Color(235, 248, 245)
        Public Property FocusColor As Color = New Color(108, 222, 203)
        Public Property SwitchWidth As Single = 36
        Public Property SwitchHeight As Single = 20
        Public Property AnimationDuration As TimeSpan = TimeSpan.FromMilliseconds(120)

        ''' <summary>Label displayed beside the switch; Text is the equivalent inherited property.</summary>
        Public Property Content As String
            Get
                Return Text
            End Get
            Set(value As String)
                Text = If(value, String.Empty)
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            AutoSize = ButtonAutoSize.None
            Size = New Vector2(152, 28)
            Font = "SegoeUI_10"
            TabStop = True
            BackgroundColor = Color.Transparent
            MouseOverBackgroundColor = Color.Transparent
            MouseDownBackgroundColor = Color.Transparent
            BorderThickness = 0
            TextElement.TextWrapping = TextWrapping.NoWrap
            track = New RectangleElement(scene) With {.isMouseBypassEnabled = True}
            thumb = New RectangleElement(scene) With {.isMouseBypassEnabled = True}
            Children.Add(track)
            Children.Add(thumb)
            AddHandler MouseLeftClick, Sub(point)
                If Not suppressClick AndAlso isEnabled Then IsChecked = Not IsChecked
                suppressClick = False
            End Sub
            AddHandler MouseLeftDown, AddressOf BeginGesture
            AddHandler MouseDrag, AddressOf DragGesture
            AddHandler MouseLeftUp, AddressOf EndGesture
            AddHandler OnKeyDown, AddressOf KeyPressed
            AddHandler scene.OnPointerCancelled, AddressOf CancelGesture
        End Sub

        Private Sub BeginTransition()
            transitionFrom = displayValue
            transitionAt = Scene.gameTime.TotalGameTime.TotalMilliseconds
            transitioning = isLoaded AndAlso AnimationDuration > TimeSpan.Zero
            If Not transitioning Then displayValue = If(IsChecked, 1.0F, 0.0F)
            Scene.RequestRender()
        End Sub

        Private Sub BeginGesture(point As Point)
            pointerDown = True
            suppressClick = False
            dragged = False
            pressX = point.X
            pressValue = If(IsChecked, 1.0F, 0.0F)
            dragOnTrack = point.X <= track.Size.X
            Scene.CapturePointer(Me)
        End Sub

        Private Sub DragGesture(current As Point, origin As Point)
            If Not pointerDown OrElse Not dragOnTrack OrElse Not isEnabled Then Return
            dragged = True
            transitioning = False
            displayValue = Math.Clamp(pressValue + (current.X - pressX) / Math.Max(1, track.Size.X - track.Size.Y), 0, 1)
            ArrangeVisuals()
            Scene.RequestRender()
        End Sub

        Private Sub EndGesture(point As Point)
            If Not pointerDown Then Return
            pointerDown = False
            Scene.ReleasePointerCapture(Me)
            If Not dragged Then Return ' Button click and keyboard activation toggle once on release.
            suppressClick = True
            dragged = False
            If isEnabled Then IsChecked = displayValue >= 0.5F
            BeginTransition()
        End Sub

        Private Sub CancelGesture()
            If Not pointerDown Then Return
            pointerDown = False
            suppressClick = True
            dragged = False
            Scene.ReleasePointerCapture(Me)
            BeginTransition()
        End Sub

        Private Sub KeyPressed(key As Keys, state As KeyboardState)
            If Not isEnabled Then Return
            Select Case key
                Case Keys.Left, Keys.Home : IsChecked = False
                Case Keys.Right, Keys.End : IsChecked = True
                Case Keys.Escape : CancelGesture()
            End Select
        End Sub

        Public Overrides Sub Tick(gameTime As GameTime)
            MyBase.Tick(gameTime)
            If Not pointerDown Then suppressClick = False
            If pointerDown AndAlso (Not isEnabled OrElse Not isVisible OrElse Scene.CapturedPointerElement IsNot Me) Then CancelGesture()
            If transitioning Then
                Dim progress = CSng(Math.Clamp((gameTime.TotalGameTime.TotalMilliseconds - transitionAt) / Math.Max(1, AnimationDuration.TotalMilliseconds), 0, 1))
                displayValue = MathHelper.Lerp(transitionFrom, If(IsChecked, 1.0F, 0.0F), progress * progress * (3 - 2 * progress))
                transitioning = progress < 1
                Scene.RequestRender()
            End If
            ArrangeVisuals()
        End Sub

        Protected Overrides Sub AlignChildren()
            ArrangeVisuals()
        End Sub

        Private Sub ArrangeVisuals()
            If track Is Nothing OrElse thumb Is Nothing OrElse TextElement Is Nothing Then Return
            Dim width = Math.Clamp(SwitchWidth, 0, Math.Max(0, Size.X))
            Dim height = Math.Clamp(SwitchHeight, 0, Math.Max(0, Math.Min(Size.Y - 4, width / 2)))
            track.Position = Position + New Vector2(0, (Size.Y - height) / 2)
            track.Size = New Vector2(width, height)
            track.CornerRadius = height / 2
            track.BackgroundColor = Color.Lerp(OffTrackColor, OnTrackColor, displayValue)
            track.BorderThickness = If(isSelected, 2, If(isMouseOver, 1, 0))
            track.BorderColor = FocusColor
            Dim diameter = Math.Max(0, height - 4)
            thumb.Position = track.Position + New Vector2(2 + displayValue * Math.Max(0, width - diameter - 4), 2)
            thumb.Size = New Vector2(diameter)
            thumb.CornerRadius = diameter / 2
            thumb.BackgroundColor = ThumbColor
            track.Opacity = If(isEnabled, 1.0F, 0.45F)
            thumb.Opacity = track.Opacity
            TextElement.Position = Position + New Vector2(width + 8, (Size.Y - Scene.MeasureText(Font, If(Text, "")).Y) / 2)
            TextElement.Size = New Vector2(Math.Max(0, Size.X - width - 8), Size.Y)
            TextElement.TextAlignment = HorizontalAlignment.Left
            TextElement.MaxWidth = Math.Max(1, Size.X - width - 8)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                RemoveHandler Scene.OnPointerCancelled, AddressOf CancelGesture
                Scene.ReleasePointerCapture(Me)
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
