Imports System.IO
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports ProjectZ.Shared.Drawing.ThreeD

Namespace [Shared].Drawing.UI.Advanced

    ''' <summary>
    ''' Backend-neutral XYZW 3D viewport for Project Z scenes. Rendering uses
    ''' MonoGame's active DirectX 12 or DirectX 11 graphics device.
    ''' </summary>
    Public Class Surface3DElement
        Inherits SceneElement

        Private NotInheritable Class GpuSurface
            Implements IDisposable

            Public Property VertexBuffer As VertexBuffer
            Public Property IndexBuffer As IndexBuffer
            Public Property Texture As Texture2D
            Public Property Material As SurfaceMaterial3D
            Public Property PrimitiveCount As Integer

            Public Sub Dispose() Implements IDisposable.Dispose
                Texture?.Dispose()
                IndexBuffer?.Dispose()
                VertexBuffer?.Dispose()
            End Sub
        End Class

        Private ReadOnly gpuSurfaces As New List(Of GpuSurface)()
        Private basicShader As BasicEffect
        Private wireframeState As RasterizerState
        Private gpuResourcesDirty As Boolean = True
        Private modelValue As Model3DAsset

        Public Property Camera As New Camera3D()
        Public Property Position3D As Vector3 = Vector3.Zero
        Public Property RotationXYZ As Vector3 = Vector3.Zero
        Public Property Scale3D As Vector3 = Vector3.One
        Public Property AutoRotateSpeed As Vector3 = Vector3.Zero
        Public Property BackgroundColor As Color = Color.Transparent
        Public Property ClearBackground As Boolean = False
        Public Property Wireframe As Boolean = False
        Public Property ShaderEffect As Effect

        Public Event ConfigureShader(effect As Effect, surface As Surface3DElement)
        Public Event ModelChanged(model As Model3DAsset)

        Public Property Model As Model3DAsset
            Get
                Return modelValue
            End Get
            Set(value As Model3DAsset)
                modelValue = value
                gpuResourcesDirty = True
                RaiseEvent ModelChanged(value)
            End Set
        End Property

        Public ReadOnly Property World As Matrix
            Get
                Return Matrix.CreateScale(Scale3D) *
                       Matrix.CreateFromYawPitchRoll(RotationXYZ.Y, RotationXYZ.X, RotationXYZ.Z) *
                       Matrix.CreateTranslation(Position3D)
            End Get
        End Property

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            Size = New Vector2(320, 240)
        End Sub

        Public Sub New(scene As Scene, model As Model3DAsset)
            Me.New(scene)
            Me.Model = model
        End Sub

        Public Sub LoadModel(path As String)
            Model = ModelImporter3D.Load(path)
        End Sub

        Public Async Function LoadModelAsync(path As String, Optional cancellationToken As Threading.CancellationToken = Nothing) As Task
            Model = Await ModelImporter3D.LoadAsync(path, cancellationToken).ConfigureAwait(True)
        End Function

        Public Sub SetSurface(positions As IReadOnlyList(Of Vector4), indices As IReadOnlyList(Of Integer),
                              Optional material As SurfaceMaterial3D = Nothing)
            Model = Model3DAsset.FromSurface(SurfaceMesh3D.FromXYZW(positions, indices, material))
        End Sub

        Public Overrides Sub Tick(gameTime As GameTime)
            MyBase.Tick(gameTime)
            If AutoRotateSpeed <> Vector3.Zero Then
                Dim seconds As Single = CSng(gameTime.ElapsedGameTime.TotalSeconds)
                RotationXYZ += AutoRotateSpeed * seconds
            End If
        End Sub

        Protected Friend Overrides Sub doDraw(gameTime As GameTime)
            If Model Is Nothing OrElse Model.Meshes.Count = 0 OrElse Size.X < 1 OrElse Size.Y < 1 Then Return
            EnsureGpuResources()
            If gpuSurfaces.Count = 0 Then Return

            Dim device As GraphicsDevice = Scene.graphicsDevice
            Dim oldViewport As Viewport = device.Viewport
            Dim oldBlend As BlendState = device.BlendState
            Dim oldDepth As DepthStencilState = device.DepthStencilState
            Dim oldRasterizer As RasterizerState = device.RasterizerState

            Dim x As Integer = Math.Max(oldViewport.X, CInt(Position.X))
            Dim y As Integer = Math.Max(oldViewport.Y, CInt(Position.Y))
            Dim right As Integer = Math.Min(oldViewport.X + oldViewport.Width, CInt(Position.X + Size.X))
            Dim bottom As Integer = Math.Min(oldViewport.Y + oldViewport.Height, CInt(Position.Y + Size.Y))
            If right <= x OrElse bottom <= y Then Return

            Try
                device.Viewport = New Viewport(x, y, right - x, bottom - y)
                Dim isFullTarget As Boolean = (x = 0 AndAlso y = 0 AndAlso
                                               right = device.PresentationParameters.BackBufferWidth AndAlso
                                               bottom = device.PresentationParameters.BackBufferHeight)
                If ClearBackground AndAlso isFullTarget Then
                    device.Clear(ClearOptions.Target Or ClearOptions.DepthBuffer, BackgroundColor, 1.0F, 0)
                Else
                    device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1.0F, 0)
                End If
                device.BlendState = BlendState.AlphaBlend
                device.DepthStencilState = DepthStencilState.Default
                device.RasterizerState = If(Wireframe, wireframeState, RasterizerState.CullNone)

                Dim view As Matrix = Camera.CreateView()
                Dim projection As Matrix = Camera.CreateProjection(device.Viewport.AspectRatio)
                For Each surface As GpuSurface In gpuSurfaces
                    device.SetVertexBuffer(surface.VertexBuffer)
                    device.Indices = surface.IndexBuffer
                    Dim effect As Effect = If(ShaderEffect, basicShader)
                    ConfigureEffect(effect, surface, view, projection)
                    RaiseEvent ConfigureShader(effect, Me)
                    For Each pass As EffectPass In effect.CurrentTechnique.Passes
                        pass.Apply()
                        device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, surface.PrimitiveCount)
                    Next
                Next
            Finally
                device.SetVertexBuffer(Nothing)
                device.Indices = Nothing
                device.Viewport = oldViewport
                device.BlendState = oldBlend
                device.DepthStencilState = oldDepth
                device.RasterizerState = oldRasterizer
            End Try
        End Sub

        Protected Friend Overrides Sub Draw(gameTime As GameTime)
            ' Surface3DElement owns its complete render pass in doDraw.
        End Sub

        Private Sub EnsureGpuResources()
            If Not gpuResourcesDirty Then Return
            DisposeGpuResources()
            gpuResourcesDirty = False
            If Model Is Nothing Then Return

            Dim device As GraphicsDevice = Scene.graphicsDevice
            basicShader = New BasicEffect(device) With {
                .LightingEnabled = True,
                .VertexColorEnabled = True,
                .PreferPerPixelLighting = True,
                .AmbientLightColor = New Vector3(0.2F)
            }
            basicShader.EnableDefaultLighting()
            wireframeState = New RasterizerState With {
                .CullMode = CullMode.None,
                .FillMode = FillMode.WireFrame
            }

            For Each mesh As SurfaceMesh3D In Model.Meshes
                If mesh.Vertices.Length = 0 OrElse mesh.Indices.Length < 3 Then Continue For
                Dim gpu As New GpuSurface With {
                    .Material = If(mesh.Material, New SurfaceMaterial3D()),
                    .PrimitiveCount = mesh.Indices.Length \ 3,
                    .VertexBuffer = New VertexBuffer(device, GetType(SurfaceVertex), mesh.Vertices.Length, BufferUsage.WriteOnly),
                    .IndexBuffer = New IndexBuffer(device, IndexElementSize.ThirtyTwoBits, mesh.Indices.Length, BufferUsage.WriteOnly)
                }
                gpu.VertexBuffer.SetData(mesh.Vertices)
                gpu.IndexBuffer.SetData(mesh.Indices)
                gpu.Texture = CreateTexture(device, gpu.Material)
                gpuSurfaces.Add(gpu)
            Next
        End Sub

        Private Shared Function CreateTexture(device As GraphicsDevice, material As SurfaceMaterial3D) As Texture2D
            If material.TextureData IsNot Nothing AndAlso material.TextureData.Length > 0 Then
                Using stream As New MemoryStream(material.TextureData, writable:=False)
                    Return Texture2D.FromStream(device, stream)
                End Using
            End If
            If material.TexturePixels IsNot Nothing AndAlso material.TextureWidth > 0 AndAlso material.TextureHeight > 0 Then
                Dim texture As New Texture2D(device, material.TextureWidth, material.TextureHeight)
                texture.SetData(material.TexturePixels)
                Return texture
            End If
            Return Nothing
        End Function

        Private Sub ConfigureEffect(effect As Effect, surface As GpuSurface, view As Matrix, projection As Matrix)
            If TypeOf effect Is BasicEffect Then
                Dim basic As BasicEffect = DirectCast(effect, BasicEffect)
                basic.World = World
                basic.View = view
                basic.Projection = projection
                basic.Texture = surface.Texture
                basic.TextureEnabled = surface.Texture IsNot Nothing
                basic.DiffuseColor = surface.Material.DiffuseColor.ToVector3()
                basic.Alpha = surface.Material.DiffuseColor.A / 255.0F
                Return
            End If

            SetMatrixParameter(effect, "World", World)
            SetMatrixParameter(effect, "View", view)
            SetMatrixParameter(effect, "Projection", projection)
            SetMatrixParameter(effect, "WorldViewProjection", World * view * projection)
            Dim textureParameter As EffectParameter = effect.Parameters("DiffuseTexture")
            If textureParameter IsNot Nothing AndAlso surface.Texture IsNot Nothing Then textureParameter.SetValue(surface.Texture)
        End Sub

        Private Shared Sub SetMatrixParameter(effect As Effect, name As String, value As Matrix)
            Dim parameter As EffectParameter = effect.Parameters(name)
            If parameter IsNot Nothing Then parameter.SetValue(value)
        End Sub

        Private Sub DisposeGpuResources()
            For Each surface As GpuSurface In gpuSurfaces
                surface.Dispose()
            Next
            gpuSurfaces.Clear()
            wireframeState?.Dispose()
            wireframeState = Nothing
            basicShader?.Dispose()
            basicShader = Nothing
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then DisposeGpuResources()
            MyBase.Dispose(disposing)
        End Sub

    End Class

End Namespace
