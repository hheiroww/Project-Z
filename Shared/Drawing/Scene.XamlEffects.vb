Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing.UI

Namespace [Shared].Drawing
    Public MustInherit Partial Class Scene
        Friend Property EffectRenderOffset As Vector2
        Private effectCanvas As RenderTarget2D
        Private effectCompositeBatch As SpriteBatch

        Private Sub DrawXamlEffectCanvas(ordered As List(Of SceneElement), device As GraphicsDevice, viewport As Rectangle, forceClip As Boolean)
            Dim targets = device.GetRenderTargets()
            Dim originalViewport = device.Viewport
            Dim scissor = device.ScissorRectangle
            Dim rasterizer = device.RasterizerState
            Dim wasBegun = hasBegun
            Dim physicalViewport = Quality.ScaleRectangle(viewport)
            ' This is a scene rasterization pass, not just a shader scratch texture.
            ' Flattening it without MSAA removes coverage from every other control
            ' as soon as one hover effect becomes active. SSAA is already scaled.
            Dim samples = If(Quality.AntiAliasingTechnique = "MSAA", Quality.SupportedSamples(device, Quality.AntiAliasingSamples), 0)
            If wasBegun Then spriteBatch.End() : hasBegun = False
            If effectCanvas Is Nothing OrElse effectCanvas.Width <> physicalViewport.Width OrElse effectCanvas.Height <> physicalViewport.Height OrElse effectCanvas.MultiSampleCount <> samples Then
                effectCanvas?.Dispose()
                effectCanvas = New RenderTarget2D(device, physicalViewport.Width, physicalViewport.Height, False, SurfaceFormat.Color, DepthFormat.None, samples, RenderTargetUsage.PreserveContents)
            End If
            If effectCompositeBatch Is Nothing Then effectCompositeBatch = New SpriteBatch(device)
            Try
                device.SetRenderTarget(effectCanvas)
                device.Clear(BackgroundColor)
                device.ScissorRectangle = physicalViewport
                DrawEffectRange(ordered, 0, ordered.Count, device, viewport, forceClip, Nothing)
            Finally
                EffectRenderOffset = Vector2.Zero
                device.SetRenderTargets(targets)
                device.Viewport = originalViewport
                device.RasterizerState = rasterizer
                device.ScissorRectangle = scissor
            End Try
            effectCompositeBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone)
            effectCompositeBatch.Draw(effectCanvas, physicalViewport, Color.White)
            effectCompositeBatch.End()
            If wasBegun Then spriteBatch.Begin() : hasBegun = True
        End Sub

        Private Sub DrawEffectRange(ordered As List(Of SceneElement), first As Integer, limit As Integer, device As GraphicsDevice, viewport As Rectangle, forceClip As Boolean, skipEffect As SceneElement)
            Dim index = first
            While index < limit
                Dim element = ordered(index)
                If Not IsEffectivelyVisible(element) Then index += 1 : Continue While
                If element.BackgroundBlur > 0 AndAlso Quality.EffectsEnabled AndAlso element IsNot skipEffect Then
                    DrawBackgroundBlur(element, device, viewport, forceClip)
                End If
                Dim fx = element.DrawingEffect
                If fx Is Nothing OrElse Not fx.IsActive OrElse element Is skipEffect Then
                    DrawElementRecursive(element, device, viewport, forceClip)
                    index += 1
                    Continue While
                End If
                Dim last = index + 1
                Dim bounds = element.Rectangle
                While last < limit AndAlso IsVisualDescendant(ordered(last), element)
                    If IsEffectivelyVisible(ordered(last)) Then bounds = Rectangle.Union(bounds, ordered(last).Rectangle)
                    last += 1
                End While
                bounds.Inflate(fx.EffectivePadding(Quality), fx.EffectivePadding(Quality))
                ' Clipping the source target remaps UVs when a card scrolls offscreen,
                ' changing distortion frequency/phase. Clip the final composite only.
                If bounds.Width > 0 AndAlso bounds.Height > 0 AndAlso bounds.Intersects(viewport) Then
                    Dim previousOffset = EffectRenderOffset
                    Dim targets = device.GetRenderTargets()
                    Dim previousViewport = device.Viewport
                    Dim previousScissor = device.ScissorRectangle
                    Dim previousRasterizer = device.RasterizerState
                    Dim texture As Texture2D = Nothing
                    Try
                        EffectRenderOffset = New Vector2(bounds.X, bounds.Y)
                        Dim startIndex = index, endIndex = last
                        Dim physicalBounds = Quality.ScaleRectangle(bounds)
                        texture = fx.Render(device, physicalBounds.Width, physicalBounds.Height,
                            Sub()
                                device.ScissorRectangle = New Rectangle(0, 0, physicalBounds.Width, physicalBounds.Height)
                                device.RasterizerState = RasterizerState.CullNone
                                DrawEffectRange(ordered, startIndex, endIndex, device, bounds, forceClip, element)
                            End Sub, element)
                    Finally
                        EffectRenderOffset = previousOffset
                        device.SetRenderTargets(targets)
                        device.Viewport = previousViewport
                        device.RasterizerState = previousRasterizer
                        device.ScissorRectangle = previousScissor
                    End Try
                    ' Effects expand outside their own geometry, but not through
                    ' an ancestor ScrollViewer's clipping boundary.
                    Dim clip = viewport
                    Dim ancestor = element.Parent
                    While ancestor IsNot Nothing
                        If ancestor.Clip Then clip = Rectangle.Intersect(clip, ancestor.ClippingRectangle)
                        ancestor = ancestor.Parent
                    End While
                    clip.Offset(-CInt(previousOffset.X), -CInt(previousOffset.Y))
                    clip = Quality.ScaleRectangle(clip)
                    clip = Rectangle.Intersect(clip, previousScissor)
                    If clip.Width > 0 AndAlso clip.Height > 0 Then
                        If _scissorRasterizerState Is Nothing Then _scissorRasterizerState = New RasterizerState With {.ScissorTestEnable = True, .CullMode = CullMode.None}
                        device.RasterizerState = _scissorRasterizerState
                        device.ScissorRectangle = clip
                        Dim destination = bounds
                        destination.Offset(-CInt(previousOffset.X), -CInt(previousOffset.Y))
                        fx.Composite(device, texture, Quality.ScaleRectangle(destination), element, 0, (element.RotationPivot - New Vector2(bounds.X, bounds.Y)) * Quality.RenderScale)
                        device.RasterizerState = previousRasterizer
                        device.ScissorRectangle = previousScissor
                    End If
                End If
                index = last
            End While
        End Sub

        Private Shared Function IsVisualDescendant(element As SceneElement, root As SceneElement) As Boolean
            Dim parent = element.Parent
            While parent IsNot Nothing
                If parent Is root Then Return True
                parent = parent.Parent
            End While
            Return False
        End Function
    End Class
End Namespace
