Imports System.Runtime.InteropServices
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].Drawing.ThreeD

    ''' <summary>A textured 3D vertex with an explicit XYZW position.</summary>
    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Public Structure SurfaceVertex
        Implements IVertexType

        Public Position As Vector4
        Public Normal As Vector3
        Public TextureCoordinate As Vector2
        Public Color As Color

        Private Shared ReadOnly declarationValue As New VertexDeclaration(
            New VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.Position, 0),
            New VertexElement(16, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            New VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            New VertexElement(36, VertexElementFormat.Color, VertexElementUsage.Color, 0))

        Public ReadOnly Property VertexDeclaration As VertexDeclaration Implements IVertexType.VertexDeclaration
            Get
                Return declarationValue
            End Get
        End Property

        Public Sub New(position As Vector4, normal As Vector3, textureCoordinate As Vector2, color As Color)
            Me.Position = position
            Me.Normal = normal
            Me.TextureCoordinate = textureCoordinate
            Me.Color = color
        End Sub

    End Structure

End Namespace
