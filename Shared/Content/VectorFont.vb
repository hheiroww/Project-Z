Imports System.Globalization
Imports LibTessDotNet
Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics
Imports Media = System.Windows.Media
Imports ProjectZ.VectorText

Namespace [Shared].Content
    ''' <summary>Compiled free vector outlines tessellated to GPU triangles, with optional host-font shaping.</summary>
    Public NotInheritable Class VectorFont
        Implements IDisposable
        Private ReadOnly face As Media.Typeface
        Private ReadOnly emSize As Double
        Private ReadOnly compiled As CompiledFont
        Private ReadOnly meshes As New TextCache(Of TextMesh)(256)
        Private ReadOnly measurements As New TextCache(Of Vector2)(1024)
        ' Evict a single cold entry, preserving the visible labels when changing
        ' log lines or model rows fill the bounded cache.
        Private NotInheritable Class TextCache(Of T)
            Private ReadOnly capacity As Integer
            Private ReadOnly entries As New Dictionary(Of String, (Value As T, Node As LinkedListNode(Of String)))(StringComparer.Ordinal)
            Private ReadOnly recent As New LinkedList(Of String)
            Public Sub New(capacity As Integer)
                Me.capacity = capacity
            End Sub
            Public Function TryGetValue(key As String, ByRef value As T) As Boolean
                Dim entry As (Value As T, Node As LinkedListNode(Of String)) = Nothing
                If Not entries.TryGetValue(key, entry) Then Return False
                recent.Remove(entry.Node)
                recent.AddFirst(entry.Node)
                value = entry.Value
                Return True
            End Function
            Public Sub Add(key As String, value As T)
                If entries.Count >= capacity Then
                    entries.Remove(recent.Last.Value)
                    recent.RemoveLast()
                End If
                entries.Add(key, (value, recent.AddFirst(key)))
            End Sub
            Public Sub Clear()
                entries.Clear()
                recent.Clear()
            End Sub
        End Class
        Private effect As BasicEffect
        Private clipRasterizer As RasterizerState
        Private ownerDevice As GraphicsDevice
        Private deviceDisposing As EventHandler(Of EventArgs)
        Public ReadOnly Property FilePath As String
        Public ReadOnly Property FamilyName As String
        Public ReadOnly Property LineSpacing As Integer
        Public Property PpiPerfectText As Boolean

        Private NotInheritable Class TextMesh
            Friend ReadOnly Vertices As VertexPositionColor()
            Private ReadOnly colors As Color()
            Private ReadOnly foreground As Boolean()
            Private lastColor As Color?
            Friend Sub New(vertices As List(Of VertexPositionColor), foreground As List(Of Boolean))
                Me.Vertices = vertices.ToArray()
                colors = vertices.Select(Function(vertex) vertex.Color).ToArray()
                Me.foreground = foreground.ToArray()
            End Sub
            Friend Sub Apply(color As Color)
                If lastColor.HasValue AndAlso lastColor.Value = color Then Return
                Dim opacity = color.A / 255.0F
                For index = 0 To Vertices.Length - 1
                    Vertices(index).Color = If(foreground(index), color, colors(index) * opacity)
                Next
                lastColor = color
            End Sub
        End Class

        Public Sub New(pointSize As Single, Optional weight As Integer = 400)
            Me.New(CompiledFont.FamilyName, weight, pointSize)
        End Sub

        Public Shared Function AlignToPixelGrid(position As Vector2, Optional pixelsPerUnit As Single = 1) As Vector2
            If Not Single.IsFinite(pixelsPerUnit) OrElse pixelsPerUnit <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(pixelsPerUnit))
            Return New Vector2(CSng(Math.Round(position.X * pixelsPerUnit, MidpointRounding.AwayFromZero)) / pixelsPerUnit,
                               CSng(Math.Round(position.Y * pixelsPerUnit, MidpointRounding.AwayFromZero)) / pixelsPerUnit)
        End Function

        Public Sub New(familyName As String, weight As Integer, pointSize As Single)
            If String.IsNullOrWhiteSpace(familyName) Then Throw New ArgumentException("Font family is required.", NameOf(familyName))
            If Not Single.IsFinite(pointSize) OrElse pointSize <= 0 OrElse pointSize > 512 Then Throw New ArgumentOutOfRangeException(NameOf(pointSize))
            If weight < 1 OrElse weight > 999 Then Throw New ArgumentOutOfRangeException(NameOf(weight))
            emSize = pointSize * 96.0 / 72.0
            If String.Equals(familyName, CompiledFont.FamilyName, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(familyName, "Segoe UI", StringComparison.OrdinalIgnoreCase) Then
                compiled = CompiledFont.ForWeight(weight)
                Me.FamilyName = CompiledFont.FamilyName
                LineSpacing = CInt(Math.Ceiling(compiled.LineHeight * emSize))
                ' Only unsupported/complex scripts use installed host fallback; no host font is redistributed.
                face = New Media.Typeface(New Media.FontFamily("Arial"), System.Windows.FontStyles.Normal,
                    System.Windows.FontWeight.FromOpenTypeWeight(weight), System.Windows.FontStretches.Normal)
                Return
            End If
            Me.FamilyName = familyName
            face = New Media.Typeface(New Media.FontFamily(familyName), System.Windows.FontStyles.Normal,
                System.Windows.FontWeight.FromOpenTypeWeight(weight), System.Windows.FontStretches.Normal)
            Dim glyphFace As Media.GlyphTypeface = Nothing
            If face.TryGetGlyphTypeface(glyphFace) Then FilePath = glyphFace.FontUri.LocalPath
            emSize = pointSize * 96.0 / 72.0
            LineSpacing = CInt(Math.Ceiling(New Media.FormattedText("Mg", CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight, face, emSize, Media.Brushes.White, 1.0).Height))
        End Sub

        Public Sub New(path As String, pointSize As Single)
            If pointSize <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(pointSize))
            FilePath = IO.Path.GetFullPath(path)
            If Not IO.File.Exists(FilePath) Then Throw New IO.FileNotFoundException("Font file not found.", FilePath)
            Dim glyphFace As New Media.GlyphTypeface(New Uri(FilePath))
            Dim familyName = glyphFace.FamilyNames.Values.First()
            Me.FamilyName = familyName
            Dim family As New Media.FontFamily(New Uri(IO.Path.GetDirectoryName(FilePath) & IO.Path.DirectorySeparatorChar), "./#" & familyName)
            face = New Media.Typeface(family, glyphFace.Style, glyphFace.Weight, glyphFace.Stretch)
            Dim resolved As Media.GlyphTypeface = Nothing
            If Not face.TryGetGlyphTypeface(resolved) OrElse Not String.Equals(resolved.FontUri.LocalPath, FilePath, StringComparison.OrdinalIgnoreCase) Then
                Throw New IO.InvalidDataException("Could not resolve the requested font file: " & FilePath)
            End If
            emSize = pointSize * 96.0 / 72.0
            LineSpacing = CInt(Math.Ceiling(glyphFace.Height * emSize))
        End Sub

        Private Function Format(text As String, Optional pixelPerfect As Boolean = False) As Media.FormattedText
            Return New Media.FormattedText(text, CultureInfo.CurrentUICulture, System.Windows.FlowDirection.LeftToRight,
                face, emSize, Media.Brushes.White, Nothing, If(pixelPerfect, Media.TextFormattingMode.Display, Media.TextFormattingMode.Ideal), 1.0) With {.LineHeight = LineSpacing}
        End Function

        Public Function Measure(text As String, Optional pixelPerfect As Boolean? = Nothing) As Vector2
            If String.IsNullOrEmpty(text) Then Return Vector2.Zero
            Dim aligned = If(pixelPerfect, PpiPerfectText)
            Dim key = If(aligned, "P:", "I:") & text
            Dim size As Vector2
            If Not measurements.TryGetValue(key, size) Then
                If compiled IsNot Nothing AndAlso compiled.SupportsText(text) Then
                    Dim measured = compiled.Measure(text, CSng(emSize), aligned)
                    size = New Vector2(measured.X, measured.Y)
                Else
                    Dim formatted = Format(text, aligned)
                    size = New Vector2(CSng(formatted.WidthIncludingTrailingWhitespace), CSng(formatted.Height))
                End If
                measurements.Add(key, size)
            End If
            Return size
        End Function

        Private Function Triangles(text As String, pixelPerfect As Boolean) As TextMesh
            Dim key = If(pixelPerfect, "P:", "I:") & text
            Dim result As TextMesh = Nothing
            If meshes.TryGetValue(key, result) Then Return result
            If compiled IsNot Nothing AndAlso compiled.SupportsText(text) Then
                Dim coordinates = compiled.GetTriangles(text, CSng(emSize), pixelPerfect)
                Dim compiledVertices As New List(Of VertexPositionColor)(coordinates.Length \ 2)
                Dim compiledForeground As New List(Of Boolean)(coordinates.Length \ 2)
                For index = 0 To coordinates.Length - 1 Step 2
                    compiledVertices.Add(New VertexPositionColor(New Vector3(coordinates(index), coordinates(index + 1), 0), Color.White))
                    compiledForeground.Add(True)
                Next
                result = New TextMesh(compiledVertices, compiledForeground)
                meshes.Add(key, result)
                Return result
            End If
            Dim formatted = Format(text, pixelPerfect)
            Dim visual As New Media.DrawingVisual
            Using context = visual.RenderOpen()
                context.DrawText(formatted, New System.Windows.Point())
            End Using
            Dim drawing = visual.Drawing
            Dim vertices As New List(Of VertexPositionColor)
            Dim foreground As New List(Of Boolean)
            If HasColorGlyphs(drawing) Then
                AppendDrawing(drawing, Media.Matrix.Identity, vertices, foreground)
            Else
                AppendGeometry(formatted.BuildGeometry(New System.Windows.Point()), Color.White, True, Media.Matrix.Identity, vertices, foreground)
            End If
            result = New TextMesh(vertices, foreground)
            meshes.Add(key, result)
            Return result
        End Function

        Private Shared Function HasColorGlyphs(drawing As Media.Drawing) As Boolean
            If TypeOf drawing Is Media.DrawingGroup Then Return DirectCast(drawing, Media.DrawingGroup).Children.Any(Function(child) HasColorGlyphs(child))
            Dim glyphDrawing = TryCast(drawing, Media.GlyphRunDrawing)
            If glyphDrawing Is Nothing Then Return False
            Dim data = ColorFontLayers.ForTypeface(glyphDrawing.GlyphRun.GlyphTypeface)
            Return data IsNot Nothing AndAlso glyphDrawing.GlyphRun.GlyphIndices.Any(Function(glyph) data.ForGlyph(glyph) IsNot Nothing)
        End Function

        Private Shared Sub AppendDrawing(drawing As Media.Drawing, transform As Media.Matrix, vertices As List(Of VertexPositionColor), foreground As List(Of Boolean))
            If TypeOf drawing Is Media.DrawingGroup Then
                Dim group = DirectCast(drawing, Media.DrawingGroup)
                Dim matrix = Media.Matrix.Multiply(If(group.Transform Is Nothing, Media.Matrix.Identity, group.Transform.Value), transform)
                For Each child In group.Children
                    AppendDrawing(child, matrix, vertices, foreground)
                Next
                Return
            End If
            Dim glyphDrawing = TryCast(drawing, Media.GlyphRunDrawing)
            If glyphDrawing Is Nothing Then Return
            Dim run = glyphDrawing.GlyphRun
            Dim data = ColorFontLayers.ForTypeface(run.GlyphTypeface)
            If data Is Nothing Then
                AppendGeometry(run.BuildGeometry(), Color.White, True, transform, vertices, foreground)
                Return
            End If
            Dim advance As Double
            For index = 0 To run.GlyphIndices.Count - 1
                Dim layers = data.ForGlyph(run.GlyphIndices(index))
                If layers Is Nothing Then layers = {New ColorFontLayers.Layer With {.Glyph = run.GlyphIndices(index), .Color = Color.White, .Foreground = True}}
                Dim origin = run.BaselineOrigin
                origin.X += If((run.BidiLevel And 1) <> 0, -advance, advance)
                Dim offsets As System.Windows.Point() = Nothing
                If run.GlyphOffsets IsNot Nothing AndAlso run.GlyphOffsets.Count > index Then offsets = {run.GlyphOffsets(index)}
                For Each layer In layers
                    Dim glyph As New Media.GlyphRun(run.GlyphTypeface, run.BidiLevel, run.IsSideways, run.FontRenderingEmSize, run.PixelsPerDip,
                        {layer.Glyph}, origin, {run.AdvanceWidths(index)}, offsets, Nothing, Nothing, Nothing, Nothing, run.Language)
                    AppendGeometry(glyph.BuildGeometry(), layer.Color, layer.Foreground, transform, vertices, foreground)
                Next
                advance += run.AdvanceWidths(index)
            Next
        End Sub

        Private Shared Sub AppendGeometry(source As Media.Geometry, color As Color, useForeground As Boolean, transform As Media.Matrix, vertices As List(Of VertexPositionColor), foreground As List(Of Boolean))
            Dim placed = source.Clone()
            placed.Transform = New Media.MatrixTransform(Media.Matrix.Multiply(If(placed.Transform Is Nothing, Media.Matrix.Identity, placed.Transform.Value), transform))
            Dim geometry = placed.GetFlattenedPathGeometry(0.025, Media.ToleranceType.Absolute)
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
            For i = 0 To tessellator.ElementCount * 3 - 1 Step 3
                If tessellator.Elements(i) < 0 OrElse tessellator.Elements(i + 1) < 0 OrElse tessellator.Elements(i + 2) < 0 Then Continue For
                For j = 0 To 2
                    Dim p = tessellator.Vertices(tessellator.Elements(i + j)).Position
                    vertices.Add(New VertexPositionColor(New Vector3(p.X, p.Y, 0), color))
                    foreground.Add(useForeground)
                Next
            Next
        End Sub

        Public Sub Draw(device As GraphicsDevice, text As String, position As Vector2, color As Color, offset As Vector2, Optional textScale As Single = 1, Optional pixelPerfect As Boolean? = Nothing)
            If String.IsNullOrWhiteSpace(text) Then Return
            Dim quality = Drawing.GraphicsQuality.ForDevice(device)
            Dim aligned = If(pixelPerfect, PpiPerfectText OrElse quality.PpiPerfectText)
            If aligned Then position = AlignToPixelGrid(position)
            Dim mesh = Triangles(text, aligned)
            Dim vertices = mesh.Vertices
            If vertices.Length = 0 Then Return
            If effect Is Nothing Then
                effect = New BasicEffect(device) With {.VertexColorEnabled = True}
                clipRasterizer = New RasterizerState With {.CullMode = CullMode.None, .ScissorTestEnable = True, .MultiSampleAntiAlias = True}
                ownerDevice = device
                deviceDisposing = Sub(sender, args) Dispose()
                AddHandler ownerDevice.Disposing, deviceDisposing
            End If
            mesh.Apply(color)
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
            If ownerDevice IsNot Nothing AndAlso deviceDisposing IsNot Nothing Then RemoveHandler ownerDevice.Disposing, deviceDisposing
            ownerDevice = Nothing
            deviceDisposing = Nothing
            effect?.Dispose()
            effect = Nothing
            clipRasterizer?.Dispose()
            clipRasterizer = Nothing
            meshes.Clear()
            measurements.Clear()
        End Sub
    End Class
End Namespace
