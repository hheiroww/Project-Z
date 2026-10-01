Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].Drawing.UI.Primitives
    Public Enum XamlEffectKind
        Blur
        DropShadow
        ChromaticAberration
        SpasticChromaticAberration
        Invert
        LinearChromaticAberration
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
        Public ReadOnly Property IsActive As Boolean
            Get
                Return If(Kind >= XamlEffectKind.ChromaticAberration, Math.Abs(Amount) > 0.00001F,
                          If(Kind = XamlEffectKind.DropShadow, Opacity > 0, Radius > 0))
            End Get
        End Property
        Public ReadOnly Property Padding As Integer
            Get
                If Kind >= XamlEffectKind.ChromaticAberration Then Return 0
                Return CInt(Math.Ceiling(Math.Clamp(Radius, 0, 100) + If(Kind = XamlEffectKind.DropShadow, Math.Abs(ShadowDepth), 0))) + 2
            End Get
        End Property
        Private source As RenderTarget2D
        Private scratch As RenderTarget2D
        Private shader As Effect
        Private batch As SpriteBatch

        Friend Function Render(device As GraphicsDevice, width As Integer, height As Integer, draw As Action) As Texture2D
            If shader Is Nothing Then
                Using stream = GetType(XamlNativeEffect).Assembly.GetManifestResourceStream("ProjectZ.Resources.XamlEffects.mgfx")
                    Using bytes As New IO.MemoryStream()
                        stream.CopyTo(bytes)
                        shader = New Effect(device, bytes.ToArray())
                    End Using
                End Using
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
            shader.Parameters("TexelSize")?.SetValue(New Vector2(1.0F / width, 1.0F / height))
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

        Friend Sub Composite(device As GraphicsDevice, texture As Texture2D, destination As Rectangle)
            If Kind = XamlEffectKind.DropShadow Then
                shader.Parameters("Mode").SetValue(2.0F)
                shader.Parameters("BlurStep").SetValue(New Vector2(0, Math.Clamp(Radius, 0, 100) * GraphicsQuality.ForDevice(device).RenderScale.Y / 4 / texture.Height))
                batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, device.RasterizerState, shader)
            Else
                batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, device.RasterizerState)
            End If
            batch.Draw(texture, destination, Color.White)
            batch.End()
        End Sub

        Private Sub Pass(device As GraphicsDevice, input As Texture2D, output As RenderTarget2D, mode As Single, stepSize As Vector2)
            device.SetRenderTarget(output)
            device.Clear(Color.Transparent)
            shader.Parameters("Mode").SetValue(mode)
            shader.Parameters("BlurStep").SetValue(stepSize)
            batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, shader)
            batch.Draw(input, New Rectangle(0, 0, output.Width, output.Height), Color.White)
            batch.End()
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            source?.Dispose() : scratch?.Dispose() : shader?.Dispose() : batch?.Dispose()
        End Sub
    End Class
End Namespace
