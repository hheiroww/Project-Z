Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Animations.Easing
Imports ProjectZ.Shared.Animations.Properties
Imports ProjectZ.Shared.Drawing.UI
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Animations

    ''' <summary>
    ''' Runtime representation of a WPF Storyboard imported by SceneXamlParser.
    ''' Each Begin creates fresh Project-Z animations so storyboards can be
    ''' replayed by XAML triggers without retaining completed tracks.
    ''' </summary>
    Public NotInheritable Class XamlStoryboard
        Private ReadOnly tracks As New List(Of TrackDefinition)()
        Private ReadOnly activeTracks As New List(Of KeyValuePair(Of SceneElement, AnimationBase))()

        Public ReadOnly Property IsRunning As Boolean
            Get
                Return activeTracks.Any(Function(track) track.Value.Running)
            End Get
        End Property

        Public Sub [Stop]()
            For Each track In activeTracks
                track.Key.StopBoundAnimation(track.Value)
            Next
            activeTracks.Clear()
        End Sub

        Public ReadOnly Property Name As String
        Public ReadOnly Property TrackCount As Integer
            Get
                Return tracks.Count
            End Get
        End Property

        Public Sub New(name As String)
            Me.Name = If(name, String.Empty)
        End Sub

        Friend Sub AddDouble(target As SceneElement, targetProperty As ElementProperty,
                             fromValue As Double?, toValue As Double, duration As TimeSpan,
                             easingFactory As Func(Of EaseFunction), repeatForever As Boolean,
                             relativeToCurrent As Boolean)
            tracks.Add(New TrackDefinition With {
                .Target = target,
                .TargetProperty = targetProperty,
                .FromDouble = fromValue,
                .ToDouble = toValue,
                .Duration = duration,
                .EasingFactory = easingFactory,
                .RepeatForever = repeatForever,
                .RelativeToCurrent = relativeToCurrent
            })
        End Sub

        Friend Sub AddColor(target As SceneElement, targetProperty As ElementProperty,
                            fromValue As Color?, toValue As Color, duration As TimeSpan,
                            easingFactory As Func(Of EaseFunction), repeatForever As Boolean)
            tracks.Add(New TrackDefinition With {
                .Target = target,
                .TargetProperty = targetProperty,
                .FromColor = fromValue,
                .ToColor = toValue,
                .Duration = duration,
                .EasingFactory = easingFactory,
                .RepeatForever = repeatForever,
                .IsColor = True
            })
        End Sub

        Friend Sub AddDoubleKeyFrames(target As SceneElement, targetProperty As ElementProperty,
                                      keyFrames As IEnumerable(Of XamlDoubleKeyFrame), duration As TimeSpan,
                                      repeatForever As Boolean)
            tracks.Add(New TrackDefinition With {
                .Target = target,
                .TargetProperty = targetProperty,
                .DoubleKeyFrames = keyFrames.ToList(),
                .Duration = duration,
                .RepeatForever = repeatForever
            })
        End Sub

        Friend Sub AddColorKeyFrames(target As SceneElement, targetProperty As ElementProperty,
                                     keyFrames As IEnumerable(Of XamlColorKeyFrame), duration As TimeSpan,
                                     repeatForever As Boolean)
            tracks.Add(New TrackDefinition With {
                .Target = target,
                .TargetProperty = targetProperty,
                .ColorKeyFrames = keyFrames.ToList(),
                .Duration = duration,
                .RepeatForever = repeatForever,
                .IsColor = True
            })
        End Sub

        Friend Sub AddCornerRadiusKeyFrames(target As SceneElement, targetProperty As ElementProperty,
                                           frames As IEnumerable(Of XamlCornerRadiusKeyFrame), duration As TimeSpan, repeatForever As Boolean)
            tracks.Add(New TrackDefinition With {.Target = target, .TargetProperty = targetProperty,
                .CornerKeyFrames = frames.ToList(), .Duration = duration, .RepeatForever = repeatForever})
        End Sub

        Public Function Begin(gameTime As GameTime) As IReadOnlyList(Of AnimationBase)
            Dim started As New List(Of AnimationBase)()
            activeTracks.Clear()
            For Each track In tracks
                Dim animation As AnimationBase
                If track.CornerKeyFrames IsNot Nothing Then
                    animation = New CornerRadiusKeyFrameAnimation(CType(track.TargetProperty.GetValue(), CornerRadii), track.CornerKeyFrames, track.Duration, gameTime)
                ElseIf track.DoubleKeyFrames IsNot Nothing Then
                    Dim current = Convert.ToDouble(track.TargetProperty.GetValue(), Globalization.CultureInfo.InvariantCulture)
                    animation = New DoubleKeyFrameAnimation(current, track.DoubleKeyFrames, track.Duration, gameTime)
                ElseIf track.ColorKeyFrames IsNot Nothing Then
                    Dim current = CType(track.TargetProperty.GetValue(), Color)
                    animation = New ColorKeyFrameAnimation(current, track.ColorKeyFrames, track.Duration, gameTime)
                ElseIf track.IsColor Then
                    Dim easing = If(track.EasingFactory, Function() New SineEase(EaseType.Ignore)).Invoke()
                    Dim current = CType(track.TargetProperty.GetValue(), Color)
                    Dim first = If(track.FromColor.HasValue, track.FromColor.Value, current)
                    animation = New ColorAnimation(easing, first, track.ToColor, track.Duration, gameTime, False) With {
                        .InterpolateInLinearColorSpace = True}
                Else
                    Dim easing = If(track.EasingFactory, Function() New SineEase(EaseType.Ignore)).Invoke()
                    Dim current = Convert.ToDouble(track.TargetProperty.GetValue(), Globalization.CultureInfo.InvariantCulture)
                    Dim first = If(track.FromDouble.HasValue, track.FromDouble.Value, current)
                    Dim last = track.ToDouble
                    If track.RelativeToCurrent Then
                        first = current + If(track.FromDouble.HasValue, track.FromDouble.Value, 0.0R)
                        last = current + track.ToDouble
                    End If
                    animation = New DoubleAnimation(easing, first, last, track.Duration, gameTime, False)
                End If
                animation.AutoRepeat = track.RepeatForever
                track.Target.BindAnimation(track.TargetProperty, animation)
                animation.Start()
                activeTracks.Add(New KeyValuePair(Of SceneElement, AnimationBase)(track.Target, animation))
                started.Add(animation)
            Next
            Return started
        End Function

        Private NotInheritable Class TrackDefinition
            Public Target As SceneElement
            Public TargetProperty As ElementProperty
            Public FromDouble As Double?
            Public ToDouble As Double
            Public FromColor As Color?
            Public ToColor As Color
            Public Duration As TimeSpan
            Public EasingFactory As Func(Of EaseFunction)
            Public RepeatForever As Boolean
            Public RelativeToCurrent As Boolean
            Public IsColor As Boolean
            Public DoubleKeyFrames As List(Of XamlDoubleKeyFrame)
            Public ColorKeyFrames As List(Of XamlColorKeyFrame)
            Public CornerKeyFrames As List(Of XamlCornerRadiusKeyFrame)
        End Class
    End Class

End Namespace
