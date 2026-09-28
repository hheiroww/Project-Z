Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Animations.Easing

Namespace [Shared].Animations

    Public MustInherit Class AnimationBase

        Public Event OnAnimationFinished(sender As Object)
        Public Property easeFunction As EaseFunction
        Public Property Duration As TimeSpan
        Public Property gameTime As GameTime
        Public Property [From] As Object
        Public Property [To] As Object
        Public Property lastValue As Object
        Public Property AutoRepeat As Boolean = False

        Public ReadOnly Property Running As Boolean
            Get
                Return _Running
            End Get
        End Property
        Private _Running As Boolean = False

        Public MustOverride Overloads Function Value(t As Double) As Object

        Public Overloads Function Value() As Object
            Return Value(time)
        End Function

        Protected ReadOnly Property time As Double
            Get
                Dim difference As Long = GetDifference()
                If Duration.Ticks <= 0 Then
                    [Stop]()
                    Return 1.0R
                End If
                Dim value = Math.Max(0.0R, Math.Min(1.0R, difference / CDbl(Duration.Ticks)))
                If difference >= Duration.Ticks Then [Stop]()
                Return value
            End Get
        End Property

        Private Function GetDifference() As Long
            Dim CurrentTick As Long = gameTime.TotalGameTime.Ticks
            Return CurrentTick - StartTick
        End Function

        Private StartTick As Long = 0

        Protected Friend Sub RaiseOnFinished(sender As Object)
            If AutoRepeat Then
                Start()
            Else
                RaiseEvent OnAnimationFinished(sender)
            End If
        End Sub

        Protected Overloads Sub Init(EaseFunction As EaseFunction, [From] As Object, [To] As Object, Duration As TimeSpan, gameTime As GameTime, Autostart As Boolean)
            Me.easeFunction = EaseFunction
            Me.From = [From]
            Me.To = [To]
            Me.Duration = Duration
            Me.gameTime = gameTime
            Me.StartTick = gameTime.TotalGameTime.Ticks
            If Autostart Then Start()
        End Sub

        Public Sub Start()
            If gameTime Is Nothing Then gameTime = New GameTime()
            Me.StartTick = gameTime.TotalGameTime.Ticks
            _Running = True
        End Sub

        Public Sub [Stop]()
            _Running = False
        End Sub

    End Class

End Namespace
