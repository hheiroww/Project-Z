Imports Microsoft.Xna.Framework

Namespace [Shared].Physics
    ''' <summary>Convenience coefficients, not a physical material database.</summary>
    Public Enum FrictionType
        Custom
        Geometry
        Ice
        Water
        Snow
        Sand
        Rock
        Sandpaper
    End Enum

    ''' <summary>An opt-in axis-aligned body. Position is the minimum corner in scene/world units.</summary>
    Public NotInheritable Class PhysicsBody
        Public Property Position As Vector3
        Public Property Velocity As Vector3
        Public Property Size As Vector3 = Vector3.One
        Public Property Parent As PhysicsBody
        Public Property Is3D As Boolean
        Public Property IsStatic As Boolean
        Public Property IsKinematic As Boolean
        Public Property CollisionEnabled As Boolean
        Public Property GravityEnabled As Boolean
        Public Property Gravity As Vector3 = New Vector3(0, 980, 0)
        Public Property LinearDamping As Double
        Public Property Tag As Object
        Public ReadOnly Property GeometryVertices As IList(Of Vector3) = New List(Of Vector3)()
        Friend Force As Vector3
        Private massValue As Double = 1
        Private bounceValue As Double
        Private frictionValue As Double = 0.5
        Private frictionKind As FrictionType

        Public Property Mass As Double
            Get
                Return massValue
            End Get
            Set(value As Double)
                massValue = FiniteClamp(value, 0.0001, 100000000, 1)
            End Set
        End Property
        Public Property Weight As Double
            Get
                Return Mass
            End Get
            Set(value As Double)
                Mass = value
            End Set
        End Property
        Public Property Bounce As Double
            Get
                Return bounceValue
            End Get
            Set(value As Double)
                bounceValue = FiniteClamp(value, 0, 1, 0)
            End Set
        End Property
        Public Property Friction As Double
            Get
                Return frictionValue
            End Get
            Set(value As Double)
                frictionValue = FiniteClamp(value, 0, 4, 0.5)
                frictionKind = FrictionType.Custom
            End Set
        End Property
        Public Property FrictionType As FrictionType
            Get
                Return frictionKind
            End Get
            Set(value As FrictionType)
                frictionKind = If([Enum].IsDefined(GetType(FrictionType), value), value, FrictionType.Custom)
                Select Case frictionKind
                    Case FrictionType.Ice : frictionValue = 0.02
                    Case FrictionType.Water : frictionValue = 0.1
                    Case FrictionType.Snow : frictionValue = 0.3
                    Case FrictionType.Sand : frictionValue = 0.8
                    Case FrictionType.Rock : frictionValue = 0.65
                    Case FrictionType.Sandpaper : frictionValue = 1.2
                End Select
            End Set
        End Property

        ''' <summary>
        ''' Geometry models roughness from ordered contour compactness. A long/jagged contour
        ''' has greater friction than a compact contour. It does not infer real material chemistry
        ''' or perform mesh contact. Missing/degenerate contours use the body rectangle or dominant cuboid face.
        ''' For 3D, provide an ordered contour projected onto the contact's dominant plane.
        ''' </summary>
        Public Function GetFriction(Optional normal As Vector3 = Nothing) As Double
            If frictionKind <> FrictionType.Geometry Then Return frictionValue
            If GeometryVertices.Count < 3 Then Return BoxGeometryFriction(normal)
            Dim axis = 2
            If Is3D Then
                Dim n = New Vector3(Math.Abs(normal.X), Math.Abs(normal.Y), Math.Abs(normal.Z))
                axis = If(n.X >= n.Y AndAlso n.X >= n.Z, 0, If(n.Y >= n.Z, 1, 2))
            End If
            Dim perimeter As Double = 0, twiceArea As Double = 0
            For i = 0 To GeometryVertices.Count - 1
                Dim a = GeometryVertices(i), b = GeometryVertices((i + 1) Mod GeometryVertices.Count)
                If Not IsFinite(a) OrElse Not IsFinite(b) Then Return BoxGeometryFriction(normal)
                Dim ax = If(axis = 0, CDbl(a.Y), CDbl(a.X))
                Dim ay = If(axis = 2, CDbl(a.Y), CDbl(a.Z))
                Dim bx = If(axis = 0, CDbl(b.Y), CDbl(b.X))
                Dim by = If(axis = 2, CDbl(b.Y), CDbl(b.Z))
                perimeter += Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay))
                twiceArea += ax * by - bx * ay
            Next
            If perimeter <= 0.000001 OrElse Math.Abs(twiceArea) <= 0.000001 Then Return BoxGeometryFriction(normal)
            Dim compactness = Math.Min(1, Math.Sqrt(2 * Math.PI * Math.Abs(twiceArea)) / perimeter)
            Return Math.Clamp(1.5 * (1 - compactness) + 0.02, 0.02, 1.5)
        End Function

        Private Function BoxGeometryFriction(normal As Vector3) As Double
            Dim width = CDbl(Size.X), height = CDbl(Size.Y)
            If Is3D Then
                Dim n = New Vector3(Math.Abs(normal.X), Math.Abs(normal.Y), Math.Abs(normal.Z))
                If n.X >= n.Y AndAlso n.X >= n.Z Then
                    width = Size.Y
                    height = Size.Z
                ElseIf n.Y >= n.Z Then
                    height = Size.Z
                End If
            End If
            If Not Double.IsFinite(width) OrElse Not Double.IsFinite(height) OrElse width <= 0 OrElse height <= 0 Then Return frictionValue
            Dim compactness = Math.Min(1, Math.Sqrt(Math.PI * width * height) / (width + height))
            Return Math.Clamp(1.5 * (1 - compactness) + 0.02, 0.02, 1.5)
        End Function
        Public Sub AddImpulse(impulse As Vector3)
            If IsStatic OrElse IsKinematic OrElse Not IsFinite(impulse) Then Return
            Velocity += impulse * CSng(1 / Mass)
        End Sub
        Public Sub AddForce(value As Vector3)
            If IsStatic OrElse IsKinematic OrElse Not IsFinite(value) Then Return
            Force += value
        End Sub
        Public Sub [Stop]()
            Velocity = Vector3.Zero
            Force = Vector3.Zero
        End Sub
        Friend ReadOnly Property InverseMass As Single
            Get
                Return If(IsStatic OrElse IsKinematic, 0.0F, CSng(1 / Mass))
            End Get
        End Property
        Friend Sub Sanitize(maximumSpeed As Single)
            If Not IsFinite(Position) Then Position = Vector3.Zero
            If Not IsFinite(Size) Then Size = Vector3.One
            Size = Vector3.Max(Vector3.Zero, Vector3.Min(Size, New Vector3(10000000)))
            If Not IsFinite(Velocity) Then Velocity = Vector3.Zero
            If Not IsFinite(Gravity) Then Gravity = Vector3.Zero
            If Not IsFinite(Force) Then Force = Vector3.Zero
            If Not Is3D Then
                Position = New Vector3(Position.X, Position.Y, 0)
                Velocity = New Vector3(Velocity.X, Velocity.Y, 0)
                Size = New Vector3(Size.X, Size.Y, 0)
            End If
            Dim squared = CDbl(Velocity.X) * Velocity.X + CDbl(Velocity.Y) * Velocity.Y + CDbl(Velocity.Z) * Velocity.Z
            If squared > CDbl(maximumSpeed) * maximumSpeed Then Velocity *= CSng(maximumSpeed / Math.Sqrt(squared))
            If IsStatic Then Velocity = Vector3.Zero
        End Sub
        Friend Shared Function IsFinite(value As Vector3) As Boolean
            Return Single.IsFinite(value.X) AndAlso Single.IsFinite(value.Y) AndAlso Single.IsFinite(value.Z)
        End Function
        Friend Shared Function FiniteClamp(value As Double, minimum As Double, maximum As Double, fallback As Double) As Double
            Return If(Double.IsFinite(value), Math.Clamp(value, minimum, maximum), fallback)
        End Function
    End Class
End Namespace
