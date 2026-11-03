Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Physics
Imports ProjectZ.Shared.Animations.Properties

Namespace [Shared].Drawing.UI
    Public Enum PhysicsMotion
        RenderTranslation
        Position
    End Enum

    Public MustInherit Partial Class SceneElement
        Private physicsBodyValue As PhysicsBody
        Private physicsDragging As Boolean
        Private physicsDragPoint As Vector2
        Private physicsDragTime As Long
        Private physicsLastMoveTime As Long

        ''' <summary>Physics writes the existing visual translation by default, preserving layout.</summary>
        Public Property PhysicsMotion As PhysicsMotion = PhysicsMotion.RenderTranslation
        Public Property RigidBody As Boolean
        ''' <summary>Compatibility alias for RigidBody.</summary>
        Public Property RidgidBody As Boolean
            Get
                Return RigidBody
            End Get
            Set(value As Boolean)
                RigidBody = value
            End Set
        End Property
        Public Property FreeMove As Boolean
        Public Property CollisionEnabled As Boolean
        Public Property Mass As Double
            Get
                Return PhysicsBody.Mass
            End Get
            Set(value As Double)
                PhysicsBody.Mass = value
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
                Return PhysicsBody.Bounce
            End Get
            Set(value As Double)
                PhysicsBody.Bounce = value
            End Set
        End Property
        Public Property Friction As Double
            Get
                Return PhysicsBody.Friction
            End Get
            Set(value As Double)
                PhysicsBody.Friction = value
            End Set
        End Property
        Public Property FrictionType As FrictionType
            Get
                Return PhysicsBody.FrictionType
            End Get
            Set(value As FrictionType)
                PhysicsBody.FrictionType = value
            End Set
        End Property
        Public Property Gravity As Vector2 = New Vector2(0, 980)
        Public Property Velocity As Vector2
            Get
                Dim value = PhysicsBody.Velocity
                Return New Vector2(value.X, value.Y)
            End Get
            Set(value As Vector2)
                PhysicsBody.Velocity = New Vector3(value, 0)
            End Set
        End Property
        Public ReadOnly Property PhysicsBody As PhysicsBody
            Get
                If physicsBodyValue Is Nothing Then physicsBodyValue = New PhysicsBody()
                Return physicsBodyValue
            End Get
        End Property

        Public Sub ApplyPhysicsImpulse(impulse As Vector2)
            If Not RigidBody Then Return
            ReadPhysicsState()
            PhysicsBody.AddImpulse(New Vector3(impulse, 0))
        End Sub

        ''' <summary>Drive an existing animation property with a scalar spring/inertia controller.</summary>
        Public Function DriveWithPhysics(target As ElementProperty) As PhysicsScalar
            If target Is Nothing Then Throw New ArgumentNullException(NameOf(target))
            If target.TargetElement IsNot Me Then Throw New ArgumentException("The animation property must belong to this element.", NameOf(target))
            Dim driver As New PhysicsScalar(Function() Convert.ToDouble(target.GetValue(), Globalization.CultureInfo.InvariantCulture),
                                           Sub(value) target.SetValue(value))
            PhysicsDrivers.Add(driver)
            Return driver
        End Function
        Public ReadOnly Property PhysicsDrivers As New List(Of PhysicsScalar)()

        Friend ReadOnly Property ParticipatesInPhysics As Boolean
            Get
                Return Not IsDisposed AndAlso isVisible AndAlso (RigidBody OrElse CollisionEnabled OrElse FreeMove)
            End Get
        End Property

        Friend Overridable Sub ReadPhysicsState()
            Dim body = PhysicsBody
            body.Is3D = False
            body.IsStatic = Not RigidBody AndAlso Not physicsDragging
            body.IsKinematic = physicsDragging
            body.GravityEnabled = RigidBody
            body.Gravity = New Vector3(Gravity, 0)
            body.CollisionEnabled = CollisionEnabled
            Dim size As Vector2
            body.Position = New Vector3(PhysicsVisualPosition(size), 0)
            body.Size = New Vector3(size, 0)
            ' Preserve sibling identity even for a layout-only, noncollidable container.
            body.Parent = If(Parent IsNot Nothing, Parent.PhysicsBody, Nothing)
            If Parent IsNot Nothing Then Parent.PhysicsBody.CollisionEnabled = Parent.CollisionEnabled AndAlso Parent.ParticipatesInPhysics
            If body.FrictionType = FrictionType.Geometry Then
                Dim polygon = TryCast(Me, Advanced.PolygonElement)
                If polygon IsNot Nothing Then
                    body.GeometryVertices.Clear()
                    For Each point In polygon.Vectors
                        body.GeometryVertices.Add(New Vector3(point, 0))
                    Next
                End If
            End If
            If physicsDragging AndAlso (Not FreeMove OrElse Not isEnabled OrElse Not isMouseDown OrElse Scene?.CapturedPointerElement IsNot Me) Then EndFreeMove(True)
        End Sub

        Friend Overridable Sub WritePhysicsState()
            Dim size As Vector2
            Dim current = PhysicsVisualPosition(size)
            ApplyPhysicsTranslation(New Vector2(PhysicsBody.Position.X, PhysicsBody.Position.Y) - current)
        End Sub

        Private Function PhysicsVisualPosition(ByRef size As Vector2) As Vector2
            size = Me.Size * RenderScale
            Dim pivot = Position + Me.Size * RenderScaleOrigin + RenderScaleCenter
            Dim point = Position + RenderTranslation + (pivot - Position) * (Vector2.One - RenderScale)
            Dim ancestor = Parent
            While ancestor IsNot Nothing
                Dim center = ancestor.Position + ancestor.Size * ancestor.RenderScaleOrigin + ancestor.RenderScaleCenter
                point = center + (point - center) * ancestor.RenderScale + ancestor.RenderTranslation
                size *= ancestor.RenderScale
                ancestor = ancestor.Parent
            End While
            Return point
        End Function

        Private Sub ApplyPhysicsTranslation(delta As Vector2)
            If Not Single.IsFinite(delta.X) OrElse Not Single.IsFinite(delta.Y) OrElse delta = Vector2.Zero Then Return
            Dim ancestor = Parent
            While ancestor IsNot Nothing
                delta /= ancestor.RenderScale
                ancestor = ancestor.Parent
            End While
            If PhysicsMotion = PhysicsMotion.Position Then
                Position += delta
            Else
                RenderTranslation += delta
            End If
            Scene?.RequestRender()
        End Sub

        Private Sub BeginFreeMove(point As Point) Handles Me.MouseLeftDown
            If Not FreeMove OrElse Scene Is Nothing OrElse Not isEnabled Then Return
            physicsDragging = True
            physicsDragPoint = New Vector2(point.X, point.Y) + Position
            physicsDragTime = Diagnostics.Stopwatch.GetTimestamp()
            physicsLastMoveTime = 0
            PhysicsBody.Stop()
            Scene.CapturePointer(Me)
        End Sub

        Private Sub DragFreeMove(current As Point, origin As Point) Handles Me.MouseDrag
            If Not physicsDragging Then Return
            Dim pointer = New Vector2(current.X, current.Y) + Position
            Dim delta = pointer - physicsDragPoint
            ReadPhysicsState()
            Dim before = PhysicsBody.Position
            Dim now = Diagnostics.Stopwatch.GetTimestamp()
            Dim elapsed = Math.Max(1.0 / 240, Diagnostics.Stopwatch.GetElapsedTime(physicsDragTime, now).TotalSeconds)
            MoveFreePhysics(delta, elapsed)
            Dim after = PhysicsBody.Position
            Dim sample = (after - before) / CSng(elapsed)
            Dim maximum = Scene.PhysicsWorld.MaximumSpeed
            If Not Single.IsFinite(maximum) Then maximum = 10000
            maximum = Math.Clamp(maximum, 1, 1000000)
            If sample.LengthSquared() > maximum * maximum Then sample = Vector3.Normalize(sample) * maximum
            PhysicsBody.Velocity = sample
            physicsDragPoint = pointer
            physicsDragTime = now
            physicsLastMoveTime = now
        End Sub

        Protected Overridable Sub MoveFreePhysics(delta As Vector2, seconds As Double)
            Scene.MovePhysicsElement(Me, PhysicsBody.Position + New Vector3(delta, 0), seconds)
        End Sub

        Private Sub ReleaseFreeMove(point As Point) Handles Me.MouseLeftUp
            EndFreeMove((point.X = -1 AndAlso point.Y = -1) OrElse Scene?.CapturedPointerElement IsNot Me)
        End Sub

        Friend Sub EndFreeMove(cancelled As Boolean)
            If Not physicsDragging Then Return
            physicsDragging = False
            PhysicsBody.IsKinematic = False
            If cancelled OrElse Not RigidBody OrElse physicsLastMoveTime = 0 OrElse Diagnostics.Stopwatch.GetElapsedTime(physicsLastMoveTime).TotalSeconds > 0.1 Then PhysicsBody.Stop()
            Scene?.ReleasePointerCapture(Me)
        End Sub
    End Class
End Namespace
