Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing.UI.Advanced
Imports ProjectZ.Shared.Drawing.UI.Layout
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.UI.Input
    <Serializable>
    Public Class Label
        Inherits TextElement
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
        End Sub
    End Class

    <Serializable>
    Public Class Border
        Inherits RectangleElement
        Private ReadOnly edges As RectangleElement()

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BorderColor = New Color(55, 255, 200)
            BorderThickness = 1
            edges = {
                New RectangleElement(scene) With {.isMouseBypassEnabled = True, .isVisible = False},
                New RectangleElement(scene) With {.isMouseBypassEnabled = True, .isVisible = False},
                New RectangleElement(scene) With {.isMouseBypassEnabled = True, .isVisible = False},
                New RectangleElement(scene) With {.isMouseBypassEnabled = True, .isVisible = False}
            }
            Children.AddRange(edges)
        End Sub

        Protected Overrides Sub AlignChildren()
            Dim thickness = Math.Max(0, BorderThickness)
            For Each edge In edges
                edge.BackgroundColor = BorderColor
                edge.isVisible = False
            Next
            edges(0).Position = Position : edges(0).Size = New Vector2(Size.X, thickness)
            edges(1).Position = New Vector2(Position.X, Position.Y + Size.Y - thickness) : edges(1).Size = New Vector2(Size.X, thickness)
            edges(2).Position = Position : edges(2).Size = New Vector2(thickness, Size.Y)
            edges(3).Position = New Vector2(Position.X + Size.X - thickness, Position.Y) : edges(3).Size = New Vector2(thickness, Size.Y)
            For Each child In Children
                If Not edges.Cast(Of SceneElement)().Contains(child) Then
                    child.Position = New Vector2(Position.X + Padding.Left, Position.Y + Padding.Top)
                End If
            Next
        End Sub

    End Class

    <Serializable>
    Public Class ToggleButton
        Inherits Button
        Private _isChecked As Boolean
        Public Event CheckedChanged(isChecked As Boolean)
        Public Property IsChecked As Boolean
            Get
                Return _isChecked
            End Get
            Set(value As Boolean)
                If _isChecked = value Then Return
                _isChecked = value
                RaiseEvent CheckedChanged(value)
            End Set
        End Property
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            AddHandler MouseLeftClick, Sub(point) IsChecked = Not IsChecked
        End Sub
    End Class

    <Serializable>
    Public Class RichTextBox
        Inherits Textbox
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            AcceptsReturn = True
        End Sub
    End Class

    <Serializable>
    Public Class Popup
        Inherits RectangleElement
        Public Property IsOpen As Boolean
            Get
                Return isVisible
            End Get
            Set(value As Boolean)
                isVisible = value
                isEnabled = value
                If value Then BringToFront()
            End Set
        End Property
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            IsOverlay = True
            IsOpen = False
        End Sub
    End Class

    <Serializable>
    Public Class MenuItem
        Inherits Button
        Public Property Header As String
            Get
                Return Text
            End Get
            Set(value As String)
                Text = value
            End Set
        End Property
        Public Sub New(scene As Scene)
            MyBase.New(scene)
        End Sub
    End Class

    <Serializable>
    Public Class ContextMenu
        Inherits StackPanel
        Private ReadOnly ownerScene As Scene
        Public Property DismissDistance As Single = 28.0F
        Public Property IsOpen As Boolean
            Get
                Return isVisible
            End Get
            Set(value As Boolean)
                isVisible = value
                isEnabled = value
                If value Then BringToFront()
            End Set
        End Property
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            ownerScene = scene
            IsOverlay = True
            Orientation = Orientation.Vertical
            BackgroundColor = New Color(18, 18, 18, 245)
            Padding = New Thickness(4)
            IsOpen = False
            If ownerScene IsNot Nothing Then
                AddHandler ownerScene.OnMouseMove, AddressOf HandleSceneMouseMove
                AddHandler ownerScene.OnMouseLeftDown, AddressOf HandleSceneMouseLeftDown
                AddHandler ownerScene.OnPointerCancelled, AddressOf HandlePointerCancelled
            End If
        End Sub

        Private Function ContainsWithMargin(point As Point, margin As Single) As Boolean
            Return point.X >= Position.X - margin AndAlso
                   point.Y >= Position.Y - margin AndAlso
                   point.X <= Position.X + Size.X + margin AndAlso
                   point.Y <= Position.Y + Size.Y + margin
        End Function

        Private Sub HandleSceneMouseMove(currentPoint As Point, lastPoint As Point)
            If IsOpen AndAlso Not ContainsWithMargin(currentPoint, DismissDistance) Then IsOpen = False
        End Sub

        Private Sub HandleSceneMouseLeftDown(point As Point)
            If IsOpen AndAlso Not ContainsWithMargin(point, 0) Then IsOpen = False
        End Sub

        Private Sub HandlePointerCancelled()
            If IsOpen Then IsOpen = False
        End Sub

        Public Sub AddItem(item As MenuItem)
            AddChild(item)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso ownerScene IsNot Nothing Then
                RemoveHandler ownerScene.OnMouseMove, AddressOf HandleSceneMouseMove
                RemoveHandler ownerScene.OnMouseLeftDown, AddressOf HandleSceneMouseLeftDown
                RemoveHandler ownerScene.OnPointerCancelled, AddressOf HandlePointerCancelled
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class

    ''' <summary>
    ''' GPU-owned video control with asynchronous Windows decoding and audio.
    ''' All presentation, clipping and masks remain in the Project-Z scene.
    ''' </summary>
    Public Class MediaElement
        Inherits ImageElement
        Public Event MediaOpened()
        Public Event MediaEnded()
        Public Event MediaFailed(message As String)
        Public Property IsPlaying As Boolean
        Public Property PlaybackPosition As TimeSpan
        Public Property Duration As TimeSpan
        Public Property Looping As Boolean
        Public Property DecodePixelSize As Integer = 640
        Public ReadOnly Property PresentedFrameCount As Long
        Private session As Global.ProjectZ.Windows.Media.WindowsVideoSession
        Private videoTexture As Texture2D
        Private pendingSeek As TimeSpan
        Private volumeValue As Double = 1
        Public Property Volume As Double
            Get
                Return volumeValue
            End Get
            Set(value As Double)
                volumeValue = Math.Clamp(value, 0, 1)
                session?.SetVolume(volumeValue)
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
        End Sub

        Public Sub Open(path As String)
            [Stop]()
            Source = path
            Try
                Dim extension = IO.Path.GetExtension(path).ToLowerInvariant()
                If {".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"}.Contains(extension) Then
                    Load(path)
                    RaiseEvent MediaOpened()
                Else
                    session = New Global.ProjectZ.Windows.Media.WindowsVideoSession(path, Volume, DecodePixelSize)
                End If
            Catch ex As Exception
                RaiseEvent MediaFailed(ex.Message)
            End Try
        End Sub

        Public Sub PresentFrame(frame As Texture2D, timestamp As TimeSpan)
            Texture = frame
            InvalidateTexturePixels()
            PlaybackPosition = timestamp
            If Not IsPlaying Then IsPlaying = True
        End Sub

        Public Sub Play()
            If session Is Nothing Then
                If String.IsNullOrWhiteSpace(Source) Then Return
                Dim position = PlaybackPosition
                Open(Source)
                pendingSeek = position
            End If
            IsPlaying = True
            session?.Play()
        End Sub
        Public Sub Pause()
            IsPlaying = False
            session?.Pause()
        End Sub
        Public Sub [Stop]()
            IsPlaying = False
            session?.Dispose()
            session = Nothing
            PlaybackPosition = TimeSpan.Zero
            pendingSeek = TimeSpan.Zero
        End Sub
        Public Sub Seek(position As TimeSpan)
            position = TimeSpan.FromTicks(Math.Max(0, position.Ticks))
            If Duration > TimeSpan.Zero AndAlso position > Duration Then position = Duration
            PlaybackPosition = position
            pendingSeek = position
            session?.Seek(position)
        End Sub

        Public Overrides Sub Tick(gameTime As GameTime)
            MyBase.Tick(gameTime)
            Dim current = session
            If current Is Nothing Then Return
            Dim failure = current.TakeError()
            If failure IsNot Nothing Then
                [Stop]()
                RaiseEvent MediaFailed(failure)
                Return
            End If
            If current.TakeOpened() Then
                Duration = current.Duration
                If pendingSeek > TimeSpan.Zero Then current.Seek(pendingSeek)
                pendingSeek = TimeSpan.Zero
                RaiseEvent MediaOpened()
            End If
            Dim frame = current.TakeFrame()
            If frame IsNot Nothing Then
                Try
                    If videoTexture Is Nothing OrElse videoTexture.Width <> frame.Width OrElse videoTexture.Height <> frame.Height Then
                        Texture = Nothing
                        videoTexture?.Dispose()
                        videoTexture = New Texture2D(Scene.graphicsDevice, frame.Width, frame.Height)
                    End If
                    videoTexture.SetData(frame.Pixels)
                    Texture = videoTexture
                    InvalidateTexturePixels()
                    _PresentedFrameCount += 1
                Finally
                    current.ReturnFrame(frame)
                End Try
            End If
            PlaybackPosition = current.Position
            If current.TakeEnded() Then
                IsPlaying = False
                RaiseEvent MediaEnded()
                If Looping AndAlso session Is current Then
                    current.Seek(TimeSpan.Zero)
                    Play()
                End If
            End If
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                [Stop]()
                Texture = Nothing
                videoTexture?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
        Public Sub SignalEnded()
            IsPlaying = False
            RaiseEvent MediaEnded()
        End Sub
    End Class

    <Serializable>
    Public Class LoadingIndicator
        Inherits TextElement
        Private elapsed As Double
        Private frame As Integer
        Public Sub New(scene As Scene)
            MyBase.New(scene)
            Text = "*..."
        End Sub
        Public Overrides Sub Tick(gameTime As GameTime)
            MyBase.Tick(gameTime)
            elapsed += gameTime.ElapsedGameTime.TotalMilliseconds
            If elapsed >= 140 Then
                elapsed = 0
                frame = (frame + 1) Mod 4
                Text = New String("."c, frame) & "*" & New String("."c, 3 - frame)
            End If
        End Sub
    End Class
End Namespace
