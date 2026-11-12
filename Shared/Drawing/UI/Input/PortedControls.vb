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
            MyBase.AlignChildren()
            Dim thickness = Math.Max(0, BorderThickness)
            For Each edge In edges
                edge.BackgroundColor = BorderColor
                edge.isVisible = False
            Next
            edges(0).Position = Position : edges(0).Size = New Vector2(Size.X, thickness)
            edges(1).Position = New Vector2(Position.X, Position.Y + Size.Y - thickness) : edges(1).Size = New Vector2(Size.X, thickness)
            edges(2).Position = Position : edges(2).Size = New Vector2(thickness, Size.Y)
            edges(3).Position = New Vector2(Position.X + Size.X - thickness, Position.Y) : edges(3).Size = New Vector2(thickness, Size.Y)
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
        ''' <summary>Project-Z elements embedded after the editable vector text.</summary>
        Public ReadOnly Property Elements As New List(Of SceneElement)
        Protected Overrides Sub AlignChildren()
            MyBase.AlignChildren()
            Dim y = TextElement.Position.Y + TextElement.Size.Y + Padding.Bottom
            For Each element In Elements
                element.Position = New Vector2(Position.X + Padding.Left, y)
                y += element.Size.Y + element.Margin.Top + element.Margin.Bottom
            Next
        End Sub
        Public Overrides Function GetCopyText() As String
            If SelectionLength > 0 Then Return MyBase.GetCopyText()
            Return String.Join(Environment.NewLine, New String() {Text}.Concat(Elements.Select(Function(element) element.GetCopyText())).Where(Function(value) Not String.IsNullOrEmpty(value)))
        End Function
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
        Private Shadows ReadOnly ownerScene As Scene
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
        ''' <summary>Attach before Open. The application owns this controller; Stop is called on disposal.</summary>
        Public Property PlaybackController As IMediaPlaybackController
#If WINDOWS Then
        Private session As Global.ProjectZ.Windows.Media.WindowsVideoSession
        Private videoTexture As Texture2D
#End If
        Private volumeValue As Double = 1
        Public Property Volume As Double
            Get
                Return volumeValue
            End Get
            Set(value As Double)
                volumeValue = Math.Clamp(value, 0, 1)
                PlaybackController?.SetVolume(volumeValue)
#If WINDOWS Then
                session?.SetVolume(volumeValue)
#End If
            End Set
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
        End Sub

        Public Sub Open(path As String)
            [Stop]()
            Duration = TimeSpan.Zero
            Source = path
            Try
                Dim extension = IO.Path.GetExtension(path).ToLowerInvariant()
                If {".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"}.Contains(extension) Then
                    Load(path)
                    RaiseEvent MediaOpened()
                Else
                    If PlaybackController IsNot Nothing Then
                        Texture = Nothing
                        PlaybackController.SetVolume(Volume)
                        PlaybackController.Open(path, DecodePixelSize)
                        Return
                    End If
#If WINDOWS Then
                    session = New Global.ProjectZ.Windows.Media.WindowsVideoSession(path, Volume, DecodePixelSize)
                    Texture = Nothing
#Else
                    Throw New PlatformNotSupportedException("Built-in video decoding requires Windows. Use PresentFrame for an application-provided decoder.")
#End If
                End If
            Catch ex As Exception
                RaiseEvent MediaFailed(ex.Message)
            End Try
        End Sub

        Public Sub PresentFrame(frame As Texture2D, timestamp As TimeSpan)
            If frame Is Nothing Then Throw New ArgumentNullException(NameOf(frame))
            PublishFrame(frame)
            PlaybackPosition = timestamp
            If PlaybackController Is Nothing AndAlso Not IsPlaying Then IsPlaying = True
        End Sub

        Private Sub PublishFrame(frame As Texture2D)
            If Texture Is frame Then
                InvalidateTexturePixels()
            Else
                Texture = frame
            End If
            _PresentedFrameCount += 1
        End Sub

        Public Sub SignalOpened(duration As TimeSpan)
            Me.Duration = duration
            RaiseEvent MediaOpened()
        End Sub

        Public Sub SignalFailed(message As String)
            IsPlaying = False
            RaiseEvent MediaFailed(message)
        End Sub

        Public Sub Play()
            If PlaybackController IsNot Nothing Then
                PlaybackController.Play()
                IsPlaying = True
                Return
            End If
#If WINDOWS Then
            If session Is Nothing Then
                If String.IsNullOrWhiteSpace(Source) Then Return
                Dim position = PlaybackPosition
                Open(Source)
                If session Is Nothing Then Return
                If position > TimeSpan.Zero Then Seek(position)
            End If
            IsPlaying = True
            session.Play()
#Else
            RaiseEvent MediaFailed("Built-in video decoding requires Windows. Use PresentFrame for an application-provided decoder.")
#End If
        End Sub
        Public Sub Pause()
            IsPlaying = False
            PlaybackController?.Pause()
#If WINDOWS Then
            session?.Pause()
#End If
        End Sub
        Public Sub [Stop]()
            IsPlaying = False
            PlaybackController?.Stop()
#If WINDOWS Then
            session?.Dispose()
            session = Nothing
#End If
            PlaybackPosition = TimeSpan.Zero
        End Sub
        Public Sub Seek(position As TimeSpan)
            position = TimeSpan.FromTicks(Math.Max(0, position.Ticks))
            If Duration > TimeSpan.Zero AndAlso position > Duration Then position = Duration
            PlaybackPosition = position
            PlaybackController?.Seek(position)
#If WINDOWS Then
            session?.Seek(position)
#End If
        End Sub

        Public Overrides Sub Tick(gameTime As GameTime)
            MyBase.Tick(gameTime)
#If WINDOWS Then
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
                RaiseEvent MediaOpened()
                If session IsNot current Then Return
            End If
            Dim frame = current.TakeFrame()
            If frame IsNot Nothing Then
                Try
                    If videoTexture Is Nothing OrElse videoTexture.IsDisposed OrElse
                       videoTexture.Width <> frame.Width OrElse videoTexture.Height <> frame.Height Then
                        Dim replacement As New Texture2D(Scene.graphicsDevice, frame.Width, frame.Height)
                        Try
                            replacement.SetData(frame.Pixels)
                        Catch
                            replacement.Dispose()
                            Throw
                        End Try
                        Dim prior = videoTexture
                        videoTexture = replacement
                        PublishFrame(replacement)
                        prior?.Dispose()
                    Else
                        videoTexture.SetData(frame.Pixels)
                        PublishFrame(videoTexture)
                    End If
                Catch ex As Exception
                    [Stop]()
                    RaiseEvent MediaFailed(ex.Message)
                    Return
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
#End If
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                [Stop]()
                Texture = Nothing
#If WINDOWS Then
                videoTexture?.Dispose()
#End If
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
