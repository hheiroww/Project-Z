Imports System.Collections.Generic

Namespace [Shared].Drawing.UI.Advanced

    ''' <summary>A vertex produced by Project Z polygon triangulation.</summary>
    Public NotInheritable Class PolygonVertex

        Public ReadOnly Property X As Double
        Public ReadOnly Property Y As Double

        Friend Sub New(x As Double, y As Double)
            Me.X = x
            Me.Y = y
        End Sub

    End Class

    ''' <summary>A triangle produced by Project Z polygon triangulation.</summary>
    Public NotInheritable Class PolygonTriangle

        Private ReadOnly vertices As PolygonVertex()

        Friend Sub New(a As PolygonVertex, b As PolygonVertex, c As PolygonVertex)
            vertices = {a, b, c}
        End Sub

        Public Function GetVertex(index As Integer) As PolygonVertex
            If index < 0 OrElse index >= vertices.Length Then
                Throw New ArgumentOutOfRangeException(NameOf(index))
            End If
            Return vertices(index)
        End Function

    End Class

    ''' <summary>A dependency-neutral triangle mesh used by PolygonElement.</summary>
    Public NotInheritable Class PolygonMesh

        Private ReadOnly triangleList As IReadOnlyList(Of PolygonTriangle)

        Public ReadOnly Property Triangles As IReadOnlyList(Of PolygonTriangle)
            Get
                Return triangleList
            End Get
        End Property

        Friend Sub New(triangles As IEnumerable(Of PolygonTriangle))
            triangleList = New List(Of PolygonTriangle)(triangles).AsReadOnly()
        End Sub

    End Class

End Namespace
