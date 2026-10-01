
#Region "File Description"
'-----------------------------------------------------------------------------
' PrimitiveBatch.cs
'
' Microsoft XNA Community Game Platform
' Copyright (C) Microsoft Corporation. All rights reserved.
'-----------------------------------------------------------------------------
#End Region

#Region "Using Statements"
Imports System.Collections.Generic
Imports System.Text
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports Microsoft.Xna.Framework.Content
Imports Microsoft.Xna.Framework.Input
#End Region

Namespace [Shared].Drawing.UI.Primitives

    ' PrimitiveBatch is a class that handles efficient rendering automatically for its
    ' users, in a similar way to SpriteBatch. PrimitiveBatch can render lines, points,
    ' and triangles to the screen.
    Public Class PrimitiveBatch
        Implements IDisposable

#Region "Constants and Fields"

        ' this constant controls how large the vertices buffer is. Larger buffers will
        ' require flushing less often, which can increase performance. However, having
        ' buffer that is unnecessarily large will waste memory.
        Private DefaultBufferSize As Integer = 500

        ' a block of vertices that calling AddVertex will fill. Flush will draw using
        ' this array, and will determine how many primitives to draw from
        ' positionInBuffer.
        Private vertices As VertexPositionColor() = New VertexPositionColor(DefaultBufferSize - 1) {}

        ' keeps track of how many vertices have been added. this value increases until
        ' we run out of space in the buffer, at which time Flush is automatically
        ' called.
        Private positionInBuffer As Integer = 0

        ' a basic effect, which contains the shaders that we will use to draw our
        ' primitives.
        Private basicEffect As BasicEffect

        ' the device that we will issue draw calls to.
        Private device As GraphicsDevice

        ' this value is set by Begin, and is the type of primitives that we are
        ' drawing.
        Private primitiveType As PrimitiveType

        ' how many verts does each of these primitives take up? points are 1,
        ' lines are 2, and triangles are 3.
        Private numVertsPerPrimitiveInt As Integer

        ' hasBegun is flipped to true once Begin is called, and is used to make
        ' sure users don't call End before Begin is called.
        Private hasBegun As Boolean = False

        Private isDisposed As Boolean = False

#End Region

#Region "Properties"

        Public Property Texture As Texture2D
            Get
                Return basicEffect.Texture
            End Get
            Set(value As Texture2D)
                basicEffect.Texture = value
            End Set
        End Property

        Public Property Color As Color
            Get
                Return _Color
            End Get
            Set(value As Color)
                _Color = value
                For Each Vector As VertexPositionColor In vertices
                    If Vector = Nothing Then
                        Exit For
                    End If
                    Vector.Color = _Color
                Next
            End Set
        End Property
        Private _Color As Color

#End Region

        ' the constructor creates a new PrimitiveBatch and sets up all of the internals
        ' that PrimitiveBatch will need.
        Public Sub New(graphicsDevice As GraphicsDevice, Texture As Texture2D, Color As Color, DefaultBufferSize As Integer)
            If graphicsDevice Is Nothing Then
                Throw New ArgumentNullException("graphicsDevice")
            End If
            device = graphicsDevice

            ' set up a new basic effect, and enable vertex colors.
            basicEffect = New BasicEffect(graphicsDevice)
            basicEffect.VertexColorEnabled = True
            Me.DefaultBufferSize = DefaultBufferSize
            Me.Texture = Texture
            Me.Color = Color
            basicEffect.Texture = Me.Texture

            ' projection uses CreateOrthographicOffCenter to create 2d projection
            ' matrix with 0,0 in the upper left.
            basicEffect.Projection = Matrix.CreateOrthographicOffCenter(0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height, 0, 0, 1)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Me.Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub

        Protected Overridable Sub Dispose(disposing As Boolean)
            If disposing AndAlso Not isDisposed Then
                If basicEffect IsNot Nothing Then
                    basicEffect.Dispose()
                End If

                isDisposed = True
            End If
        End Sub

        ' Begin is called to tell the PrimitiveBatch what kind of primitives will be
        ' drawn, and to prepare the graphics card to render those primitives.
        Public Sub Begin(primitiveType__1 As PrimitiveType)
            Dim scale = GraphicsQuality.ForDevice(device).RenderScale
            basicEffect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width / scale.X, device.Viewport.Height / scale.Y, 0, 0, 1)
            If hasBegun Then
                Throw New InvalidOperationException("End must be called before Begin can be called again.")
            End If

            ' these three types reuse vertices, so we can't flush properly without more
            ' complex logic. Since that's a bit too complicated for this sample, we'll
            ' simply disallow them.
            If primitiveType__1 = primitiveType.LineStrip OrElse primitiveType__1 = primitiveType.TriangleStrip Then
                Throw New NotSupportedException("The specified primitiveType is not supported by PrimitiveBatch.")
            End If

            Me.primitiveType = primitiveType__1

            ' how many verts will each of these primitives require?
            Me.numVertsPerPrimitiveInt = NumVertsPerPrimitive(primitiveType__1)

            'tell our basic effect to begin.
            basicEffect.CurrentTechnique.Passes(0).Apply()

            ' flip the error checking boolean. It's now ok to call AddVertex, Flush,
            ' and End.
            hasBegun = True
        End Sub

        ' AddVertex is called to add another vertex to be rendered. To draw a point,
        ' AddVertex must be called once. for lines, twice, and for triangles 3 times.
        ' this function can only be called once begin has been called.
        ' if there is not enough room in the vertices buffer, Flush is called
        ' automatically.
        Public Sub AddVertex(vertex As Vector2)
            If Not hasBegun Then
                Throw New InvalidOperationException("Begin must be called before AddVertex can be called.")
            End If

            ' are we starting a new primitive? if so, and there will not be enough room
            ' for a whole primitive, flush.
            Dim newPrimitive As Boolean = ((positionInBuffer Mod numVertsPerPrimitiveInt) = 0)

            If newPrimitive AndAlso (positionInBuffer + numVertsPerPrimitiveInt) >= vertices.Length Then
                Flush()
            End If

            ' once we know there's enough room, set the vertex in the buffer,
            ' and increase position.
            vertices(positionInBuffer).Position = New Vector3(vertex, 0)
            vertices(positionInBuffer).Color = Color

            positionInBuffer += 1
        End Sub

        ' End is called once all the primitives have been drawn using AddVertex.
        ' it will call Flush to actually submit the draw call to the graphics card, and
        ' then tell the basic effect to end.
        Public Sub [End]()
            If Not hasBegun Then
                Throw New InvalidOperationException("Begin must be called before End can be called.")
            End If

            ' Draw whatever the user wanted us to draw
            Flush()

            hasBegun = False
        End Sub

        ' Flush is called to issue the draw call to the graphics card. Once the draw
        ' call is made, positionInBuffer is reset, so that AddVertex can start over
        ' at the beginning. End will call this to draw the primitives that the user
        ' requested, and AddVertex will call this if there is not enough room in the
        ' buffer.
        Private Sub Flush()
            If Not hasBegun Then
                Throw New InvalidOperationException("Begin must be called before Flush can be called.")
            End If

            ' no work to do
            If positionInBuffer = 0 Then
                Return
            End If

            ' how many primitives will we draw?
            Dim primitiveCount As Integer = positionInBuffer \ numVertsPerPrimitiveInt

            ' submit the draw call to the graphics card
            device.DrawUserPrimitives(Of VertexPositionColor)(primitiveType, vertices, 0, primitiveCount)

            ' now that we've drawn, it's ok to reset positionInBuffer back to zero,
            ' and write over any vertices that may have been set previously.
            positionInBuffer = 0
        End Sub

#Region "Helper functions"

        ' NumVertsPerPrimitive is a boring helper function that tells how many vertices
        ' it will take to draw each kind of primitive.
        Private Shared Function NumVertsPerPrimitive(primitive As PrimitiveType) As Integer
            Dim numVertsPerPrimitive__1 As Integer
            Select Case primitive
                Case primitiveType.LineList
                    numVertsPerPrimitive__1 = 2
                    Exit Select
                Case primitiveType.TriangleList
                    numVertsPerPrimitive__1 = 3
                    Exit Select
                Case Else
                    Throw New InvalidOperationException("primitive is not valid")
            End Select
            Return numVertsPerPrimitive__1
        End Function

#End Region

    End Class

End Namespace
