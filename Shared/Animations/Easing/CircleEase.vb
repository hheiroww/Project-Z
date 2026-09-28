Namespace [Shared].Animations.Easing

    Public Class CircleEase
        Inherits EaseFunction

        Public Overrides Function Ease(t As Double) As Double
            Select Case easeType
                Case Easing.EaseType.EaseIn
                    Return 1 - Math.Sqrt(1 - t ^ 2)
                Case Easing.EaseType.EaseOut
                    Return Math.Sqrt(1 - ((t - 1) ^ 2))
                Case Easing.EaseType.EaseInOut
                    If t < 0.5R Then Return (1 - Math.Sqrt(1 - ((2 * t) ^ 2))) / 2
                    Return (Math.Sqrt(1 - ((-2 * t + 2) ^ 2)) + 1) / 2
                Case Else
                    Return t
            End Select
        End Function

        Public Sub New(easeType As EaseType)
            MyBase.New(easeType)
        End Sub

    End Class

End Namespace
