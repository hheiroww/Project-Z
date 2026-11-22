Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing
    Public MustInherit Partial Class Scene
        Private textContrastColor As Color?
        Private textContrastMask As RenderTarget2D
        Private textContrastResult As RenderTarget2D
        Private textContrastShader As Effect

        ''' <summary>
        ''' The exact TextElement foreground color to adapt to the pixels beneath each glyph.
        ''' Nothing leaves authored colors untouched. Transparent pixels with no known backdrop keep their authored color.
        ''' </summary>
        Public Property BackdropTextContrastColor As Color?
            Get
                Return textContrastColor
            End Get
            Set(value As Color?)
                If Object.Equals(textContrastColor, value) Then Return
                textContrastColor = value
                RequestRender()
            End Set
        End Property

        Private Function UsesBackdropTextContrast(element As SceneElement) As Boolean
            Dim text = TryCast(element, TextElement)
            Return textContrastColor.HasValue AndAlso text IsNot Nothing AndAlso
                text.ForegroundColor = textContrastColor.Value AndAlso Not String.IsNullOrWhiteSpace(text.Text)
        End Function

        Private Function DrawBackdropTextContrast(element As SceneElement, device As GraphicsDevice,
                                                  viewport As Rectangle, forceClip As Boolean) As Boolean
            If Not UsesBackdropTextContrast(element) Then Return False
            Dim targets = device.GetRenderTargets()
            ' An ancestor effect's scratch target does not have the composed backdrop beneath its children.
            If targets.Length <> 1 OrElse targets(0).RenderTarget IsNot effectCanvas Then Return False

            Dim logical = element.Rectangle
            logical.Inflate(2, 2)
            logical = Rectangle.Intersect(logical, viewport)
            Dim originalViewport = device.Viewport
            Dim originalScissor = device.ScissorRectangle
            Dim originalRasterizer = device.RasterizerState
            Dim originalBlend = device.BlendState
            Dim originalDepth = device.DepthStencilState
            Dim originalBackdropBinding = device.Textures(1)
            Dim bounds = Rectangle.Intersect(PhysicalClip(logical), originalViewport.Bounds)
            If originalRasterizer.ScissorTestEnable Then bounds = Rectangle.Intersect(bounds, originalScissor)
            If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return True

            EnsureBackdropTextTargets(device, bounds.Width, bounds.Height)
            Dim tile As New Rectangle(0, 0, bounds.Width, bounds.Height)
            Dim previousOffset = EffectRenderOffset
            Try
                ' Unbinding the scene resolves its existing pixels before either shader input is sampled.
                device.SetRenderTarget(textContrastMask)
                device.Clear(Color.Transparent)
                device.RasterizerState = SceneScissorRasterizer()
                device.ScissorRectangle = tile
                EffectRenderOffset = New Vector2(bounds.X, bounds.Y) / Quality.RenderScale
                DrawElementRecursive(element, device, viewport, forceClip)

                device.SetRenderTarget(textContrastResult)
                textContrastShader.Parameters("Mode").SetValue(9.0F)
                textContrastShader.Parameters("OriginalTexture").SetValue(effectCanvas)
                textContrastShader.Parameters("BackdropTextColor").SetValue(textContrastColor.Value.ToVector4())
                textContrastShader.Parameters("BackdropBounds").SetValue(New Vector4(
                    bounds.X / CSng(effectCanvas.Width), bounds.Y / CSng(effectCanvas.Height),
                    textContrastMask.Width / CSng(effectCanvas.Width), textContrastMask.Height / CSng(effectCanvas.Height)))
                effectCompositeBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp,
                                            DepthStencilState.None, RasterizerState.CullNone, textContrastShader)
                effectCompositeBatch.Draw(textContrastMask, tile, tile, Color.White)
                effectCompositeBatch.End()
            Finally
                EffectRenderOffset = previousOffset
                device.Textures(1) = Nothing
                device.SetRenderTargets(targets)
                device.Textures(1) = originalBackdropBinding
                device.Viewport = originalViewport
                device.RasterizerState = originalRasterizer
                device.ScissorRectangle = originalScissor
                device.BlendState = originalBlend
                device.DepthStencilState = originalDepth
            End Try

            Try
                effectCompositeBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                                            DepthStencilState.None, originalRasterizer)
                effectCompositeBatch.Draw(textContrastResult, bounds, tile, Color.White)
                effectCompositeBatch.End()
            Finally
                device.RasterizerState = originalRasterizer
                device.ScissorRectangle = originalScissor
                device.BlendState = originalBlend
                device.DepthStencilState = originalDepth
            End Try
            Return True
        End Function

        Private Sub EnsureBackdropTextTargets(device As GraphicsDevice, width As Integer, height As Integer)
            If textContrastShader Is Nothing Then
                Using stream = GetType(Scene).Assembly.GetManifestResourceStream("ProjectZ.Resources.XamlEffects.mgfx")
                    Using bytes As New IO.MemoryStream()
                        stream.CopyTo(bytes)
                        textContrastShader = New Effect(device, bytes.ToArray())
                    End Using
                End Using
            End If

            Dim samples = effectCanvas.MultiSampleCount
            If textContrastMask IsNot Nothing AndAlso textContrastMask.Width >= width AndAlso
                textContrastMask.Height >= height AndAlso textContrastMask.MultiSampleCount = samples AndAlso
                textContrastMask.Width <= effectCanvas.Width AndAlso textContrastMask.Height <= effectCanvas.Height Then Return
            width = Math.Min(effectCanvas.Width, Math.Max(width, If(textContrastMask?.Width, 0)))
            height = Math.Min(effectCanvas.Height, Math.Max(height, If(textContrastMask?.Height, 0)))
            textContrastMask?.Dispose()
            textContrastResult?.Dispose()
            textContrastMask = New RenderTarget2D(device, width, height, False, SurfaceFormat.Color, DepthFormat.None,
                                                 samples, RenderTargetUsage.DiscardContents)
            textContrastResult = New RenderTarget2D(device, width, height, False, SurfaceFormat.Color, DepthFormat.None)
        End Sub
    End Class
End Namespace
