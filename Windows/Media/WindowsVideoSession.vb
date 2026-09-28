Imports System.Collections.Concurrent
Imports System.Threading
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading

Namespace Windows.Media
    ' Windows' media engine supplies codecs, seeking and synchronized audio.
    ' No WPF control/window is hosted: decoded pixels are presented by Project-Z.
    Friend NotInheritable Class WindowsVideoSession
        Implements IDisposable

        Friend NotInheritable Class VideoFrame
            Public Pixels As Byte()
            Public Width As Integer
            Public Height As Integer
        End Class

        Private ReadOnly commands As New ConcurrentQueue(Of Action(Of MediaPlayer))()
        Private ReadOnly buffers As New ConcurrentQueue(Of Byte())()
        Private ReadOnly sync As New Object()
        Private latest As VideoFrame
        Private failure As String
        Private disposed As Integer
        Private opened As Integer
        Private ended As Integer
        Private positionTicks As Long
        Private durationTicks As Long
        Private playing As Boolean
        Private player As MediaPlayer
        Private timer As DispatcherTimer
        Private dispatcher As Dispatcher
        Private bitmap As RenderTargetBitmap
        Private visual As DrawingVisual
        Private ReadOnly source As String
        Private ReadOnly maxSize As Integer

        Public Sub New(source As String, volume As Double, maxSize As Integer)
            Me.source = source
            Me.maxSize = Math.Clamp(maxSize, 64, 1280)
            Dim worker As New Thread(Sub() Run(volume)) With {.IsBackground = True, .Name = "Project-Z media decoder"}
            worker.SetApartmentState(ApartmentState.STA)
            worker.Start()
        End Sub

        Private Sub Run(volume As Double)
            Try
                dispatcher = Dispatcher.CurrentDispatcher
                visual = New DrawingVisual()
                player = New MediaPlayer With {.Volume = volume, .ScrubbingEnabled = True}
                AddHandler player.MediaOpened,
                    Sub()
                        If player.NaturalVideoWidth <= 0 OrElse player.NaturalVideoHeight <= 0 Then
                            Fail("This file has no supported video stream.")
                            Return
                        End If
                        Dim scale = Math.Min(1.0, maxSize / CDbl(Math.Max(player.NaturalVideoWidth, player.NaturalVideoHeight)))
                        bitmap = New RenderTargetBitmap(Math.Max(1, CInt(player.NaturalVideoWidth * scale)),
                            Math.Max(1, CInt(player.NaturalVideoHeight * scale)), 96, 96, System.Windows.Media.PixelFormats.Pbgra32)
                        If player.NaturalDuration.HasTimeSpan Then Interlocked.Exchange(durationTicks, player.NaturalDuration.TimeSpan.Ticks)
                        Interlocked.Exchange(opened, 1)
                    End Sub
                AddHandler player.MediaFailed, Sub(sender, args) Fail(args.ErrorException.Message)
                AddHandler player.MediaEnded,
                    Sub()
                        playing = False
                        Interlocked.Exchange(ended, 1)
                    End Sub
                timer = New DispatcherTimer(DispatcherPriority.Background, dispatcher) With {.Interval = TimeSpan.FromSeconds(1 / 30.0)}
                AddHandler timer.Tick, AddressOf Tick
                timer.Start()
                Dim uri As Uri = Nothing
                If Not Uri.TryCreate(source, UriKind.Absolute, uri) Then uri = New Uri(IO.Path.GetFullPath(source))
                player.Open(uri)
                Dispatcher.Run()
            Catch ex As Exception
                Fail(ex.Message)
            Finally
                timer?.Stop()
                player?.Close()
            End Try
        End Sub

        Private Sub Tick(sender As Object, args As EventArgs)
            If Volatile.Read(disposed) <> 0 Then
                timer.Stop()
                player.Close()
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal)
                Return
            End If
            Try
                Dim command As Action(Of MediaPlayer) = Nothing
                While commands.TryDequeue(command)
                    command(player)
                End While
                Interlocked.Exchange(positionTicks, player.Position.Ticks)
                If Not playing OrElse bitmap Is Nothing Then Return
                Using drawing = visual.RenderOpen()
                    drawing.DrawVideo(player, New System.Windows.Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight))
                End Using
                bitmap.Clear()
                bitmap.Render(visual)
                Dim pixels As Byte() = Nothing
                Dim length = bitmap.PixelWidth * bitmap.PixelHeight * 4
                If Not buffers.TryDequeue(pixels) OrElse pixels.Length <> length Then pixels = New Byte(length - 1) {}
                bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0)
                ' WPF's premultiplied BGRA -> MonoGame's premultiplied RGBA.
                For offset = 0 To length - 1 Step 4
                    Dim blue = pixels(offset)
                    pixels(offset) = pixels(offset + 2)
                    pixels(offset + 2) = blue
                Next
                SyncLock sync
                    If latest IsNot Nothing Then buffers.Enqueue(latest.Pixels)
                    latest = New VideoFrame With {.Pixels = pixels, .Width = bitmap.PixelWidth, .Height = bitmap.PixelHeight}
                End SyncLock
            Catch ex As Exception
                Fail(ex.Message)
            End Try
        End Sub

        Private Sub Fail(message As String)
            SyncLock sync
                failure = message
            End SyncLock
            playing = False
        End Sub

        Public Sub Play()
            commands.Enqueue(Sub(media)
                                 Interlocked.Exchange(ended, 0)
                                 playing = True
                                 media.Play()
                             End Sub)
        End Sub

        Public Sub Pause()
            commands.Enqueue(Sub(media)
                                 playing = False
                                 media.Pause()
                             End Sub)
        End Sub

        Public Sub Seek(position As TimeSpan)
            commands.Enqueue(Sub(media) media.Position = position)
        End Sub

        Public Sub SetVolume(volume As Double)
            commands.Enqueue(Sub(media) media.Volume = volume)
        End Sub

        Public ReadOnly Property Position As TimeSpan
            Get
                Return TimeSpan.FromTicks(Interlocked.Read(positionTicks))
            End Get
        End Property

        Public ReadOnly Property Duration As TimeSpan
            Get
                Return TimeSpan.FromTicks(Interlocked.Read(durationTicks))
            End Get
        End Property

        Public Function TakeOpened() As Boolean
            Return Interlocked.Exchange(opened, 0) <> 0
        End Function

        Public Function TakeEnded() As Boolean
            Return Interlocked.Exchange(ended, 0) <> 0
        End Function

        Public Function TakeError() As String
            SyncLock sync
                Dim result = failure
                failure = Nothing
                Return result
            End SyncLock
        End Function

        Public Function TakeFrame() As VideoFrame
            SyncLock sync
                Dim frame = latest
                latest = Nothing
                Return frame
            End SyncLock
        End Function

        Public Sub ReturnFrame(frame As VideoFrame)
            buffers.Enqueue(frame.Pixels)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Interlocked.Exchange(disposed, 1)
        End Sub
    End Class
End Namespace
