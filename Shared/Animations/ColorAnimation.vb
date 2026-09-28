Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Animations.Easing

Namespace [Shared].Animations

    Public Class ColorAnimation
        Inherits AnimationBase

        Public Property InterpolateInLinearColorSpace As Boolean

        Public Sub New(EaseFunction As EaseFunction, [From] As Color, [To] As Color, Duration As TimeSpan, gameTime As GameTime)
            Me.Init(EaseFunction, From, [To], Duration, gameTime, False)
        End Sub

        Public Sub New(EaseFunction As EaseFunction, [From] As Color, [To] As Color, Duration As TimeSpan, gameTime As GameTime, Autostart As Boolean)
            Me.Init(EaseFunction, From, [To], Duration, gameTime, Autostart)
        End Sub

        Public Overrides Function Value(t As Double) As Object
            Dim progress = easeFunction.Ease(Math.Max(0.0R, Math.Min(1.0R, t)))
            lastValue = If(InterpolateInLinearColorSpace,
                           XamlColorInterpolation.Lerp(CType(From, Color), CType([To], Color), progress),
                           Color.Lerp(CType(From, Color), CType([To], Color), CSng(progress)))
            Return lastValue
        End Function

    End Class

End Namespace
