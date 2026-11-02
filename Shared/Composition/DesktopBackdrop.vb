Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports System.Threading.Tasks

Namespace [Shared].Composition
    ''' <summary>Local desktop capture and cached GPU Gaussian material. No screen pixels are saved or transmitted.</summary>
    Public NotInheritable Class DesktopBackdrop
        Implements IDisposable
        Private ReadOnly game As Game
        Public ReadOnly Property Settings As New DesktopBackdropSettings()
        ''' <summary>Set before enabling, for a compositor-specific source or application integration.</summary>
        Public Property CaptureSourceFactory As Func(Of IDesktopCaptureSource)
        Public Property NativeWindowHandle As IntPtr
        ''' <summary>Show the framework setup prompt when dependencies or capture permission are missing.</summary>
        Public Property ShowSetupPrompt As Boolean = True
        Public ReadOnly Property State As DesktopBackdropState = DesktopBackdropState.Disabled
        Public ReadOnly Property StatusMessage As String = String.Empty
        Public ReadOnly Property CapturedFrameCount As Long
        Public ReadOnly Property LastCaptureMilliseconds As Double
        Public Event Unavailable(state As DesktopBackdropState, message As String)
        Private source As IDesktopCaptureSource
        Private captureTask As Task(Of Boolean)
        Private captureArea, frameArea, windowArea As Rectangle
        Private captureScale As Integer = 2
        Private dpi As Single = 1
        Private rgba As Byte()
        Private nextCaptureAt As Long
        Private stopped As Boolean
        Private failed As Boolean
        Private disposed As Boolean
        Private lastRequest As DesktopBackdropRequests.Request
        Private input As Texture2D
        Private downsampled, scratch, blurred As RenderTarget2D
        Private shader As Effect
        Private batch As SpriteBatch
        Private white As Texture2D
        Private dirty As Boolean
        Private renderedRadius As Single = -1
        Friend Sub New(game As Game)
            Me.game = game
        End Sub
        Public Sub Retry()
            ReleaseCapture()
            failed = False
            _StatusMessage = String.Empty
        End Sub
        Public Function RequestCapturePermission() As Boolean
            If Not OperatingSystem.IsMacOS() Then Return False
            Dim library = Runtime.InteropServices.NativeLibrary.Load("libProjectZDesktopCapture.dylib", GetType(DesktopBackdrop).Assembly, Nothing)
            Try
                Dim request = Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(Of RequestPermissionDelegate)(Runtime.InteropServices.NativeLibrary.GetExport(library, "pz_capture_request_permission"))
                Return request() <> 0
            Finally
                Runtime.InteropServices.NativeLibrary.Free(library)
            End Try
        End Function
        <Runtime.InteropServices.UnmanagedFunctionPointer(Runtime.InteropServices.CallingConvention.Cdecl)>
        Private Delegate Function RequestPermissionDelegate() As Integer
        Friend Sub Update()
            If disposed Then Return
            If NativeWindowHandle = IntPtr.Zero AndAlso CaptureSourceFactory Is Nothing Then
                Try
                    NativeWindowHandle = DesktopNativeWindow.Resolve(game)
                Catch ex As Exception
                    If Settings.Enabled Then Fail(DesktopBackdropState.Unsupported, ex.Message)
                End Try
            End If
            Dim requested As DesktopBackdropRequests.Request = Nothing
            If DesktopBackdropRequests.TryGet(NativeWindowHandle, requested) AndAlso requested IsNot lastRequest Then
                Settings.Enabled = requested.Enabled
                Settings.Tint = requested.Tint
                lastRequest = requested
            End If
            If Not Settings.Enabled Then
                If Not stopped Then ReleaseCapture()
                failed = False : stopped = True : _State = DesktopBackdropState.Disabled
                Return
            End If
            stopped = False
            If failed Then Return
            Try
                If source Is Nothing Then
                    source = If(CaptureSourceFactory Is Nothing, DesktopCaptureFactory.Create(NativeWindowHandle), CaptureSourceFactory.Invoke())
                    If source Is Nothing OrElse Not source.ExcludesHostWindow Then Throw New DesktopCaptureUnavailableException(DesktopBackdropState.Unsupported, "The capture provider must exclude the host window without returning a black placeholder.")
                    _State = DesktopBackdropState.Starting
                End If
                windowArea = source.WindowBounds
                dpi = Math.Clamp(source.DpiScale, 0.5F, 4)
                If captureTask IsNot Nothing Then
                    If Not captureTask.IsCompleted Then Return
                    Dim available = captureTask.GetAwaiter().GetResult()
                    captureTask = Nothing
                    If available Then
                        Dim width = (captureArea.Width + captureScale - 1) \ captureScale
                        Dim height = (captureArea.Height + captureScale - 1) \ captureScale
                        If input Is Nothing OrElse input.Width <> width OrElse input.Height <> height Then
                            input?.Dispose()
                            input = New Texture2D(game.GraphicsDevice, width, height)
                        End If
                        input.SetData(rgba)
                        frameArea = captureArea
                        dirty = True
                        _CapturedFrameCount += 1
                        _State = DesktopBackdropState.Active
                    End If
                End If
                If windowArea.Width <= 0 OrElse windowArea.Height <= 0 OrElse Environment.TickCount64 < nextCaptureAt Then Return
                Dim padding = CInt(Math.Ceiling(Math.Clamp(Settings.BlurRadius, 0, 100) * dpi * 3)) + 4
                captureArea = windowArea
                captureArea.Inflate(padding, padding)
                captureScale = If(Settings.BlurRadius >= 10, 2, 1)
                While (CLng(captureArea.Width) * captureArea.Height \ (captureScale * captureScale) > 8L * 1024 * 1024 OrElse
                       captureArea.Width \ captureScale > 8192 OrElse captureArea.Height \ captureScale > 8192) AndAlso captureScale < 8
                    captureScale *= 2
                End While
                Dim length = CInt(CLng((captureArea.Width + captureScale - 1) \ captureScale) * ((captureArea.Height + captureScale - 1) \ captureScale) * 4)
                If rgba Is Nothing OrElse rgba.Length <> length Then ReDim rgba(length - 1)
                Dim current = source, area = captureArea, scale = captureScale, buffer = rgba
                nextCaptureAt = Environment.TickCount64 + 1000 \ Math.Clamp(Settings.CaptureFramesPerSecond, 1, 60)
                captureTask = Task.Run(Function()
                                           Dim started = Diagnostics.Stopwatch.GetTimestamp()
                                           Try
                                               Return current.TryCapture(area, scale, buffer)
                                           Finally
                                               _LastCaptureMilliseconds = Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds
                                           End Try
                                       End Function)
            Catch ex As DesktopCaptureUnavailableException
                Fail(ex.State, ex.Message)
            Catch ex As Exception
                Fail(DesktopBackdropState.Failed, ex.Message)
            End Try
        End Sub
        Private Sub Fail(state As DesktopBackdropState, message As String)
            ReleaseCapture()
            failed = True
            _State = state : _StatusMessage = message
            RaiseEvent Unavailable(state, message)
        End Sub
        Private Sub ReleaseCapture()
            Dim previous = source
            source = Nothing
            If previous IsNot Nothing Then
                If captureTask IsNot Nothing AndAlso Not captureTask.IsCompleted Then
                    captureTask.ContinueWith(Sub(completed)
                                                 Dim ignored = completed.Exception
                                                 previous.Dispose()
                                             End Sub, TaskScheduler.Default)
                Else
                    If captureTask IsNot Nothing Then
                        Dim ignored = captureTask.Exception
                    End If
                    previous.Dispose()
                End If
            End If
            captureTask = Nothing
            ' A released source can still be finishing on its worker. A retry must
            ' get a separate buffer rather than race that source for the same pixels.
            rgba = Nothing
            frameArea = Rectangle.Empty
            _State = DesktopBackdropState.Starting
        End Sub
        Friend Function Draw(device As GraphicsDevice, overlay As Color) As Boolean
            If Not Settings.Enabled OrElse disposed Then Return False
            If batch Is Nothing Then
                batch = New SpriteBatch(device)
                white = New Texture2D(device, 1, 1)
                white.SetData({Color.White})
            End If
            Dim targets = device.GetRenderTargets()
            Dim viewport = device.Viewport
            Dim scissor = device.ScissorRectangle
            Dim rasterizer = device.RasterizerState
            Dim blend = device.BlendState
            Dim depth = device.DepthStencilState
            Try
                Dim valid = input IsNot Nothing AndAlso frameArea.Contains(windowArea) AndAlso windowArea.Width > 0
                If valid Then
                    If shader Is Nothing Then
                        Using stream = GetType(DesktopBackdrop).Assembly.GetManifestResourceStream("ProjectZ.Resources.DesktopBlur.mgfx")
                            Using bytes As New IO.MemoryStream()
                                stream.CopyTo(bytes)
                                shader = New Effect(device, bytes.ToArray())
                            End Using
                        End Using
                    End If
                    Dim reduction = If(Settings.BlurRadius >= 16, 2, 1)
                    Dim width = Math.Max(1, input.Width \ reduction), height = Math.Max(1, input.Height \ reduction)
                    If blurred Is Nothing OrElse blurred.Width <> width OrElse blurred.Height <> height Then
                        downsampled?.Dispose() : scratch?.Dispose() : blurred?.Dispose()
                        downsampled = New RenderTarget2D(device, width, height, False, SurfaceFormat.Color, DepthFormat.None)
                        scratch = New RenderTarget2D(device, width, height, False, SurfaceFormat.Color, DepthFormat.None)
                        blurred = New RenderTarget2D(device, width, height, False, SurfaceFormat.Color, DepthFormat.None)
                        dirty = True
                    End If
                    Dim radius = Math.Clamp(Settings.BlurRadius, 0, 100) * dpi
                    If dirty OrElse radius <> renderedRadius Then
                        device.SetRenderTarget(downsampled)
                        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone)
                        batch.Draw(input, New Rectangle(0, 0, width, height), Color.White)
                        batch.End()
                        shader.Parameters("Mode").SetValue(0.0F)
                        Dim stepSize = radius / 2.94F / (frameArea.Width / CSng(width))
                        Pass(device, downsampled, scratch, New Vector2(stepSize / width, 0))
                        Pass(device, scratch, blurred, New Vector2(0, stepSize / height))
                        dirty = False : renderedRadius = radius
                    End If
                    If targets.Length = 0 Then device.SetRenderTarget(Nothing) Else device.SetRenderTargets(targets)
                    device.Viewport = viewport
                    shader.Parameters("Mode").SetValue(1.0F)
                    shader.Parameters("Saturation").SetValue(Math.Clamp(Settings.Saturation, 0, 2))
                    shader.Parameters("NoiseOpacity").SetValue(Math.Clamp(Settings.NoiseOpacity, 0, 0.1F))
                    shader.Parameters("MaterialTint").SetValue(Settings.Tint.ToVector4())
                    shader.Parameters("ScreenOrigin").SetValue(New Vector2(frameArea.X, frameArea.Y))
                    shader.Parameters("DesktopExtent").SetValue(New Vector2(frameArea.Width, frameArea.Height))
                    Dim sourceArea As New Rectangle(CInt((windowArea.X - frameArea.X) * (blurred.Width / CSng(frameArea.Width))),
                        CInt((windowArea.Y - frameArea.Y) * (blurred.Height / CSng(frameArea.Height))),
                        Math.Max(1, CInt(windowArea.Width * (blurred.Width / CSng(frameArea.Width)))), Math.Max(1, CInt(windowArea.Height * (blurred.Height / CSng(frameArea.Height)))))
                    batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, shader)
                    batch.Draw(blurred, viewport.Bounds, sourceArea, Color.White)
                    batch.End()
                Else
                    batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque)
                    batch.Draw(white, viewport.Bounds, New Color(Settings.FallbackColor.R, Settings.FallbackColor.G, Settings.FallbackColor.B, CByte(255)))
                    batch.End()
                End If
                If overlay.A > 0 Then
                    batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend)
                    batch.Draw(white, viewport.Bounds, overlay)
                    batch.End()
                End If
                Return True
            Finally
                If targets.Length = 0 Then device.SetRenderTarget(Nothing) Else device.SetRenderTargets(targets)
                device.Viewport = viewport : device.ScissorRectangle = scissor
                device.RasterizerState = rasterizer : device.BlendState = blend : device.DepthStencilState = depth
            End Try
        End Function
        Private Sub Pass(device As GraphicsDevice, sourceTexture As Texture2D, target As RenderTarget2D, direction As Vector2)
            device.SetRenderTarget(target)
            shader.Parameters("BlurStep").SetValue(direction)
            batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, shader)
            batch.Draw(sourceTexture, target.Bounds, Color.White)
            batch.End()
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True
            ReleaseCapture()
            _State = DesktopBackdropState.Disabled
            DesktopBackdropRequests.Remove(NativeWindowHandle)
            input?.Dispose() : downsampled?.Dispose() : scratch?.Dispose() : blurred?.Dispose()
            white?.Dispose() : shader?.Dispose() : batch?.Dispose()
        End Sub
    End Class
End Namespace
