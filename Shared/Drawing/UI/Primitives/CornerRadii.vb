Imports System.Globalization

Namespace [Shared].Drawing.UI.Primitives
    ''' <summary>Logical-unit radii in XAML order: top-left, top-right, bottom-right, bottom-left.</summary>
    <Serializable>
    Public Structure CornerRadii
        Implements IEquatable(Of CornerRadii)

        Public ReadOnly TopLeft As Single
        Public ReadOnly TopRight As Single
        Public ReadOnly BottomRight As Single
        Public ReadOnly BottomLeft As Single

        Public Sub New(uniform As Single)
            Me.New(uniform, uniform, uniform, uniform)
        End Sub

        Public Sub New(topLeft As Single, topRight As Single, bottomRight As Single, bottomLeft As Single)
            For Each value In {topLeft, topRight, bottomRight, bottomLeft}
                If Not Single.IsFinite(value) OrElse value < 0 Then Throw New ArgumentOutOfRangeException(NameOf(topLeft), "Corner radii must be finite and nonnegative.")
            Next
            Me.TopLeft = topLeft : Me.TopRight = topRight
            Me.BottomRight = bottomRight : Me.BottomLeft = bottomLeft
        End Sub

        Public Shared Function Parse(value As String) As CornerRadii
            Dim parts = value.Split({","c, " "c, ChrW(9), ChrW(10), ChrW(13)}, StringSplitOptions.RemoveEmptyEntries)
            If parts.Length <> 1 AndAlso parts.Length <> 4 Then Throw New FormatException("CornerRadius requires one or four values.")
            Dim numbers = parts.Select(Function(part) Single.Parse(part, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray()
            Return If(numbers.Length = 1, New CornerRadii(numbers(0)), New CornerRadii(numbers(0), numbers(1), numbers(2), numbers(3)))
        End Function

        Public ReadOnly Property IsEmpty As Boolean
            Get
                Return TopLeft = 0 AndAlso TopRight = 0 AndAlso BottomRight = 0 AndAlso BottomLeft = 0
            End Get
        End Property

        ''' <summary>Prevent adjacent arcs from overlapping on small or resized controls.</summary>
        Public Function Fit(width As Single, height As Single) As CornerRadii
            Dim factor As Single = 1
            For Each pair In {(width, TopLeft + TopRight), (width, BottomLeft + BottomRight),
                              (height, TopLeft + BottomLeft), (height, TopRight + BottomRight)}
                If pair.Item2 > 0 Then factor = Math.Min(factor, Math.Max(0, pair.Item1) / pair.Item2)
            Next
            Return New CornerRadii(TopLeft * factor, TopRight * factor, BottomRight * factor, BottomLeft * factor)
        End Function

        ' Signed distance in logical units; unlike a quadrant-only formula this
        ' also supports a single large corner extending beyond the center line.
        Friend Function Distance(x As Single, y As Single, width As Single, height As Single) As Single
            Dim result = -Math.Min(Math.Min(x, width - x), Math.Min(y, height - y))
            If x < TopLeft AndAlso y < TopLeft Then result = ArcDistance(x - TopLeft, y - TopLeft, TopLeft)
            If x > width - TopRight AndAlso y < TopRight Then result = ArcDistance(x - width + TopRight, y - TopRight, TopRight)
            If x > width - BottomRight AndAlso y > height - BottomRight Then result = ArcDistance(x - width + BottomRight, y - height + BottomRight, BottomRight)
            If x < BottomLeft AndAlso y > height - BottomLeft Then result = ArcDistance(x - BottomLeft, y - height + BottomLeft, BottomLeft)
            Return result
        End Function

        Private Shared Function ArcDistance(x As Single, y As Single, radius As Single) As Single
            Return MathF.Sqrt(x * x + y * y) - radius
        End Function

        Public Overloads Function Equals(other As CornerRadii) As Boolean Implements IEquatable(Of CornerRadii).Equals
            Return TopLeft = other.TopLeft AndAlso TopRight = other.TopRight AndAlso BottomRight = other.BottomRight AndAlso BottomLeft = other.BottomLeft
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return TypeOf obj Is CornerRadii AndAlso Equals(DirectCast(obj, CornerRadii))
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return HashCode.Combine(TopLeft, TopRight, BottomRight, BottomLeft)
        End Function

        Public Overrides Function ToString() As String
            Return String.Join(",", {TopLeft, TopRight, BottomRight, BottomLeft}.Select(Function(value) value.ToString("R", CultureInfo.InvariantCulture)))
        End Function
    End Structure
End Namespace
