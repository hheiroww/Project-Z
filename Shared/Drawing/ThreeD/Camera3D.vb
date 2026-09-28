Imports Microsoft.Xna.Framework

Namespace [Shared].Drawing.ThreeD

    Public Class Camera3D

        Public Property Position As Vector3 = New Vector3(0.0F, 0.0F, 4.0F)
        Public Property Target As Vector3 = Vector3.Zero
        Public Property Up As Vector3 = Vector3.Up
        Public Property FieldOfView As Single = MathHelper.PiOver4
        Public Property NearPlane As Single = 0.01F
        Public Property FarPlane As Single = 10000.0F

        Public Function CreateView() As Matrix
            Return Matrix.CreateLookAt(Position, Target, Up)
        End Function

        Public Function CreateProjection(aspectRatio As Single) As Matrix
            Dim safeAspect As Single = Math.Max(0.0001F, aspectRatio)
            Dim safeNear As Single = Math.Max(0.0001F, NearPlane)
            Dim safeFar As Single = Math.Max(safeNear + 0.0001F, FarPlane)
            Return Matrix.CreatePerspectiveFieldOfView(FieldOfView, safeAspect, safeNear, safeFar)
        End Function

    End Class

End Namespace
