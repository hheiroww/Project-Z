Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
Imports Microsoft.Win32.SafeHandles

Namespace Windows.Media
    ' The Windows media engine supplies codecs, seeking and synchronized audio.
    ' Decoded pixels are presented by Project-Z's scene.
    Friend NotInheritable Class WindowsVideoSession
        Implements IDisposable

        Friend NotInheritable Class VideoFrame
            Public Pixels As Byte()
            Public Width As Integer
            Public Height As Integer
        End Class

        Private Shared ReadOnly FrameInterval As Long = Stopwatch.Frequency \ 30
        Private Shared ReadOnly SeekSettleInterval As Long = Stopwatch.Frequency \ 20
        Private Shared ReadOnly StillRetryWindow As Long = Stopwatch.Frequency \ 3
        Private Const HighResolutionTimer As UInteger = &H2UI
        Private Const SynchronizeTimer As UInteger = &H100000UI
        Private Const ModifyTimer As UInteger = &H2UI
        Private ReadOnly sync As New Object()
        Private ReadOnly reusableFrames As New Stack(Of VideoFrame)(3)
        Private latest As VideoFrame
        Private failure As String
        Private disposed As Integer
        Private opened As Integer
        Private ended As Integer
        Private wakeQueued As Integer
        Private positionTicks As Long
        Private durationTicks As Long
        Private requestedPlaying As Boolean
        Private requestedVolume As Double
        Private requestedPosition As TimeSpan?
        Private requestedSeekRevision As Long
        Private activeSeekRevision As Long
        Private mediaReady As Boolean
        Private playing As Boolean
        Private nextCapture As Long
        Private stillDeadline As Long
        Private player As MediaPlayer
        Private timer As DispatcherTimer
        Private timerSignal As WaitHandle
        Private timerRegistration As RegisteredWaitHandle
        Private dispatcher As Dispatcher
        Private bitmap As RenderTargetBitmap
        Private visual As DrawingVisual
        Private ReadOnly source As String
        Private ReadOnly maxSize As Integer

        Public Sub New(source As String, volume As Double, maxSize As Integer)
            Me.source = source
            Me.maxSize = Math.Clamp(maxSize, 64, 1280)
            requestedVolume = volume
            Dim worker As New Thread(AddressOf Run) With {.IsBackground = True, .Name = "Project-Z media decoder"}
            worker.SetApartmentState(ApartmentState.STA)
            worker.Start()
        End Sub

        Private Sub Run()
            Try
                Dim current = Dispatcher.CurrentDispatcher
                player = New MediaPlayer With {.ScrubbingEnabled = True}
                AddHandler player.MediaOpened, AddressOf OnOpened
                AddHandler player.MediaFailed, Sub(sender, args) Fail(If(args.ErrorException?.Message, "Windows media playback failed."))
                AddHandler player.MediaEnded, AddressOf OnEnded
                timer = New DispatcherTimer(DispatcherPriority.Background, current)
                AddHandler timer.Tick, Sub() Pump()
                ' Keep the 30 Hz capture phase without the Windows message
                ' timer's granularity. Older systems use the dispatcher timer.
                Dim handle = CreateWaitableTimerEx(IntPtr.Zero, Nothing, HighResolutionTimer, SynchronizeTimer Or ModifyTimer)
                If handle <> IntPtr.Zero Then
                    timerSignal = New WaitableTimer(handle)
                    timerRegistration = ThreadPool.RegisterWaitForSingleObject(timerSignal, Sub(state, timedOut) Wake(), Nothing, Timeout.Infinite, False)
                End If
                Volatile.Write(dispatcher, current)
                If Volatile.Read(disposed) <> 0 Then Return

                Dim uri As Uri = Nothing
                If Not Uri.TryCreate(source, UriKind.Absolute, uri) Then uri = New Uri(IO.Path.GetFullPath(source))
                player.Open(uri)
                Dispatcher.Run()
            Catch ex As Exception
                Fail(ex.Message)
            Finally
                Volatile.Write(dispatcher, Nothing)
                timerRegistration?.Unregister(Nothing)
                timerSignal?.Dispose()
                timer?.Stop()
                player?.Close()
                timer = Nothing
                player = Nothing
                bitmap = Nothing
                visual = Nothing
            End Try
        End Sub

        Private Sub OnOpened(sender As Object, args As EventArgs)
            If Volatile.Read(disposed) <> 0 Then Return
            Try
                If player.NaturalVideoWidth > 0 AndAlso player.NaturalVideoHeight > 0 Then
                    Dim scale = Math.Min(1.0, maxSize / CDbl(Math.Max(player.NaturalVideoWidth, player.NaturalVideoHeight)))
                    bitmap = New RenderTargetBitmap(Math.Max(1, CInt(player.NaturalVideoWidth * scale)),
                        Math.Max(1, CInt(player.NaturalVideoHeight * scale)), 96, 96, System.Windows.Media.PixelFormats.Pbgra32)
                    visual = New DrawingVisual()
                    Using drawing = visual.RenderOpen()
                        drawing.DrawVideo(player, New System.Windows.Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight))
                    End Using
                    PrepareStillCapture()
                End If
                If player.NaturalDuration.HasTimeSpan Then Interlocked.Exchange(durationTicks, player.NaturalDuration.TimeSpan.Ticks)
                player.Pause()
                mediaReady = True
                Pump()
                Interlocked.Exchange(opened, 1)
            Catch ex As Exception
                Fail(ex.Message)
            End Try
        End Sub

        Private Sub OnEnded(sender As Object, args As EventArgs)
            playing = False
            Dim ticks = player.Position.Ticks
            SyncLock sync
                If requestedSeekRevision <> activeSeekRevision Then Return
                requestedPlaying = False
                positionTicks = ticks
                Interlocked.Exchange(ended, 1)
            End SyncLock
        End Sub

        Private Sub PrepareStillCapture()
            ' The WPF player updates its paused picture asynchronously after a seek.
            nextCapture = Stopwatch.GetTimestamp() + SeekSettleInterval
            stillDeadline = nextCapture + StillRetryWindow
        End Sub

        ' The media dispatcher sleeps when a paused picture is ready. Coalesce
        ' input changes so a drag never queues one callback or seek per event.
        Private Sub Wake()
            Dim current = Volatile.Read(dispatcher)
            If current Is Nothing OrElse current.HasShutdownStarted OrElse Interlocked.Exchange(wakeQueued, 1) <> 0 Then Return
            current.BeginInvoke(DispatcherPriority.Background, New Action(Sub()
                                                                              Interlocked.Exchange(wakeQueued, 0)
                                                                              Pump()
                                                                          End Sub))
        End Sub

        Private Sub Pump()
            timer.Stop()
            If Volatile.Read(disposed) <> 0 Then
                player?.Close()
                player = Nothing
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal)
                Return
            End If
            If Not mediaReady Then Return

            Try
                Dim shouldPlay As Boolean, volume As Double, seek As TimeSpan?, seekRevision As Long
                SyncLock sync
                    shouldPlay = requestedPlaying
                    volume = requestedVolume
                    seek = requestedPosition
                    seekRevision = requestedSeekRevision
                    requestedPosition = Nothing
                End SyncLock
                If player.Volume <> volume Then player.Volume = volume
                If seek.HasValue Then
                    player.Position = seek.Value
                    activeSeekRevision = seekRevision
                    If bitmap IsNot Nothing Then PrepareStillCapture()
                End If
                If shouldPlay <> playing Then
                    playing = shouldPlay
                    If playing Then
                        player.Play()
                        If nextCapture = 0 Then nextCapture = Stopwatch.GetTimestamp()
                    Else
                        player.Pause()
                    End If
                End If

                Dim ticks = player.Position.Ticks
                SyncLock sync
                    If requestedSeekRevision = activeSeekRevision Then positionTicks = ticks
                End SyncLock
                Dim now = Stopwatch.GetTimestamp()
                If now >= nextCapture AndAlso (playing OrElse stillDeadline <> 0) Then
                    ' Anchor the next deadline to the capture clock. Scheduling
                    ' a full interval after rendering loses several frames per second.
                    nextCapture += ((now - nextCapture) \ FrameInterval + 1) * FrameInterval
                    If bitmap IsNot Nothing AndAlso (CaptureFrame(now < stillDeadline) OrElse now >= stillDeadline) Then stillDeadline = 0
                End If
                If playing OrElse stillDeadline <> 0 Then
                    Dim delay = TimeSpan.FromSeconds(Math.Max(0.001, (nextCapture - Stopwatch.GetTimestamp()) / CDbl(Stopwatch.Frequency)))
                    Dim relativeDueTime = -delay.Ticks
                    If timerSignal IsNot Nothing AndAlso SetWaitableTimer(timerSignal.SafeWaitHandle, relativeDueTime, 0, IntPtr.Zero, IntPtr.Zero, False) Then Return
                    timer.Interval = delay
                    timer.Start()
                Else
                    nextCapture = 0
                    If timerSignal IsNot Nothing Then CancelWaitableTimer(timerSignal.SafeWaitHandle)
                End If
            Catch ex As Exception
                Fail(ex.Message)
            End Try
        End Sub

        Private Function CaptureFrame(waitForPicture As Boolean) As Boolean
            bitmap.Clear()
            bitmap.Render(visual)
            Dim frame As VideoFrame
            SyncLock sync
                frame = If(reusableFrames.Count > 0, reusableFrames.Pop(), Nothing)
            End SyncLock
            If frame Is Nothing Then
                frame = New VideoFrame With {
                    .Pixels = New Byte(bitmap.PixelWidth * bitmap.PixelHeight * 4 - 1) {},
                    .Width = bitmap.PixelWidth, .Height = bitmap.PixelHeight}
            End If
            Dim published = False
            Try
                Dim pixels = frame.Pixels
                bitmap.CopyPixels(pixels, frame.Width * 4, 0)
                ' WPF may draw a fully transparent bitmap before a seek settles.
                ' Retry briefly, then publish even an entirely transparent frame.
                If waitForPicture Then
                    Dim centerAlpha = ((frame.Height \ 2) * frame.Width + frame.Width \ 2) * 4 + 3
                    If pixels(centerAlpha) = 0 Then
                        Dim alphaOffset = 3
                        While alphaOffset < pixels.Length AndAlso pixels(alphaOffset) = 0
                            alphaOffset += 4
                        End While
                        If alphaOffset >= pixels.Length Then Return False
                    End If
                End If
                ' WPF's premultiplied BGRA -> MonoGame's premultiplied RGBA.
                For offset = 0 To pixels.Length - 1 Step 4
                    Dim blue = pixels(offset)
                    pixels(offset) = pixels(offset + 2)
                    pixels(offset + 2) = blue
                Next
                SyncLock sync
                    If disposed <> 0 OrElse activeSeekRevision <> requestedSeekRevision Then Return False
                    If latest IsNot Nothing Then RecycleFrame(latest)
                    latest = frame
                    published = True
                End SyncLock
                Return True
            Finally
                If Not published Then ReturnFrame(frame)
            End Try
        End Function

        Private Sub Fail(message As String)
            SyncLock sync
                If disposed = 0 Then failure = message
            End SyncLock
            mediaReady = False
            timer?.Stop()
            dispatcher?.BeginInvokeShutdown(DispatcherPriority.Normal)
        End Sub

        Public Sub Play()
            SyncLock sync
                requestedPlaying = True
                Interlocked.Exchange(ended, 0)
            End SyncLock
            Wake()
        End Sub

        Public Sub Pause()
            SyncLock sync
                requestedPlaying = False
            End SyncLock
            Wake()
        End Sub

        Public Sub Seek(position As TimeSpan)
            SyncLock sync
                requestedPosition = position
                positionTicks = position.Ticks
                requestedSeekRevision += 1
                Interlocked.Exchange(ended, 0)
                If latest IsNot Nothing Then RecycleFrame(latest)
                latest = Nothing
            End SyncLock
            Wake()
        End Sub

        Public Sub SetVolume(volume As Double)
            SyncLock sync
                requestedVolume = volume
            End SyncLock
            Wake()
        End Sub

        Public ReadOnly Property Position As TimeSpan
            Get
                SyncLock sync
                    Return TimeSpan.FromTicks(positionTicks)
                End SyncLock
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

        Private Sub RecycleFrame(frame As VideoFrame)
            If disposed = 0 AndAlso reusableFrames.Count < 3 Then reusableFrames.Push(frame)
        End Sub

        Public Sub ReturnFrame(frame As VideoFrame)
            SyncLock sync
                RecycleFrame(frame)
            End SyncLock
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            SyncLock sync
                If Interlocked.Exchange(disposed, 1) <> 0 Then Return
                latest = Nothing
                reusableFrames.Clear()
            End SyncLock
            Wake()
        End Sub

        Private NotInheritable Class WaitableTimer
            Inherits WaitHandle

            Public Sub New(handle As IntPtr)
                SafeWaitHandle = New SafeWaitHandle(handle, True)
            End Sub
        End Class

        <DllImport("kernel32.dll", EntryPoint:="CreateWaitableTimerExW", CharSet:=CharSet.Unicode, ExactSpelling:=True)>
        Private Shared Function CreateWaitableTimerEx(attributes As IntPtr, name As String, flags As UInteger, access As UInteger) As IntPtr
        End Function

        <DllImport("kernel32.dll", ExactSpelling:=True)>
        Private Shared Function SetWaitableTimer(handle As SafeWaitHandle, ByRef dueTime As Long, period As Integer, callback As IntPtr, state As IntPtr, resumeFromSleep As Boolean) As Boolean
        End Function

        <DllImport("kernel32.dll", ExactSpelling:=True)>
        Private Shared Function CancelWaitableTimer(handle As SafeWaitHandle) As Boolean
        End Function
    End Class
End Namespace
