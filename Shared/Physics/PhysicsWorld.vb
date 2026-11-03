Imports Microsoft.Xna.Framework

Namespace [Shared].Physics
    ''' <summary>
    ''' Fixed-step, bounded CPU contact solver for 2D/3D AABBs. It deliberately does not
    ''' read GPU geometry back to the CPU. Presentation remains with the existing renderer.
    ''' Bodies collide with siblings in the same dimension. A collidable parent contains
    ''' its collidable children; it is not also treated as a solid enclosing obstacle.
    ''' </summary>
    Public NotInheritable Class PhysicsWorld
        Public ReadOnly Property Bodies As IList(Of PhysicsBody) = New List(Of PhysicsBody)()
        Public Property FixedTimeStep As Double = 1.0 / 120
        Public Property MaximumFrameTime As Double = 0.25
        Public Property MaximumSubsteps As Integer = 64
        Public Property SolverIterations As Integer = 12
        Public Property MaximumSpeed As Single = 10000
        Private sweptKinematic As PhysicsBody
        Private accumulator As Double
        Private active As PhysicsBody() = Array.Empty(Of PhysicsBody)()
        Private ReadOnly children As New Dictionary(Of PhysicsBody, List(Of PhysicsBody))()
        Private ReadOnly previous As New Dictionary(Of PhysicsBody, Vector3)()

        ''' <summary>Advances by bounded fixed steps. Nonfinite/negative time cannot inject energy.</summary>
        Public Sub Advance(seconds As Double)
            Prepare()
            If Not Double.IsFinite(seconds) OrElse seconds <= 0 Then
                ResolveContacts(0, False)
                Return
            End If
            Dim interval = PhysicsBody.FiniteClamp(FixedTimeStep, 1.0 / 1000, 1.0 / 15, 1.0 / 120)
            Dim frameLimit = PhysicsBody.FiniteClamp(MaximumFrameTime, interval, 1, 0.25)
            accumulator = Math.Min(accumulator + Math.Min(seconds, frameLimit), frameLimit)
            Dim count = 0, limit = Math.Clamp(MaximumSubsteps, 1, 256)
            While accumulator + 0.0000000001 >= interval AndAlso count < limit
                StepOnce(CSng(interval))
                accumulator = Math.Max(0, accumulator - interval)
                count += 1
            End While
            If count = limit Then accumulator = Math.Min(accumulator, interval)
            If count > 0 Then
                For Each body In active
                    body.Force = Vector3.Zero
                Next
            Else
                ResolveContacts(0, False)
            End If
        End Sub

        ''' <summary>Projects invalid initial/dragged placements without simulating elapsed time.</summary>
        Public Sub ResolveOverlaps()
            Prepare()
            ResolveContacts(0, False)
        End Sub

        ''' <summary>
        ''' Sweeps a pointer-driven body from its current position to a desired position.
        ''' Stops at its first blocking contact instead of teleporting across thin obstacles.
        ''' </summary>
        Public Sub MoveBody(body As PhysicsBody, target As Vector3, Optional seconds As Double = 1.0 / 120)
            If body Is Nothing Then Throw New ArgumentNullException(NameOf(body))
            If Not PhysicsBody.IsFinite(target) Then Return
            Prepare()
            If Not active.Contains(body) OrElse body.IsStatic Then Return
            previous.Clear()
            For Each current In active
                previous(current) = current.Position
            Next
            Dim elapsed = CSng(PhysicsBody.FiniteClamp(seconds, 1.0 / 1000, 0.25, 1.0 / 120))
            Dim delta = target - body.Position
            body.Velocity = delta / elapsed
            body.Sanitize(CSng(PhysicsBody.FiniteClamp(MaximumSpeed, 1, 1000000, 10000)))
            Move(body, delta)
            sweptKinematic = body
            Try
                ResolveContacts(elapsed, True)
            Finally
                sweptKinematic = Nothing
            End Try
        End Sub
        Private Sub Prepare()
            active = Bodies.Where(Function(body) body IsNot Nothing).Distinct().ToArray()
            children.Clear()
            Dim speed = CSng(PhysicsBody.FiniteClamp(MaximumSpeed, 1, 1000000, 10000))
            For Each body In active
                body.Sanitize(speed)
                If body.Parent IsNot Nothing AndAlso body.Parent IsNot body AndAlso body.Parent.Is3D = body.Is3D Then
                    Dim list As List(Of PhysicsBody) = Nothing
                    If Not children.TryGetValue(body.Parent, list) Then
                        list = New List(Of PhysicsBody)()
                        children.Add(body.Parent, list)
                    End If
                    list.Add(body)
                End If
            Next
        End Sub

        Private Sub StepOnce(dt As Single)
            previous.Clear()
            For Each body In active
                previous(body) = body.Position
            Next
            For Each body In active
                If body.IsStatic OrElse body.IsKinematic Then Continue For
                Dim acceleration = body.Force * CSng(1 / body.Mass)
                If body.GravityEnabled Then acceleration += body.Gravity
                body.Velocity += acceleration * dt
                Dim damping = PhysicsBody.FiniteClamp(body.LinearDamping, 0, 1000, 0)
                If damping > 0 Then body.Velocity *= CSng(Math.Exp(-damping * dt))
                body.Sanitize(CSng(PhysicsBody.FiniteClamp(MaximumSpeed, 1, 1000000, 10000)))
                Move(body, body.Velocity * dt)
            Next
            ResolveContacts(dt, True)
        End Sub

        Private Sub Move(body As PhysicsBody, delta As Vector3)
            If delta = Vector3.Zero OrElse Not PhysicsBody.IsFinite(delta) Then Return
            If Not children.ContainsKey(body) Then
                body.Position += delta
                Return
            End If
            Dim pending As New Stack(Of PhysicsBody)(), visited As New HashSet(Of PhysicsBody)()
            pending.Push(body)
            While pending.Count > 0
                Dim current = pending.Pop()
                If Not visited.Add(current) Then Continue While
                current.Position += delta
                Dim list As List(Of PhysicsBody) = Nothing
                If children.TryGetValue(current, list) Then
                    For Each child In list
                        pending.Push(child)
                    Next
                End If
            End While
        End Sub

        Private Sub ResolveContacts(dt As Single, swept As Boolean)
            Dim iterations = Math.Clamp(SolverIterations, 1, 64)
            For iteration = 0 To iterations - 1
                Dim changed = False
                For Each body In active
                    If body.CollisionEnabled AndAlso Not body.IsStatic Then changed = Contain(body) OrElse changed
                Next
                ' Swept X bounds avoid testing distant pairs; stable ordering preserves determinism.
                Dim useSweep = swept AndAlso iteration = 0
                Dim ordered = active.Where(Function(body) body.CollisionEnabled).OrderBy(Function(body) SweptMinX(body, useSweep)).ToArray()
                For aIndex = 0 To ordered.Length - 1
                    Dim a = ordered(aIndex)
                    Dim aRight = SweptMaxX(a, swept AndAlso iteration = 0)
                    For bIndex = aIndex + 1 To ordered.Length - 1
                        Dim b = ordered(bIndex)
                        If SweptMinX(b, swept AndAlso iteration = 0) > aRight Then Exit For
                        If a.Is3D <> b.Is3D OrElse Not Object.ReferenceEquals(a.Parent, b.Parent) OrElse (a.IsStatic AndAlso b.IsStatic) Then Continue For
                        If ResolvePair(a, b, dt, swept AndAlso iteration = 0) Then changed = True
                    Next
                Next
                If Not changed Then Exit For
            Next
            ' Pair correction can move a child past a wall on the last iteration.
            For Each body In active
                If body.CollisionEnabled AndAlso Not body.IsStatic Then Contain(body)
                body.Sanitize(CSng(PhysicsBody.FiniteClamp(MaximumSpeed, 1, 1000000, 10000)))
            Next
        End Sub

        Private Function SweptMinX(body As PhysicsBody, swept As Boolean) As Single
            Dim start As Vector3
            If swept AndAlso previous.TryGetValue(body, start) Then Return Math.Min(start.X, body.Position.X)
            Return body.Position.X
        End Function
        Private Function SweptMaxX(body As PhysicsBody, swept As Boolean) As Single
            Dim start As Vector3
            If swept AndAlso previous.TryGetValue(body, start) Then Return Math.Max(start.X, body.Position.X) + body.Size.X
            Return body.Position.X + body.Size.X
        End Function

        Private Function Contain(body As PhysicsBody) As Boolean
            Dim parent = body.Parent
            If parent Is Nothing OrElse parent Is body OrElse Not parent.CollisionEnabled OrElse parent.Is3D <> body.Is3D Then Return False
            Dim changed = False
            For axis = 0 To If(body.Is3D, 2, 1)
                Dim low = Component(parent.Position, axis), extent = Component(parent.Size, axis) - Component(body.Size, axis)
                Dim value = Component(body.Position, axis)
                If extent < 0 Then
                    ' No valid fit exists: center deterministically, cancel this velocity component.
                    Dim target = low + extent * 0.5F
                    If value <> target Then
                        Move(body, AxisVector(axis, target - value))
                        changed = True
                    End If
                    body.Velocity -= AxisVector(axis, Component(body.Velocity, axis))
                    Continue For
                End If
                Dim targetValue = Math.Clamp(value, low, low + extent)
                Dim speed = Component(body.Velocity, axis)
                Dim atLow = targetValue <= low + 0.00001F AndAlso speed < 0
                Dim atHigh = targetValue >= low + extent - 0.00001F AndAlso speed > 0
                If targetValue <> value Then
                    Move(body, AxisVector(axis, targetValue - value))
                    changed = True
                End If
                If atLow OrElse atHigh Then
                    Dim normal = AxisVector(axis, If(atLow, -1.0F, 1.0F))
                    Dim normalSpeed = Vector3.Dot(body.Velocity, normal)
                    Dim threshold = RestingThreshold(body, Nothing)
                    Dim restitution = If(normalSpeed < threshold, 0, Math.Max(body.Bounce, parent.Bounce))
                    body.Velocity -= normal * CSng((1 + restitution) * normalSpeed)
                    Dim tangent = body.Velocity - normal * Vector3.Dot(body.Velocity, normal)
                    Dim tangentLength = tangent.Length()
                    If tangentLength > 0 Then
                        Dim friction = Math.Sqrt(body.GetFriction(normal) * parent.GetFriction(normal))
                        body.Velocity -= tangent * CSng(Math.Min(1, friction * (1 + restitution) * normalSpeed / tangentLength))
                    End If
                End If
            Next
            Return changed
        End Function

        Private Function ResolvePair(a As PhysicsBody, b As PhysicsBody, dt As Single, swept As Boolean) As Boolean
            Dim normal As Vector3, depth As Single
            ' Test the swept path before endpoint penetration. An endpoint deeply inside a
            ' thick obstacle must return to the entry face, rather than exit its far side.
            If swept AndAlso dt > 0 Then
                Dim startA As Vector3, startB As Vector3
                If previous.TryGetValue(a, startA) AndAlso previous.TryGetValue(b, startB) Then
                    Dim fraction As Single
                    If Sweep(startA, a.Position, a.Size, startB, b.Position, b.Size, a.Is3D, normal, fraction) Then
                        ' Stop at the first impact this fixed step. Keeping the outgoing velocity
                        ' for the next step conservatively prevents a reflected path from tunneling
                        ' through a second thin wall in a tight corridor.
                        Dim hitA = Vector3.Lerp(startA, a.Position, fraction)
                        Dim hitB = Vector3.Lerp(startB, b.Position, fraction)
                        If Not a.IsStatic AndAlso (Not a.IsKinematic OrElse a Is sweptKinematic) Then Move(a, hitA - a.Position)
                        If Not b.IsStatic AndAlso (Not b.IsKinematic OrElse b Is sweptKinematic) Then Move(b, hitB - b.Position)
                        ApplyContact(a, b, normal)
                        Return True
                    End If
                End If
            End If
            If Penetration(a.Position, a.Size, b.Position, b.Size, a.Is3D, normal, depth) Then
                Dim ia = a.InverseMass, ib = b.InverseMass
                If ia + ib = 0 Then
                    ia = If(a.IsKinematic AndAlso Not a.IsStatic, CSng(1 / a.Mass), 0)
                    ib = If(b.IsKinematic AndAlso Not b.IsStatic, CSng(1 / b.Mass), 0)
                End If
                Dim total = ia + ib
                If total <= 0 Then Return False
                Dim distanceA = depth * ia / total, distanceB = depth * ib / total
                If depth > 0.00001F Then SelectSeparation(a, b, ia, ib, normal, depth, distanceA, distanceB)
                Move(a, -normal * distanceA)
                Move(b, normal * distanceB)
                ApplyContact(a, b, normal)
                Return True
            End If
            Return False
        End Function
        Private Shared Sub SelectSeparation(a As PhysicsBody, b As PhysicsBody, ia As Single, ib As Single,
                                            ByRef normal As Vector3, ByRef depth As Single,
                                            ByRef distanceA As Single, ByRef distanceB As Single)
            ' Prefer the nearest valid exit, not an apparently shorter exit outside a wall.
            ' Redistribute positional correction when a resting body is already against a wall.
            Dim best = Single.MaxValue
            For axis = 0 To If(a.Is3D, 2, 1)
                For sign = -1 To 1 Step 2
                    Dim amount = If(sign > 0,
                                    Component(a.Position, axis) + Component(a.Size, axis) - Component(b.Position, axis),
                                    Component(b.Position, axis) + Component(b.Size, axis) - Component(a.Position, axis))
                    If amount <= 0 OrElse amount >= best Then Continue For
                    Dim capacityA = If(ia > 0, MovementCapacity(a, axis, -sign), 0.0F)
                    Dim capacityB = If(ib > 0, MovementCapacity(b, axis, sign), 0.0F)
                    If CDbl(capacityA) + capacityB + 0.00001 < amount Then Continue For
                    Dim first = Math.Min(capacityA, amount * ia / (ia + ib))
                    Dim second = amount - first
                    If second > capacityB Then
                        second = capacityB
                        first = amount - second
                    End If
                    best = amount
                    normal = AxisVector(axis, CSng(sign))
                    depth = amount
                    distanceA = first
                    distanceB = second
                Next
            Next
        End Sub

        Private Shared Function MovementCapacity(body As PhysicsBody, axis As Integer, direction As Integer) As Single
            Dim parent = body.Parent
            If parent Is Nothing OrElse Not parent.CollisionEnabled OrElse parent.Is3D <> body.Is3D Then Return Single.MaxValue
            If direction < 0 Then Return Math.Max(0, Component(body.Position, axis) - Component(parent.Position, axis))
            Return Math.Max(0, Component(parent.Position, axis) + Component(parent.Size, axis) - Component(body.Position, axis) - Component(body.Size, axis))
        End Function
        Private Shared Function RestingThreshold(a As PhysicsBody, b As PhysicsBody) As Double
            Dim acceleration = If(a.GravityEnabled, a.Gravity.Length(), 0.0F)
            If b IsNot Nothing AndAlso b.GravityEnabled Then acceleration = Math.Max(acceleration, b.Gravity.Length())
            ' Suppress tiny repeated rebounds under gravity while preserving substantial impacts.
            Return Math.Max(0.05, acceleration / 30)
        End Function

        Private Shared Sub ApplyContact(a As PhysicsBody, b As PhysicsBody, normal As Vector3)
            Dim inverse = a.InverseMass + b.InverseMass
            If inverse <= 0 Then Return
            Dim relative = b.Velocity - a.Velocity
            Dim closing = Vector3.Dot(relative, normal)
            If closing >= 0 Then Return
            Dim restitution = If(-closing < RestingThreshold(a, b), 0, Math.Max(a.Bounce, b.Bounce))
            Dim impulse = CSng(-(1 + restitution) * closing / inverse)
            a.Velocity -= normal * (impulse * a.InverseMass)
            b.Velocity += normal * (impulse * b.InverseMass)
            relative = b.Velocity - a.Velocity
            Dim tangent = relative - normal * Vector3.Dot(relative, normal)
            Dim length = tangent.Length()
            If length <= 0.000001F Then Return
            tangent /= length
            Dim friction = Math.Sqrt(a.GetFriction(normal) * b.GetFriction(normal))
            Dim tangentImpulse = CSng(Math.Clamp(-Vector3.Dot(relative, tangent) / inverse, -impulse * friction, impulse * friction))
            a.Velocity -= tangent * (tangentImpulse * a.InverseMass)
            b.Velocity += tangent * (tangentImpulse * b.InverseMass)
        End Sub

        Private Shared Function Penetration(a As Vector3, sizeA As Vector3, b As Vector3, sizeB As Vector3, is3D As Boolean, ByRef normal As Vector3, ByRef depth As Single) As Boolean
            depth = Single.MaxValue
            For axis = 0 To If(is3D, 2, 1)
                Dim av = Component(a, axis), bv = Component(b, axis)
                Dim an = av + Component(sizeA, axis), bn = bv + Component(sizeB, axis)
                If an < bv - 0.00001F OrElse bn < av - 0.00001F Then Return False
                Dim negative = an - bv, positive = bn - av
                Dim candidate = Math.Min(negative, positive)
                If candidate < depth Then
                    depth = candidate
                    normal = AxisVector(axis, If(negative <= positive, 1.0F, -1.0F))
                End If
            Next
            If depth < -0.00001F OrElse depth = Single.MaxValue Then Return False
            depth = Math.Max(0, depth)
            Return True
        End Function

        Private Shared Function Sweep(startA As Vector3, endA As Vector3, sizeA As Vector3, startB As Vector3, endB As Vector3, sizeB As Vector3, is3D As Boolean, ByRef normal As Vector3, ByRef fraction As Single) As Boolean
            Dim ignoredNormal As Vector3, ignoredDepth As Single
            If Penetration(startA, sizeA, startB, sizeB, is3D, ignoredNormal, ignoredDepth) AndAlso ignoredDepth > 0.00001F Then Return False
            Dim motion = (endA - startA) - (endB - startB)
            Dim entry As Single = -Single.MaxValue, leave As Single = Single.MaxValue
            For axis = 0 To If(is3D, 2, 1)
                Dim av = Component(startA, axis), bv = Component(startB, axis)
                Dim an = av + Component(sizeA, axis), bn = bv + Component(sizeB, axis)
                Dim speed = Component(motion, axis)
                If Math.Abs(speed) < 0.00000001F Then
                    If an < bv - 0.00001F OrElse bn < av - 0.00001F Then Return False
                    Continue For
                End If
                Dim first = (bv - an) / speed, last = (bn - av) / speed
                Dim axisEntry = Math.Min(first, last), axisLeave = Math.Max(first, last)
                If axisEntry > entry Then
                    entry = axisEntry
                    normal = AxisVector(axis, If(speed > 0, 1.0F, -1.0F))
                End If
                leave = Math.Min(leave, axisLeave)
            Next
            If entry < 0 OrElse entry > 1 OrElse entry > leave OrElse leave < 0 Then Return False
            fraction = Math.Clamp(entry, 0, 1)
            Return True
        End Function

        Private Shared Function Component(value As Vector3, axis As Integer) As Single
            Return If(axis = 0, value.X, If(axis = 1, value.Y, value.Z))
        End Function
        Private Shared Function AxisVector(axis As Integer, value As Single) As Vector3
            Return If(axis = 0, New Vector3(value, 0, 0), If(axis = 1, New Vector3(0, value, 0), New Vector3(0, 0, value)))
        End Function
    End Class
End Namespace
