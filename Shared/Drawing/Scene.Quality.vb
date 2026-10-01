Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].Drawing
    Partial Public MustInherit Class Scene
        Private qualityTarget As RenderTarget2D
        Private qualityBatch As SpriteBatch
        Private qualityResolve As Effect

        Public ReadOnly Property Quality As GraphicsQuality
            Get
                Return GraphicsQuality.ForDevice(graphicsDevice)
            End Get
        End Property

        Private Sub DrawWithQuality(time As GameTime, Optional bypassRenderTarget As Boolean = False)
            Dim samples = Quality.SupportedSamples(graphicsDevice, Quality.AntiAliasingSamples)
            If samples = 0 OrElse (UseRenderTarget AndAlso Not bypassRenderTarget) Then
                DrawCore(time)
                Return
            End If
            Dim viewport = graphicsDevice.Viewport
            Dim scale = Vector2.One
            Dim hardwareSamples = samples
            If Quality.AntiAliasingTechnique = "SSAA" Then
                scale = New Vector2(If(samples >= 8, 4, 2), If(samples = 16, 4, If(samples >= 4, 2, 1)))
                hardwareSamples = 0
            End If
            Dim width = CInt(viewport.Width * scale.X), height = CInt(viewport.Height * scale.Y)
            If qualityTarget Is Nothing OrElse qualityTarget.Width <> width OrElse
                qualityTarget.Height <> height OrElse qualityTarget.MultiSampleCount <> hardwareSamples Then
                qualityTarget?.Dispose()
                qualityTarget = New RenderTarget2D(graphicsDevice, width, height, False,
                    SurfaceFormat.Color, DepthFormat.Depth24Stencil8, hardwareSamples, RenderTargetUsage.DiscardContents)
            End If
            If qualityBatch Is Nothing Then qualityBatch = New SpriteBatch(graphicsDevice)
            Dim prior = graphicsDevice.GetRenderTargets()
            Dim scissor = graphicsDevice.ScissorRectangle
            Dim previousScale = Quality.RenderScale
            Try
                graphicsDevice.SetRenderTarget(qualityTarget)
                Quality.RenderScale = scale
                DrawCore(time, True)
            Finally
                Quality.RenderScale = previousScale
                ' Unbinding resolves the multisample surface before SpriteBatch samples it.
                If prior.Length = 0 Then graphicsDevice.SetRenderTarget(Nothing) Else graphicsDevice.SetRenderTargets(prior)
                graphicsDevice.Viewport = viewport
                graphicsDevice.ScissorRectangle = scissor
            End Try
            If scale <> Vector2.One Then
                If qualityResolve Is Nothing Then
                    Using stream = GetType(Scene).Assembly.GetManifestResourceStream("ProjectZ.Resources.XamlEffects.mgfx")
                        Using bytes As New IO.MemoryStream()
                            stream.CopyTo(bytes)
                            qualityResolve = New Effect(graphicsDevice, bytes.ToArray())
                        End Using
                    End Using
                End If
                qualityResolve.Parameters("Mode").SetValue(7.0F)
                qualityResolve.Parameters("BlurStep").SetValue(scale)
                qualityResolve.Parameters("TexelSize").SetValue(New Vector2(1.0F / width, 1.0F / height))
            End If
            qualityBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, If(scale = Vector2.One, Nothing, qualityResolve))
            qualityBatch.Draw(qualityTarget, New Rectangle(0, 0, viewport.Width, viewport.Height), Color.White)
            qualityBatch.End()
        End Sub
    End Class
End Namespace
