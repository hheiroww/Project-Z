Imports System.Runtime.InteropServices
Imports System.Threading
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Windows.Composition
Imports Forms = System.Windows.Forms

Partial Public Class zWindow
    Inherits Game

    Private ReadOnly windowSurface As LayeredSceneWindow
    Private tetris As TetrisViewport
    Private backdrop As DwmBackdropHost
    Private frame As RenderTarget2D
    Private pixels As Color()
    Private pendingAppearance As WindowAppearanceSettings
    Private frameReady, closing As Boolean
    Private failure As Exception

    Public Sub New()
        Size = New Point(950, 650)
        Window.Title = "Project-Z Tetris rendering device"
        Window.AllowUserResizing = False
        windowSurface = New LayeredSceneWindow("Project-Z", Size.X, Size.Y, 46, 112, Size.X, Size.Y, resizable:=False) With {
            .MaximumSize = New Global.System.Drawing.Size(Size.X, Size.Y),
            .MaximizeBox = False
        }
        AddHandler windowSurface.FormClosing, Sub(sender, args) closing = True
        graphics = New GraphicsDeviceManager(Me) With {
            .SynchronizeWithVerticalRetrace = False,
            .PreferredBackBufferHeight = Size.Y,
            .PreferredBackBufferWidth = Size.X,
            .GraphicsProfile = GraphicsProfile.HiDef
        }
        IsFixedTimeStep = False
        InactiveSleepTime = TimeSpan.Zero
        IsMouseVisible = True
        Content.RootDirectory = "Content"
    End Sub

    Public Shadows Sub Run()
        Dim unused = windowSurface.Handle
        RunOneFrame()
        Dim renderHandle = WindowsComposition.ResolveHwnd(Me)
        If renderHandle <> IntPtr.Zero Then ShowWindow(renderHandle, 0)
        PresentFrame()
        Using timer As New Forms.Timer With {.Interval = 16}
            AddHandler timer.Tick, AddressOf FrameTick
            timer.Start()
            Forms.Application.Run(windowSurface)
        End Using
        If failure IsNot Nothing Then Throw New InvalidOperationException("Project-Z could not present the Tetris workspace.", failure)
    End Sub

    Protected Sub SetupSceneManager()
        sceneManager = New SceneManager(Me, 60) With {
            .AutoResizeViewport = False,
            .UseWindowMessagesInput = True,
            .NativeInputWindow = windowSurface.Handle
        }
        tetris = New TetrisViewport(sceneManager)
        sceneManager.AddScene("Tetris", tetris)
        sceneManager.ActiveScene = tetris

        AddHandler WindowAppearance.Default.Changed, AddressOf AppearanceChanged
        AppearanceChanged(Me, EventArgs.Empty)
        ApplyPendingAppearance()
    End Sub

    Private Sub FrameTick(sender As Object, args As EventArgs)
        Dim timer = DirectCast(sender, Forms.Timer)
        timer.Stop()
        Try
            If windowSurface.WindowState <> Forms.FormWindowState.Minimized Then
                ApplyPendingAppearance()
                Tick()
                If Not closing AndAlso Not windowSurface.IsDisposed Then
                    PresentFrame()
                    backdrop?.Tick()
                End If
            End If
        Catch errorInfo As Exception
            failure = errorInfo
            closing = True
        Finally
            If closing Then
                If Not windowSurface.IsDisposed Then windowSurface.Close()
            Else
                timer.Start()
            End If
        End Try
    End Sub

    Private Sub AppearanceChanged(sender As Object, args As EventArgs)
        Interlocked.Exchange(pendingAppearance, WindowAppearance.Default.Settings)
    End Sub

    Private Sub ApplyPendingAppearance()
        Dim settings = Interlocked.Exchange(pendingAppearance, Nothing)
        If settings Is Nothing Then Return
        tetris.ApplyAppearance(settings)
        If settings.DwmBlurEnabled Then
            Dim tint = New Color(CInt(settings.BackgroundColor.R), CInt(settings.BackgroundColor.G), CInt(settings.BackgroundColor.B), 110)
            If backdrop Is Nothing Then
                backdrop = New DwmBackdropHost(windowSurface.Handle, tint)
            Else
                WindowsComposition.ApplyToHandle(backdrop.Handle, New WindowCompositionOptions With {.Resizable = False, .Tint = tint})
            End If
        Else
            backdrop?.Dispose()
            backdrop = Nothing
        End If
    End Sub

    Private Sub DrawFrame(gameTime As GameTime)
        If frame Is Nothing Then
            frame = New RenderTarget2D(GraphicsDevice, Size.X, Size.Y, False, SurfaceFormat.Color,
                                       DepthFormat.None, 0, RenderTargetUsage.PreserveContents)
            pixels = New Color(frame.Width * frame.Height - 1) {}
        End If
        GraphicsDevice.SetRenderTarget(frame)
        Try
            sceneManager.Draw(gameTime)
        Finally
            GraphicsDevice.SetRenderTarget(Nothing)
        End Try
        frameReady = True
    End Sub

    Private Sub PresentFrame()
        If Not frameReady Then Return
        frameReady = False
        frame.GetData(pixels)
        windowSurface.Present(pixels, frame.Width, frame.Height)
    End Sub

    Friend Sub Minimize()
        windowSurface.WindowState = Forms.FormWindowState.Minimized
    End Sub

    Friend Sub CloseWindow()
        closing = True
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            RemoveHandler WindowAppearance.Default.Changed, AddressOf AppearanceChanged
            sceneManager?.Dispose()
            backdrop?.Dispose()
            frame?.Dispose()
            spriteBatch?.Dispose()
            windowSurface.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    <DllImport("user32.dll")>
    Private Shared Function ShowWindow(hwnd As IntPtr, command As Integer) As Boolean
    End Function
End Class
