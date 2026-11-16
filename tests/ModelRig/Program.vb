Imports System.Numerics
Imports ProjectZ.Shared.Drawing.ThreeD

Module Program
    Sub Main()
        Dim joints = {
            New ModelJoint3D("root", -1, Matrix4x4.Identity),
            New ModelJoint3D("bend", 0, Matrix4x4.CreateTranslation(1, 0, 0)),
            New ModelJoint3D("tip", 1, Matrix4x4.CreateTranslation(1, 0, 0))}
        Dim rootMotion As New ModelJointTrack3D With {.Joint = 0,
            .Positions = {New ModelVectorKey3D(0, Vector3.Zero), New ModelVectorKey3D(2, New Vector3(2, 0, 0))},
            .Rotations = {New ModelRotationKey3D(0, Quaternion.Identity),
                          New ModelRotationKey3D(2, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2))}}
        Dim rig As New ModelRig3D(joints, {New ModelAnimation3D With {.Name = "Turn and move", .Duration = 2, .Tracks = {rootMotion}}})
        Dim middle = rig.Sample(1, 0).WorldTransforms(2).Translation
        Near(middle, New Vector3(1 + MathF.Sqrt(2), MathF.Sqrt(2), 0), "interpolated rotation drives the descendant")
        Near(rig.Sample(3, 0).WorldTransforms(2).Translation, middle, "loop uses the authored duration")
        Near(rig.Sample(3, 0, False).WorldTransforms(2).Translation, New Vector3(2, 2, 0), "one-shot motion holds at its endpoint")

        Dim pose = rig.Sample(0)
        Dim goal As New Vector3(1.2F, 0.8F, 0), pole As New Vector3(0, 0, 1)
        If Not pose.ApplyTwoBoneIk(0, 1, 2, goal, pole) Then Throw New Exception("Reachable IK goal did not solve.")
        Near(pose.WorldTransforms(2).Translation, goal, "reachable two-bone IK")
        Dim bend = pose.WorldTransforms(1).Translation
        If bend.Z <= 0 Then Throw New Exception("IK bent away from the selected pole.")
        If Math.Abs(bend.Length() - 1) > 0.0001 OrElse Math.Abs(Vector3.Distance(bend, goal) - 1) > 0.0001 Then Throw New Exception("Two-bone IK did not preserve the bone lengths.")

        pose = rig.Sample(0)
        pose.ApplyTwoBoneIk(0, 1, 2, New Vector3(10, 0, 0), pole)
        Near(pose.WorldTransforms(2).Translation, New Vector3(2, 0, 0), "out-of-range goal is clamped to the arm's length")
        pose = rig.Sample(0)
        pose.ApplyTwoBoneIk(0, 1, 2, goal, pole, 0)
        Near(pose.WorldTransforms(2).Translation, New Vector3(2, 0, 0), "zero IK influence keeps the original pose")
        Console.WriteLine("PASS rig hierarchy, interpolation, loop and endpoint sampling, two-bone goal, pole, reach bounds, and blend")
    End Sub

    Private Sub Near(actual As Vector3, expected As Vector3, label As String)
        If Vector3.Distance(actual, expected) > 0.0001 Then Throw New Exception(label & " expected " & expected.ToString() & " but got " & actual.ToString())
    End Sub
End Module
