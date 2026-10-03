Imports System.Globalization
Imports LibTessDotNet
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports Media = System.Windows.Media

Namespace [Shared].Content
    ''' <summary>Shaped font-file outlines tessellated to GPU triangles, without a bitmap atlas.</summary>
    Public NotInheritable Class VectorFont
        Implements IDisposable
        Private ReadOnly face As Media.Typeface
        Private ReadOnly emSize As Double
        Private ReadOnly meshes As New Dictionary(Of String, VertexPositionColor())(StringComparer.Ordinal)
        Private ReadOnly measurements As New Dictionary(Of String, Vector2)(StringComparer.Ordinal)
        Private effect As BasicEffect
        Private clipRasterizer As RasterizerState
        Public ReadOnly Property FilePath As String
        Public ReadOnly Property LineSpacing As Integer

        Public Sub New(path As String, pointSize As Single)
            If pointSize <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(pointSize))
            FilePath = IO.Path.GetFullPath(path)
            If Not IO.File.Exists(FilePath) Then Throw New IO.FileNotFoundException("Font file not found.", FilePath)
            Dim glyphFace As New Media.GlyphTypeface(New Uri(FilePath))
            Dim familyName = glyphFace.FamilyNames.Values.First()
            Dim family As New Media.FontFamily(New Uri(IO.Path.GetDirectoryName(FilePath) & IO.Path.DirectorySeparatorChar), "./#" & familyName)
            face = New Media.Typeface(family, glyphFace.Style, glyphFace.Weight, glyphFace.Stretch)
            Dim resolved As Media.GlyphTypeface = Nothing
            If Not face.TryGetGlyphTypeface(resolved) OrElse Not String.Equals(resolved.FontUri.LocalPath, FilePath, StringComparison.OrdinalIgnoreCase) Then
                Throw New IO.InvalidDataException("Could not resolve the requested font file: " & FilePath)
            End If
            emSize = pointSize * 96.0 / 72.0
            LineSpacing = CInt(Math.Ceiling(glyphFace.Height * emSize))
        End Sub

        Private Function Format(text As String) As Media.FormattedText
            Return New Media.FormattedText(text, CultureInfo.CurrentUICulture, System.Windows.FlowDirection.LeftToRight,
                face, emSize, Media.Brushes.White, 1.0) With {.LineHeight = LineSpacing}
        End Function

        Public Function Measure(text As String) As Vector2
            If String.IsNullOrEmpty(text) Then Return Vector2.Zero
            Dim size As Vector2
            If Not measurements.TryGetValue(text, size) Then
                Dim formatted = Format(text)
                size = New Vector2(CSng(formatted.WidthIncludingTrailingWhitespace), CSng(formatted.Height))
                If measurements.Count >= 1024 Then measurements.Clear()
                measurements.Add(text, size)
            End If
            Return size
        End Function

        Private Function Triangles(text As String) As VertexPositionColor()
            Dim result As VertexPositionColor() = Nothing
            If meshes.TryGetValue(text, result) Then Return result
            Dim geometry = Format(text).BuildGeometry(New System.Windows.Point()).GetFlattenedPathGeometry(0.025, Media.ToleranceType.Absolute)
            Dim tessellator As New Tess()
            For Each figure In geometry.Figures
                If Not figure.IsFilled Then Continue For
                Dim points As New List(Of System.Windows.Point) From {figure.StartPoint}
                For Each segment In figure.Segments
                    If TypeOf segment Is Media.PolyLineSegment Then
                        points.AddRange(DirectCast(segment, Media.PolyLineSegment).Points)
                    ElseIf TypeOf segment Is Media.LineSegment Then
                        points.Add(DirectCast(segment, Media.LineSegment).Point)
                    End If
                Next
                If points.Count < 3 Then Continue For
                tessellator.AddContour(points.Select(Function(p) New ContourVertex With {.Position = New Vec3(CSng(p.X), CSng(p.Y), 0)}).ToArray())
            Next
            tessellator.Tessellate(If(geometry.FillRule = Media.FillRule.Nonzero, WindingRule.NonZero, WindingRule.EvenOdd), ElementType.Polygons, 3)
            Dim vertices As New List(Of VertexPositionColor)
            For i = 0 To tessellator.ElementCount * 3 - 1 Step 3
                If tessellator.Elements(i) < 0 OrElse tessellator.Elements(i + 1) < 0 OrElse tessellator.Elements(i + 2) < 0 Then Continue For
                For j = 0 To 2
                    Dim p = tessellator.Vertices(tessellator.Elements(i + j)).Position
                    vertices.Add(New VertexPositionColor(New Vector3(p.X, p.Y, 0), Color.White))
                Next
            Next
            result = vertices.ToArray()
            If meshes.Count >= 256 Then meshes.Clear()
            meshes.Add(text, result)
            Return result
        End Function

        Public Sub Draw(device As GraphicsDevice, text As String, position As Vector2, color As Color, offset As Vector2, Optional textScale As Single = 1)
            If String.IsNullOrWhiteSpace(text) Then Return
            Dim vertices = Triangles(text)
            If vertices.Length = 0 Then Return
            If effect Is Nothing Then
                effect = New BasicEffect(device) With {.VertexColorEnabled = True}
                clipRasterizer = New RasterizerState With {.CullMode = CullMode.None, .ScissorTestEnable = True, .MultiSampleAntiAlias = True}
                AddHandler device.Disposing, Sub(sender, args) Dispose()
            End If
            If vertices(0).Color <> color Then
                For i = 0 To vertices.Length - 1
                    vertices(i).Color = color
                Next
            End If
            Dim scale = Drawing.GraphicsQuality.ForDevice(device).RenderScale
            effect.World = Matrix.CreateScale(textScale, textScale, 1) * Matrix.CreateTranslation(position.X - offset.X, position.Y - offset.Y, 0) * Matrix.CreateScale(scale.X, scale.Y, 1)
            effect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, 1)
            Dim rasterizer = device.RasterizerState
            Dim blend = device.BlendState
            Dim depth = device.DepthStencilState
            Try
                device.BlendState = BlendState.AlphaBlend
                device.DepthStencilState = DepthStencilState.None
                device.RasterizerState = If(rasterizer.ScissorTestEnable, clipRasterizer, RasterizerState.CullNone)
                For Each pass In effect.CurrentTechnique.Passes
                    pass.Apply()
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length \ 3)
                Next
            Finally
                device.RasterizerState = rasterizer
                device.BlendState = blend
                device.DepthStencilState = depth
            End Try
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            effect?.Dispose()
            effect = Nothing
            clipRasterizer?.Dispose()
            clipRasterizer = Nothing
            meshes.Clear()
            measurements.Clear()
        End Sub
    End Class
End Namespace
