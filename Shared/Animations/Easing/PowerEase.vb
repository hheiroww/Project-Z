Namespace [Shared].Animations.Easing

    Public Class PowerEase
        Inherits EaseFunction

        Public Property Power As Double = 2

        Public Overrides Function Ease(t As Double) As Double
            Select Case easeType
                Case Easing.EaseType.EaseIn
                    Return t ^ Power
                Case Easing.EaseType.EaseOut
                    Return 1 - ((1 - t) ^ Power)
                Case Easing.EaseType.EaseInOut
                    If t < 0.5R Then Return Math.Pow(2, Power - 1) * Math.Pow(t, Power)
                    Return 1 - Math.Pow(-2 * t + 2, Power) / 2
                Case Else
                    Return t
            End Select
        End Function

        Public Sub New(easeType As EaseType)
            MyBase.New(easeType)
        End Sub

        Public Sub New(easeType As EaseType, Power As Double)
            MyBase.New(easeType)
            Me.Power = Power
        End Sub

    End Class

End Namespace
