Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].Drawing.UI.Primitives
    Public Enum XamlEffectKind
        PassThrough = -1
        Blur
        DropShadow
        ChromaticAberration
        SpasticChromaticAberration
        Invert
        LinearChromaticAberration
        CompiledShader
    End Enum

    ''' <summary>Native GPU adapter for common WPF effects. No WPF rendering or bitmap readback.</summary>
    Public NotInheritable Class XamlNativeEffect
        Implements IDisposable
        Public Property Kind As XamlEffectKind
        Public Property Radius As Single = 5
        Public Property Amount As Single
        Public Property Phase As Single
        Public Property Angle As Single
        Public Property ShadowDepth As Single = 5
        Public Property Direction As Single = 315
        Public Property Color As Color = Color.Black
        Public Property Opacity As Single = 1

        Private Const MgfxVersion As Byte = 11
#If PROJECTZ_DIRECTX12 Then
        Private Const MgfxProfile As Byte = 2
#Else
        Private Const MgfxProfile As Byte = 1
#End If
        Private Shared ReadOnly shaderClock As System.Diagnostics.Stopwatch = System.Diagnostics.Stopwatch.StartNew()
        Private compiledShader As Byte()
        Friend Property TimeOverride As Single?

        ''' <summary>Creates a visual effect from MGFX bytecode compiled for the current graphics backend.</summary>
        Public Shared Function FromCompiledShader(bytecode As Byte()) As XamlNativeEffect
            ArgumentNullException.ThrowIfNull(bytecode)
            If bytecode.Length <= 14 OrElse Not HasMgfxMarker(bytecode, 0) OrElse Not HasMgfxMarker(bytecode, bytecode.Length - 4) Then
                Throw New ArgumentException("Expected compiled MGFX shader bytecode.", NameOf(bytecode))
            End If
            If bytecode(4) <> MgfxVersion OrElse bytecode(5) <> MgfxProfile Then
                Throw New ArgumentException("The compiled effect must match this Project-Z build's DirectX backend and MGFX version.", NameOf(bytecode))
            End If
            Return New XamlNativeEffect With {.Kind = XamlEffectKind.CompiledShader, .compiledShader = DirectCast(bytecode.Clone(), Byte())}
        End Function

        Private Shared Function HasMgfxMarker(bytes As Byte(), offset As Integer) As Boolean
            Return bytes(offset) = &H4D AndAlso bytes(offset + 1) = &H47 AndAlso bytes(offset + 2) = &H46 AndAlso bytes(offset + 3) = &H58
        End Function

        Public ReadOnly Property IsActive As Boolean
            Get
                If Kind = XamlEffectKind.PassThrough Then Return True
                If Kind = XamlEffectKind.CompiledShader Then Return compiledShader IsNot Nothing
                Return If(Kind >= XamlEffectKind.ChromaticAberration, Math.Abs(Amount) > 0.00001F,
                          If(Kind = XamlEffectKind.DropShadow, Opacity > 0, Radius > 0))
            End Get
        End Property
        Public ReadOnly Property Padding As Integer
            Get
                If Kind = XamlEffectKind.PassThrough Then Return 0
                If Kind >= XamlEffectKind.ChromaticAberration Then Return 0
                Return CInt(Math.Ceiling(Math.Clamp(Radius, 0, 100) + If(Kind = XamlEffectKind.DropShadow, Math.Abs(ShadowDepth), 0))) + 2
            End Get
        End Property
        Public Function EffectivePadding(quality As GraphicsQuality) As Integer
            If Kind = XamlEffectKind.PassThrough OrElse Kind >= XamlEffectKind.ChromaticAberration Then Return 0
            Return CInt(Math.Ceiling(Math.Clamp(Radius * Math.Clamp(quality.BlurRadiusScale, 0, 3), 0, 100) + If(Kind = XamlEffectKind.DropShadow, Math.Abs(ShadowDepth), 0))) + 2
        End Function
        Private source As RenderTarget2D
        Private scratch As RenderTarget2D
        Private shader As Effect
        Private batch As SpriteBatch
        Private ReadOnly renderers As New Runtime.CompilerServices.ConditionalWeakTable(Of UI.SceneElement, XamlNativeEffect)
        Private ReadOnly rendererReferences As New List(Of WeakReference(Of XamlNativeEffect))

        Friend Function Render(device As GraphicsDevice, width As Integer, height As Integer, draw As Action, Optional owner As UI.SceneElement = Nothing) As Texture2D
            If owner IsNot Nothing Then
                Dim renderer = renderers.GetValue(owner, Function(key)
                                                            Dim created As New XamlNativeEffect
                                                            rendererReferences.Add(New WeakReference(Of XamlNativeEffect)(created))
                                                            Return created
                                                        End Function)
                Dim quality = GraphicsQuality.ForDevice(device)
                renderer.Kind = Me.Kind : renderer.Radius = Me.Radius * Math.Clamp(quality.BlurRadiusScale, 0, 3) : renderer.Amount = Me.Amount * Math.Clamp(quality.ChromaticAberrationScale, 0, 3)
                renderer.Phase = Me.Phase * Math.Clamp(quality.ShaderMotionSpeed, 0, 3) : renderer.Angle = Me.Angle : renderer.ShadowDepth = Me.ShadowDepth
                renderer.Direction = Me.Direction : renderer.Color = Me.Color : renderer.Opacity = Me.Opacity * Math.Clamp(quality.ShadowOpacityScale, 0, 2)
                renderer.compiledShader = Me.compiledShader
                renderer.TimeOverride = Me.TimeOverride
                Return renderer.Render(device, width, height, draw)
            End If
            If shader Is Nothing Then
                If Kind = XamlEffectKind.CompiledShader Then
                    shader = New Effect(device, compiledShader)
                Else
                    Using stream = GetType(XamlNativeEffect).Assembly.GetManifestResourceStream("ProjectZ.Resources.XamlEffects.mgfx")
                        Using bytes As New IO.MemoryStream()
                            stream.CopyTo(bytes)
                            shader = New Effect(device, bytes.ToArray())
                        End Using
                    End Using
                End If
                batch = New SpriteBatch(device)
            End If
            If source Is Nothing OrElse source.Width <> width OrElse source.Height <> height Then
                source?.Dispose() : scratch?.Dispose()
                source = New RenderTarget2D(device, width, height, False, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents)
                scratch = New RenderTarget2D(device, width, height, False, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents)
            End If
            device.SetRenderTarget(source)
            device.Clear(Color.Transparent)
            draw()
            If Kind = XamlEffectKind.PassThrough Then Return source
            shader.Parameters("TexelSize")?.SetValue(New Vector2(1.0F / width, 1.0F / height))
            If Kind = XamlEffectKind.CompiledShader Then
                shader.Parameters("Time")?.SetValue(If(TimeOverride, CSng(shaderClock.Elapsed.TotalSeconds)))
                DrawPass(device, source, scratch)
                Return scratch
            End If
            shader.Parameters("Amount").SetValue(Amount)
            shader.Parameters("Phase").SetValue(Phase)
            shader.Parameters("Angle").SetValue(Angle)
            If Kind >= XamlEffectKind.ChromaticAberration Then
                Pass(device, source, scratch, If(Kind = XamlEffectKind.SpasticChromaticAberration, 4,
                    If(Kind = XamlEffectKind.Invert, 5, If(Kind = XamlEffectKind.LinearChromaticAberration, 6, 3))), Vector2.Zero)
                Return scratch
            End If
            Dim radius = Math.Clamp(Me.Radius, 0, 100)
            Dim scale = GraphicsQuality.ForDevice(device).RenderScale
            Pass(device, source, scratch, 1, New Vector2(radius * scale.X / 4 / width, 0))
            If Kind = XamlEffectKind.DropShadow Then
                shader.Parameters("OriginalTexture").SetValue(source)
                shader.Parameters("ShadowColor").SetValue(New Vector4(Color.ToVector3(), Math.Clamp(Opacity, 0, 1) * Color.A / 255.0F))
                Dim radians = MathHelper.ToRadians(Direction)
                shader.Parameters("ShadowOffset").SetValue(New Vector2(MathF.Cos(radians) * ShadowDepth * scale.X / width, -MathF.Sin(radians) * ShadowDepth * scale.Y / height))
                ' The original must remain available at t1 while composing the shadow.
                Return scratch
            End If
            Pass(device, scratch, source, 1, New Vector2(0, radius * scale.Y / 4 / height))
            Return source
        End Function

        Friend Sub Composite(device As GraphicsDevice, texture As Texture2D, destination As Rectangle, Optional owner As UI.SceneElement = Nothing, Optional rotation As Single = 0, Optional pivot As Vector2 = Nothing)
            If owner IsNot Nothing Then
                Dim renderer As XamlNativeEffect = Nothing
                If Not renderers.TryGetValue(owner, renderer) Then Throw New InvalidOperationException("The effect must render its visual before compositing.")
                renderer.Composite(device, texture, destination, Nothing, MathHelper.ToRadians(owner.RenderRotation), pivot)
                Return
            End If
            If Kind = XamlEffectKind.DropShadow Then
                shader.Parameters("Mode").SetValue(2.0F)
                shader.Parameters("BlurStep").SetValue(New Vector2(0, Math.Clamp(Radius, 0, 100) * GraphicsQuality.ForDevice(device).RenderScale.Y / 4 / texture.Height))
                batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, device.RasterizerState, shader)
            Else
                batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, device.RasterizerState)
            End If
            If rotation = 0 Then
                batch.Draw(texture, destination, Color.White)
            Else
                batch.Draw(texture, New Vector2(destination.X, destination.Y) + pivot, Nothing, Color.White, rotation, pivot, New Vector2(destination.Width / CSng(texture.Width), destination.Height / CSng(texture.Height)), SpriteEffects.None, 0)
            End If
            batch.End()
        End Sub

        Private Sub Pass(device As GraphicsDevice, input As Texture2D, output As RenderTarget2D, mode As Single, stepSize As Vector2)
            shader.Parameters("Mode").SetValue(mode)
            shader.Parameters("BlurStep").SetValue(stepSize)
            DrawPass(device, input, output)
        End Sub

        Private Sub DrawPass(device As GraphicsDevice, input As Texture2D, output As RenderTarget2D)
            device.SetRenderTarget(output)
            device.Clear(Color.Transparent)
            batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, shader)
            batch.Draw(input, New Rectangle(0, 0, output.Width, output.Height), Color.White)
            batch.End()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            For Each reference In rendererReferences
                Dim renderer As XamlNativeEffect = Nothing
                If reference.TryGetTarget(renderer) Then renderer.Dispose()
            Next
            rendererReferences.Clear() : renderers.Clear()
            source?.Dispose() : scratch?.Dispose() : shader?.Dispose() : batch?.Dispose()
            source = Nothing : scratch = Nothing : shader = Nothing : batch = Nothing
        End Sub
    End Class
End Namespace
