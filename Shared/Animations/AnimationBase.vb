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
        Public Property BeginDelay As TimeSpan
        Public Property AutoReverse As Boolean
        Public Property SpeedRatio As Double = 1.0R
        Public Property RestoreBaseOnCompletion As Boolean
        Public Property CaptureFromOnStart As Boolean
        Public Property RelativeToStart As Boolean
        Public ReadOnly Property WaitingToStart As Boolean
            Get
                Return GetDifference() < BeginDelay.Ticks
            End Get
        End Property

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
                Dim difference As Double = (GetDifference() - BeginDelay.Ticks) * SpeedRatio
                If Duration.Ticks <= 0 Then
                    [Stop]()
                    Return 1.0R
                End If
                Dim completion = If(AutoReverse, 2.0R, 1.0R)
                Dim progress = Math.Max(0.0R, Math.Min(completion, difference / CDbl(Duration.Ticks)))
                Dim value = If(AutoReverse AndAlso progress > 1.0R, 2.0R - progress, progress)
                If difference >= Duration.Ticks * completion Then [Stop]()
                Return value
            End Get
        End Property

        Private Function GetDifference() As Long
            Dim CurrentTick As Long = gameTime.TotalGameTime.Ticks
            If startOnNextTick Then
                StartTick = CurrentTick
                startOnNextTick = False
            End If
            Return CurrentTick - StartTick
        End Function

        Private StartTick As Long = 0
        Private startOnNextTick As Boolean

        Protected Friend Sub RaiseOnFinished(sender As Object)
            If AutoRepeat Then
                BeginDelay = TimeSpan.Zero
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
            startOnNextTick = False
            _Running = True
        End Sub

        Friend Sub StartAtNextTick()
            Start()
            ' A hidden or newly created scene has a stale clock. Start imported
            ' storyboards when Timeline supplies the next actual frame time.
            startOnNextTick = True
        End Sub

        Public Sub [Stop]()
            _Running = False
        End Sub

    End Class

End Namespace
