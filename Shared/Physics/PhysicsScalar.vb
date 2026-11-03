Namespace [Shared].Physics
    ''' <summary>
    ''' Drives an existing numeric animation/property parameter through callbacks. This is
    ''' not a separate animation timeline. Stiffness=0 coasts; Stiffness&gt;0 springs to Target.
    ''' Exact damped-spring integration stays bounded even with stiff springs/light masses.
    ''' </summary>
    Public NotInheritable Class PhysicsScalar
        Private ReadOnly readValue As Func(Of Double)
        Private ReadOnly writeValue As Action(Of Double)
        Private massValue As Double = 1
        Private velocityValue As Double
        Public Property Minimum As Double = -Double.MaxValue
        Public Property Maximum As Double = Double.MaxValue
        Public Property Target As Double
        Public Property Stiffness As Double
        Public Property Damping As Double = 12
        Public Property Friction As Double
        Public Property Bounce As Double
        Public Property Enabled As Boolean = True

        Public Sub New(getter As Func(Of Double), setter As Action(Of Double))
            If getter Is Nothing Then Throw New ArgumentNullException(NameOf(getter))
            If setter Is Nothing Then Throw New ArgumentNullException(NameOf(setter))
            readValue = getter
            writeValue = setter
        End Sub
        Public Property Mass As Double
            Get
                Return massValue
            End Get
            Set(value As Double)
                massValue = PhysicsBody.FiniteClamp(value, 0.0001, 100000000, 1)
            End Set
        End Property
        Public Property Velocity As Double
            Get
                Return velocityValue
            End Get
            Set(value As Double)
                velocityValue = PhysicsBody.FiniteClamp(value, -1000000, 1000000, 0)
            End Set
        End Property
        Public Sub AddImpulse(impulse As Double)
            If Double.IsFinite(impulse) Then Velocity += impulse / Mass
        End Sub
        Public Sub [Stop]()
            Velocity = 0
        End Sub
        Public Sub Advance(seconds As Double)
            If Not Enabled OrElse Not Double.IsFinite(seconds) OrElse seconds <= 0 Then Return
            Dim dt = Math.Min(seconds, 0.25)
            Dim value = readValue()
            If Not Double.IsFinite(value) Then value = 0
            Dim targetValue = If(Double.IsFinite(Target), Target, value)
            Dim k = PhysicsBody.FiniteClamp(Stiffness, 0, 1000000, 0)
            Dim drag = PhysicsBody.FiniteClamp(Damping, 0, 1000000, 0) / Mass + PhysicsBody.FiniteClamp(Friction, 0, 1000, 0)
            Dim speed = Velocity
            If k <= 0 Then
                If drag <= 0.000000001 Then
                    value += speed * dt
                Else
                    Dim decay = Math.Exp(-drag * dt)
                    value += speed * (1 - decay) / drag
                    speed *= decay
                End If
            Else
                Dim x = value - targetValue
                Dim omegaSquared = k / Mass, alpha = drag / 2
                Dim difference = alpha * alpha - omegaSquared
                If Math.Abs(difference) <= omegaSquared * 0.000000001 Then
                    Dim c = speed + alpha * x, decay = Math.Exp(-alpha * dt)
                    value = targetValue + (x + c * dt) * decay
                    speed = (speed - alpha * c * dt) * decay
                ElseIf difference < 0 Then
                    Dim omega = Math.Sqrt(-difference), decay = Math.Exp(-alpha * dt)
                    Dim cosine = Math.Cos(omega * dt), sine = Math.Sin(omega * dt)
                    Dim c = (speed + alpha * x) / omega
                    value = targetValue + decay * (x * cosine + c * sine)
                    speed = decay * ((c * omega - alpha * x) * cosine - (x * omega + alpha * c) * sine)
                Else
                    Dim root = Math.Sqrt(difference)
                    Dim r1 = -omegaSquared / (alpha + root), r2 = -alpha - root
                    Dim c1 = (speed - r2 * x) / (r1 - r2), c2 = x - c1
                    Dim e1 = Math.Exp(r1 * dt), e2 = Math.Exp(r2 * dt)
                    value = targetValue + c1 * e1 + c2 * e2
                    speed = c1 * r1 * e1 + c2 * r2 * e2
                End If
            End If
            Dim low = If(Double.IsFinite(Minimum), Minimum, -Double.MaxValue)
            Dim high = If(Double.IsFinite(Maximum), Maximum, Double.MaxValue)
            If high < low Then high = low
            Dim restitution = PhysicsBody.FiniteClamp(Bounce, 0, 1, 0)
            If value <= low Then
                value = low
                If speed < 0 Then speed = -speed * restitution
            ElseIf value >= high Then
                value = high
                If speed > 0 Then speed = -speed * restitution
            End If
            If Not Double.IsFinite(value) Then
                value = Math.Clamp(0.0, low, high)
                speed = 0
            End If
            Velocity = If(Math.Abs(speed) < 0.000001, 0, speed)
            writeValue(value)
        End Sub
    End Class
End Namespace
