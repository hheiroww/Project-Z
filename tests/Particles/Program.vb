Imports System.IO
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.Particles

Module Program
    Private assertions As Integer
    Sub Main()
        Dim simulation As New ParticleSimulation(3, 42) With {.EmissionRate = 0, .InitialVelocity = Vector3.Zero, .VelocitySpread = Vector3.Zero, .GravityEnabled = True, .Gravity = New Vector3(0, 10, 0)}
        Check(simulation.Emit(20) = 3 AndAlso simulation.Count = 3, "Capacity bounds")
        simulation.Advance(0.2)
        Check(Math.Abs(simulation(0).Position.Y - 0.2) < 0.00001 AndAlso Math.Abs(simulation(0).Velocity.Y - 2) < 0.00001, "Analytical gravity integration")
        simulation.PhysicsEnabled = False
        simulation.Advance(0.2)
        Check(Math.Abs(simulation(0).Position.Y - 0.6) < 0.00001, "Gravity requires physics")
        simulation.Clear()
        Check(simulation.Count = 0, "Clear")
        simulation.ParticleLifetime = 0.1
        simulation.Emit()
        simulation.Advance(0.2)
        Check(simulation.Count = 0, "Expired particles removed")
        simulation.EmissionRate = 100000
        simulation.Advance(1000000)
        Check(simulation.Count = 3, "Long stalls remain bounded")
        simulation.Advance(Double.NaN)
        Check(simulation.Count = 3, "Invalid elapsed time ignored")
        simulation.Clear() : simulation.InitialVelocity = New Vector3(Single.NaN, Single.PositiveInfinity, 123)
        simulation.VelocitySpread = New Vector3(Single.NaN)
        simulation.ParticleLifetime = Double.NaN
        simulation.Gravity = New Vector3(Single.NaN)
        simulation.ParticleSize = New Vector2(Single.PositiveInfinity)
        simulation.Emit()
        simulation.Advance(0.01)
        Check(Single.IsFinite(simulation(0).Position.X) AndAlso Single.IsFinite(simulation(0).Velocity.Y) AndAlso simulation(0).Position.Z = 0, "Finite 2D state")
        Dim one As New ParticleSimulation(10, 5) With {.EmissionRate = 0, .Is3D = True}
        Dim two As New ParticleSimulation(10, 5) With {.EmissionRate = 0, .Is3D = True}
        one.Emit(10) : two.Emit(10) : one.Advance(0.2) : two.Advance(0.2)
        Check(one(3).Position = two(3).Position, "Deterministic seed")
        Dim drag As New ParticleSimulation(1) With {.EmissionRate = 0, .InitialVelocity = New Vector3(10, 0, 0), .VelocitySpread = Vector3.Zero, .LinearDrag = 2}
        drag.Emit() : drag.Advance(0.25)
        Check(Math.Abs(drag(0).Velocity.X - 10 * Math.Exp(-0.5)) < 0.00001, "Analytical drag")
        Dim ends As Double() = {0.1, 0.4, 1.0}
        Check(ParticleSpriteAnimation.FrameIndexAt(ends, 0.1) = 1, "Exact frame boundary")
        Check(ParticleSpriteAnimation.FrameIndexAt(ends, 1.2) = 1, "Variable delay looping")
        Check(ParticleSpriteAnimation.FrameIndexAt(ends, 5, False) = 2, "Nonlooping last frame")
        Check(ParticleSpriteAnimation.FrameIndexAt(ends, Double.NaN) = 0, "Invalid frame time")
        TestGif()
        Console.WriteLine($"PASS: {assertions} particle/animation/GIF assertions")
    End Sub
    Private Sub TestGif()
        ' One black pixel with a valid clear/literal/end LZW sequence.
        Dim bytes As Byte() = {71, 73, 70, 56, 57, 97, 1, 0, 1, 0, 128, 0, 0, 0, 0, 0, 255, 255, 255,
            33, 249, 4, 0, 5, 0, 0, 0, 44, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 68, 1, 0, 59}
        Using stream As New MemoryStream(bytes)
            Dim frames = GifSpriteDecoder.Decode(stream)
            Check(frames.Count = 1 AndAlso frames(0).Pixels(0) = Color.Black AndAlso Math.Abs(frames(0).Seconds - 0.05) < 0.000001, "GIF palette and delay")
        End Using
        bytes(22) = 1 ' Transparency flag in the graphics control.
        Using stream As New MemoryStream(bytes)
            Check(GifSpriteDecoder.Decode(stream)(0).Pixels(0).A = 0, "GIF transparency")
        End Using
        Dim rejected As Boolean
        Try
            Using stream As New MemoryStream(New Byte() {0, 0, 0, 0, 0, 0})
                GifSpriteDecoder.Decode(stream)
            End Using
        Catch ex As InvalidDataException
            rejected = True
        End Try
        Check(rejected, "Malformed GIF rejected")
        Using stream = GifWithFrames(False)
            Dim frames = GifSpriteDecoder.Decode(stream)
            Check(frames.Count = 3 AndAlso frames(1).Pixels(3) = Color.Lime, "GIF overlay frame")
            Check(frames(2).Pixels(3) = Color.Red AndAlso frames(2).Pixels(0) = Color.Blue, "GIF restore-previous disposal and local palette")
        End Using
        Using stream = GifWithFrames(True)
            Dim frames = GifSpriteDecoder.Decode(stream)
            Check(frames(0).Pixels(0) = Color.Red AndAlso frames(0).Pixels(2) = Color.Lime AndAlso frames(0).Pixels(4) = Color.Blue AndAlso frames(0).Pixels(6) = Color.Black, "GIF interlaced row ordering")
        End Using
    End Sub
    Private Function GifWithFrames(interlaced As Boolean) As MemoryStream
        Dim stream As New MemoryStream
        Using writer As New BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen:=True)
            writer.Write(System.Text.Encoding.ASCII.GetBytes("GIF89a"))
            writer.Write(CUShort(2)) : writer.Write(CUShort(If(interlaced, 4, 2)))
            writer.Write(CByte(&H81)) : writer.Write(CByte(0)) : writer.Write(CByte(0))
            writer.Write(New Byte() {255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0})
            If interlaced Then
                GifFrame(writer, 0, 0, 2, 4, New Byte() {0, 0, 2, 2, 1, 1, 3, 3}, 0, &H40)
            Else
                GifFrame(writer, 0, 0, 2, 2, New Byte() {0, 0, 0, 0}, 0, 0)
                GifFrame(writer, 1, 1, 1, 1, New Byte() {1}, 3, 0)
                GifFrame(writer, 0, 0, 1, 1, New Byte() {0}, 0, &H80)
            End If
            writer.Write(CByte(&H3B))
        End Using
        stream.Position = 0
        Return stream
    End Function
    Private Sub GifFrame(writer As BinaryWriter, left As Integer, top As Integer, width As Integer, height As Integer, indices As Byte(), disposal As Integer, flags As Integer)
        writer.Write(New Byte() {&H21, &HF9, 4, CByte(disposal << 2), 10, 0, 0, 0, &H2C})
        writer.Write(CUShort(left)) : writer.Write(CUShort(top)) : writer.Write(CUShort(width)) : writer.Write(CUShort(height))
        writer.Write(CByte(flags))
        If (flags And &H80) <> 0 Then writer.Write(New Byte() {0, 0, 255, 0, 0, 0})
        ' Clear before each literal keeps this fixture's code width constant at three bits.
        Dim codes As New List(Of Integer)
        For Each index In indices
            codes.Add(4) : codes.Add(index)
        Next
        codes.Add(5)
        Dim packed((codes.Count * 3 + 7) \ 8 - 1) As Byte
        Dim offset = 0
        For Each code In codes
            For bit = 0 To 2
                If (code And (1 << bit)) <> 0 Then packed(offset >> 3) = CByte(packed(offset >> 3) Or (1 << (offset And 7)))
                offset += 1
            Next
        Next
        writer.Write(CByte(2)) : writer.Write(CByte(packed.Length)) : writer.Write(packed) : writer.Write(CByte(0))
    End Sub
    Private Sub Check(condition As Boolean, name As String)
        If Not condition Then Throw New Exception("FAILED: " & name)
        assertions += 1
    End Sub
End Module
