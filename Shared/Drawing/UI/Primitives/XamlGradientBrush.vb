Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Namespace [Shared].Drawing.UI.Primitives
    Public Class XamlGradientStop
        Public Property Offset As Single
        Public Property Color As Color
    End Class

    ''' <summary>Mutable brush stops are animation targets, in original XAML order.</summary>
    Public NotInheritable Class XamlGradientBrush
        Implements IDisposable
        Public ReadOnly Stops As New List(Of XamlGradientStop)
        Public Property IsRadial As Boolean
        Public Property StartPoint As Vector2 = Vector2.Zero
        Public Property EndPoint As Vector2 = Vector2.One
        Public Property Center As New Vector2(0.5F)
        Public Property GradientOrigin As New Vector2(0.5F)
        Public Property Opacity As Single = 1
        Public Property AbsoluteMapping As Boolean
        Public Property SpreadMethod As String = "Pad"
        Public Property Radius As New Vector2(0.5F)
        ''' <summary>Transforms the brush within its relative bounding rectangle.</summary>
        Public Property RelativeTransform As System.Numerics.Matrix3x2 = System.Numerics.Matrix3x2.Identity
        Friend Property PremultipliedStops As Boolean
        Private texture As Texture2D
        Private ReadOnly deviceTextures As New Dictionary(Of GraphicsDevice, Texture2D)
        Private previousGeometry As (Width As Integer, Height As Integer, Corners As CornerRadii, Border As Single, BorderColor As Color)
        Private previousGradient As (Radial As Boolean, Start As Vector2, [End] As Vector2, Center As Vector2, Radius As Vector2, Origin As Vector2, Opacity As Single, Absolute As Boolean, Spread As String, Transform As System.Numerics.Matrix3x2, Premultiplied As Boolean)
        Private previousBorderKey As String
        Private previousStops As (Offset As Single, Color As Color)() = Array.Empty(Of (Single, Color))()
        Private Shared ReadOnly pixelCache As New Dictionary(Of String, Color())(StringComparer.Ordinal)
        Private Shared ReadOnly pixelCacheOrder As New Queue(Of String)
        Private Shared pixelCacheBytes As Long
        Private Shared ReadOnly parallelOptions As New Threading.Tasks.ParallelOptions With {.MaxDegreeOfParallelism = 4}
        ' Immutable after construction; CPU masks can consume this without a
        ' synchronous GPU readback of the pixels we just uploaded.
        Friend ReadOnly Property PixelData As Color()
        Public ReadOnly Property Revision As Integer

        Public Function GetTexture(device As GraphicsDevice, width As Integer, height As Integer, Optional radius As Single = 0,
                                   Optional border As Single = 0, Optional borderColor As Color = Nothing) As Texture2D
            Return GetTexture(device, width, height, New CornerRadii(Math.Max(0, radius)), border, borderColor)
        End Function

        Public Function GetTexture(device As GraphicsDevice, width As Integer, height As Integer, corners As CornerRadii,
                                   Optional border As Single = 0, Optional borderColor As Color = Nothing) As Texture2D
            Return GetTexture(device, width, height, corners, border, borderColor, Nothing)
        End Function

        Friend Function GetTexture(device As GraphicsDevice, width As Integer, height As Integer, corners As CornerRadii,
                                   border As Single, borderColor As Color, borderBrush As XamlGradientBrush) As Texture2D
            Dim logicalWidth = Math.Max(1, width), logicalHeight = Math.Max(1, height)
            Dim switchedDevice = texture IsNot Nothing AndAlso texture.GraphicsDevice IsNot device
            If texture Is Nothing OrElse switchedDevice Then deviceTextures.TryGetValue(device, texture)
            corners = corners.Fit(logicalWidth, logicalHeight)
            border = Math.Max(0, border)
            width = Math.Clamp(width, 1, 512)
            height = Math.Clamp(height, 1, 512)
            Dim geometry = (logicalWidth, logicalHeight, corners, border, borderColor)
            Dim gradient = GradientSettings()
            Dim borderKey = If(border > 0 AndAlso borderBrush IsNot Nothing, borderBrush.GradientKey(), String.Empty)
            Dim unchanged = Not switchedDevice AndAlso texture IsNot Nothing AndAlso Not texture.IsDisposed AndAlso previousGeometry.Equals(geometry) AndAlso previousGradient.Equals(gradient) AndAlso previousBorderKey = borderKey AndAlso previousStops.Length = Stops.Count
            If unchanged Then
                For i = 0 To Stops.Count - 1
                    If previousStops(i).Offset <> Stops(i).Offset OrElse previousStops(i).Color <> Stops(i).Color Then
                        unchanged = False
                        Exit For
                    End If
                Next
                If unchanged Then Return texture
            End If
            Dim key = logicalWidth.ToString() & ":" & logicalHeight.ToString() & ":" & corners.ToString() & ":" & border.ToString() & borderColor.ToString() &
                GradientKey() & "|" & borderKey
            Dim pixels As Color() = Nothing
            SyncLock pixelCache
                pixelCache.TryGetValue(key, pixels)
            End SyncLock
            If pixels Is Nothing Then
                pixels = New Color(width * height - 1) {}
                Dim pixelWidth = CSng(logicalWidth) / width, pixelHeight = CSng(logicalHeight) / height
                Dim antialiasWidth = Math.Max(pixelWidth, pixelHeight)
                Dim solidStroke = Color.FromNonPremultiplied(borderColor.ToVector4()).ToVector4()
                Dim fillSample = CreateSampler(logicalWidth, logicalHeight)
                Dim strokeSample = If(border > 0, borderBrush?.CreateSampler(logicalWidth, logicalHeight), Nothing)
                Dim bands = Math.Min(4, height)
                Threading.Tasks.Parallel.For(0, bands, parallelOptions,
                    Sub(band)
                        For y = height * band \ bands To height * (band + 1) \ bands - 1
                            For x = 0 To width - 1
                                Dim point As New Vector2((x + 0.5F) / width, (y + 0.5F) / height)
                                Dim distance = corners.Distance((x + 0.5F) * pixelWidth, (y + 0.5F) * pixelHeight, logicalWidth, logicalHeight)
                                Dim coverage = Math.Clamp(0.5F - distance / antialiasWidth, 0, 1)
                                Dim fillCoverage = If(border = 0, coverage, Math.Clamp(0.5F - (distance + border) / antialiasWidth, 0, 1))
                                Dim strokeCoverage = coverage - fillCoverage
                                Dim fillPixel = If(fillCoverage > 0, fillSample(point) * fillCoverage, Vector4.Zero)
                                Dim strokePixel = If(strokeCoverage > 0, If(strokeSample Is Nothing, solidStroke, strokeSample(point)) * strokeCoverage, Vector4.Zero)
                                pixels(y * width + x) = New Color(fillPixel + strokePixel)
                            Next
                        Next
                    End Sub)
                SyncLock pixelCache
                    If Not pixelCache.ContainsKey(key) Then
                        While pixelCacheOrder.Count > 0 AndAlso (pixelCacheBytes + pixels.LongLength * 4 > 32L * 1024 * 1024 OrElse pixelCache.Count >= 256)
                            Dim expired = pixelCacheOrder.Dequeue()
                            pixelCacheBytes -= pixelCache(expired).LongLength * 4
                            pixelCache.Remove(expired)
                        End While
                        pixelCache.Add(key, pixels)
                        pixelCacheOrder.Enqueue(key)
                        pixelCacheBytes += pixels.LongLength * 4
                    End If
                End SyncLock
            End If
            If texture Is Nothing OrElse texture.IsDisposed OrElse texture.Width <> width OrElse texture.Height <> height Then
                texture?.Dispose()
                texture = New Texture2D(device, width, height)
                deviceTextures(device) = texture
            End If
            texture.SetData(pixels)
            _PixelData = pixels
            _Revision += 1
            previousGeometry = geometry
            previousGradient = gradient
            previousBorderKey = borderKey
            previousStops = Stops.Select(Function(s) (s.Offset, s.Color)).ToArray()
            Return texture
        End Function

        Private Function GradientSettings() As (Radial As Boolean, Start As Vector2, [End] As Vector2, Center As Vector2, Radius As Vector2, Origin As Vector2, Opacity As Single, Absolute As Boolean, Spread As String, Transform As System.Numerics.Matrix3x2, Premultiplied As Boolean)
            Return (IsRadial, StartPoint, EndPoint, Center, Radius, GradientOrigin, Opacity, AbsoluteMapping, SpreadMethod, RelativeTransform, PremultipliedStops)
        End Function

        Private Function GradientKey() As String
            Return GradientSettings().ToString() & String.Join(";", Stops.Select(Function(s) s.Offset.ToString() & ":" & s.Color.ToString()))
        End Function

        Private Function CreateSampler(logicalWidth As Integer, logicalHeight As Integer) As Func(Of Vector2, Vector4)
            ' Capture mutable brush state once before parallel rendering.
            Dim ordered = Stops.OrderBy(Function(s) s.Offset).Select(Function(s) New XamlGradientStop With {.Offset = s.Offset, .Color = s.Color}).ToArray()
            Dim settings = GradientSettings()
            Dim transform As System.Numerics.Matrix3x2
            If Not System.Numerics.Matrix3x2.Invert(settings.Transform, transform) Then Return Function(point) Vector4.Zero
            Dim transformPoint = Not settings.Transform.IsIdentity
            Dim opacity = Math.Clamp(settings.Opacity, 0, 1)
            Dim direction = settings.[End] - settings.Start
            Dim length = direction.LengthSquared()
            Dim ellipse = New Vector2(Math.Max(0.001F, settings.Radius.X), Math.Max(0.001F, settings.Radius.Y))
            Dim centerOffset = (settings.Origin - settings.Center) / ellipse
            If centerOffset.LengthSquared() >= 1 Then centerOffset = Vector2.Normalize(centerOffset) * 0.999F
            Dim centerDistance = centerOffset.LengthSquared() - 1
            Dim extent As New Vector2(logicalWidth, logicalHeight)
            Return Function(point)
                       If transformPoint Then
                           point = New Vector2(point.X * transform.M11 + point.Y * transform.M21 + transform.M31,
                                               point.X * transform.M12 + point.Y * transform.M22 + transform.M32)
                       End If
                       If settings.Absolute Then point *= extent
                       Dim t As Single
                       If settings.Radial Then
                           Dim ray = (point - settings.Origin) / ellipse
                           Dim a = ray.LengthSquared(), b = 2 * Vector2.Dot(centerOffset, ray)
                           Dim hit = If(a < 0.0000001F, 1.0F, (-b + MathF.Sqrt(Math.Max(0, b * b - 4 * a * centerDistance))) / (2 * a))
                           t = If(a < 0.0000001F, 0, 1 / Math.Max(0.000001F, hit))
                       Else
                           t = If(length = 0, 0, Vector2.Dot(point - settings.Start, direction) / length)
                       End If
                       If settings.Spread = "Repeat" Then t -= MathF.Floor(t)
                       If settings.Spread = "Reflect" Then t = 1 - Math.Abs((t - 2 * MathF.Floor(t / 2)) - 1)
                       Dim color = Sample(ordered, t)
                       Return If(settings.Premultiplied, color, Color.FromNonPremultiplied(color.ToVector4())).ToVector4() * opacity
                   End Function
        End Function

        Private Shared Function Sample(stops As XamlGradientStop(), t As Single) As Color
            If stops.Length = 0 Then Return Color.Transparent
            If t <= stops(0).Offset Then Return stops(0).Color
            For i = 1 To stops.Length - 1
                If t <= stops(i).Offset Then
                    Dim span = stops(i).Offset - stops(i - 1).Offset
                    Return Color.Lerp(stops(i - 1).Color, stops(i).Color, If(span <= 0, 1, (t - stops(i - 1).Offset) / span))
                End If
            Next
            Return stops.Last().Color
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            For Each cached In deviceTextures.Values : cached.Dispose() : Next
            deviceTextures.Clear()
            texture = Nothing
            _PixelData = Nothing
        End Sub
    End Class
End Namespace
