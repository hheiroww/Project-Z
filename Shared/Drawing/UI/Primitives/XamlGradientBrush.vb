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
        Private texture As Texture2D
        Private previousGeometry As (Width As Integer, Height As Integer, Corners As CornerRadii, Border As Single, BorderColor As Color)
        Private previousGradient As (Radial As Boolean, Start As Vector2, [End] As Vector2, Center As Vector2, Radius As Vector2, Origin As Vector2, Opacity As Single, Absolute As Boolean, Spread As String)
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
            Dim logicalWidth = Math.Max(1, width), logicalHeight = Math.Max(1, height)
            corners = corners.Fit(logicalWidth, logicalHeight)
            border = Math.Max(0, border)
            width = Math.Clamp(width, 1, 512)
            height = Math.Clamp(height, 1, 512)
            Dim geometry = (logicalWidth, logicalHeight, corners, border, borderColor)
            Dim gradient = (IsRadial, StartPoint, EndPoint, Center, Me.Radius, GradientOrigin, Opacity, AbsoluteMapping, SpreadMethod)
            Dim unchanged = texture IsNot Nothing AndAlso previousGeometry.Equals(geometry) AndAlso previousGradient.Equals(gradient) AndAlso previousStops.Length = Stops.Count
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
                gradient.ToString() &
                String.Join(";", Stops.Select(Function(s) s.Offset.ToString() & ":" & s.Color.ToString()))
            Dim pixels As Color() = Nothing
            SyncLock pixelCache
                pixelCache.TryGetValue(key, pixels)
            End SyncLock
            If pixels Is Nothing Then
                ' Snapshot all mutable animation values before parallelizing rows.
                Dim ordered = Stops.OrderBy(Function(s) s.Offset).Select(Function(s) New XamlGradientStop With {.Offset = s.Offset, .Color = s.Color}).ToArray()
                pixels = New Color(width * height - 1) {}
                Dim direction = EndPoint - StartPoint
                Dim length = direction.LengthSquared()
                Dim pixelWidth = CSng(logicalWidth) / width, pixelHeight = CSng(logicalHeight) / height
                Dim antialiasWidth = Math.Max(pixelWidth, pixelHeight)
                Dim borderPixel = Color.FromNonPremultiplied(borderColor.ToVector4()).ToVector4()
                Dim origin = StartPoint
                Dim centerPoint = Center
                Dim brushRadius = Me.Radius
                Dim radialMode = IsRadial
                Dim gradientOpacity = Math.Clamp(Opacity, 0, 1)
                Dim absolute = AbsoluteMapping
                Dim spread = SpreadMethod
                Dim focus = GradientOrigin
                Dim bands = Math.Min(4, height)
                Threading.Tasks.Parallel.For(0, bands, parallelOptions,
                    Sub(band)
                        For y = height * band \ bands To height * (band + 1) \ bands - 1
                            For x = 0 To width - 1
                                Dim point As New Vector2((x + 0.5F) / width, (y + 0.5F) / height)
                                If absolute Then point *= New Vector2(logicalWidth, logicalHeight)
                                Dim t As Single
                                If radialMode Then
                                    Dim ellipse = New Vector2(Math.Max(0.001F, brushRadius.X), Math.Max(0.001F, brushRadius.Y))
                                    Dim ray = (point - focus) / ellipse
                                    Dim f = (focus - centerPoint) / ellipse
                                    If f.LengthSquared() >= 1 Then f = Vector2.Normalize(f) * 0.999F
                                    Dim a = ray.LengthSquared(), b = 2 * Vector2.Dot(f, ray), c = f.LengthSquared() - 1
                                    Dim hit = If(a < 0.0000001F, 1.0F, (-b + MathF.Sqrt(Math.Max(0, b * b - 4 * a * c))) / (2 * a))
                                    t = If(a < 0.0000001F, 0, 1 / Math.Max(0.000001F, hit))
                                Else
                                    t = If(length = 0, 0, Vector2.Dot(point - origin, direction) / length)
                                End If
                                If spread = "Repeat" Then t -= MathF.Floor(t)
                                If spread = "Reflect" Then t = 1 - Math.Abs((t - 2 * MathF.Floor(t / 2)) - 1)
                                Dim color = Sample(ordered, t)
                                Dim distance = corners.Distance((x + 0.5F) * pixelWidth, (y + 0.5F) * pixelHeight, logicalWidth, logicalHeight)
                                Dim coverage = Math.Clamp(0.5F - distance / antialiasWidth, 0, 1)
                                Dim fillCoverage = If(border = 0, coverage, Math.Clamp(0.5F - (distance + border) / antialiasWidth, 0, 1))
                                Dim fillPixel = Color.FromNonPremultiplied(color.ToVector4()).ToVector4()
                                pixels(y * width + x) = New Color(fillPixel * (fillCoverage * gradientOpacity) + borderPixel * (coverage - fillCoverage))
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
            If texture Is Nothing OrElse texture.Width <> width OrElse texture.Height <> height Then
                texture?.Dispose()
                texture = New Texture2D(device, width, height)
            End If
            texture.SetData(pixels)
            _PixelData = pixels
            _Revision += 1
            previousGeometry = geometry
            previousGradient = gradient
            previousStops = Stops.Select(Function(s) (s.Offset, s.Color)).ToArray()
            Return texture
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
            texture?.Dispose()
            texture = Nothing
            _PixelData = Nothing
        End Sub
    End Class
End Namespace
