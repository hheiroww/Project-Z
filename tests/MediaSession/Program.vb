Imports System.Diagnostics
Imports System.Threading
Imports ProjectZ.Windows.Media

Module Program
    Sub Main(args As String())
        Dim minimumDistinctFrames = If(args.Length > 1, Integer.Parse(args(1), Globalization.CultureInfo.InvariantCulture), 30)
        Using session As New WindowsVideoSession(args(0), 0, 640)
            WaitUntil(Function() session.TakeOpened(), session, "Windows media opened")
            Require(session.Duration >= TimeSpan.FromSeconds(7), "fixture duration is available")
            Require(PictureChannel(session) = 0, "opening paused presents the red first frame")
            Require(session.Position < TimeSpan.FromMilliseconds(20), "opening a still frame leaves playback paused")

            session.Seek(TimeSpan.FromSeconds(2.3))
            Require(PictureChannel(session) = 2, "paused seek presents the blue frame")
            session.Seek(TimeSpan.FromSeconds(1.2))
            session.Seek(TimeSpan.FromSeconds(2.3))
            session.Seek(TimeSpan.FromSeconds(0.3))
            Require(PictureChannel(session) = 0, "the latest scrub position presents the red frame")
            Require(session.Position = TimeSpan.FromSeconds(0.3), "paused scrubbing does not advance the playhead")

            session.Seek(TimeSpan.FromSeconds(3))
            session.Play()
            Dim frames As Integer
            Dim times As New List(Of Long)()
            Dim pictures As New HashSet(Of ULong)()
            Dim watch = Stopwatch.StartNew()
            While watch.ElapsedMilliseconds < 3100
                CheckError(session)
                Dim frame = session.TakeFrame()
                If frame IsNot Nothing Then
                    frames += 1
                    times.Add(watch.ElapsedMilliseconds)
                    Dim checksum = 0UL
                    For p = 0 To frame.Pixels.Length - 1 Step 167
                        checksum = ((checksum << 5) Xor (checksum >> 27)) Xor frame.Pixels(p)
                    Next
                    pictures.Add(checksum)
                    session.ReturnFrame(frame)
                End If
                Thread.Sleep(3)
            End While
            Dim gaps = times.Skip(1).Select(Function(t, i) t - times(i)).OrderBy(Function(t) t).ToArray()
            Dim p95Gap = If(gaps.Length > 0, gaps(CInt(Math.Ceiling(gaps.Length * 0.95)) - 1), 0)
            Console.WriteLine($"Playback over {watch.ElapsedMilliseconds} ms: {frames} presented, {pictures.Count} distinct, p95 gap {p95Gap} ms, max gap {gaps.DefaultIfEmpty().Max()} ms.")
            Require(pictures.Count >= minimumDistinctFrames AndAlso session.Position > TimeSpan.FromSeconds(5), $"playback presents at least {minimumDistinctFrames} distinct pictures and advances the clock")

            session.Pause()
            Thread.Sleep(150)
            Dim last = session.TakeFrame()
            If last IsNot Nothing Then session.ReturnFrame(last)
            Dim stopped = session.Position
            Thread.Sleep(200)
            CheckError(session)
            Require(session.TakeFrame() Is Nothing AndAlso session.Position = stopped, "pause stops publishing frames and advancing the clock")
        End Using
        Using abandoned As New WindowsVideoSession(args(0), 0, 640)
            ' Closing immediately must also work before the dispatcher is ready.
        End Using
        ' Give WPF's asynchronous close time to finish before process teardown.
        Thread.Sleep(250)
        Console.WriteLine("PASS Windows media session")
    End Sub

    Private Function PictureChannel(session As WindowsVideoSession) As Integer
        Dim frame As WindowsVideoSession.VideoFrame = Nothing
        WaitUntil(Function()
                      frame = session.TakeFrame()
                      Return frame IsNot Nothing
                  End Function, session, "decoded picture is available")
        Try
            Dim center = (frame.Width * (frame.Height \ 2) + frame.Width \ 2) * 4
            Require(frame.Pixels(center + 3) = 255, "decoded picture is opaque")
            For channel = 0 To 2
                If frame.Pixels(center + channel) > 180 Then Return channel
            Next
            Return -1
        Finally
            session.ReturnFrame(frame)
        End Try
    End Function

    Private Sub WaitUntil(condition As Func(Of Boolean), session As WindowsVideoSession, message As String)
        Dim watch = Stopwatch.StartNew()
        While watch.ElapsedMilliseconds < 5000
            CheckError(session)
            If condition() Then Return
            Thread.Sleep(5)
        End While
        Throw New InvalidOperationException(message)
    End Sub

    Private Sub CheckError(session As WindowsVideoSession)
        Dim failure = session.TakeError()
        If failure IsNot Nothing Then Throw New InvalidOperationException(failure)
    End Sub

    Private Sub Require(condition As Boolean, message As String)
        If Not condition Then Throw New InvalidOperationException(message)
    End Sub
End Module
