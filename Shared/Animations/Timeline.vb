Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Animations
Imports ProjectZ.Shared.Animations.Properties

Namespace [Shared].Animations

    Public Class Timeline

        Private ActiveAnimations As New Collections.Generic.Dictionary(Of AnimationBase, ElementProperty)
        Private InactiveAnimations As New Collections.Generic.List(Of AnimationBase)
        Private BindQueue As New Collections.Generic.List(Of Object())
        Private ReadOnly propertyOwners As New Collections.Generic.List(Of PropertyOwner)

        Private Class PropertyOwner
            Public Target As ElementProperty
            Public Animation As AnimationBase
            Public BaseValue As Object
        End Class

        Public Property gameTime As GameTime

        Public Sub AddChild(Animation As AnimationBase, TargetProperty As ElementProperty)
            If Animation.From Is Nothing Then Animation.From = TargetProperty.GetValue
            Dim owner = propertyOwners.FirstOrDefault(Function(p) p.Target.TargetsSameProperty(TargetProperty))
            If owner Is Nothing Then
                owner = New PropertyOwner With {.Target = TargetProperty, .BaseValue = TargetProperty.GetValue()}
                propertyOwners.Add(owner)
            End If
            owner.Animation = Animation
            BindQueue.Add({Animation, TargetProperty})
        End Sub

        Public Sub RemoveChild(Animation As AnimationBase)
            BindQueue.RemoveAll(Function(binding) Object.ReferenceEquals(binding(0), Animation))
            If isChild(Animation) AndAlso Not InactiveAnimations.Contains(Animation) Then
                InactiveAnimations.Add(Animation)
            End If
        End Sub

        Public Sub StopChild(Animation As AnimationBase)
            Animation.Stop()
            RemoveChild(Animation)
            ' Completed clocks still hold their final value. Stop restores the
            ' unanimated base, but must not overwrite a newer replacement clock.
            Dim owner = propertyOwners.FirstOrDefault(Function(p) p.Animation Is Animation)
            If owner Is Nothing Then Return
            owner.Target.SetValue(owner.BaseValue)
            propertyOwners.Remove(owner)
        End Sub

        Public Function isChild(Animation As AnimationBase) As Boolean
            Return ActiveAnimations.ContainsKey(Animation)
        End Function

        Public Sub Tick(gameTime As GameTime)

            ' Most UI elements are static. Returning before allocating a pending
            ' array avoids one allocation per element per scene tick; large
            ' galleries otherwise spend the majority of their update budget
            ' proving that thousands of empty timelines have no work to do.
            If BindQueue.Count = 0 AndAlso ActiveAnimations.Count = 0 AndAlso InactiveAnimations.Count = 0 Then Return

            ' Hack to enable the concurrent binding of animations
            If BindQueue.Count > 0 Then
                Dim pending = BindQueue.ToArray()
                BindQueue.Clear()
                For Each binding In pending
                    If binding Is Nothing Then Continue For
                    Dim Animation As AnimationBase = CType(binding(0), AnimationBase)
                    Dim TargetProperty As ElementProperty = CType(binding(1), ElementProperty)
                    ' WPF SnapshotAndReplace: rapid hover reversals must not leave the
                    ' previous timeline fighting the new one for the same property.
                    For Each existing In ActiveAnimations.Where(Function(p) p.Value.TargetsSameProperty(TargetProperty)).Select(Function(p) p.Key).ToArray()
                        ActiveAnimations.Remove(existing)
                    Next
                    ActiveAnimations.Add(Animation, TargetProperty)
                Next
            End If

            ' Continue the Animation
            For Each A As AnimationBase In ActiveAnimations.Keys
                If A.Running Then
                    A.gameTime = gameTime
                    Dim TargetProperty As ElementProperty = CType(ActiveAnimations(A), ElementProperty)
                    TargetProperty.SetValue(A.Value)
                    If Not A.Running Then
                        A.RaiseOnFinished(Me)
                        If Not A.Running AndAlso Not InactiveAnimations.Contains(A) Then InactiveAnimations.Add(A)
                    End If
                End If
            Next

            For i As Integer = InactiveAnimations.Count - 1 To 0 Step -1
                Dim A As AnimationBase = InactiveAnimations(i)
                ActiveAnimations.Remove(A)
                InactiveAnimations.RemoveAt(i)
            Next

        End Sub

        Public Sub New(gameTime As GameTime)
            Me.gameTime = gameTime
        End Sub
    End Class

End Namespace
