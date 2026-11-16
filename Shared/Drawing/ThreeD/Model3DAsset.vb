Imports System.Collections.Generic
Imports Microsoft.Xna.Framework
Imports NumericsMatrix = System.Numerics.Matrix4x4
Imports NumericsVector = System.Numerics.Vector3
#If Not PROJECTZ_DESKTOPGL Then
Imports ProjectZ.ShaderEditor
#End If

Namespace [Shared].Drawing.ThreeD

    Public Class SurfaceMaterial3D
        Private shader As ShaderSnapshot

        Public Property Name As String = "Material"
        Public Property DiffuseColor As Color = Color.White
        Public Property TexturePath As String
        Public Property TextureData As Byte()
        Public Property TexturePixels As Color()
        Public Property TextureWidth As Integer
        Public Property TextureHeight As Integer

        ''' <summary>An optional editable Project-Z material graph for DirectX surface hosts.</summary>
        Public Property ShaderGraph As String
            Get
                Return shader?.Graph
            End Get
            Set(value As String)
                If value Is Nothing Then
                    shader = Nothing
                    Return
                End If
#If PROJECTZ_DESKTOPGL Then
                Throw New PlatformNotSupportedException("Surface material shaders require a DirectX host.")
#Else
                Dim document = ShaderDocument.Parse(value)
                Dim source = SurfaceMaterials.GenerateHlsl(document)
                Dim graph = document.ToJson()
                If System.Text.Encoding.UTF8.GetByteCount(graph) > ShaderDocument.MaximumGraphBytes Then
                    Throw New IO.InvalidDataException("The material graph exceeds the 256 KiB transfer limit.")
                End If
                shader = New ShaderSnapshot(graph, source)
#End If
            End Set
        End Property

        ''' <summary>Returns the shader function associated with this material, or Nothing for diffuse rendering.</summary>
        Public Function GenerateShaderSource() As String
            Return shader?.Source
        End Function

        Private NotInheritable Class ShaderSnapshot
            Public ReadOnly Graph As String
            Public ReadOnly Source As String

            Public Sub New(graph As String, source As String)
                Me.Graph = graph
                Me.Source = source
            End Sub
        End Class
    End Class

    Public Class SurfaceMesh3D
        Public Property Name As String = "Surface"
        Public Property Vertices As SurfaceVertex() = Array.Empty(Of SurfaceVertex)()
        Public Property Indices As Integer() = Array.Empty(Of Integer)()
        Public Property Material As SurfaceMaterial3D = New SurfaceMaterial3D()
        Public Property BindVertices As SurfaceVertex()
        Public Property Binding As ModelMeshBinding3D

        Public Function DeformVertices(pose As ModelRigPose3D) As SurfaceVertex()
            If pose Is Nothing OrElse Binding Is Nothing OrElse BindVertices Is Nothing Then Return Vertices
            Dim positions(BindVertices.Length - 1) As NumericsVector, normals(BindVertices.Length - 1) As NumericsVector
            Dim weights(BindVertices.Length - 1) As Single
            For Each bone In Binding.Bones
                Dim skin = bone.Offset * pose.WorldTransforms(bone.Joint)
                Dim normalMatrix = ComputeNormalTransform(skin)
                For Each influence In bone.Weights
                    Dim index = influence.Vertex
                    Dim vertex = BindVertices(index)
                    positions(index) += NumericsVector.Transform(New NumericsVector(vertex.Position.X, vertex.Position.Y, vertex.Position.Z), skin) * influence.Weight
                    normals(index) += NumericsVector.TransformNormal(New NumericsVector(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z), normalMatrix) * influence.Weight
                    weights(index) += influence.Weight
                Next
            Next
            Dim node = pose.WorldTransforms(Binding.Node), nodeNormal = ComputeNormalTransform(node)
            Dim result(BindVertices.Length - 1) As SurfaceVertex
            For index = 0 To result.Length - 1
                Dim vertex = BindVertices(index)
                Dim p = positions(index), n = normals(index)
                If weights(index) > 0.000001F Then
                    p /= weights(index)
                Else
                    p = NumericsVector.Transform(New NumericsVector(vertex.Position.X, vertex.Position.Y, vertex.Position.Z), node)
                    n = NumericsVector.TransformNormal(New NumericsVector(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z), nodeNormal)
                End If
                If n.LengthSquared() > 0.000000001F Then n = NumericsVector.Normalize(n)
                vertex.Position = New Vector4(p.X, p.Y, p.Z, 1)
                vertex.Normal = New Vector3(n.X, n.Y, n.Z)
                result(index) = vertex
            Next
            Return result
        End Function

        Private Shared Function ComputeNormalTransform(transform As NumericsMatrix) As NumericsMatrix
            Dim inverse As NumericsMatrix
            Return If(NumericsMatrix.Invert(transform, inverse), NumericsMatrix.Transpose(inverse), transform)
        End Function

        Public Shared Function FromXYZW(positions As IReadOnlyList(Of Vector4), indices As IReadOnlyList(Of Integer),
                                        Optional material As SurfaceMaterial3D = Nothing) As SurfaceMesh3D
            If positions Is Nothing Then Throw New ArgumentNullException(NameOf(positions))
            If indices Is Nothing Then Throw New ArgumentNullException(NameOf(indices))
            If positions.Count < 3 Then Throw New ArgumentException("A surface needs at least three positions.", NameOf(positions))
            If indices.Count < 3 OrElse indices.Count Mod 3 <> 0 Then
                Throw New ArgumentException("Surface indices must contain complete triangles.", NameOf(indices))
            End If

            Dim vertices(positions.Count - 1) As SurfaceVertex
            For i As Integer = 0 To positions.Count - 1
                vertices(i) = New SurfaceVertex(positions(i), Vector3.Zero, Vector2.Zero, Color.White)
            Next

            Dim indexArray(indices.Count - 1) As Integer
            For i As Integer = 0 To indices.Count - 1
                If indices(i) < 0 OrElse indices(i) >= positions.Count Then
                    Throw New ArgumentOutOfRangeException(NameOf(indices), "A surface index is outside the position array.")
                End If
                indexArray(i) = indices(i)
            Next

            GenerateNormals(vertices, indexArray)
            Return New SurfaceMesh3D With {
                .Vertices = vertices,
                .Indices = indexArray,
                .Material = If(material, New SurfaceMaterial3D())
            }
        End Function

        Public Shared Function CreateCube(Optional size As Single = 1.0F) As SurfaceMesh3D
            Dim h As Single = size / 2.0F
            Dim positions As Vector4() = {
                New Vector4(-h, -h, -h, 1), New Vector4(h, -h, -h, 1),
                New Vector4(h, h, -h, 1), New Vector4(-h, h, -h, 1),
                New Vector4(-h, -h, h, 1), New Vector4(h, -h, h, 1),
                New Vector4(h, h, h, 1), New Vector4(-h, h, h, 1)
            }
            Dim indices As Integer() = {
                0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5,
                3, 7, 6, 3, 6, 2, 0, 1, 5, 0, 5, 4
            }
            Return FromXYZW(positions, indices)
        End Function

        Friend Shared Sub GenerateNormals(vertices As SurfaceVertex(), indices As Integer())
            Dim accumulated(vertices.Length - 1) As Vector3
            For i As Integer = 0 To indices.Length - 1 Step 3
                Dim a As Integer = indices(i)
                Dim b As Integer = indices(i + 1)
                Dim c As Integer = indices(i + 2)
                Dim pa As New Vector3(vertices(a).Position.X, vertices(a).Position.Y, vertices(a).Position.Z)
                Dim pb As New Vector3(vertices(b).Position.X, vertices(b).Position.Y, vertices(b).Position.Z)
                Dim pc As New Vector3(vertices(c).Position.X, vertices(c).Position.Y, vertices(c).Position.Z)
                Dim normal As Vector3 = Vector3.Cross(pb - pa, pc - pa)
                If normal.LengthSquared() > 0.0000001F Then
                    accumulated(a) += normal
                    accumulated(b) += normal
                    accumulated(c) += normal
                End If
            Next
            For i As Integer = 0 To vertices.Length - 1
                If accumulated(i).LengthSquared() > 0.0000001F Then accumulated(i).Normalize()
                vertices(i).Normal = accumulated(i)
            Next
        End Sub
    End Class

    Public Class Model3DAsset
        Public Property SourcePath As String
        Public Property Rig As ModelRig3D
        Public ReadOnly Property Meshes As New List(Of SurfaceMesh3D)()

        Public Shared Function FromSurface(surface As SurfaceMesh3D) As Model3DAsset
            If surface Is Nothing Then Throw New ArgumentNullException(NameOf(surface))
            Dim model As New Model3DAsset()
            model.Meshes.Add(surface)
            Return model
        End Function
    End Class

End Namespace
