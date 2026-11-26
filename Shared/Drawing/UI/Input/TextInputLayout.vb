Imports Microsoft.Xna.Framework
Imports System.Text

Namespace [Shared].Drawing.UI.Input
    ' Keep display-only wrapping separate from the editable value. Every source
    ' insertion point uses the same line and font measurements as the drawn text.
    Friend NotInheritable Class TextInputLayout
        Public ReadOnly DisplayText As String
        Public ReadOnly Points As Vector2()
        Private ReadOnly ends As Vector2()
        Private ReadOnly validStops As Boolean()
        Private ReadOnly hardBreaks As Boolean()
        Private ReadOnly rowHeight As Single
        Private ReadOnly wrapWidth As Single
        Private ReadOnly sourceText As String
        Private ReadOnly lastLine As String
        Public ReadOnly Height As Single

        Public Sub New(text As String, width As Single, lineHeight As Single, measure As Func(Of String, Single),
                       Optional previous As TextInputLayout = Nothing)
            ReDim Points(text.Length)
            ReDim ends(text.Length - 1)
            validStops = Enumerable.Repeat(True, text.Length + 1).ToArray()
            ReDim hardBreaks(text.Length - 1)
            rowHeight = lineHeight
            wrapWidth = width
            sourceText = text
            Dim output As New StringBuilder(text.Length)
            Dim line As String = String.Empty
            Dim emptyLineWidth = measure(String.Empty)
            Dim lineWidth = emptyLineWidth
            Dim y As Single
            Dim index As Integer
            If previous IsNot Nothing AndAlso width = previous.wrapWidth AndAlso lineHeight = previous.rowHeight AndAlso previous.CanAppend(text) Then
                index = previous.sourceText.Length
                Array.Copy(previous.Points, Points, index)
                Array.Copy(previous.ends, ends, index)
                Array.Copy(previous.validStops, validStops, index)
                Array.Copy(previous.hardBreaks, hardBreaks, index)
                output.Append(previous.DisplayText)
                line = previous.lastLine
                lineWidth = previous.Points(index).X
                y = previous.Points(index).Y
            End If

            While index < text.Length
                Dim ch = text(index)
                Points(index) = New Vector2(lineWidth, y)
                If ch = ChrW(13) OrElse ch = ChrW(10) Then
                    hardBreaks(index) = True
                    ends(index) = Points(index) + New Vector2(Math.Max(2, measure(" ")), 0)
                    output.Append(ChrW(10))
                    y += lineHeight
                    line = String.Empty
                    lineWidth = emptyLineWidth
                    If ch = ChrW(13) AndAlso index + 1 < text.Length AndAlso text(index + 1) = ChrW(10) Then
                        index += 1
                        Points(index) = Points(index - 1)
                        ends(index) = Points(index)
                        validStops(index) = False
                        hardBreaks(index) = True
                    End If
                Else
                    Dim nextLine = line & ch
                    Dim nextWidth = measure(nextLine)
                    If line.Length > 0 AndAlso nextWidth > width Then
                        output.Append(ChrW(10))
                        y += lineHeight
                        nextLine = ch.ToString()
                        nextWidth = measure(nextLine)
                        Points(index) = New Vector2(0, y)
                    End If
                    output.Append(ch)
                    line = nextLine
                    lineWidth = nextWidth
                    ends(index) = New Vector2(lineWidth, y)
                End If
                index += 1
            End While
            Points(text.Length) = New Vector2(lineWidth, y)
            DisplayText = output.ToString()
            lastLine = line
            Height = y + lineHeight
        End Sub

        Private Function CanAppend(text As String) As Boolean
            If sourceText.Length = 0 OrElse text.Length <= sourceText.Length OrElse
                Not text.StartsWith(sourceText, StringComparison.Ordinal) Then Return False

            ' A split CRLF changes the insertion positions at the end of the previous batch.
            Return sourceText(sourceText.Length - 1) <> ChrW(13) OrElse text(sourceText.Length) <> ChrW(10)
        End Function

        Public Function HitTest(point As Vector2, lineHeight As Single) As Integer
            Dim row = Math.Max(0, CSng(Math.Floor(point.Y / lineHeight)) * lineHeight)
            row = Math.Min(row, Points(Points.Length - 1).Y)
            Dim best As Integer
            Dim distance As Single = Single.MaxValue
            For index = 0 To Points.Length - 1
                If Not validStops(index) Then Continue For
                If Math.Abs(Points(index).Y - row) > 0.5F Then Continue For
                Dim delta = Math.Abs(point.X - Points(index).X)
                If delta < distance Then
                    distance = delta
                    best = index
                End If
            Next
            Return best
        End Function

        Public Function SelectionRectangles(start As Integer, length As Integer) As List(Of Rectangle)
            Dim result As New List(Of Rectangle)
            For index = start To Math.Min(ends.Length, start + length) - 1
                If ends(index).X <= Points(index).X Then Continue For
                Dim cell As New Rectangle(CInt(Math.Floor(Points(index).X)), CInt(Points(index).Y),
                    CInt(Math.Ceiling(ends(index).X) - Math.Floor(Points(index).X)), CInt(Math.Ceiling(rowHeight)))
                If result.Count > 0 AndAlso result(result.Count - 1).Y = cell.Y Then
                    result(result.Count - 1) = Rectangle.Union(result(result.Count - 1), cell)
                Else
                    result.Add(cell)
                End If
            Next
            Return result
        End Function

        Public Function LineBoundary(index As Integer, atEnd As Boolean) As Integer
            Dim y = Points(index).Y
            If atEnd Then
                Dim last = index
                For i = index To ends.Length - 1
                    If hardBreaks(i) Then Return i
                    If Points(i).Y <> y OrElse ends(i).Y <> y Then Exit For
                    ' A soft-wrap boundary is also the start of the next visual row.
                    If Points(i + 1).Y > y AndAlso ends(i).X > Points(i).X Then
                        Return i + 1
                    End If
                    last = i + 1
                Next
                Return last
            End If
            While index > 0 AndAlso Points(index - 1).Y = y
                index -= 1
            End While
            Return index
        End Function
    End Class
End Namespace
