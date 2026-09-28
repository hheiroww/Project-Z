Imports System.IO
Imports Microsoft.Xna.Framework
Imports ProjectZ.ModelImport

Namespace [Shared].Drawing.ThreeD

    Public NotInheritable Class ModelImporter3D

        Private Shared ReadOnly supportedExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ".obj", ".fbx"
        }

        Private Sub New()
        End Sub

        Public Shared ReadOnly Property SupportedFileExtensions As IReadOnlyCollection(Of String)
            Get
                Return supportedExtensions
            End Get
        End Property

        Public Shared Function Load(path As String) As Model3DAsset
            If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("A model path is required.", NameOf(path))
            Dim fullPath As String = IO.Path.GetFullPath(path)
            If Not File.Exists(fullPath) Then Throw New FileNotFoundException("3D model file was not found.", fullPath)
            If Not supportedExtensions.Contains(IO.Path.GetExtension(fullPath)) Then
                Throw New NotSupportedException("Project Z currently imports OBJ and FBX model files.")
            End If

            Dim imported As ImportedModel = AssimpModelBridge.Load(fullPath)
            Dim asset As New Model3DAsset With {.SourcePath = fullPath}
            Dim materials As SurfaceMaterial3D() = LoadMaterials(imported, fullPath)

            For Each source As ImportedMesh In imported.Meshes
                Dim vertices(source.Positions.Length - 1) As SurfaceVertex
                For i As Integer = 0 To vertices.Length - 1
                    Dim position = source.Positions(i)
                    Dim normal = source.Normals(i)
                    Dim uv = source.TextureCoordinates(i)
                    vertices(i) = New SurfaceVertex(
                        New Vector4(position.X, position.Y, position.Z, 1.0F),
                        New Vector3(normal.X, normal.Y, normal.Z),
                        New Vector2(uv.X, uv.Y),
                        Color.White)
                Next

                Dim material As New SurfaceMaterial3D()
                If source.MaterialIndex >= 0 AndAlso source.MaterialIndex < materials.Length Then
                    material = materials(source.MaterialIndex)
                End If
                asset.Meshes.Add(New SurfaceMesh3D With {
                    .Name = source.Name,
                    .Vertices = vertices,
                    .Indices = source.Indices,
                    .Material = material
                })
            Next
            Return asset
        End Function

        Public Shared Function LoadAsync(path As String, Optional cancellationToken As Threading.CancellationToken = Nothing) As Task(Of Model3DAsset)
            Return Task.Run(Function()
                                cancellationToken.ThrowIfCancellationRequested()
                                Dim result As Model3DAsset = Load(path)
                                cancellationToken.ThrowIfCancellationRequested()
                                Return result
                            End Function, cancellationToken)
        End Function

        Private Shared Function LoadMaterials(imported As ImportedModel, modelPath As String) As SurfaceMaterial3D()
            If imported.Materials.Length = 0 Then Return Array.Empty(Of SurfaceMaterial3D)()
            Dim result(imported.Materials.Length - 1) As SurfaceMaterial3D
            Dim modelDirectory As String = IO.Path.GetDirectoryName(modelPath)

            For i As Integer = 0 To imported.Materials.Length - 1
                Dim source As ImportedMaterial = imported.Materials(i)
                Dim color = source.DiffuseColor
                Dim material As New SurfaceMaterial3D With {
                    .Name = source.Name,
                    .DiffuseColor = New Color(Clamp01(color.X), Clamp01(color.Y), Clamp01(color.Z), Clamp01(color.W)),
                    .TexturePath = source.TextureReference,
                    .TextureData = source.EmbeddedTextureData,
                    .TextureWidth = source.EmbeddedTextureWidth,
                    .TextureHeight = source.EmbeddedTextureHeight
                }

                If source.EmbeddedTextureRgba IsNot Nothing Then
                    Dim pixels(source.EmbeddedTextureWidth * source.EmbeddedTextureHeight - 1) As Color
                    For pixelIndex As Integer = 0 To pixels.Length - 1
                        Dim offset As Integer = pixelIndex * 4
                        pixels(pixelIndex) = New Color(source.EmbeddedTextureRgba(offset),
                                                       source.EmbeddedTextureRgba(offset + 1),
                                                       source.EmbeddedTextureRgba(offset + 2),
                                                       source.EmbeddedTextureRgba(offset + 3))
                    Next
                    material.TexturePixels = pixels
                ElseIf Not String.IsNullOrWhiteSpace(source.TextureReference) AndAlso
                       Not source.TextureReference.StartsWith("*", StringComparison.Ordinal) Then
                    Dim normalizedReference As String = source.TextureReference.Replace("/"c, IO.Path.DirectorySeparatorChar)
                    Dim texturePath As String = If(IO.Path.IsPathRooted(normalizedReference),
                                                   normalizedReference,
                                                   IO.Path.Combine(modelDirectory, normalizedReference))
                    If File.Exists(texturePath) Then
                        material.TexturePath = IO.Path.GetFullPath(texturePath)
                        material.TextureData = File.ReadAllBytes(texturePath)
                    End If
                End If
                result(i) = material
            Next
            Return result
        End Function

        Private Shared Function Clamp01(value As Single) As Single
            Return Math.Max(0.0F, Math.Min(1.0F, value))
        End Function

    End Class

End Namespace
