Imports System.Globalization

Namespace [Shared].Drawing.UI.Input
    Friend NotInheritable Class TextSelectionNavigation
        Private Enum UnitKind
            Word
            Space
            LineBreak
            Other
        End Enum

        Private ReadOnly boundaries As Integer()
        Public ReadOnly Source As String

        Public Sub New(text As String)
            Source = text
            boundaries = StringInfo.ParseCombiningCharacters(text)
        End Sub

        Public Function Previous(position As Integer) As Integer
            Dim index = Array.BinarySearch(boundaries, position)
            If index < 0 Then index = Not index
            Return If(index > 0, boundaries(index - 1), 0)
        End Function

        Public Function [Next](position As Integer) As Integer
            Dim index = Array.BinarySearch(boundaries, position)
            index = If(index < 0, Not index, index + 1)
            Return If(index < boundaries.Length, boundaries(index), Source.Length)
        End Function

        Public Function UnitAt(position As Integer) As (Start As Integer, Finish As Integer)
            If boundaries.Length = 0 Then Return (0, 0)
            Dim index = Array.BinarySearch(boundaries, Math.Clamp(position, 0, Source.Length))
            If index < 0 Then index = (Not index) - 1
            Return (boundaries(UnitStart(index)), PositionAt(UnitEnd(index)))
        End Function

        Public Function MoveWord(position As Integer, backwards As Boolean) As Integer
            If Source.Length = 0 Then Return 0
            Dim index = Array.BinarySearch(boundaries, position)
            If backwards Then
                index = If(index < 0, Not index, index) - 1
                While index >= 0 AndAlso IsWhiteSpace(index)
                    index -= 1
                End While
                Return If(index >= 0, boundaries(UnitStart(index)), 0)
            End If
            If index < 0 Then index = (Not index) - 1
            index = UnitEnd(index)
            While index < boundaries.Length AndAlso IsWhiteSpace(index)
                index += 1
            End While
            Return PositionAt(index)
        End Function

        Private Function UnitStart(index As Integer) As Integer
            Dim kind = Classify(index)
            While index > 0 AndAlso kind <> UnitKind.LineBreak AndAlso Classify(index - 1) = kind
                index -= 1
            End While
            Return index
        End Function

        Private Function UnitEnd(index As Integer) As Integer
            Dim kind = Classify(index)
            index += 1
            While index < boundaries.Length AndAlso kind <> UnitKind.LineBreak AndAlso Classify(index) = kind
                index += 1
            End While
            Return index
        End Function

        Private Function PositionAt(index As Integer) As Integer
            Return If(index < boundaries.Length, boundaries(index), Source.Length)
        End Function

        Private Function IsWhiteSpace(index As Integer) As Boolean
            Return Char.IsWhiteSpace(Source(boundaries(index)))
        End Function

        Private Function Classify(index As Integer) As UnitKind
            Dim position = boundaries(index)
            Dim character = Source(position)
            If character = ChrW(13) OrElse character = ChrW(10) Then Return UnitKind.LineBreak
            If Char.IsWhiteSpace(character) Then Return UnitKind.Space
            If Char.IsLetterOrDigit(Source, position) OrElse character = "_"c Then Return UnitKind.Word
            Return UnitKind.Other
        End Function
    End Class
End Namespace
