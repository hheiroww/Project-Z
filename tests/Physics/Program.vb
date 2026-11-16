Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Physics
Imports System.Collections.Generic
Imports System.Linq
Imports NumericsVector = System.Numerics.Vector3

Module Program
    Private passes As Integer
    Private ReadOnly failures As New List(Of String)

    Sub Main()
        Run("impulses respect mass", AddressOf MassImpulse)
        Run("gravity is opt-in and static bodies stay fixed", AddressOf GravityGate)
        Run("parent bounds require both collision flags", AddressOf ParentOptIn)
        Run("embedded body takes the nearest exit", AddressOf NearestExit)
        Run("coincident bodies separate without adding energy", AddressOf CoincidentBodies)
        Run("collision impulse conserves elastic momentum", AddressOf ElasticCollision)
        Run("inelastic collision removes normal energy", AddressOf InelasticCollision)
        Run("friction presets affect tangential movement", AddressOf FrictionPresets)
        Run("geometry friction responds to the supplied contour", AddressOf GeometryFriction)
        Run("fixed steps give the same result at 30 and 120 Hz", AddressOf FrameRates)
        Run("fast body cannot pass through a thin wall", AddressOf ThinWall)
        Run("fast embedded endpoint resolves at the entry face", AddressOf ThickWall)
        Run("multiple fast impacts stay inside a narrow corridor", AddressOf FastCorridor)
        Run("diagonal fast impact resolves both corner faces", AddressOf FastCorner)
        Run("overlap chooses a separation valid inside the parent", AddressOf ParentValidSeparation)
        Run("pointer-driven movement cannot teleport through a wall", AddressOf KinematicSweep)
        Run("3D resolves the shortest Z separation", AddressOf ThreeDSeparation)
        Run("2D and 3D collision spaces remain independent", AddressOf MixedDimensions)
        Run("parent movement stays in its own dimension", AddressOf DimensionInheritance)
        Run("same-dimension children inherit parent movement", AddressOf ParentMovement)
        Run("3D fast impacts stay between Z walls", AddressOf ThreeDCorridor)
        Run("separate parent groups do not collide", AddressOf SeparateParents)
        Run("resting body stays bounded over long simulation", AddressOf RestingContact)
        Run("stack settles without explosive velocities", AddressOf RestingStack)
        Run("overlap recovery remains finite under repeated collisions", AddressOf OverlapStress)
        Run("dense coincident bodies recover without motion energy", AddressOf DenseOverlap)
        Run("invalid body state and elapsed time are contained", AddressOf InvalidState)
        Run("oversized body has deterministic parent placement", AddressOf OversizedBody)
        Run("scalar impulse drives an existing property", AddressOf ScalarImpulse)
        Run("stiff scalar spring stays stable with small mass", AddressOf StiffScalar)
        Run("scalar bounds and invalid values stay finite", AddressOf ScalarBounds)
        Run("water impulses spread radially and settle", AddressOf WaterSpreadsAndDamps)
        Run("water reflects at walls and conserves its mean height", AddressOf WaterReflectsAndConservesMean)
        Run("water sampling is identical after forward and backward seeking", AddressOf WaterSeeksDeterministically)
        Run("water mesh has upward winding and normalized coordinates", AddressOf WaterMeshHasUpwardWinding)
        Run("water resolves swept sphere contact against the displaced surface", AddressOf WaterDetectsActualSphereContact)
        Run("water bounds inputs and supports cancellation", AddressOf WaterRejectsInvalidAndCanceledRequests)
        If failures.Count > 0 Then Throw New Exception(String.Join(Environment.NewLine, failures))
        Console.WriteLine($"PASS {passes} physics regression cases without a graphics device")
    End Sub

    Private Sub WaterSpreadsAndDamps()
        Dim pool As New WaterHeightfield(impacts:={New WaterSurfaceImpact(0, 0.5F, 0.5F, 0.12F, 1.0F)})
        Check(pool.IsResting AndAlso pool.SampleHeight(0.5F, 0.5F) = 0, "unstepped water is flat")
        pool.Seek(1)
        Check(pool.SampleHeight(0.5F, 0.5F) > 0 AndAlso pool.SampleHeight(0.75F, 0.5F) = 0, "impact must begin locally")
        pool.Seek(40)
        Dim left = pool.SampleHeight(0.25F, 0.5F)
        Dim right = pool.SampleHeight(0.75F, 0.5F)
        Dim front = pool.SampleHeight(0.5F, 0.25F)
        Check(Math.Abs(left) > 0.0001, "the wave did not reach water beyond the impact radius")
        Near(left, right, 0.000001, "radial impulse left/right")
        Near(left, front, 0.000001, "radial impulse X/Z")
        pool.Seek(WaterHeightfield.TickAt(120))
        Check(pool.IsResting AndAlso pool.SampleHeight(0.5F, 0.5F) = 0, "damping did not settle the pool")
    End Sub

    Private Sub WaterReflectsAndConservesMean()
        Dim pool As New WaterHeightfield(64, 1.25F, 0.75F, {New WaterSurfaceImpact(0, 0.06F, 0.5F, 0.08F, -1.0F)})
        pool.Seek(34)
        Dim edge = pool.GetVertex(0, 32)
        Check(Math.Abs(edge.Position.Y) > 0.00001, "a reflecting edge should move vertically")
        Near(edge.Normal.X, 0, 0.000001, "wall-normal slope")
        For Each tick In {34L, 360L, 1440L}
            pool.Seek(tick)
            Dim sum = 0.0
            For row = 0 To pool.GridSize - 1
                For column = 0 To pool.GridSize - 1
                    Dim point = pool.GetVertex(column, row)
                    Check(Single.IsFinite(point.Position.Y) AndAlso Math.Abs(point.Position.Y) < 0.1F, "edge impulse destabilized the surface")
                    sum += point.Position.Y
                Next
            Next
            Near(sum, 0, 0.00025, "height sum across the closed pool")
        Next
    End Sub

    Private Sub WaterSeeksDeterministically()
        Dim impacts = {New WaterSurfaceImpact(800, 0.1F, 0.82F, 0.15F, 0.65F),
                       New WaterSurfaceImpact(19, 0.71F, 0.39F, 0.13F, -0.8F),
                       New WaterSurfaceImpact(800, 0.5F, 0.41F, 0.19F, -0.8F),
                       New WaterSurfaceImpact(2500000, 0.1F, 0.1F, 0.08F, 1.0F)}
        Dim sequential As New WaterHeightfield(21, impacts:=impacts)
        Dim randomAccess As New WaterHeightfield(21, impacts:=impacts)
        For tick = 0L To 1500L Step 3
            sequential.Seek(tick)
        Next
        For Each tick In {2500040L, 810L, 245L, 143L, 1500L}
            randomAccess.Seek(tick)
        Next
        For row = 0 To sequential.GridSize - 1
            For column = 0 To sequential.GridSize - 1
                Dim expected = sequential.GetVertex(column, row)
                Dim actual = randomAccess.GetVertex(column, row)
                Check(actual.Position = expected.Position AndAlso actual.Normal = expected.Normal, "seek order changed a vertex")
            Next
        Next
        randomAccess.Seek(72576000)
        Check(randomAccess.IsResting AndAlso randomAccess.CurrentTick = 72576000, "long silent intervals must advance to the target")
        randomAccess.Seek(19)
        Check(randomAccess.SampleHeight(0.71F, 0.39F) = 0, "an impact should affect the following state")
        randomAccess.Seek(20)
        Check(randomAccess.SampleHeight(0.71F, 0.39F) < 0, "an impact must be applied once at its declared tick")
    End Sub

    Private Sub WaterMeshHasUpwardWinding()
        Dim pool As New WaterHeightfield(9)
        Dim indices = pool.CreateIndices()
        Check(indices.Length = 8 * 8 * 6, "unexpected mesh topology")
        For offset = 0 To indices.Length - 1 Step 3
            Dim a = pool.GetVertex(indices(offset) Mod pool.GridSize, indices(offset) \ pool.GridSize)
            Dim b = pool.GetVertex(indices(offset + 1) Mod pool.GridSize, indices(offset + 1) \ pool.GridSize)
            Dim c = pool.GetVertex(indices(offset + 2) Mod pool.GridSize, indices(offset + 2) \ pool.GridSize)
            Check(NumericsVector.Cross(b.Position - a.Position, c.Position - a.Position).Y > 0, "water triangles are not wound upward")
            Check(a.Normal = NumericsVector.UnitY, "flat-water normal must be up")
        Next
        Check(pool.GetVertex(8, 8).Position = New NumericsVector(1, 0, 1), "last vertex is outside the unit pool")
    End Sub

    Private Sub WaterDetectsActualSphereContact()
        Dim rising As New WaterHeightfield(21, impacts:={New WaterSurfaceImpact(0, 0.5F, 0.5F, 0.12F, 1.0F)})
        Dim impact As WaterSurfaceImpact
        rising.Seek(3)
        Dim height = rising.SampleHeight(0.5F, 0.5F)
        Check(height > 0.003, "test requires a raised water surface")
        Dim above As New NumericsVector(0.5F, 0.1F + height + 0.01F, 0.5F)
        Dim below As New NumericsVector(0.5F, 0.1F + height - 0.002F, 0.5F)
        Check(rising.TryCreateSphereImpact(above, below, 0.1F, 3, impact), "descending sphere missed displaced water")
        Check(impact.Tick = 3 AndAlso impact.Strength < 0, "sphere impulse is not recorded at contact")
        Dim flat As New WaterHeightfield(21)
        flat.Seek(3)
        Check(Not flat.TryCreateSphereImpact(above, below, 0.1F, 3, impact), "sphere that stayed above flat water should not hit it")
        Check(flat.TryCreateSphereImpact(New NumericsVector(0.1F, 0.4F, 0.5F), New NumericsVector(0.9F, -0.4F, 0.5F), 0.1F, 3, impact), "swept collision missed a fast drop")
        Near(impact.X, 0.4, 0.0001, "swept sphere contact X")
        Check(Not flat.TryCreateSphereImpact(New NumericsVector(1.1F, 0.4F, 0.5F), New NumericsVector(1.1F, -0.4F, 0.5F), 0.1F, 3, impact), "outside drop reported contact")
        Check(Not flat.TryCreateSphereImpact(below, above, 0.1F, 3, impact), "upward sphere should not record a drop")
    End Sub

    Private Sub WaterRejectsInvalidAndCanceledRequests()
        ExpectArgumentError(Sub() GC.KeepAlive(New WaterHeightfield(65)))
        ExpectArgumentError(Sub() GC.KeepAlive(New WaterHeightfield(waveSpeed:=Single.NaN)))
        ExpectArgumentError(Sub() GC.KeepAlive(New WaterHeightfield(damping:=0)))
        ExpectArgumentError(Sub() GC.KeepAlive(New WaterSurfaceImpact(0, -0.2F, 0.5F, 0.1F, 1.0F)))
        ExpectArgumentError(Sub() GC.KeepAlive(New WaterHeightfield(impacts:={New WaterSurfaceImpact()})))
        ExpectArgumentError(Sub() GC.KeepAlive(New WaterHeightfield(impacts:=Enumerable.Repeat(New WaterSurfaceImpact(0, 0.5F, 0.5F, 0.1F, 1.0F), 33))))
        ExpectArgumentError(Sub() GC.KeepAlive(WaterHeightfield.TickAt(Double.PositiveInfinity)))
        Check(WaterHeightfield.TickAt(25.0 / 30) = 100, "frame time should align with the fixed tick grid")

        Dim pool As New WaterHeightfield()
        Using cancellation As New Threading.CancellationTokenSource()
            cancellation.Cancel()
            Try
                pool.Seek(120, cancellation.Token)
                Throw New Exception("canceled seek returned normally")
            Catch ex As OperationCanceledException
                Check(pool.CurrentTick = 0, "canceled seek changed the current state")
            End Try
        End Using
        pool.Seek(120)
        Check(pool.CurrentTick = 120, "pool is unusable after cancellation")
    End Sub

    Private Sub ExpectArgumentError(action As Action)
        Try
            action()
        Catch ex As ArgumentOutOfRangeException
            Return
        End Try
        Throw New Exception("expected argument validation to reject the input")
    End Sub

    Private Sub Run(name As String, action As Action)
        Try
            action()
            passes += 1
            Console.WriteLine("PASS " & name)
        Catch ex As Exception
            Dim failure = "FAIL " & name & ": " & ex.Message
            failures.Add(failure)
            Console.WriteLine(failure)
        End Try
    End Sub

    Private Function MakeBody(x As Single, y As Single, Optional width As Single = 10, Optional height As Single = 10) As PhysicsBody
        Return New PhysicsBody With {.Position = New Vector3(x, y, 0), .Size = New Vector3(width, height, 10), .CollisionEnabled = True, .GravityEnabled = False, .Friction = 0, .Bounce = 0}
    End Function

    Private Function MakeWorld(ParamArray bodies As PhysicsBody()) As PhysicsWorld
        Dim result As New PhysicsWorld()
        For Each item In bodies
            result.Bodies.Add(item)
        Next
        Return result
    End Function

    Private Sub Simulate(world As PhysicsWorld, seconds As Double, Optional rate As Integer = 120)
        For frame = 1 To CInt(seconds * rate)
            world.Advance(1.0 / rate)
        Next
    End Sub

    Private Sub Check(condition As Boolean, message As String)
        If Not condition Then Throw New Exception(message)
    End Sub

    Private Sub Near(actual As Double, expected As Double, tolerance As Double, message As String)
        Check(Double.IsFinite(actual) AndAlso Math.Abs(actual - expected) <= tolerance, $"{message}: expected {expected}, got {actual}")
    End Sub

    Private Sub Finite(body As PhysicsBody)
        For Each value In {body.Position.X, body.Position.Y, body.Position.Z, body.Velocity.X, body.Velocity.Y, body.Velocity.Z}
            Check(Single.IsFinite(value), "A position or velocity became non-finite")
        Next
    End Sub

    Private Function Overlap(a As PhysicsBody, b As PhysicsBody, Optional tolerance As Single = 0.005F) As Boolean
        Return a.Position.X < b.Position.X + b.Size.X - tolerance AndAlso a.Position.X + a.Size.X > b.Position.X + tolerance AndAlso
               a.Position.Y < b.Position.Y + b.Size.Y - tolerance AndAlso a.Position.Y + a.Size.Y > b.Position.Y + tolerance AndAlso
               (Not a.Is3D OrElse (a.Position.Z < b.Position.Z + b.Size.Z - tolerance AndAlso a.Position.Z + a.Size.Z > b.Position.Z + tolerance))
    End Function

    Private Sub MassImpulse()
        Dim light = MakeBody(0, 0), heavy = MakeBody(0, 0)
        light.Mass = 1 : heavy.Weight = 4
        light.AddImpulse(New Vector3(80, 0, 0)) : heavy.AddImpulse(New Vector3(80, 0, 0))
        Near(light.Velocity.X, 80, 0.0001, "light velocity")
        Near(heavy.Velocity.X, 20, 0.0001, "heavy velocity")
        heavy.Mass = 0
        Check(Double.IsFinite(heavy.Mass) AndAlso heavy.Mass > 0, "zero mass was not sanitized")
    End Sub

    Private Sub GravityGate()
        Dim disabled = MakeBody(0, 0), falling = MakeBody(50, 0), fixed = MakeBody(100, 0)
        disabled.Gravity = New Vector3(0, 100, 0)
        falling.Gravity = disabled.Gravity : falling.GravityEnabled = True
        fixed.Gravity = disabled.Gravity : fixed.GravityEnabled = True : fixed.IsStatic = True
        Simulate(MakeWorld(disabled, falling, fixed), 1)
        Near(disabled.Position.Y, 0, 0.0001, "disabled gravity moved body")
        Check(falling.Position.Y > 40 AndAlso falling.Position.Y < 60, "enabled gravity did not accelerate")
        Near(fixed.Position.Y, 0, 0.0001, "static body moved")
    End Sub

    Private Sub ParentOptIn()
        For Each parentEnabled In {False, True}
            For Each childEnabled In {False, True}
                Dim parent = MakeBody(0, 0, 100, 100), child = MakeBody(95, 20)
                parent.IsStatic = True : parent.CollisionEnabled = parentEnabled
                child.Parent = parent : child.CollisionEnabled = childEnabled
                MakeWorld(parent, child).Advance(1.0 / 120)
                Near(child.Position.X, If(parentEnabled AndAlso childEnabled, 90, 95), 0.01, "containment opt-in mismatch")
            Next
        Next
    End Sub

    Private Sub NearestExit()
        Dim obstacle = MakeBody(0, 0, 20, 20), embedded = MakeBody(8, 8, 2, 2)
        obstacle.IsStatic = True
        MakeWorld(obstacle, embedded).Advance(1.0 / 120)
        Check(Not Overlap(obstacle, embedded), "contained body still overlaps")
        Near(Vector3.Distance(embedded.Position, New Vector3(8, 8, 0)), 10, 0.02, "separation was not the nearest exit")
        Near(embedded.Velocity.Length(), 0, 0.0001, "overlap correction added energy")
    End Sub

    Private Sub CoincidentBodies()
        Dim a = MakeBody(0, 0), b = MakeBody(0, 0)
        MakeWorld(a, b).Advance(1.0 / 120)
        Check(Not Overlap(a, b), "coincident bodies still overlap")
        Near((a.Position + b.Position).Length(), 0, 0.01, "equal masses moved their center of mass")
        Near(a.Velocity.Length() + b.Velocity.Length(), 0, 0.0001, "projection created velocity")
    End Sub

    Private Sub ElasticCollision()
        Dim a = MakeBody(0, 0), b = MakeBody(10, 0)
        a.Velocity = New Vector3(60, 0, 0) : a.Bounce = 1 : b.Bounce = 1
        Dim world = MakeWorld(a, b)
        Simulate(world, 0.1)
        Near(a.Velocity.X + b.Velocity.X, 60, 0.1, "momentum changed")
        Check(a.Velocity.X < 1 AndAlso b.Velocity.X > 59, "elastic equal masses did not exchange velocity")
    End Sub

    Private Sub InelasticCollision()
        Dim wall = MakeBody(10, 0), moving = MakeBody(0, 0)
        wall.IsStatic = True : moving.Velocity = New Vector3(60, 0, 0)
        Simulate(MakeWorld(wall, moving), 0.1)
        Near(moving.Velocity.X, 0, 0.01, "zero bounce retained normal velocity")
        Check(moving.Position.X + moving.Size.X <= wall.Position.X + 0.01, "body penetrated wall")
    End Sub

    Private Function SlideVelocity(friction As FrictionType) As Single
        Dim floor = MakeBody(-1000, 100, 2000, 20), moving = MakeBody(0, 90)
        floor.IsStatic = True : floor.FrictionType = friction
        moving.FrictionType = friction : moving.GravityEnabled = True : moving.Gravity = New Vector3(0, 100, 0)
        moving.Velocity = New Vector3(100, 0, 0)
        Simulate(MakeWorld(floor, moving), 0.5)
        Return Math.Abs(moving.Velocity.X)
    End Function

    Private Sub FrictionPresets()
        Dim ice = SlideVelocity(FrictionType.Ice), sandpaper = SlideVelocity(FrictionType.Sandpaper)
        Check(ice > sandpaper + 5, $"preset friction did not affect sliding: ice={ice}, sandpaper={sandpaper}")
        For Each kind In {FrictionType.Ice, FrictionType.Water, FrictionType.Snow, FrictionType.Sand, FrictionType.Rock, FrictionType.Sandpaper}
            Dim body = MakeBody(0, 0)
            body.FrictionType = kind
            Check(Double.IsFinite(body.Friction) AndAlso body.Friction >= 0, "invalid preset friction")
        Next
    End Sub

    Private Sub GeometryFriction()
        Dim compact = MakeBody(0, 0), rough = MakeBody(0, 0)
        compact.GeometryVertices.Add(New Vector3(0, 0, 0))
        compact.GeometryVertices.Add(New Vector3(10, 0, 0))
        compact.GeometryVertices.Add(New Vector3(10, 10, 0))
        compact.GeometryVertices.Add(New Vector3(0, 10, 0))
        For Each p In {New Vector3(0, 0, 0), New Vector3(10, 0, 0), New Vector3(6, 2, 0), New Vector3(10, 4, 0), New Vector3(6, 6, 0), New Vector3(10, 10, 0), New Vector3(0, 10, 0)}
            rough.GeometryVertices.Add(p)
        Next
        compact.FrictionType = FrictionType.Geometry : rough.FrictionType = FrictionType.Geometry
        Check(Math.Abs(compact.GetFriction() - rough.GetFriction()) > 0.001, "geometry coefficient ignored contour")
    End Sub

    Private Sub FrameRates()
        Dim a = MakeBody(0, 0), b = MakeBody(0, 0)
        a.GravityEnabled = True : b.GravityEnabled = True
        a.Velocity = New Vector3(12, 0, 0) : b.Velocity = a.Velocity
        Simulate(MakeWorld(a), 2, 30) : Simulate(MakeWorld(b), 2, 120)
        Near(Vector3.Distance(a.Position, b.Position), 0, 0.02, "frame-rate dependent position")
        Near(Vector3.Distance(a.Velocity, b.Velocity), 0, 0.02, "frame-rate dependent velocity")
    End Sub

    Private Sub ThinWall()
        Dim wall = MakeBody(100, -100, 2, 300), moving = MakeBody(0, 0)
        wall.IsStatic = True : moving.Velocity = New Vector3(10000, 0, 0)
        MakeWorld(wall, moving).Advance(1.0 / 30)
        Check(moving.Position.X + moving.Size.X <= 100.02, $"fast body tunneled to {moving.Position.X}")
    End Sub

    Private Sub ThickWall()
        Dim wall = MakeBody(20, -100, 100, 300), moving = MakeBody(0, 0)
        wall.IsStatic = True : moving.Velocity = New Vector3(10000, 0, 0)
        MakeWorld(wall, moving).Advance(1.0 / 120)
        Check(moving.Position.X + moving.Size.X <= 20.02, $"embedded endpoint escaped the far wall face to {moving.Position.X}")
    End Sub

    Private Sub FastCorridor()
        Dim leftWall = MakeBody(0, -100, 2, 300), rightWall = MakeBody(22, -100, 2, 300), moving = MakeBody(8, 0)
        leftWall.IsStatic = True : rightWall.IsStatic = True
        leftWall.Bounce = 1 : rightWall.Bounce = 1 : moving.Bounce = 1
        moving.Velocity = New Vector3(10000, 0, 0)
        Dim world = MakeWorld(leftWall, rightWall, moving)
        For frame = 1 To 120
            world.Advance(1.0 / 120)
            Finite(moving)
            Check(moving.Position.X >= 1.99 AndAlso moving.Position.X + moving.Size.X <= 22.01, $"successive impacts tunneled out of corridor to {moving.Position.X}")
            Check(moving.Velocity.Length() <= 10000.1, "successive impacts injected energy")
        Next
    End Sub

    Private Sub FastCorner()
        Dim wall = MakeBody(100, -100, 2, 300), floor = MakeBody(-100, 100, 300, 2), moving = MakeBody(0, 0)
        wall.IsStatic = True : floor.IsStatic = True
        moving.Velocity = New Vector3(10000, 10000, 0)
        MakeWorld(wall, floor, moving).Advance(1.0 / 30)
        Check(moving.Position.X + moving.Size.X <= 100.02 AndAlso moving.Position.Y + moving.Size.Y <= 100.02, $"corner CCD missed a face at {moving.Position}")
        Finite(moving)
    End Sub

    Private Sub ParentValidSeparation()
        Dim parent = MakeBody(0, 0, 100, 100), obstacle = MakeBody(0, 0, 30, 100), moving = MakeBody(5, 40, 20, 20)
        parent.IsStatic = True : obstacle.IsStatic = True
        obstacle.Parent = parent : moving.Parent = parent
        MakeWorld(parent, obstacle, moving).ResolveOverlaps()
        Near(moving.Position.X, 30, 0.01, "nearest unconstrained exit fought parent containment")
        Check(Not Overlap(obstacle, moving), "parent-valid projection retained overlap")
    End Sub

    Private Sub KinematicSweep()
        Dim wall = MakeBody(100, -100, 2, 300), moving = MakeBody(0, 0)
        wall.IsStatic = True : moving.IsKinematic = True
        Dim world = MakeWorld(wall, moving)
        world.MoveBody(moving, New Vector3(200, 0, 0), 1.0 / 60)
        Check(moving.Position.X + moving.Size.X <= 100.02, $"pointer jump tunneled through a wall to {moving.Position.X}")
        Finite(moving)
    End Sub

    Private Sub ThreeDSeparation()
        Dim a = MakeBody(0, 0), b = MakeBody(0, 0)
        a.Is3D = True : b.Is3D = True : a.IsStatic = True
        b.Position = New Vector3(0, 0, 9)
        MakeWorld(a, b).Advance(1.0 / 120)
        Near(b.Position.Z, 10, 0.01, "3D did not choose Z exit")
        Near(b.Position.X, 0, 0.001, "3D changed unnecessary X")
    End Sub

    Private Sub MixedDimensions()
        Dim a = MakeBody(0, 0), b = MakeBody(0, 0)
        b.Is3D = True
        MakeWorld(a, b).Advance(1.0 / 120)
        Near(a.Position.Length() + b.Position.Length(), 0, 0.0001, "mixed dimensions collided")
    End Sub

    Private Sub DimensionInheritance()
        Dim parent = MakeBody(0, 0, 100, 100), child = MakeBody(1, 2)
        parent.CollisionEnabled = False : parent.Velocity = New Vector3(120, 0, 0)
        child.Is3D = True : child.Position = New Vector3(1, 2, 3) : child.Parent = parent
        MakeWorld(parent, child).Advance(1.0 / 120)
        Near(parent.Position.X, 1, 0.001, "2D parent did not move")
        Near(Vector3.Distance(child.Position, New Vector3(1, 2, 3)), 0, 0.001, "2D pixels changed 3D model coordinates")
    End Sub

    Private Sub ParentMovement()
        Dim parent = MakeBody(0, 0, 100, 100), child = MakeBody(20, 20)
        parent.CollisionEnabled = False : parent.Velocity = New Vector3(120, 0, 0)
        child.Parent = parent
        MakeWorld(parent, child).Advance(1.0 / 120)
        Near(child.Position.X, 21, 0.001, "2D child lost inherited movement")
    End Sub

    Private Sub ThreeDCorridor()
        Dim nearWall = MakeBody(-100, -100, 300, 300), farWall = MakeBody(-100, -100, 300, 300), moving = MakeBody(0, 0)
        nearWall.Is3D = True : farWall.Is3D = True : moving.Is3D = True
        nearWall.Size = New Vector3(300, 300, 2) : farWall.Size = nearWall.Size
        farWall.Position = New Vector3(-100, -100, 22)
        moving.Position = New Vector3(0, 0, 8)
        nearWall.IsStatic = True : farWall.IsStatic = True
        nearWall.Bounce = 1 : farWall.Bounce = 1 : moving.Bounce = 1
        moving.Velocity = New Vector3(0, 0, 10000)
        Dim world = MakeWorld(nearWall, farWall, moving)
        For frame = 1 To 120
            world.Advance(1.0 / 120)
            Finite(moving)
            Check(moving.Position.Z >= 1.99 AndAlso moving.Position.Z + moving.Size.Z <= 22.01, $"3D impacts escaped the Z corridor to {moving.Position.Z}")
        Next
    End Sub

    Private Sub SeparateParents()
        Dim a = MakeBody(0, 0), b = MakeBody(0, 0)
        a.Parent = MakeBody(-100, -100, 1000, 1000) : b.Parent = MakeBody(-100, -100, 1000, 1000)
        MakeWorld(a, b).Advance(1.0 / 120)
        Near(a.Position.Length() + b.Position.Length(), 0, 0.0001, "unrelated parent groups collided")
    End Sub

    Private Sub RestingContact()
        Dim floor = MakeBody(-100, 100, 1000, 20), moving = MakeBody(0, 0)
        floor.IsStatic = True : moving.GravityEnabled = True : moving.Bounce = 0.5
        Simulate(MakeWorld(floor, moving), 10)
        Check(Not Overlap(floor, moving), "resting body penetrated floor")
        Near(moving.Position.Y, 90, 0.1, "body did not settle")
        Check(moving.Velocity.Length() < 2, "resting contact kept bouncing")
    End Sub

    Private Sub RestingStack()
        Dim floor = MakeBody(-100, 100, 1000, 20), boxes As New List(Of PhysicsBody)
        floor.IsStatic = True
        For i = 0 To 4
            Dim box = MakeBody(0, 90 - i * 10)
            box.GravityEnabled = True
            boxes.Add(box)
        Next
        Dim world = MakeWorld(boxes.Concat({floor}).ToArray())
        Simulate(world, 10)
        For Each box In boxes
            Finite(box)
            Check(box.Position.Y <= 90.1 AndAlso box.Position.Y > -100, "stack escaped its plausible range")
            Check(box.Velocity.Length() < 10, $"resting stack built up velocity at {box.Position}: {box.Velocity}")
        Next
        For i = 0 To boxes.Count - 2
            Check(Not Overlap(boxes(i), boxes(i + 1), 0.1F), "resting stack retained material overlap")
        Next
    End Sub

    Private Sub OverlapStress()
        Dim parent = MakeBody(0, 0, 300, 300)
        parent.IsStatic = True
        Dim world = MakeWorld(parent), random As New Random(4123), boxes As New List(Of PhysicsBody)
        For i = 0 To 31
            Dim box = MakeBody(140 + CSng(random.NextDouble() * 20), 140 + CSng(random.NextDouble() * 20), 8, 8)
            box.Parent = parent : box.Bounce = 0.9 : box.Mass = 0.1 + random.NextDouble() * 10
            box.Velocity = New Vector3(CSng(random.NextDouble() * 100 - 50), CSng(random.NextDouble() * 100 - 50), 0)
            world.Bodies.Add(box) : boxes.Add(box)
        Next
        For frame = 1 To 1200
            world.Advance(1.0 / 120)
            For Each box In boxes
                Finite(box)
                Check(box.Position.X >= -0.1 AndAlso box.Position.Y >= -0.1 AndAlso box.Position.X + box.Size.X <= 300.1 AndAlso box.Position.Y + box.Size.Y <= 300.1, "body escaped parent")
                Check(box.Velocity.Length() < 150, "collision amplified velocity without external force")
            Next
        Next
    End Sub

    Private Sub DenseOverlap()
        Dim parent = MakeBody(0, 0, 300, 300), boxes As New List(Of PhysicsBody)
        parent.IsStatic = True
        For i = 0 To 15
            Dim box = MakeBody(146, 146, 8, 8)
            box.Parent = parent
            boxes.Add(box)
        Next
        Dim world = MakeWorld(boxes.Concat({parent}).ToArray())
        Simulate(world, 2)
        For i = 0 To boxes.Count - 1
            Finite(boxes(i))
            Near(boxes(i).Velocity.Length(), 0, 0.0001, "dense overlap projection created energy")
            For j = i + 1 To boxes.Count - 1
                Check(Not Overlap(boxes(i), boxes(j), 0.02F), $"dense initial overlap persisted for pair {i},{j}")
            Next
        Next
    End Sub

    Private Sub InvalidState()
        Dim moving = MakeBody(0, 0)
        moving.Position = New Vector3(Single.NaN, Single.PositiveInfinity, 0)
        moving.Velocity = New Vector3(Single.NegativeInfinity, Single.NaN, 0)
        moving.Mass = Double.NaN : moving.Bounce = Double.PositiveInfinity : moving.Friction = Double.NaN
        moving.Size = New Vector3(-1, Single.NaN, 10)
        Dim world = MakeWorld(moving)
        world.Advance(Double.NaN) : world.Advance(Double.PositiveInfinity) : world.Advance(-1)
        world.Advance(1.0 / 120)
        Finite(moving)
        Check(moving.Size.X >= 0 AndAlso Single.IsFinite(moving.Size.Y), "invalid size escaped sanitation")
    End Sub

    Private Sub OversizedBody()
        Dim parent = MakeBody(20, 30, 10, 10), moving = MakeBody(0, 0, 20, 20)
        parent.IsStatic = True : moving.Parent = parent : moving.Velocity = New Vector3(100, 100, 0)
        Dim world = MakeWorld(parent, moving)
        Simulate(world, 1)
        Finite(moving)
        Check(moving.Position.X >= 10 AndAlso moving.Position.X <= 20 AndAlso moving.Position.Y >= 20 AndAlso moving.Position.Y <= 30, "oversized body placement is unbounded")
        Check(moving.Velocity.Length() < 1, "oversized bounds keep injecting bounce")
    End Sub

    Private Sub ScalarImpulse()
        Dim existingParameter As Double = 0
        Dim scalar As New PhysicsScalar(Function() existingParameter, Sub(value) existingParameter = value) With {.Mass = 4, .Friction = 0, .Damping = 0}
        scalar.AddImpulse(80)
        For i = 1 To 120
            scalar.Advance(1.0 / 120)
        Next
        Near(existingParameter, 20, 0.1, "scalar did not drive external value with weighted inertia")
    End Sub

    Private Sub StiffScalar()
        For Each frameRate As Integer In {30, 120}
            Dim parameter As Double = 100
            Dim scalar As New PhysicsScalar(Function() parameter, Sub(value) parameter = value) With {.Mass = 0.0001, .Target = 0, .Stiffness = 1000000, .Damping = 12}
            For frame = 1 To frameRate * 2
                scalar.Advance(1.0 / frameRate)
                Check(Double.IsFinite(parameter) AndAlso Math.Abs(parameter) <= 100.01, "stiff scalar created unbounded position")
                Check(Double.IsFinite(scalar.Velocity), "stiff scalar created nonfinite velocity")
            Next
            Near(parameter, 0, 0.01, "stiff spring did not settle")
        Next
    End Sub

    Private Sub ScalarBounds()
        Dim parameter As Double = 0.5
        Dim scalar As New PhysicsScalar(Function() parameter, Sub(value) parameter = value) With {.Minimum = 0, .Maximum = 1, .Bounce = 0.5, .Damping = 0}
        scalar.AddImpulse(1000)
        For i = 1 To 120
            scalar.Advance(1.0 / 120)
            Check(Double.IsFinite(parameter) AndAlso parameter >= 0 AndAlso parameter <= 1, "scalar escaped parameter range")
        Next
        parameter = Double.NaN
        scalar.Velocity = Double.PositiveInfinity
        scalar.Advance(0.01)
        Check(Double.IsFinite(parameter) AndAlso Double.IsFinite(scalar.Velocity), "scalar leaked invalid input")
    End Sub
End Module
