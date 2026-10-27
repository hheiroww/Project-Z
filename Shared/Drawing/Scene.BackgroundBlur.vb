Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing
    Public MustInherit Partial Class Scene
        Private Sub DrawBackgroundBlur(element As SceneElement, device As GraphicsDevice, viewport As Rectangle, forceClip As Boolean)
            Dim targets = device.GetRenderTargets()
            If targets.Length <> 1 Then Return
            Dim backdrop = TryCast(targets(0).RenderTarget, RenderTarget2D)
            If backdrop Is Nothing Then Return
            Dim originalViewport = device.Viewport
            Dim originalScissor = device.ScissorRectangle
            Dim originalRasterizer = device.RasterizerState
            Dim bounds = element.Rectangle
            Dim clip = Rectangle.Intersect(bounds, viewport)
            Dim ancestor = element.Parent
            While ancestor IsNot Nothing
                If ancestor.Clip Then clip = Rectangle.Intersect(clip, ancestor.ClippingRectangle)
                ancestor = ancestor.Parent
            End While
            If forceClip AndAlso Not element.Clip AndAlso element.Parent IsNot Nothing Then clip = Rectangle.Intersect(clip, element.Parent.Rectangle)
            clip.Offset(-CInt(EffectRenderOffset.X), -CInt(EffectRenderOffset.Y))
            clip = Rectangle.Intersect(Quality.ScaleRectangle(clip), originalScissor)
            If clip.Width <= 0 OrElse clip.Height <= 0 Then Return
            bounds.Offset(-CInt(EffectRenderOffset.X), -CInt(EffectRenderOffset.Y))
            Dim mask = Quality.ScaleRectangle(bounds)
            Dim sample = mask
            Dim padding = CInt(Math.Ceiling(element.BackgroundBlur * Math.Max(Quality.RenderScale.X, Quality.RenderScale.Y) * Math.Clamp(Quality.BlurRadiusScale, 0, 3))) + 2
            sample.Inflate(padding, padding)
            sample = Rectangle.Intersect(sample, New Rectangle(0, 0, backdrop.Width, backdrop.Height))
            If sample.Width <= 0 OrElse sample.Height <= 0 Then Return
            Dim fx = element.BackgroundBlurEffect
            Dim texture As Texture2D = Nothing
            Try
                ' Switching targets resolves MSAA before sampling. Never sample a bound target.
                texture = fx.Render(device, sample.Width, sample.Height,
                    Sub()
                        effectCompositeBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone)
                        effectCompositeBatch.Draw(backdrop, New Rectangle(0, 0, sample.Width, sample.Height), sample, Color.White)
                        effectCompositeBatch.End()
                    End Sub)
            Finally
                device.SetRenderTargets(targets)
                device.Viewport = originalViewport
                device.RasterizerState = originalRasterizer
                device.ScissorRectangle = originalScissor
            End Try
            If _scissorRasterizerState Is Nothing Then _scissorRasterizerState = New RasterizerState With {.ScissorTestEnable = True, .CullMode = CullMode.None, .MultiSampleAntiAlias = True}
            Try
                device.RasterizerState = _scissorRasterizerState
                device.ScissorRectangle = clip
                Dim corners = If(TryCast(element, RectangleElement)?.CornerRadii, New CornerRadii(0))
                Dim opacity = element.ApplyOpacity(Color.White).A / 255.0F
                fx.CompositeBackdrop(device, texture, sample, New Vector4(mask.X - sample.X, mask.Y - sample.Y, mask.Width, mask.Height),
                    New Vector4(corners.TopLeft, corners.TopRight, corners.BottomRight, corners.BottomLeft) * Math.Min(Quality.RenderScale.X, Quality.RenderScale.Y), opacity)
            Finally
                device.RasterizerState = originalRasterizer
                device.ScissorRectangle = originalScissor
            End Try
        End Sub
    End Class
End Namespace
