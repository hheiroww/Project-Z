#Region "Using Statements"
Imports System.Collections.Generic
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Advanced

Imports ProjectZ.Shared.Drawing.UI.Input
Imports ProjectZ.Shared.Drawing.UI.Primitives
Imports ProjectZ.Shared.XNA
Imports Microsoft.Xna.Framework.Input
Imports ProjectZ.Shared.Animations
Imports ProjectZ.Shared.Animations.Easing
Imports ProjectZ.Shared.Audio
Imports NAudio.Dsp
Imports ProjectZ.Shared.Audio.Quantization
Imports System.Timers
Imports System.Reflection
Imports ProjectZ.Shared

#End Region

Public Class MainScene
    Inherits Scene

    Dim CircleScene As HostScene

    Private WithEvents rSlider As New Trackbar(Me)
    Private WithEvents gSlider As New Trackbar(Me)
    Private WithEvents bSlider As New Trackbar(Me)

    Private WithEvents Spectrum As SpectrumAnalyser
    Private WithEvents ContrastTrackbar As Trackbar = New Trackbar(Me)

    Private WithEvents HostScene As SceneProjectionHost

    Private ControlContainer As New RectangleElement(Me)
    Private WithEvents ToggleButton As Button = New Button(Me)
    Private WithEvents WireframeButton As Button = New Button(Me)

    Private lastPeak As Single = 0.0, lastNotif As Long = 0
    Private lastIntensitySamples As New Queue(Of Integer)

    Private Function GetFilename(Optional ForceDialog As Boolean = False) As String
        Dim tempDir As String = System.IO.Path.GetTempPath()
        If Not System.IO.Directory.Exists(tempDir) Then
            System.IO.Directory.CreateDirectory(tempDir)
        End If

        Dim cachedFilename As String = System.IO.Path.Combine(tempDir, "ProjectZ_Temp")

        If Not ForceDialog AndAlso System.IO.File.Exists(cachedFilename) Then
            Dim cached As String = System.IO.File.ReadAllText(cachedFilename)
            If Not String.IsNullOrWhiteSpace(cached) AndAlso System.IO.File.Exists(cached) Then
                Return cached
            End If
        End If

        Dim dlg As New System.Windows.Forms.OpenFileDialog()
        dlg.CheckFileExists = True
        dlg.Filter = "Music Files|*.mp3"
        If dlg.ShowDialog() = System.Windows.Forms.DialogResult.OK AndAlso Not String.IsNullOrWhiteSpace(dlg.FileName) Then
            System.IO.File.WriteAllText(cachedFilename, dlg.FileName)
            Return dlg.FileName
        End If

        Return String.Empty
    End Function

    Private WithEvents Monitor As MonitorSource
    Private Sub InitSpectrum()

        'Monitor = New LoopbackMonitor
        'Dim E As New PlaybackEngine
        'E.Load(GetFilename)

        Monitor = New LoopbackMonitor  'New PlaybackMonitor(E)
        Dim LoopbackCast As LoopbackMonitor = CType(Monitor, LoopbackMonitor)

        Monitor.BeginMonitoring()

        'E.Play()
        Spectrum = New SpectrumAnalyser(Me, Monitor)
        Monitor.BeginMonitoring()

        With Spectrum
            .Position = New Vector2(0, 0)
            '.FillColor = Color.Yellow
            .isMovable = False
            '.Easing = New SineEase(EaseType.Ignore)
        End With

        With Spectrum.RenderProperties
            .HighQuality = False
            .MinAngle = 0
            .WireFrame = False
            .FillPolygon = True
            .WireFrameColor = Color.LimeGreen
        End With

        AddElement(Spectrum)

    End Sub

    '  Dim vsteffect As 

    Private Sub Buttons()

        With ControlContainer
            .Position = New Vector2(8, graphicsDevice.Viewport.Height - (40 + 26))
            .Size = New Vector2(graphicsDevice.Viewport.Width - 16, 26)
            .BackgroundColor = New Color(100, 100, 100, 20)
            .isMovable = True
            .Padding = New Thickness(1)
        End With

        ' Toggle Button
        With ToggleButton
            '.Text = Spectrum.Easing.easeType.ToString
            .Text = "Easing"
            .HorizontalAlign = HorizontalAlignment.Left
            .VerticalAlign = VerticalAlignment.Stretch
            .OrientationReserve = DisplayReservation.ReserveX
            .AutoSize = ButtonAutoSize.X
            .isAnimated = True
            .isVisible = True
        End With

        ' Wireframe Toggle Button
        With WireframeButton
            .Text = "Wireframe: OFF"
            .HorizontalAlign = HorizontalAlignment.Left
            .VerticalAlign = VerticalAlignment.Stretch
            .AutoSize = ButtonAutoSize.X
            .OrientationReserve = DisplayReservation.ReserveX
            .isAnimated = True
            .isVisible = True
        End With

        ControlContainer.Children.AddRange({ToggleButton, WireframeButton})
        AddElement(ControlContainer)

    End Sub

    Private Sub Sliders()

        'Dim SliderContainer As New RectangleElement(Me)

        With rSlider
            .OrientationReserve = DisplayReservation.FloatBoth
            .Position = New Vector2(8, 40)
            .Size = New Vector2(300, 32)
            .MinimumValue = 0
            .MaximumValue = 1000
            .Value = 70
            .isVisible = True
        End With

        With gSlider
            .HorizontalAlign = HorizontalAlignment.Left
            .OrientationReserve = DisplayReservation.FloatBoth
            .Position = New Vector2(8, rSlider.Rectangle.Bottom + 4)
            .Size = New Vector2(300, 32)
            .MinimumValue = 0
            .MaximumValue = 255
            .Value = 0
            .isVisible = True
        End With


        With bSlider
            .OrientationReserve = DisplayReservation.FloatBoth
            .Position = New Vector2(8, gSlider.Rectangle.Bottom + 4)
            .Size = New Vector2(300, 32)
            .MinimumValue = 0
            .MaximumValue = 255
            .Value = 0
            .isVisible = True
        End With

        AddElement(rSlider)
        AddElement(gSlider)
        AddElement(bSlider)

        ' Contrast Trackbar
        ContrastTrackbar.Size = New Vector2(400, 32)
        ContrastTrackbar.Position = New Vector2(bSlider.Rectangle.Left, bSlider.Rectangle.Bottom + 4)
        ContrastTrackbar.MinimumValue = 1
        ContrastTrackbar.MaximumValue = 60
        ContrastTrackbar.FixedInterval = False
        ContrastTrackbar.isVisible = True
        If Not ContainsElement(ContrastTrackbar) Then
            AddElement(ContrastTrackbar)
        End If

    End Sub

    Dim d As DoubleAnimation
    Dim ee As New CircleEase(EaseType.EaseInOut)
    Private lastPeakSamples As New Queue(Of Double)
    Private Sub Monitor_MaximumCalculated(sender As Object, e As MaxSampleEventArgs) Handles Monitor.MaximumCalculated
        Dim duration As TimeSpan = TimeSpan.FromMilliseconds(10 / FPS)
        Dim newPeak As Single = Math.Max(e.MaxSample, Math.Abs(e.MinSample))

        ' Peak amplitude -> dBFS (0 dB at full-scale), then map to slider range
        Const epsilon As Double = 0.0000000001
        Dim amp As Double = Math.Max(epsilon, newPeak)
        Dim db As Double = 20 * Math.Log10(amp)
        If Double.IsNaN(db) OrElse Double.IsInfinity(db) Then db = -120

        ' Visible meter range (dBFS)
        Const minDb As Double = -60
        Const maxDb As Double = 0
        db = Math.Max(minDb, Math.Min(maxDb, db))
        Dim t As Double = (db - minDb) / (maxDb - minDb) ' 0..1

        Dim target As Double = Animations.DoubleAnimation.Interpolate(t, 0, 1, bSlider.MinimumValue, bSlider.MaximumValue)
        lastPeakSamples.Enqueue(target)
        While lastPeakSamples.Count > 3
            lastPeakSamples.Dequeue()
        End While

        Dim avg As Double = 0
        For Each s In lastPeakSamples
            avg += s
        Next
        avg /= Math.Max(1, lastPeakSamples.Count)

        Dim range As Double = Math.Max(1.0R, (bSlider.MaximumValue - bSlider.MinimumValue))
        Dim eased As Double = ee.Ease((avg - bSlider.MinimumValue) / range) * range + bSlider.MinimumValue

        d = New DoubleAnimation(ee, bSlider.Value, eased, duration, gameTime, True)
        bSlider.Value = eased
        'ContrastTrackbar.Value = newPeak
    End Sub

#Region "Slider Events"

    Private Sub ContrastTrackbar_ValueChanged(value As Double) Handles ContrastTrackbar.ValueChanged
        If Spectrum IsNot Nothing Then
            Spectrum.Intensity = value
        End If
    End Sub

    Private Sub ColorSlider_ValueChanged() Handles rSlider.ValueChanged, gSlider.ValueChanged, bSlider.ValueChanged
        If Spectrum IsNot Nothing Then
            lastPeak = CSng(bSlider.Value)
            Spectrum.FillColor = New Color(CInt(rSlider.Value), CInt(gSlider.Value), CInt(bSlider.Value), 255)

            Dim v As Integer = CInt(Math.Max(1, Math.Min(255, bSlider.Value)))
            lastIntensitySamples.Enqueue(v)
            While lastIntensitySamples.Count > 3
                lastIntensitySamples.Dequeue()
            End While

            Dim avg As Double = 0
            SyncLock (lastIntensitySamples)

                For Each s In lastIntensitySamples
                    avg += s
                Next
                avg /= Math.Max(1, lastIntensitySamples.Count)
            End SyncLock



            Spectrum.IntensityByte = CInt(avg)
        End If
    End Sub

#End Region

#Region "Click Events"

    Private Sub ToggleButton_OnMouseLeftClick(p As Point) Handles ToggleButton.MouseLeftClick
        Dim EaseType As Integer = CInt(Spectrum.Easing.easeType)
        EaseType += 1
        If EaseType > 3 Then EaseType = 0
        Spectrum.Easing.easeType = CType(EaseType, EaseType)
        ToggleButton.Text = Spectrum.Easing.easeType.ToString
    End Sub

    Private Sub WireframeButton_OnMouseLeftClick(p As Point) Handles WireframeButton.MouseLeftClick
        Spectrum.RenderProperties.WireFrame = Not Spectrum.RenderProperties.WireFrame
        If Spectrum.RenderProperties.WireFrame Then
            WireframeButton.Text = "Wireframe: ON"
        Else
            WireframeButton.Text = "Wireframe: OFF"
        End If
    End Sub

#End Region

    Public Sub New(ByRef SceneManager As SceneManager)
        MyBase.New(SceneManager)

        'Initialize Settings

        isCursorVisible = True
        UseRenderTarget = True

        InitSpectrum()
        Buttons()
        Sliders()

        'SceneManager.DebugEnabled = True
    End Sub

    Private Sub MainScene_PreDraw(gameTime As GameTime) Handles Me.PreDraw
        'Dim CircleTexture As Texture2D = CircleScene.DrawToRenderTarget()

        'SpriteSettings.Begin(spriteBatch)

        'For x As Integer = 0 To graphicsDevice.Viewport.Width Step 20
        '    For y As Integer = 0 To graphicsDevice.Viewport.Height Step 20
        '        Dim dest As New Rectangle(x, y, 20, 20)
        '        Dim source As New Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height)
        '        spriteBatch.Draw(CircleTexture, dest, source, Color.White)
        '    Next
        'Next

        'spriteBatch.End()
    End Sub

    Private Sub MainScene_Initialized(gameTime As GameTime) Handles Me.Initialized
        AddHandler Spectrum.Source.SignalMonitor.OnSignalPeak, AddressOf SignalPeak
        t.Start()
        'Dim bfs As New MicroLibrary.Serialization.JsonSerializer()
        'My.Computer.FileSystem.WriteAllText("C:\test.txt", Text.UTF8Encoding.UTF8.GetString(bfs.Serialize(Me)), False)
    End Sub

    Dim currentbpm As Double = 0
    Dim realBPM As Double = 0

    Dim WithEvents t As New Timers.Timer(7500)

    Private Sub SignalPeak(Signal As SignalRegister)
        Select Case Signal.Name
            Case "beatcount"
                currentbpm += 1
        End Select
    End Sub

    Private Sub t_Elapsed(sender As Object, e As ElapsedEventArgs) Handles t.Elapsed
        realBPM = currentbpm * 8
        currentbpm = 0
    End Sub
End Class
