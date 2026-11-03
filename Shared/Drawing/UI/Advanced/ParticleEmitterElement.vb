Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing.Particles
Imports ProjectZ.Shared.Drawing.ThreeD

Namespace [Shared].Drawing.UI.Advanced
    ''' <summary>
    ''' 2D scene particles or camera-facing 3D billboards in a scene viewport. Reuses a GPU
    ''' vertex/index buffer and batches atlas sprites; CPU simulation never reads GPU data back.
    ''' A 3D emitter can share a Camera3D with a Surface3DElement. It preserves surrounding GPU state.
    ''' </summary>
    Public Class ParticleEmitterElement
        Inherits SceneElement

        Private ReadOnly simulationValue As ParticleSimulation
        Private ReadOnly vertices As VertexPositionColorTexture()
        Private ReadOnly order As Integer()
        Private ReadOnly textures As Texture2D()
        Private vertexBuffer As DynamicVertexBuffer
        Private indexBuffer As IndexBuffer
        Private shader As BasicEffect
        Private whiteTexture As Texture2D
        Private ReadOnly comparer As ParticleDepthComparer

        Public Sub New()
            Me.New(Nothing)
        End Sub
        Public Sub New(scene As Scene, Optional capacity As Integer = 4096, Optional seed As Integer = 1)
            MyBase.New()
            If scene IsNot Nothing Then updateScene(scene)
            simulationValue = New ParticleSimulation(capacity, seed)
            ReDim vertices(capacity * 4 - 1) : ReDim order(capacity - 1) : ReDim textures(capacity - 1)
            comparer = New ParticleDepthComparer(simulationValue)
            Size = New Vector2(320, 240)
            isMouseBypassEnabled = True
        End Sub

        Public ReadOnly Property Simulation As ParticleSimulation
            Get
                Return simulationValue
            End Get
        End Property
        ''' <summary>Caller owns this reusable animation and disposes it after all emitters stop using it.</summary>
        Public Property SpriteAnimation As ParticleSpriteAnimation
        Public Property Camera As New Camera3D
        Public Property Position3D As Vector3
        Public Property Is3D As Boolean
            Get
                Return simulationValue.Is3D
            End Get
            Set(value As Boolean)
                simulationValue.Is3D = value
            End Set
        End Property
        Public Property FadeOut As Boolean = True
        Public Property SortBackToFront As Boolean = True
        Public Property ParticleBlendState As BlendState = BlendState.NonPremultiplied
        ''' <summary>DepthRead lets opaque 3D surfaces drawn earlier occlude particles without particles writing depth.</summary>
        Public Property ParticleDepthState As DepthStencilState = DepthStencilState.DepthRead
        Public ReadOnly Property LastDrawCallCount As Integer
            Get
                Return drawCallCount
            End Get
        End Property
        Private drawCallCount As Integer

        Public Overrides Sub Tick(gameTime As GameTime)
            MyBase.Tick(gameTime)
            Simulation.Advance(gameTime.ElapsedGameTime.TotalSeconds)
            If Simulation.Enabled AndAlso (Simulation.Count > 0 OrElse Simulation.EmissionRate > 0) Then Scene?.RequestRender()
        End Sub

        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            ' doDraw owns one complete GPU pass instead of opening a SpriteBatch per particle.
        End Sub

        Protected Friend Overrides Sub doDraw(gameTime As GameTime)
            drawCallCount = 0
            If IsDisposed OrElse Scene Is Nothing OrElse Simulation.Count = 0 OrElse Size.X <= 0 OrElse Size.Y <= 0 Then Return
            Dim device = Scene.graphicsDevice
            EnsureGpuResources(device)
            Dim oldViewport = device.Viewport, oldBlend = device.BlendState, oldDepth = device.DepthStencilState
            Dim oldRasterizer = device.RasterizerState, oldSampler = device.SamplerStates(0)
            Dim oldIndex = device.Indices
            Dim oldTexture = device.Textures(0)
            Try
                Dim right = Vector3.Right, up = Vector3.UnitY
                Dim qualityScale = GraphicsQuality.ForDevice(device).RenderScale
                If Is3D Then
                    Dim viewportBounds = GraphicsQuality.ForDevice(device).ScaleRectangle(New Rectangle(Rectangle.X - CInt(Scene.EffectRenderOffset.X), Rectangle.Y - CInt(Scene.EffectRenderOffset.Y), Rectangle.Width, Rectangle.Height))
                    viewportBounds = Microsoft.Xna.Framework.Rectangle.Intersect(viewportBounds, oldViewport.Bounds)
                    If viewportBounds.Width <= 0 OrElse viewportBounds.Height <= 0 Then Return
                    device.Viewport = New Viewport(viewportBounds)
                    Dim view = Camera.CreateView()
                    Dim inverseView = Matrix.Invert(view)
                    right = inverseView.Right : up = -inverseView.Up
                    shader.World = Matrix.CreateTranslation(Position3D)
                    shader.View = view : shader.Projection = Camera.CreateProjection(device.Viewport.AspectRatio)
                Else
                    Dim localScale As New Vector3(Rectangle.Width / Math.Max(Size.X, 0.001F), Rectangle.Height / Math.Max(Size.Y, 0.001F), 1)
                    shader.World = Matrix.CreateScale(localScale) * Matrix.CreateTranslation(Rectangle.X - Scene.EffectRenderOffset.X, Rectangle.Y - Scene.EffectRenderOffset.Y, 0) * Matrix.CreateScale(qualityScale.X, qualityScale.Y, 1)
                    shader.View = Matrix.Identity
                    shader.Projection = Matrix.CreateOrthographicOffCenter(0, oldViewport.Width, oldViewport.Height, 0, -1, 1)
                End If
                Dim count = Simulation.Count
                For index = 0 To count - 1
                    order(index) = index
                Next
                If Is3D AndAlso SortBackToFront Then
                    comparer.CameraPosition = Camera.Position - Position3D
                    Array.Sort(order, 0, count, comparer)
                End If
                For index = 0 To count - 1
                    Dim particle = Simulation(order(index))
                    Dim frame = SpriteAnimation?.FrameAt(particle.Age)
                    Dim texture = If(frame?.Texture, whiteTexture)
                    If texture.IsDisposed Then texture = whiteTexture
                    textures(index) = texture
                    Dim region = If(frame Is Nothing, New Rectangle(0, 0, 1, 1), frame.Source)
                    Dim uv0 As New Vector2(region.Left / CSng(texture.Width), region.Top / CSng(texture.Height))
                    Dim uv1 As New Vector2(region.Right / CSng(texture.Width), region.Bottom / CSng(texture.Height))
                    Dim cosine = MathF.Cos(particle.Rotation), sine = MathF.Sin(particle.Rotation)
                    Dim horizontal = (right * cosine + up * sine) * particle.Size.X * 0.5F
                    Dim vertical = (-right * sine + up * cosine) * particle.Size.Y * 0.5F
                    Dim tint = particle.Tint
                    Dim alpha = Math.Clamp(Opacity, 0, 1) * If(FadeOut, CSng(Math.Clamp(1 - particle.Age / particle.Lifetime, 0, 1)), 1.0F)
                    tint.A = CByte(Math.Clamp(CInt(Math.Round(tint.A * alpha)), 0, 255))
                    Dim offset = index * 4
                    vertices(offset) = New VertexPositionColorTexture(particle.Position - horizontal - vertical, tint, uv0)
                    vertices(offset + 1) = New VertexPositionColorTexture(particle.Position + horizontal - vertical, tint, New Vector2(uv1.X, uv0.Y))
                    vertices(offset + 2) = New VertexPositionColorTexture(particle.Position + horizontal + vertical, tint, uv1)
                    vertices(offset + 3) = New VertexPositionColorTexture(particle.Position - horizontal + vertical, tint, New Vector2(uv0.X, uv1.Y))
                Next
                vertexBuffer.SetData(vertices, 0, count * 4, SetDataOptions.Discard)
                device.SetVertexBuffer(vertexBuffer) : device.Indices = indexBuffer
                device.BlendState = If(ParticleBlendState, BlendState.NonPremultiplied)
                device.DepthStencilState = If(Is3D, If(ParticleDepthState, DepthStencilState.DepthRead), DepthStencilState.None)
                ' Preserve the scene's scissor-enabled rasterizer so ancestor clipping still applies.
                device.RasterizerState = If(oldRasterizer IsNot Nothing AndAlso oldRasterizer.ScissorTestEnable, oldRasterizer, RasterizerState.CullNone)
                device.SamplerStates(0) = Scene.Quality.Sampler
                Dim first = 0
                While first < count
                    Dim last = first + 1
                    While last < count AndAlso textures(last) Is textures(first)
                        last += 1
                    End While
                    shader.Texture = textures(first)
                    For Each pass In shader.CurrentTechnique.Passes
                        pass.Apply()
                        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, first * 6, (last - first) * 2)
                        drawCallCount += 1
                    Next
                    first = last
                End While
            Finally
                ' MonoGame exposes no public vertex-binding getter. Like Surface3DElement,
                ' unbind our buffer; following SpriteBatch/surface passes bind their own.
                device.SetVertexBuffer(Nothing) : device.Indices = oldIndex
                device.Viewport = oldViewport : device.BlendState = oldBlend : device.DepthStencilState = oldDepth
                device.RasterizerState = oldRasterizer : device.SamplerStates(0) = oldSampler : device.Textures(0) = oldTexture
            End Try
        End Sub

        Private Sub EnsureGpuResources(device As GraphicsDevice)
            If vertexBuffer IsNot Nothing AndAlso Not vertexBuffer.IsDisposed Then Return
            vertexBuffer = New DynamicVertexBuffer(device, GetType(VertexPositionColorTexture), vertices.Length, BufferUsage.WriteOnly)
            Dim indices(Simulation.Capacity * 6 - 1) As UShort
            For index = 0 To Simulation.Capacity - 1
                Dim vertex = index * 4, offset = index * 6
                indices(offset) = CUShort(vertex) : indices(offset + 1) = CUShort(vertex + 1) : indices(offset + 2) = CUShort(vertex + 2)
                indices(offset + 3) = CUShort(vertex) : indices(offset + 4) = CUShort(vertex + 2) : indices(offset + 5) = CUShort(vertex + 3)
            Next
            indexBuffer = New IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly)
            indexBuffer.SetData(indices)
            whiteTexture = New Texture2D(device, 1, 1) : whiteTexture.SetData(New Color() {Color.White})
            shader = New BasicEffect(device) With {.TextureEnabled = True, .VertexColorEnabled = True, .LightingEnabled = False}
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                vertexBuffer?.Dispose() : indexBuffer?.Dispose() : shader?.Dispose() : whiteTexture?.Dispose()
                vertexBuffer = Nothing : indexBuffer = Nothing : shader = Nothing : whiteTexture = Nothing
                Simulation.Clear()
            End If
            MyBase.Dispose(disposing)
        End Sub

        Private NotInheritable Class ParticleDepthComparer
            Implements IComparer(Of Integer)
            Private ReadOnly simulation As ParticleSimulation
            Public CameraPosition As Vector3
            Public Sub New(simulation As ParticleSimulation)
                Me.simulation = simulation
            End Sub
            Public Function Compare(left As Integer, right As Integer) As Integer Implements IComparer(Of Integer).Compare
                Dim distanceLeft = Vector3.DistanceSquared(simulation(left).Position, CameraPosition)
                Dim distanceRight = Vector3.DistanceSquared(simulation(right).Position, CameraPosition)
                Dim result = distanceRight.CompareTo(distanceLeft)
                Return If(result = 0, left.CompareTo(right), result)
            End Function
        End Class
    End Class
End Namespace
