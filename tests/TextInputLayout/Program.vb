Imports ProjectZLayoutChecks.Shared.Drawing.UI.Input

Module Program
    Sub Main()
        Dim sample = "AV café" & vbCrLf & "👋 and text" & vbCr & "final AV" & vbLf
        Dim previous As TextInputLayout = Nothing
        For length = 0 To sample.Length
            Dim prefix = sample.Substring(0, length)
            Dim incremental = New TextInputLayout(prefix, 6, 3, AddressOf Measure, previous)
            AssertEquivalent(incremental, New TextInputLayout(prefix, 6, 3, AddressOf Measure), prefix.Length)
            previous = incremental
        Next
        Console.WriteLine("PASS streamed text, soft wrapping, split CRLF, and selection match a full layout")

        AssertEquivalent(New TextInputLayout(sample & "added", 11, 3, AddressOf Measure, previous),
                         New TextInputLayout(sample & "added", 11, 3, AddressOf Measure), sample.Length + 5)
        AssertEquivalent(New TextInputLayout(sample & "added", 6, 5, AddressOf Measure, previous),
                         New TextInputLayout(sample & "added", 6, 5, AddressOf Measure), sample.Length + 5)
        Dim edited = sample.Replace("café", "notes", StringComparison.Ordinal)
        AssertEquivalent(New TextInputLayout(edited, 6, 3, AddressOf Measure, previous),
                         New TextInputLayout(edited, 6, 3, AddressOf Measure), edited.Length)
        Console.WriteLine("PASS width, line-height, and mid-text changes rebuild the layout")

        Dim fullText = New String("x"c, 512) & "abc"
        Dim measurementCount = 0
        Dim measurePrefix As Func(Of String, Single) = Function(value)
                                                          measurementCount += 1
                                                          Return Measure(value)
                                                      End Function
        previous = New TextInputLayout(fullText.Substring(0, 512), 30, 3, AddressOf Measure)
        Dim finalLayout = New TextInputLayout(fullText, 30, 3, measurePrefix, previous)
        AssertEquivalent(finalLayout, New TextInputLayout(fullText, 30, 3, AddressOf Measure), fullText.Length)
        Require(measurementCount <= 5, "appended text remeasured the original prefix")
        Console.WriteLine("PASS appending three characters reuses measurements from a 512-character prefix")
    End Sub

    Private Function Measure(text As String) As Single
        ' Include a kerning-like interaction so a character-width sum would give incorrect caret positions.
        Dim width As Single = text.Length
        For index = 1 To text.Length - 1
            If text(index - 1) = "A"c AndAlso text(index) = "V"c Then width -= 0.25F
        Next
        Return width
    End Function

    Private Sub AssertEquivalent(actual As TextInputLayout, expected As TextInputLayout, textLength As Integer)
        Require(actual.DisplayText = expected.DisplayText AndAlso actual.Height = expected.Height, "visual lines differ")
        Require(actual.Points.SequenceEqual(expected.Points), "insertion positions differ")
        Dim lineHeight = expected.Height / (expected.DisplayText.Count(Function(ch) ch = ChrW(10)) + 1)
        For index = 0 To textLength
            Require(actual.LineBoundary(index, False) = expected.LineBoundary(index, False), "line start differs")
            Require(actual.LineBoundary(index, True) = expected.LineBoundary(index, True), "line end differs")
            Require(actual.HitTest(expected.Points(index), lineHeight) = expected.HitTest(expected.Points(index), lineHeight), "hit test differs")
        Next
        Require(actual.SelectionRectangles(0, textLength).SequenceEqual(expected.SelectionRectangles(0, textLength)), "selection bounds differ")
    End Sub

    Private Sub Require(condition As Boolean, message As String)
        If Not condition Then Throw New InvalidOperationException(message)
    End Sub
End Module
