Imports Microsoft.Xna.Framework

Namespace [Shared].Drawing.Particles

    ''' <summary>A particle snapshot. Coordinates are local to its emitter; 2D ignores Z.</summary>
    Public Structure ParticleState
        Public Position As Vector3
        Public Velocity As Vector3
        Public Age As Double
        Public Lifetime As Double
        Public Size As Vector2
        Public Tint As Color
        Public Rotation As Single
        Public AngularVelocity As Single
    End Structure

    ''' <summary>
    ''' Bounded, deterministic particle pool. CPU simulation uses analytical constant-acceleration
    ''' integration; the emitter uploads one reusable billboard buffer for GPU rendering.
    ''' Particles do not participate in scene rigid-body collisions.
    ''' </summary>
    Public NotInheritable Class ParticleSimulation
        Private ReadOnly states As ParticleState()
        Private ReadOnly random As Random
        Private countValue As Integer
        Private emissionRemainder As Double

        Public Sub New(Optional capacity As Integer = 4096, Optional seed As Integer = 1)
            If capacity < 1 OrElse capacity > 16000 Then Throw New ArgumentOutOfRangeException(NameOf(capacity), "Use 1 through 16000 particles per emitter.")
            ReDim states(capacity - 1)
            random = New Random(seed)
        End Sub

        Public ReadOnly Property Capacity As Integer
            Get
                Return states.Length
            End Get
        End Property
        Public ReadOnly Property Count As Integer
            Get
                Return countValue
            End Get
        End Property

        Public Property Enabled As Boolean = True
        Public Property Is3D As Boolean
        Public Property PhysicsEnabled As Boolean = True
        Public Property GravityEnabled As Boolean
        Public Property Gravity As Vector3 = New Vector3(0, 980, 0)
        Public Property LinearDrag As Double
        Public Property EmissionRate As Double = 30
        Public Property EmissionPosition As Vector3
        Public Property InitialVelocity As Vector3 = New Vector3(0, -50, 0)
        Public Property VelocitySpread As Vector3 = New Vector3(20, 20, 0)
        Public Property ParticleLifetime As Double = 2
        Public Property ParticleSize As Vector2 = New Vector2(8)
        Public Property ParticleTint As Color = Color.White
        Public Property InitialRotation As Single
        Public Property AngularVelocity As Single

        Default Public ReadOnly Property Item(index As Integer) As ParticleState
            Get
                If index < 0 OrElse index >= countValue Then Throw New ArgumentOutOfRangeException(NameOf(index))
                Return states(index)
            End Get
        End Property

        Public Function Emit(Optional amount As Integer = 1) As Integer
            Dim emitted = Math.Min(Math.Max(amount, 0), Capacity - countValue)
            For index = 1 To emitted
                Dim velocity = SafeVector(InitialVelocity) + New Vector3(RandomSpread(VelocitySpread.X), RandomSpread(VelocitySpread.Y), RandomSpread(VelocitySpread.Z))
                Dim position = SafeVector(EmissionPosition)
                If Not Is3D Then
                    position.Z = 0 : velocity.Z = 0
                End If
                states(countValue) = New ParticleState With {
                    .Position = position, .Velocity = LimitVector(velocity),
                    .Lifetime = SafeRange(ParticleLifetime, 0.001, 3600, 2),
                    .Size = New Vector2(CSng(SafeRange(ParticleSize.X, 0, 100000, 8)), CSng(SafeRange(ParticleSize.Y, 0, 100000, 8))),
                    .Tint = ParticleTint, .Rotation = CSng(SafeRange(InitialRotation, -1000000, 1000000, 0)),
                    .AngularVelocity = CSng(SafeRange(AngularVelocity, -10000, 10000, 0))}
                countValue += 1
            Next
            Return emitted
        End Function

        Public Sub Clear()
            countValue = 0 : emissionRemainder = 0
        End Sub

        ''' <summary>Long stalls are capped at 250 ms so a resumed emitter cannot create an unbounded burst.</summary>
        Public Sub Advance(seconds As Double)
            If Not Enabled OrElse Not Double.IsFinite(seconds) OrElse seconds <= 0 Then Return
            Dim dt = Math.Min(seconds, 0.25)
            Dim acceleration = If(PhysicsEnabled AndAlso GravityEnabled, LimitVector(SafeVector(Gravity)), Vector3.Zero)
            If Not Is3D Then acceleration.Z = 0
            Dim drag = If(PhysicsEnabled, SafeRange(LinearDrag, 0, 1000, 0), 0)
            Dim decay = Math.Exp(-drag * dt)
            Dim velocityIntegral = If(drag > 0.000001, (1 - decay) / drag, dt)
            Dim accelerationIntegral = If(drag > 0.000001, (dt - velocityIntegral) / drag, dt * dt * 0.5)
            Dim index = 0
            While index < countValue
                Dim state = states(index)
                state.Age += dt
                If state.Age >= state.Lifetime Then
                    countValue -= 1 : states(index) = states(countValue)
                    Continue While
                End If
                state.Position = LimitVector(state.Position + state.Velocity * CSng(velocityIntegral) + acceleration * CSng(accelerationIntegral))
                state.Velocity = LimitVector(state.Velocity * CSng(decay) + acceleration * CSng(velocityIntegral))
                state.Rotation = CSng((state.Rotation + state.AngularVelocity * dt) Mod (Math.PI * 2))
                If Not Is3D Then state.Position.Z = 0 : state.Velocity.Z = 0
                states(index) = state
                index += 1
            End While
            emissionRemainder += SafeRange(EmissionRate, 0, 100000, 0) * dt
            Dim births = CInt(Math.Floor(emissionRemainder))
            emissionRemainder -= births
            Emit(births)
        End Sub

        Private Function RandomSpread(value As Single) As Single
            Return CSng((random.NextDouble() * 2 - 1) * SafeRange(Math.Abs(CDbl(value)), 0, 100000, 0))
        End Function
        Private Shared Function SafeVector(value As Vector3) As Vector3
            Return New Vector3(CSng(SafeRange(value.X, -1000000, 1000000, 0)), CSng(SafeRange(value.Y, -1000000, 1000000, 0)), CSng(SafeRange(value.Z, -1000000, 1000000, 0)))
        End Function
        Private Shared Function LimitVector(value As Vector3) As Vector3
            Return SafeVector(value)
        End Function
        Private Shared Function SafeRange(value As Double, minimum As Double, maximum As Double, fallback As Double) As Double
            Return If(Double.IsFinite(value), Math.Clamp(value, minimum, maximum), fallback)
        End Function
    End Class
End Namespace
