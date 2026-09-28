Imports Microsoft.Xna.Framework
Imports System.Text

Namespace [Shared].Drawing.UI.Input
    ' Keep display-only wrapping separate from the editable value. Every source
    ' insertion point uses the same line and font measurements as the drawn text.
    Friend NotInheritable Class TextInputLayout
        Public ReadOnly DisplayText As String
        Public ReadOnly Points As Vector2()
        Public ReadOnly Height As Single

        Public Sub New(text As String, width As Single, lineHeight As Single, measure As Func(Of String, Single))
            ReDim Points(text.Length)
            Dim output As New StringBuilder()
            Dim line As String = String.Empty
            Dim y As Single
            Dim index As Integer
            While index < text.Length
                Dim ch = text(index)
                Points(index) = New Vector2(measure(line), y)
                If ch = ChrW(13) OrElse ch = ChrW(10) Then
                    output.Append(ChrW(10))
                    y += lineHeight
                    line = String.Empty
                    If ch = ChrW(13) AndAlso index + 1 < text.Length AndAlso text(index + 1) = ChrW(10) Then
                        index += 1
                        Points(index) = New Vector2(0, y)
                    End If
                Else
                    If line.Length > 0 AndAlso measure(line & ch) > width Then
                        output.Append(ChrW(10))
                        y += lineHeight
                        line = String.Empty
                        Points(index) = New Vector2(0, y)
                    End If
                    output.Append(ch)
                    line &= ch
                End If
                index += 1
            End While
            Points(text.Length) = New Vector2(measure(line), y)
            DisplayText = output.ToString()
            Height = y + lineHeight
        End Sub

        Public Function HitTest(point As Vector2, lineHeight As Single) As Integer
            Dim row = Math.Max(0, CSng(Math.Floor(point.Y / lineHeight)) * lineHeight)
            row = Math.Min(row, Points(Points.Length - 1).Y)
            Dim best As Integer
            Dim distance As Single = Single.MaxValue
            For index = 0 To Points.Length - 1
                If Math.Abs(Points(index).Y - row) > 0.5F Then Continue For
                Dim delta = Math.Abs(point.X - Points(index).X)
                If delta < distance Then
                    distance = delta
                    best = index
                End If
            Next
            Return best
        End Function
    End Class
End Namespace
