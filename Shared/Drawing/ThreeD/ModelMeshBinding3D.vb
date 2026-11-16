Imports System.Numerics

Namespace [Shared].Drawing.ThreeD
    Public Structure ModelSkinWeight3D
        Public ReadOnly Vertex As Integer
        Public ReadOnly Weight As Single

        Public Sub New(vertex As Integer, weight As Single)
            Me.Vertex = vertex
            Me.Weight = weight
        End Sub
    End Structure

    Public NotInheritable Class ModelBone3D
        Public ReadOnly Property Joint As Integer
        Public ReadOnly Property Offset As Matrix4x4
        Public ReadOnly Property Weights As ModelSkinWeight3D()

        Public Sub New(joint As Integer, offset As Matrix4x4, weights As ModelSkinWeight3D())
            Me.Joint = joint
            Me.Offset = offset
            Me.Weights = weights
        End Sub
    End Class

    Public NotInheritable Class ModelMeshBinding3D
        Public ReadOnly Property Node As Integer
        Public ReadOnly Property Bones As ModelBone3D()

        Public Sub New(node As Integer, bones As ModelBone3D())
            Me.Node = node
            Me.Bones = bones
        End Sub
    End Class
End Namespace
