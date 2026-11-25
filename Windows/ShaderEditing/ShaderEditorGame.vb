Imports System.Diagnostics
Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.ShaderEditor
Imports ProjectZ.Shared.Content
Imports ProjectZ.Shared.Drawing
Imports ProjectZ.Windows.Composition
Imports Forms = System.Windows.Forms

Namespace Windows.ShaderEditing
    Friend NotInheritable Class ShaderEditorGame
        Inherits Game

        Private ReadOnly graphics As GraphicsDeviceManager
        Private ReadOnly windowSurface As LayeredSceneWindow
        Private ReadOnly documentPath As String
        Private ReadOnly graphJson As String
        Private ReadOnly getAppearance As Func(Of WindowAppearanceSettings)
        Private ReadOnly publish As Action(Of Byte(), String)
        Private ReadOnly shouldClose As Func(Of Boolean)
        Private ReadOnly takeReceipt As Func(Of ShaderGraphReceipt)
        Private ReadOnly hosted, surface As Boolean
        Private ReadOnly ownerHandle As IntPtr
        Private ReadOnly ownerProcess As UInteger
        Private ReadOnly ready As Action(Of Long)
        Private ReadOnly takeActivation As Func(Of Boolean)
        Private manager As SceneManager
        Private scene As ShaderEditorScene
        Private sprite As SpriteBatch
        Private frame As RenderTarget2D
        Private pixels As Color()
        Private backdrop As DwmBackdropHost
        Private appliedTheme As WindowAppearanceSettings
        Private revision As Long = -1
        Private nextIdleFrame As Long
        Private frameReady, closing, promptToClose, initialized As Boolean
        Private failure As Exception

        Friend Sub New(documentPath As String, graphJson As String, getAppearance As Func(Of WindowAppearanceSettings),
                       publish As Action(Of Byte(), String), shouldClose As Func(Of Boolean), takeReceipt As Func(Of ShaderGraphReceipt),
                       hosted As Boolean, surface As Boolean, ownerHandle As IntPtr, title As String,
                       ready As Action(Of Long), takeActivation As Func(Of Boolean))
            Me.documentPath = documentPath
            Me.graphJson = graphJson
            Me.getAppearance = getAppearance
            Me.publish = publish
            Me.shouldClose = shouldClose
            Me.takeReceipt = takeReceipt
            Me.hosted = hosted
            Me.surface = surface
            Me.ready = ready
            Me.takeActivation = takeActivation
            If ownerHandle <> IntPtr.Zero Then
                Me.ownerHandle = GetAncestor(ownerHandle, 2UI) ' GA_ROOT
                Dim process As UInteger
                If Me.ownerHandle = IntPtr.Zero OrElse GetWindowThreadProcessId(Me.ownerHandle, process) = 0UI Then
                    Throw New ArgumentException("The shader editor's host window is no longer available.", NameOf(ownerHandle))
                End If
                ownerProcess = process
            End If
            Window.Title = "Project-Z shader rendering device " & Environment.ProcessId.ToString(Globalization.CultureInfo.InvariantCulture)
            IsFixedTimeStep = False
            InactiveSleepTime = TimeSpan.Zero
            IsMouseVisible = True
            Dim display = If(Me.ownerHandle = IntPtr.Zero, Forms.Screen.PrimaryScreen, Forms.Screen.FromHandle(Me.ownerHandle))
            Dim work = display.WorkingArea
            Dim width = Math.Clamp(work.Width - 80, 1010, 1380), height = Math.Clamp(work.Height - 84, 560, 790)
            windowSurface = New LayeredSceneWindow(If(String.IsNullOrWhiteSpace(title), "Shader Editor · Project-Z", title), width, height, 43, 130, 1010, 560)
            If Me.ownerHandle <> IntPtr.Zero Then
                windowSurface.StartPosition = Forms.FormStartPosition.Manual
                windowSurface.Location = New Global.System.Drawing.Point(work.Left + Math.Max(0, (work.Width - width) \ 2),
                                                                           work.Top + Math.Max(0, (work.Height - height) \ 2))
                windowSurface.ShowInTaskbar = False
            End If
            graphics = New GraphicsDeviceManager(Me) With {
                .PreferredBackBufferWidth = width, .PreferredBackBufferHeight = height, .GraphicsProfile = GraphicsProfile.HiDef,
                .SynchronizeWithVerticalRetrace = False, .PreferMultiSampling = False}
            AddHandler windowSurface.FormClosing, AddressOf WindowClosing
        End Sub

        Public Shadows Sub Run()
            If Not IsOwnerAlive() OrElse shouldClose() Then Return
            Dim handle = windowSurface.Handle
            If ownerHandle <> IntPtr.Zero Then BindOwner(handle)
            Using timer As New Forms.Timer With {.Interval = 16}
                AddHandler timer.Tick, AddressOf FrameTick
                timer.Start()
                Forms.Application.Run(windowSurface)
            End Using
            If failure IsNot Nothing Then Throw New InvalidOperationException("The Project-Z shader editor stopped rendering.", failure)
        End Sub

        Protected Overrides Sub LoadContent()
            sprite = New SpriteBatch(GraphicsDevice)
            Dim resources As New ContentContainer(Content, GraphicsDevice)
            resources.LoadVectorFontsOnly()
            Services.AddService(GetType(SpriteBatch), sprite)
            Services.AddService(GetType(GraphicsDevice), GraphicsDevice)
            Services.AddService(GetType(ContentContainer), resources)
            manager = New SceneManager(Me, 60) With {.AutoResizeViewport = False, .UseWindowMessagesInput = True, .NativeInputWindow = windowSurface.Handle}
            manager.DesktopBackdrop.NativeWindowHandle = windowSurface.Handle
            scene = New ShaderEditorScene(manager, windowSurface, documentPath, graphJson, publish,
                Sub() windowSurface.WindowState = Forms.FormWindowState.Minimized,
                Sub() windowSurface.WindowState = If(windowSurface.WindowState = Forms.FormWindowState.Maximized, Forms.FormWindowState.Normal, Forms.FormWindowState.Maximized),
                Sub() closing = True, takeReceipt, hosted, surface, windowSurface.Text)
            scene.Quality.AntiAliasingSamples = 4
            scene.Quality.PpiPerfectText = True
            manager.AddScene("shader-editor", scene)
            manager.ActiveScene = scene
            scene.Arrange(graphics.PreferredBackBufferWidth, graphics.PreferredBackBufferHeight)
            RefreshAppearance()
        End Sub

        Private Sub FrameTick(sender As Object, args As EventArgs)
            Dim timer = DirectCast(sender, Forms.Timer)
            timer.Stop()
            Try
                If shouldClose() OrElse Not IsOwnerAlive() Then closing = True
                If promptToClose Then
                    promptToClose = False
                    If scene Is Nothing Then closing = True Else scene.RequestClose()
                End If
                If closing Then
                    windowSurface.Close()
                    Return
                End If
                If Not initialized Then
                    ' Enter the form's message loop before starting the device.
                    ' Do not publish readiness until the first frame is visible.
                    RunOneFrame()
                    Dim renderHandle = WindowsComposition.ResolveHwnd(Me)
                    If renderHandle <> IntPtr.Zero Then ShowWindow(renderHandle, 0)
                    ShowFrame()
                    initialized = True
                    ready(windowSurface.Handle.ToInt64())
                    Return
                End If
                If takeActivation() Then
                    If windowSurface.WindowState = Forms.FormWindowState.Minimized Then windowSurface.WindowState = Forms.FormWindowState.Normal
                    windowSurface.Activate()
                End If
                If windowSurface.WindowState = Forms.FormWindowState.Minimized Then Return
                RefreshAppearance()
                ResizeFrame()
                Tick()
                ShowFrame()
                backdrop?.Tick()
            Catch ex As Exception
                failure = ex
                closing = True
                windowSurface.Close()
            Finally
                If Not closing Then timer.Start()
            End Try
        End Sub

        Private Function IsOwnerAlive() As Boolean
            If ownerHandle = IntPtr.Zero Then Return True
            Dim process As UInteger
            Return GetWindowThreadProcessId(ownerHandle, process) <> 0UI AndAlso ownerProcess = process
        End Function

        Private Sub BindOwner(handle As IntPtr)
            Const ownerIndex As Integer = -8 ' GWLP_HWNDPARENT for a top-level window
            Marshal.SetLastPInvokeError(0)
            Dim previous = If(IntPtr.Size = 8, SetWindowLongPtr(handle, ownerIndex, ownerHandle),
                              New IntPtr(SetWindowLong(handle, ownerIndex, ownerHandle.ToInt32())))
            Dim errorCode = Marshal.GetLastPInvokeError()
            If previous = IntPtr.Zero AndAlso errorCode <> 0 Then Throw New Win32Exception(errorCode, "Could not attach the shader editor to its host window.")
        End Sub

        Private Sub WindowClosing(sender As Object, e As Forms.FormClosingEventArgs)
            If closing Then Return
            e.Cancel = True
            promptToClose = True
        End Sub

        Private Sub ResizeFrame()
            Dim size = windowSurface.ClientSize
            If size.Width = graphics.PreferredBackBufferWidth AndAlso size.Height = graphics.PreferredBackBufferHeight Then Return
            graphics.PreferredBackBufferWidth = size.Width
            graphics.PreferredBackBufferHeight = size.Height
            graphics.ApplyChanges()
            scene.Arrange(size.Width, size.Height)
        End Sub

        Private Sub RefreshAppearance()
            Dim settings = getAppearance()
            If settings Is appliedTheme Then Return
            appliedTheme = settings
            scene.ApplyAppearance(settings)
            If settings.DwmBlurEnabled Then
                Dim tint = New Color(CInt(settings.BackgroundColor.R), CInt(settings.BackgroundColor.G), CInt(settings.BackgroundColor.B), 110)
                If backdrop Is Nothing Then
                    backdrop = New DwmBackdropHost(windowSurface.Handle, tint)
                Else
                    backdrop.SetMaterial(True, tint)
                End If
            Else
                backdrop?.Dispose()
                backdrop = Nothing
            End If
        End Sub

        Protected Overrides Sub Update(gameTime As GameTime)
            manager.Tick(gameTime)
            scene.UpdateEditor()
            MyBase.Update(gameTime)
        End Sub

        Protected Overrides Function BeginDraw() As Boolean
            Return (scene.RenderRevision <> revision OrElse Stopwatch.GetTimestamp() >= nextIdleFrame) AndAlso MyBase.BeginDraw()
        End Function

        Protected Overrides Sub Draw(gameTime As GameTime)
            Dim size = GraphicsDevice.PresentationParameters
            If frame Is Nothing OrElse frame.Width <> size.BackBufferWidth OrElse frame.Height <> size.BackBufferHeight Then
                frame?.Dispose()
                frame = New RenderTarget2D(GraphicsDevice, size.BackBufferWidth, size.BackBufferHeight, False, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents)
                pixels = New Color(frame.Width * frame.Height - 1) {}
            End If
            GraphicsDevice.SetRenderTarget(frame)
            Try
                manager.Draw(gameTime)
            Finally
                GraphicsDevice.SetRenderTarget(Nothing)
            End Try
            revision = scene.RenderRevision
            nextIdleFrame = Stopwatch.GetTimestamp() + Stopwatch.Frequency \ 5
            frameReady = True
        End Sub

        Private Sub ShowFrame()
            If Not frameReady Then Return
            frameReady = False
            frame.GetData(pixels)
            windowSurface.Present(pixels, frame.Width, frame.Height)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                scene?.CancelCompilation()
                manager?.Dispose()
                backdrop?.Dispose()
                frame?.Dispose()
                sprite?.Dispose()
                windowSurface.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub

        <DllImport("user32.dll")>
        Private Shared Function ShowWindow(hwnd As IntPtr, command As Integer) As Boolean
        End Function

        <DllImport("user32.dll", ExactSpelling:=True)>
        Private Shared Function GetAncestor(hwnd As IntPtr, flags As UInteger) As IntPtr
        End Function

        <DllImport("user32.dll", ExactSpelling:=True)>
        Private Shared Function GetWindowThreadProcessId(hwnd As IntPtr, ByRef process As UInteger) As UInteger
        End Function

        <DllImport("user32.dll", EntryPoint:="SetWindowLongPtrW", SetLastError:=True)>
        Private Shared Function SetWindowLongPtr(hwnd As IntPtr, index As Integer, value As IntPtr) As IntPtr
        End Function

        <DllImport("user32.dll", EntryPoint:="SetWindowLongW", SetLastError:=True)>
        Private Shared Function SetWindowLong(hwnd As IntPtr, index As Integer, value As Integer) As Integer
        End Function
    End Class
End Namespace
