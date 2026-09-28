Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Animations.Easing
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Animations

    Public NotInheritable Class XamlCornerRadiusKeyFrame
        Public Property KeyTime As TimeSpan
        Public Property Value As CornerRadii
    End Class

    ''' <summary>WPF object keyframes switch the complete CornerRadius atomically.</summary>
    Public NotInheritable Class CornerRadiusKeyFrameAnimation
        Inherits AnimationBase
        Private ReadOnly frames As List(Of XamlCornerRadiusKeyFrame)
        Public Sub New(initial As CornerRadii, values As IEnumerable(Of XamlCornerRadiusKeyFrame), duration As TimeSpan, time As GameTime)
            frames = values.OrderBy(Function(frame) frame.KeyTime).ToList()
            Init(New SineEase(EaseType.Ignore), initial, If(frames.Count = 0, initial, frames.Last().Value), duration, time, False)
        End Sub
        Public Overrides Function Value(t As Double) As Object
            Dim elapsed = TimeSpan.FromTicks(CLng(Math.Clamp(t, 0, 1) * Duration.Ticks))
            Dim current = CType(From, CornerRadii)
            For Each frame In frames
                If elapsed < frame.KeyTime Then Exit For
                current = frame.Value
            Next
            lastValue = current
            Return current
        End Function
    End Class

    ' WPF ColorAnimation interpolates linear scRGB, not the stored sRGB bytes.
    ' Keep this separate from gradient brushes, whose default interpolation is sRGB.
    Public NotInheritable Class XamlColorInterpolation
        Public Shared Function FromScRgb(alpha As Double, red As Double, green As Double, blue As Double) As Color
            Return New Color(ToSrgb(red), ToSrgb(green), ToSrgb(blue),
                             CByte(Math.Floor(Math.Clamp(alpha, 0, 1) * 255 + 0.5)))
        End Function

        Private Shared Function ToSrgb(value As Double) As Byte
            value = Math.Clamp(value, 0, 1)
            Dim encoded = If(value <= 0.0031308, value * 12.92, 1.055 * Math.Pow(value, 1 / 2.4) - 0.055)
            Return CByte(Math.Floor(Math.Clamp(encoded, 0, 1) * 255 + 0.5))
        End Function

        Private Shared Function ToLinear(value As Byte) As Double
            Dim encoded = value / 255.0
            Return If(encoded <= 0.04045, encoded / 12.92, Math.Pow((encoded + 0.055) / 1.055, 2.4))
        End Function

        Public Shared Function Lerp(first As Color, last As Color, progress As Double) As Color
            progress = Math.Clamp(progress, 0, 1)
            Return FromScRgb((first.A + (CDbl(last.A) - first.A) * progress) / 255,
                             ToLinear(first.R) + (ToLinear(last.R) - ToLinear(first.R)) * progress,
                             ToLinear(first.G) + (ToLinear(last.G) - ToLinear(first.G)) * progress,
                             ToLinear(first.B) + (ToLinear(last.B) - ToLinear(first.B)) * progress)
        End Function
    End Class

    Public NotInheritable Class XamlDoubleKeyFrame
        Public Property KeyTime As TimeSpan
        Public Property Value As Double
        Public Property EasingFactory As Func(Of EaseFunction)
        Public Property IsDiscrete As Boolean
    End Class

    Public NotInheritable Class XamlColorKeyFrame
        Public Property KeyTime As TimeSpan
        Public Property Value As Color
        Public Property EasingFactory As Func(Of EaseFunction)
        Public Property IsDiscrete As Boolean
    End Class

    ''' <summary>Project-Z timeline for WPF DoubleAnimationUsingKeyFrames.</summary>
    Public NotInheritable Class DoubleKeyFrameAnimation
        Inherits AnimationBase

        Private ReadOnly frames As List(Of XamlDoubleKeyFrame)

        Public Sub New(initialValue As Double, keyFrames As IEnumerable(Of XamlDoubleKeyFrame),
                       duration As TimeSpan, gameTime As GameTime)
            frames = keyFrames.OrderBy(Function(frame) frame.KeyTime).ToList()
            Init(New SineEase(EaseType.Ignore), initialValue,
                 If(frames.Count = 0, initialValue, frames(frames.Count - 1).Value),
                 duration, gameTime, False)
        End Sub

        Public Overrides Function Value(t As Double) As Object
            If frames.Count = 0 Then Return CDbl(From)
            Dim elapsed = TimeSpan.FromTicks(CLng(Math.Max(0.0R, Math.Min(1.0R, t)) * Duration.Ticks))
            Dim previousTime = TimeSpan.Zero
            Dim previousValue = CDbl(From)
            For Each frame In frames
                If elapsed <= frame.KeyTime Then
                    If frame.IsDiscrete AndAlso elapsed < frame.KeyTime Then Return previousValue
                    Dim span = (frame.KeyTime - previousTime).TotalMilliseconds
                    Dim progress = If(span <= 0, 1.0R,
                                      Math.Max(0.0R, Math.Min(1.0R, (elapsed - previousTime).TotalMilliseconds / span)))
                    Dim easing = If(frame.EasingFactory, Function() New SineEase(EaseType.Ignore)).Invoke()
                    lastValue = DoubleAnimation.Interpolate(easing.Ease(progress), 0, 1, previousValue, frame.Value)
                    Return lastValue
                End If
                previousTime = frame.KeyTime
                previousValue = frame.Value
            Next
            lastValue = frames(frames.Count - 1).Value
            Return lastValue
        End Function
    End Class

    ''' <summary>Project-Z timeline for WPF ColorAnimationUsingKeyFrames.</summary>
    Public NotInheritable Class ColorKeyFrameAnimation
        Inherits AnimationBase

        Private ReadOnly frames As List(Of XamlColorKeyFrame)

        Public Sub New(initialValue As Color, keyFrames As IEnumerable(Of XamlColorKeyFrame),
                       duration As TimeSpan, gameTime As GameTime)
            frames = keyFrames.OrderBy(Function(frame) frame.KeyTime).ToList()
            Init(New SineEase(EaseType.Ignore), initialValue,
                 If(frames.Count = 0, initialValue, frames(frames.Count - 1).Value),
                 duration, gameTime, False)
        End Sub

        Public Overrides Function Value(t As Double) As Object
            If frames.Count = 0 Then Return CType(From, Color)
            Dim elapsed = TimeSpan.FromTicks(CLng(Math.Max(0.0R, Math.Min(1.0R, t)) * Duration.Ticks))
            Dim previousTime = TimeSpan.Zero
            Dim previousValue = CType(From, Color)
            For Each frame In frames
                If elapsed <= frame.KeyTime Then
                    If frame.IsDiscrete AndAlso elapsed < frame.KeyTime Then Return previousValue
                    Dim span = (frame.KeyTime - previousTime).TotalMilliseconds
                    Dim progress = If(span <= 0, 1.0R,
                                      Math.Max(0.0R, Math.Min(1.0R, (elapsed - previousTime).TotalMilliseconds / span)))
                    Dim easing = If(frame.EasingFactory, Function() New SineEase(EaseType.Ignore)).Invoke()
                    lastValue = XamlColorInterpolation.Lerp(previousValue, frame.Value, easing.Ease(progress))
                    Return lastValue
                End If
                previousTime = frame.KeyTime
                previousValue = frame.Value
            Next
            lastValue = frames(frames.Count - 1).Value
            Return lastValue
        End Function
    End Class

End Namespace
