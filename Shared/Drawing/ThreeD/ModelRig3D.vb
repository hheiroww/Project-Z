Imports System.Numerics

Namespace [Shared].Drawing.ThreeD
    Public NotInheritable Class ModelJoint3D
        Public ReadOnly Property Name As String
        Public ReadOnly Property Parent As Integer
        Public ReadOnly Property BindTransform As Matrix4x4

        Public Sub New(name As String, parent As Integer, bindTransform As Matrix4x4)
            Me.Name = name
            Me.Parent = parent
            Me.BindTransform = bindTransform
        End Sub
    End Class

    Public Structure ModelVectorKey3D
        Public ReadOnly Time As Double
        Public ReadOnly Value As Vector3

        Public Sub New(time As Double, value As Vector3)
            Me.Time = time
            Me.Value = value
        End Sub
    End Structure

    Public Structure ModelRotationKey3D
        Public ReadOnly Time As Double
        Public ReadOnly Value As Quaternion

        Public Sub New(time As Double, value As Quaternion)
            Me.Time = time
            Me.Value = value
        End Sub
    End Structure

    Public NotInheritable Class ModelJointTrack3D
        Public Property Joint As Integer
        Public Property Positions As ModelVectorKey3D() = Array.Empty(Of ModelVectorKey3D)()
        Public Property Rotations As ModelRotationKey3D() = Array.Empty(Of ModelRotationKey3D)()
        Public Property Scales As ModelVectorKey3D() = Array.Empty(Of ModelVectorKey3D)()

        Friend Function Sample(seconds As Double, bind As Matrix4x4) As Matrix4x4
            Dim scale As Vector3, translation As Vector3, orientation As Quaternion
            If Not Matrix4x4.Decompose(bind, scale, orientation, translation) Then Return bind
            scale = SampleVector(Scales, seconds, scale)
            translation = SampleVector(Positions, seconds, translation)
            If Rotations.Length > 0 Then
                Dim upper = UpperBound(Rotations.Length, seconds, Function(index) Rotations(index).Time)
                Dim earlier = Rotations(Math.Max(0, upper - 1)), later = Rotations(Math.Min(upper, Rotations.Length - 1))
                orientation = Quaternion.Normalize(Quaternion.Slerp(earlier.Value, later.Value, Fraction(seconds, earlier.Time, later.Time)))
            End If
            Return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(orientation) * Matrix4x4.CreateTranslation(translation)
        End Function

        Private Shared Function SampleVector(keys As ModelVectorKey3D(), seconds As Double, fallback As Vector3) As Vector3
            If keys.Length = 0 Then Return fallback
            Dim upper = UpperBound(keys.Length, seconds, Function(index) keys(index).Time)
            Dim earlier = keys(Math.Max(0, upper - 1)), later = keys(Math.Min(upper, keys.Length - 1))
            Return Vector3.Lerp(earlier.Value, later.Value, Fraction(seconds, earlier.Time, later.Time))
        End Function

        Private Shared Function Fraction(seconds As Double, first As Double, last As Double) As Single
            Return If(last <= first, 0.0F, CSng(Math.Clamp((seconds - first) / (last - first), 0, 1)))
        End Function

        Private Shared Function UpperBound(count As Integer, seconds As Double, timeAt As Func(Of Integer, Double)) As Integer
            Dim left = 0, right = count
            While left < right
                Dim middle = left + (right - left) \ 2
                If timeAt(middle) <= seconds Then
                    left = middle + 1
                Else
                    right = middle
                End If
            End While
            Return left
        End Function
    End Class

    Public NotInheritable Class ModelAnimation3D
        Public Property Name As String = "Animation"
        Public Property Duration As Double
        Public Property Tracks As ModelJointTrack3D() = Array.Empty(Of ModelJointTrack3D)()
    End Class

    Public NotInheritable Class ModelRig3D
        Private ReadOnly orderedJoints As ModelJoint3D()
        Public ReadOnly Property Joints As IReadOnlyList(Of ModelJoint3D)
        Public ReadOnly Property Animations As IReadOnlyList(Of ModelAnimation3D)

        Public Sub New(joints As IEnumerable(Of ModelJoint3D), Optional animations As IEnumerable(Of ModelAnimation3D) = Nothing)
            orderedJoints = joints.ToArray()
            For index = 0 To orderedJoints.Length - 1
                Dim joint = orderedJoints(index)
                If joint Is Nothing OrElse joint.Parent < -1 OrElse joint.Parent >= index Then
                    Throw New ArgumentException("Rig joints must be ordered with each parent before its children.", NameOf(joints))
                End If
            Next
            Me.Joints = Array.AsReadOnly(orderedJoints)
            Me.Animations = Array.AsReadOnly(If(animations, Array.Empty(Of ModelAnimation3D)()).ToArray())
        End Sub

        Public Function Sample(seconds As Double, Optional animationIndex As Integer = -1, Optional loopAnimation As Boolean = True) As ModelRigPose3D
            If Not Double.IsFinite(seconds) Then Throw New ArgumentOutOfRangeException(NameOf(seconds))
            Dim pose As New ModelRigPose3D(Me, orderedJoints.Select(Function(item) item.BindTransform).ToArray())
            If animationIndex = -1 Then Return pose
            If animationIndex < 0 OrElse animationIndex >= Animations.Count Then Throw New ArgumentOutOfRangeException(NameOf(animationIndex))
            Dim animation = Animations(animationIndex)
            Dim duration = Math.Max(0, animation.Duration)
            Dim time = If(loopAnimation AndAlso duration > 0, ((seconds Mod duration) + duration) Mod duration, Math.Clamp(seconds, 0, duration))
            For Each track In animation.Tracks
                If track.Joint >= 0 AndAlso track.Joint < orderedJoints.Length Then
                    pose.LocalTransforms(track.Joint) = track.Sample(time, orderedJoints(track.Joint).BindTransform)
                End If
            Next
            pose.UpdateWorld()
            Return pose
        End Function
    End Class

    Public NotInheritable Class ModelRigPose3D
        Public ReadOnly Property Rig As ModelRig3D
        Public ReadOnly Property LocalTransforms As Matrix4x4()
        Public ReadOnly Property WorldTransforms As Matrix4x4()

        Friend Sub New(rig As ModelRig3D, locals As Matrix4x4())
            Me.Rig = rig
            LocalTransforms = locals
            WorldTransforms = New Matrix4x4(locals.Length - 1) {}
            UpdateWorld()
        End Sub

        Public Sub UpdateWorld()
            For index = 0 To WorldTransforms.Length - 1
                Dim parent = Rig.Joints(index).Parent
                WorldTransforms(index) = If(parent < 0, LocalTransforms(index), LocalTransforms(index) * WorldTransforms(parent))
            Next
        End Sub

        ''' <summary>Solves a directly parented three-joint chain in model space. Target and pole are points.</summary>
        Public Function ApplyTwoBoneIk(root As Integer, bend As Integer, tip As Integer, target As Vector3,
                                       pole As Vector3, Optional weight As Single = 1) As Boolean
            If root < 0 OrElse bend < 0 OrElse tip < 0 OrElse root >= Rig.Joints.Count OrElse bend >= Rig.Joints.Count OrElse
                tip >= Rig.Joints.Count OrElse Rig.Joints(bend).Parent <> root OrElse Rig.Joints(tip).Parent <> bend Then
                Throw New ArgumentException("IK requires a direct root, bend, and tip joint chain.")
            End If
            If Not Finite(target) OrElse Not Finite(pole) OrElse Not Single.IsFinite(weight) OrElse weight < 0 OrElse weight > 1 Then
                Throw New ArgumentOutOfRangeException(NameOf(target))
            End If
            If weight = 0 Then Return False
            UpdateWorld()
            Dim start = WorldTransforms(root).Translation
            Dim middle = WorldTransforms(bend).Translation
            Dim finish = WorldTransforms(tip).Translation
            Dim firstLength = Vector3.Distance(start, middle), secondLength = Vector3.Distance(middle, finish)
            If firstLength < 0.000001F OrElse secondLength < 0.000001F Then Return False
            Dim goal = target - start
            Dim targetDistance = goal.Length()
            Dim direction = If(targetDistance > 0.000001F, goal / targetDistance, Vector3.Normalize(middle - start))
            Dim bendDirection = pole - start
            bendDirection -= direction * Vector3.Dot(bendDirection, direction)
            If bendDirection.LengthSquared() < 0.00000001F Then
                bendDirection = middle - start
                bendDirection -= direction * Vector3.Dot(bendDirection, direction)
            End If
            If bendDirection.LengthSquared() < 0.00000001F Then bendDirection = Perpendicular(direction)
            bendDirection = Vector3.Normalize(bendDirection)
            Dim distance = Math.Clamp(targetDistance, Math.Max(Math.Abs(firstLength - secondLength), 0.000001F), firstLength + secondLength)
            Dim along = (firstLength * firstLength - secondLength * secondLength + distance * distance) / (2 * distance)
            Dim elbow = start + direction * along + bendDirection * MathF.Sqrt(Math.Max(0, firstLength * firstLength - along * along))
            Dim endAt = start + direction * distance
            If Not TurnJoint(root, middle - start, elbow - start, weight) Then Return False
            middle = WorldTransforms(bend).Translation
            finish = WorldTransforms(tip).Translation
            Return TurnJoint(bend, finish - middle, endAt - middle, weight)
        End Function

        Private Function TurnJoint(joint As Integer, direction As Vector3, desired As Vector3, weight As Single) As Boolean
            If direction.LengthSquared() < 0.000000000001F OrElse desired.LengthSquared() < 0.000000000001F Then Return False
            Dim first = Vector3.Normalize(direction), last = Vector3.Normalize(desired)
            Dim cosine = Math.Clamp(Vector3.Dot(first, last), -1, 1)
            Dim rotation = If(cosine < -0.999999F, Quaternion.CreateFromAxisAngle(Perpendicular(first), MathF.PI),
                              Quaternion.Normalize(New Quaternion(Vector3.Cross(first, last), 1 + cosine)))
            rotation = Quaternion.Slerp(Quaternion.Identity, rotation, weight)
            Dim world = WorldTransforms(joint), position = world.Translation
            world = world * Matrix4x4.CreateTranslation(-position) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position)
            Dim parent = Rig.Joints(joint).Parent
            If parent >= 0 Then
                Dim inverse As Matrix4x4
                If Not Matrix4x4.Invert(WorldTransforms(parent), inverse) Then Return False
                world *= inverse
            End If
            LocalTransforms(joint) = world
            UpdateWorld()
            Return True
        End Function

        Private Shared Function Perpendicular(direction As Vector3) As Vector3
            Return Vector3.Normalize(Vector3.Cross(direction, If(Math.Abs(direction.Y) < 0.95F, Vector3.UnitY, Vector3.UnitX)))
        End Function

        Private Shared Function Finite(value As Vector3) As Boolean
            Return Single.IsFinite(value.X) AndAlso Single.IsFinite(value.Y) AndAlso Single.IsFinite(value.Z)
        End Function
    End Class
End Namespace
