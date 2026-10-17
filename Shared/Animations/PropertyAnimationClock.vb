Imports Microsoft.Xna.Framework

Namespace [Shared].Animations
    ''' <summary>A property clock driven by a native scene's frame time, also usable by imported transforms and brushes.</summary>
    Public NotInheritable Class PropertyAnimationClock
        Implements IDisposable
        Private ReadOnly scene As Drawing.Scene
        Private ReadOnly apply As Action(Of Double)
        Private ReadOnly restore As Action
        Private startTime As TimeSpan?
        Private previousTime As TimeSpan?
        Private elapsed As TimeSpan
        Private finished As Boolean
        Public Property Duration As TimeSpan = TimeSpan.FromSeconds(1)
        Public Property BeginDelay As TimeSpan
        Public Property RepeatCount As Double = 1
        Public Property RepeatDuration As TimeSpan?
        Public Property AutoReverse As Boolean
        Public Property SpeedRatio As Double = 1
        Public Property HoldEnd As Boolean = True
        Public Property IsPaused As Boolean
        Public Event Completed As EventHandler
        Public Sub New(scene As Drawing.Scene, apply As Action(Of Double), restore As Action)
            Me.scene = scene : Me.apply = apply : Me.restore = restore
            AddHandler scene.FrameUpdated, AddressOf OnFrame
        End Sub
        Private Sub OnFrame(time As GameTime)
            Tick(time.TotalGameTime)
        End Sub
        Public Sub Tick(time As TimeSpan)
            If finished Then Return
            If Not startTime.HasValue Then startTime = time
            If previousTime.HasValue AndAlso Not IsPaused Then elapsed += TimeSpan.FromTicks(Math.Max(0, time.Ticks - previousTime.Value.Ticks))
            previousTime = time
            Dim seconds = (elapsed - BeginDelay).TotalSeconds * SpeedRatio
            If seconds < 0 OrElse IsPaused Then Return
            Dim length = Math.Max(0, Duration.TotalSeconds)
            Dim cycle = length * If(AutoReverse, 2, 1)
            Dim limit = If(RepeatDuration.HasValue, RepeatDuration.Value.TotalSeconds, cycle * RepeatCount)
            Dim done = length = 0 OrElse seconds >= limit
            Dim phase = If(cycle = 0, 0, Math.Min(seconds, limit) Mod cycle)
            If done AndAlso phase = 0 Then phase = cycle
            Dim progress = If(length = 0, 1, phase / length)
            If AutoReverse AndAlso progress > 1 Then progress = 2 - progress
            apply(Math.Max(0, Math.Min(1, progress)))
            If done Then
                finished = True : RemoveHandler scene.FrameUpdated, AddressOf OnFrame
                If Not HoldEnd Then restore()
                RaiseEvent Completed(Me, EventArgs.Empty)
            End If
        End Sub
        Public Sub [Stop](Optional restoreBase As Boolean = True)
            finished = True : RemoveHandler scene.FrameUpdated, AddressOf OnFrame
            If restoreBase Then restore()
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            [Stop]()
        End Sub
    End Class
End Namespace
