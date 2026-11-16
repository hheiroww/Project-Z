Imports System.Collections.Generic
Imports System.Linq
Imports System.Numerics
Imports System.Threading

Namespace [Shared].Physics
    ''' <summary>A radial velocity impulse in pool-local coordinates, applied before stepping from Tick to Tick + 1.</summary>
    Public Structure WaterSurfaceImpact
        Public ReadOnly Property Tick As Long
        Public ReadOnly Property X As Single
        Public ReadOnly Property Z As Single
        Public ReadOnly Property Radius As Single
        Public ReadOnly Property Strength As Single

        Public Sub New(tick As Long, x As Single, z As Single, radius As Single, strength As Single)
            Me.Tick = tick
            Me.X = x
            Me.Z = z
            Me.Radius = radius
            Me.Strength = strength
            Validate()
        End Sub

        Friend Sub Validate()
            If Tick < 0 Then Throw New ArgumentOutOfRangeException(NameOf(Tick))
            If Not WaterHeightfield.InRange(X, 0, 1) Then Throw New ArgumentOutOfRangeException(NameOf(X))
            If Not WaterHeightfield.InRange(Z, 0, 1) Then Throw New ArgumentOutOfRangeException(NameOf(Z))
            If Not WaterHeightfield.InRange(Radius, WaterHeightfield.MinimumImpactRadius, WaterHeightfield.MaximumImpactRadius) Then Throw New ArgumentOutOfRangeException(NameOf(Radius))
            If Not WaterHeightfield.InRange(Strength, -1, 1) Then Throw New ArgumentOutOfRangeException(NameOf(Strength))
        End Sub
    End Structure

    Public Structure WaterSurfaceVertex
        Public ReadOnly Property Position As Vector3
        Public ReadOnly Property Normal As Vector3

        Friend Sub New(position As Vector3, normal As Vector3)
            Me.Position = position
            Me.Normal = normal
        End Sub
    End Structure

    ''' <summary>
    ''' Fixed-step shallow-water surface on a unit-square, closed pool. X and Z lie in [0, 1] and Y is up.
    ''' Give each playback or export worker its own instance. Edits recreate the instance from the impact list.
    ''' </summary>
    Public NotInheritable Class WaterHeightfield
        Public Const TicksPerSecond As Integer = 120
        Public Const MinimumGridSize As Integer = 9
        Public Const MaximumGridSize As Integer = 64
        Public Const MaximumImpacts As Integer = 32
        Public Const MinimumWaveSpeed As Single = 0.25F
        Public Const MaximumWaveSpeed As Single = 1.25F
        Public Const MinimumDamping As Single = 0.75F
        Public Const MaximumDamping As Single = 4.0F
        Public Const MinimumImpactRadius As Single = 0.02F
        Public Const MaximumImpactRadius As Single = 0.5F
        Private Const StepSeconds As Single = 1.0F / TicksPerSecond
        Private Const CheckpointPeriod As Integer = 2 * TicksPerSecond
        Private Const CheckpointBytes As Integer = 256 * 1024
        Private Const RestingHeight As Single = 0.000025F
        Private Const RestingVelocity As Single = 0.0001F

        Private NotInheritable Class Checkpoint
            Friend ReadOnly Tick As Long
            Friend ReadOnly Heights As Single()
            Friend ReadOnly Velocities As Single()
            Friend ReadOnly NextImpact As Integer

            Friend Sub New(tick As Long, heights As Single(), velocities As Single(), nextImpact As Integer)
                Me.Tick = tick
                Me.Heights = DirectCast(heights.Clone(), Single())
                Me.Velocities = DirectCast(velocities.Clone(), Single())
                Me.NextImpact = nextImpact
            End Sub
        End Class

        Private ReadOnly impacts As WaterSurfaceImpact()
        Private ReadOnly velocities As Single()
        Private ReadOnly spacing, velocityDecay, waveAcceleration As Single
        Private ReadOnly checkpoints As New LinkedList(Of Checkpoint)
        Private ReadOnly checkpointCapacity As Integer
        Private heights, nextHeights As Single()
        Private tickValue As Long
        Private nextImpact As Integer
        Private resting As Boolean = True

        Public ReadOnly Property GridSize As Integer

        Public ReadOnly Property CurrentTick As Long
            Get
                Return tickValue
            End Get
        End Property

        Public ReadOnly Property IsResting As Boolean
            Get
                Return resting
            End Get
        End Property

        Public Sub New(Optional gridSize As Integer = 41, Optional waveSpeed As Single = 0.75F,
                       Optional damping As Single = 1.4F, Optional impacts As IEnumerable(Of WaterSurfaceImpact) = Nothing)
            If gridSize < MinimumGridSize OrElse gridSize > MaximumGridSize Then Throw New ArgumentOutOfRangeException(NameOf(gridSize))
            If Not InRange(waveSpeed, MinimumWaveSpeed, MaximumWaveSpeed) Then Throw New ArgumentOutOfRangeException(NameOf(waveSpeed))
            If Not InRange(damping, MinimumDamping, MaximumDamping) Then Throw New ArgumentOutOfRangeException(NameOf(damping))

            Me.impacts = If(impacts, Enumerable.Empty(Of WaterSurfaceImpact)()).Take(MaximumImpacts + 1).
                OrderBy(Function(impact) impact.Tick).ToArray()
            If Me.impacts.Length > MaximumImpacts Then Throw New ArgumentOutOfRangeException(NameOf(impacts))
            For Each impact In Me.impacts
                impact.Validate()
            Next

            Me.GridSize = gridSize
            Dim cells = gridSize * gridSize
            heights = New Single(cells - 1) {}
            nextHeights = New Single(cells - 1) {}
            velocities = New Single(cells - 1) {}
            checkpointCapacity = CheckpointBytes \ (2 * cells * 4)
            spacing = 1.0F / (gridSize - 1)
            velocityDecay = MathF.Exp(-damping * StepSeconds)

            ' Keep c * dt / dx below the two-dimensional wave stability limit.
            Dim courant = Math.Min(0.67F, waveSpeed * StepSeconds / spacing)
            waveAcceleration = courant * courant / StepSeconds
        End Sub

        Public Shared Function TickAt(seconds As Double) As Long
            If Not Double.IsFinite(seconds) OrElse seconds < 0 OrElse seconds >= CDbl(Long.MaxValue) / TicksPerSecond Then
                Throw New ArgumentOutOfRangeException(NameOf(seconds))
            End If
            Return CLng(Math.Floor(seconds * TicksPerSecond + 0.000000001))
        End Function

        ''' <summary>Evaluate the same state at a tick regardless of seek order. Cancellation leaves a valid earlier state.</summary>
        Public Sub Seek(tick As Long, Optional cancellationToken As CancellationToken = Nothing)
            If tick < 0 Then Throw New ArgumentOutOfRangeException(NameOf(tick))
            cancellationToken.ThrowIfCancellationRequested()
            If tick = tickValue Then Return
            RestoreNearestCheckpoint(tick)

            While tickValue < tick
                cancellationToken.ThrowIfCancellationRequested()
                If resting Then
                    tickValue = If(nextImpact = impacts.Length, tick, Math.Min(tick, impacts(nextImpact).Tick))
                    If tickValue = tick Then Exit While
                End If
                While nextImpact < impacts.Length AndAlso impacts(nextImpact).Tick = tickValue
                    ApplyImpact(impacts(nextImpact))
                    nextImpact += 1
                End While
                If Not resting Then StepSurface()
                tickValue += 1
                If Not resting AndAlso tickValue Mod TicksPerSecond = 0 Then
                    SleepIfSettled()
                    If Not resting AndAlso tickValue Mod CheckpointPeriod = 0 Then SaveCheckpoint()
                End If
            End While
        End Sub

        Public Function SampleHeight(x As Single, z As Single) As Single
            If Not InRange(x, 0, 1) Then Throw New ArgumentOutOfRangeException(NameOf(x))
            If Not InRange(z, 0, 1) Then Throw New ArgumentOutOfRangeException(NameOf(z))
            Dim columnPosition = x * (GridSize - 1)
            Dim rowPosition = z * (GridSize - 1)
            Dim column = Math.Min(GridSize - 2, CInt(MathF.Floor(columnPosition)))
            Dim row = Math.Min(GridSize - 2, CInt(MathF.Floor(rowPosition)))
            Dim fractionX = columnPosition - column
            Dim fractionZ = rowPosition - row
            Dim cell = row * GridSize + column
            Dim upper = heights(cell) + (heights(cell + 1) - heights(cell)) * fractionX
            Dim lower = heights(cell + GridSize) + (heights(cell + GridSize + 1) - heights(cell + GridSize)) * fractionX
            Return upper + (lower - upper) * fractionZ
        End Function

        Public Function GetVertex(column As Integer, row As Integer) As WaterSurfaceVertex
            If column < 0 OrElse column >= GridSize Then Throw New ArgumentOutOfRangeException(NameOf(column))
            If row < 0 OrElse row >= GridSize Then Throw New ArgumentOutOfRangeException(NameOf(row))
            Dim cell = row * GridSize + column
            Dim dx = If(column = 0 OrElse column = GridSize - 1, 0.0F, (heights(cell + 1) - heights(cell - 1)) / (2.0F * spacing))
            Dim dz = If(row = 0 OrElse row = GridSize - 1, 0.0F, (heights(cell + GridSize) - heights(cell - GridSize)) / (2.0F * spacing))
            Dim position As New Vector3(column / CSng(GridSize - 1), heights(cell), row / CSng(GridSize - 1))
            Return New WaterSurfaceVertex(position, Vector3.Normalize(New Vector3(-dx, 1, -dz)))
        End Function

        Public Function CreateIndices() As Integer()
            Dim indices((GridSize - 1) * (GridSize - 1) * 6 - 1) As Integer
            Dim offset = 0
            For row = 0 To GridSize - 2
                For column = 0 To GridSize - 2
                    Dim cell = row * GridSize + column
                    indices(offset) = cell
                    indices(offset + 1) = cell + GridSize
                    indices(offset + 2) = cell + 1
                    indices(offset + 3) = cell + 1
                    indices(offset + 4) = cell + GridSize
                    indices(offset + 5) = cell + GridSize + 1
                    offset += 6
                Next
            Next
            Return indices
        End Function

        ''' <summary>
        ''' Resolve a downward sphere crossing during one fixed step against the current surface.
        ''' The caller may record the result and construct a new heightfield to replay the contact.
        ''' </summary>
        Public Function TryCreateSphereImpact(previous As Vector3, current As Vector3, radius As Single, tick As Long,
                                              ByRef impact As WaterSurfaceImpact) As Boolean
            impact = Nothing
            If tick <> tickValue OrElse Not IsFinite(previous) OrElse Not IsFinite(current) OrElse
               Not InRange(radius, Single.Epsilon, MaximumImpactRadius) OrElse current.Y >= previous.Y Then Return False

            Dim delta = current - previous
            If Not IsFinite(delta) Then Return False
            Dim start = 0.0F, finish = 1.0F
            If Not ClipAxis(previous.X, delta.X, start, finish) OrElse Not ClipAxis(previous.Z, delta.Z, start, finish) Then Return False
            Dim last = start
            Dim clearance = SphereClearance(previous, delta, radius, last)
            If clearance <= 0 Then Return False

            ' Cover at least two samples per crossed grid cell in the horizontal plane.
            Dim travel = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Z)) * (finish - start)
            Dim segments = Math.Clamp(CInt(Math.Ceiling(CDbl(travel) * (GridSize - 1) * 2)), 8, 2 * MaximumGridSize)
            For index = 1 To segments
                Dim nextSample = start + (finish - start) * (index / CSng(segments))
                If SphereClearance(previous, delta, radius, nextSample) <= 0 Then
                    For iteration = 1 To 12
                        Dim middle = (last + nextSample) / 2.0F
                        If SphereClearance(previous, delta, radius, middle) > 0 Then
                            last = middle
                        Else
                            nextSample = middle
                        End If
                    Next
                    Dim point = previous + delta * nextSample
                    impact = New WaterSurfaceImpact(tick, Math.Clamp(point.X, 0, 1), Math.Clamp(point.Z, 0, 1),
                                                    Math.Max(radius, MinimumImpactRadius), Math.Max(-1.0F, delta.Y * TicksPerSecond))
                    Return True
                End If
                last = nextSample
            Next
            Return False
        End Function

        Private Shared Function ClipAxis(origin As Single, delta As Single, ByRef start As Single, ByRef finish As Single) As Boolean
            If delta = 0 Then Return origin >= 0 AndAlso origin <= 1
            Dim first = -origin / delta
            Dim last = (1.0F - origin) / delta
            start = Math.Max(start, Math.Min(first, last))
            finish = Math.Min(finish, Math.Max(first, last))
            Return start < finish
        End Function

        Private Function SphereClearance(origin As Vector3, delta As Vector3, radius As Single, fraction As Single) As Single
            Dim point = origin + delta * fraction
            Return point.Y - radius - SampleHeight(Math.Clamp(point.X, 0, 1), Math.Clamp(point.Z, 0, 1))
        End Function

        Private Shared Function IsFinite(value As Vector3) As Boolean
            Return Single.IsFinite(value.X) AndAlso Single.IsFinite(value.Y) AndAlso Single.IsFinite(value.Z)
        End Function

        Friend Shared Function InRange(value As Single, minimum As Single, maximum As Single) As Boolean
            Return Single.IsFinite(value) AndAlso value >= minimum AndAlso value <= maximum
        End Function

        Private Sub ApplyImpact(impact As WaterSurfaceImpact)
            If impact.Strength = 0 Then Return

            ' Balance the core and the surrounding ring so an impact moves water without adding it.
            Dim radius = Math.Max(impact.Radius, spacing * 2)
            Dim coreTotal = 0.0F, ringTotal = 0.0F
            For row = 0 To GridSize - 1
                For column = 0 To GridSize - 1
                    Dim distance = ImpactDistance(impact, column, row, radius)
                    If distance >= 1 Then Continue For
                    coreTotal += CoreWeight(distance)
                    ringTotal += RingWeight(distance)
                Next
            Next
            Dim ringScale = coreTotal / ringTotal
            For row = 0 To GridSize - 1
                For column = 0 To GridSize - 1
                    Dim distance = ImpactDistance(impact, column, row, radius)
                    If distance >= 1 Then Continue For
                    velocities(row * GridSize + column) += impact.Strength * (CoreWeight(distance) - ringScale * RingWeight(distance))
                Next
            Next
            resting = False
        End Sub

        Private Function ImpactDistance(impact As WaterSurfaceImpact, column As Integer, row As Integer, radius As Single) As Single
            Dim x = column * spacing - impact.X
            Dim z = row * spacing - impact.Z
            Return MathF.Sqrt(x * x + z * z) / radius
        End Function

        Private Shared Function CoreWeight(distance As Single) As Single
            Dim falloff = 1.0F - distance
            Return falloff * falloff * falloff
        End Function

        Private Shared Function RingWeight(distance As Single) As Single
            Return distance * distance * (1.0F - distance)
        End Function

        Private Sub StepSurface()
            For row = 0 To GridSize - 1
                Dim rowBase = row * GridSize
                Dim north = Math.Max(0, row - 1) * GridSize
                Dim south = Math.Min(GridSize - 1, row + 1) * GridSize
                For column = 0 To GridSize - 1
                    Dim cell = rowBase + column
                    Dim height = heights(cell)
                    Dim curvature = heights(rowBase + Math.Max(0, column - 1)) + heights(rowBase + Math.Min(GridSize - 1, column + 1)) +
                                    heights(north + column) + heights(south + column) - 4.0F * height
                    velocities(cell) = velocities(cell) * velocityDecay + curvature * waveAcceleration
                    nextHeights(cell) = height + velocities(cell) * StepSeconds
                Next
            Next
            Dim previous = heights
            heights = nextHeights
            nextHeights = previous
        End Sub

        Private Sub SleepIfSettled()
            For cell = 0 To heights.Length - 1
                If Math.Abs(heights(cell)) > RestingHeight OrElse Math.Abs(velocities(cell)) > RestingVelocity Then Return
            Next
            Array.Clear(heights)
            Array.Clear(velocities)
            resting = True
        End Sub

        Private Sub SaveCheckpoint()
            Dim existing = checkpoints.First
            While existing IsNot Nothing
                If existing.Value.Tick = tickValue Then
                    checkpoints.Remove(existing)
                    checkpoints.AddLast(existing)
                    Return
                End If
                existing = existing.Next
            End While
            If checkpoints.Count >= checkpointCapacity Then checkpoints.RemoveFirst()
            checkpoints.AddLast(New Checkpoint(tickValue, heights, velocities, nextImpact))
        End Sub

        Private Sub RestoreNearestCheckpoint(target As Long)
            Dim best As LinkedListNode(Of Checkpoint) = Nothing
            Dim node = checkpoints.First
            While node IsNot Nothing
                Dim candidate = node.Value.Tick
                If candidate <= target AndAlso (target < tickValue OrElse candidate > tickValue) AndAlso
                   (best Is Nothing OrElse candidate > best.Value.Tick) Then
                    best = node
                End If
                node = node.Next
            End While
            If best IsNot Nothing Then
                tickValue = best.Value.Tick
                nextImpact = best.Value.NextImpact
                Array.Copy(best.Value.Heights, heights, heights.Length)
                Array.Copy(best.Value.Velocities, velocities, velocities.Length)
                resting = False
                checkpoints.Remove(best)
                checkpoints.AddLast(best)
            ElseIf target < tickValue Then
                Array.Clear(heights)
                Array.Clear(velocities)
                tickValue = 0
                nextImpact = 0
                resting = True
            End If
        End Sub
    End Class
End Namespace
