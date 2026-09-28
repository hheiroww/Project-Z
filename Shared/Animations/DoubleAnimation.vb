Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Animations.Easing

Namespace [Shared].Animations

    Public Class DoubleAnimation
        Inherits AnimationBase

        Public Shared Function Interpolate(value As Double, min As Double, max As Double, start As Double, [end] As Double) As Double
            Return start + ((([end] - start) / (max - min)) * (value - min))
        End Function

        Public Overrides Function Value(t As Double) As Object
            lastValue = Interpolate(easeFunction.Ease(Math.Max(0.0R, Math.Min(1.0R, t))),
                                    0.0R, 1.0R, CDbl([From]), CDbl([To]))
            Return lastValue
        End Function

        Public Sub New(EaseFunction As EaseFunction, [From] As Double, [To] As Double, Duration As TimeSpan, gameTime As GameTime)
            Me.Init(EaseFunction, [From], [To], Duration, gameTime, False)
        End Sub

        Public Sub New(EaseFunction As EaseFunction, [From] As Double, [To] As Double, Duration As TimeSpan, gameTime As GameTime, Autostart As Boolean)
            Me.Init(EaseFunction, [From], [To], Duration, gameTime, Autostart)
        End Sub

    End Class

End Namespace
