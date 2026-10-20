Imports Microsoft.Xna.Framework
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Threading
Imports System.Threading.Tasks

Namespace [Shared].Drawing.UI.Input
    Public Partial Class Textbox
        ''' <summary>Automatic offline spelling is enabled for ordinary editable text by default.</summary>
        Public Property IsSpellCheckEnabled As Boolean = True
        Public Property SpellCheckLanguage As String = If(String.IsNullOrEmpty(CultureInfo.CurrentCulture.Name), "en-US", CultureInfo.CurrentCulture.Name)
        ''' <summary>Set to share a custom word list, or leave Nothing for the per-application dictionary.</summary>
        Public Property SpellChecker As OfflineSpellChecker
        Public ReadOnly Property SpellCheckError As String
            Get
                Return spellingFailure
            End Get
        End Property
        Private spellingFailure As String
        Private spellingTask As Task(Of IReadOnlyList(Of SpellingIssue))
        Private spellingSource As String
        Private spellingLanguage As String
        Private spellingChecker As OfflineSpellChecker
        Private spellingRevision As Integer = -1
        Private spellingChangedAt As Long
        Private spellingIssues As IReadOnlyList(Of SpellingIssue) = Array.Empty(Of SpellingIssue)()
        Private spellingComplete As Boolean
        Private spellingCancellation As CancellationTokenSource
        Private ReadOnly Property CanSpellCheck As Boolean
            Get
                Return IsSpellCheckEnabled AndAlso Not IsReadOnly AndAlso Not MaskCharacter.HasValue AndAlso Not TypeOf Me Is PasswordBox
            End Get
        End Property
        Public ReadOnly Property SpellingErrors As IReadOnlyList(Of SpellingIssue)
            Get
                If Not CurrentSpellingMatches() Then Return Array.Empty(Of SpellingIssue)()
                Return spellingIssues
            End Get
        End Property
        Private Function CurrentSpellingMatches() As Boolean
            Return CanSpellCheck AndAlso spellingChecker IsNot Nothing AndAlso spellingSource = Text AndAlso
                spellingLanguage = SpellCheckLanguage AndAlso ReferenceEquals(spellingChecker, If(SpellChecker, OfflineSpellChecker.Default)) AndAlso
                spellingRevision = spellingChecker.Revision
        End Function
        Private Sub UpdateSpellCheck()
            If Not CanSpellCheck Then
                spellingCancellation?.Cancel()
                spellingIssues = Array.Empty(Of SpellingIssue)()
                spellingComplete = False
                Return
            End If
            Dim checker As OfflineSpellChecker
            Try
                checker = If(SpellChecker, OfflineSpellChecker.Default)
            Catch ex As Exception
                spellingFailure = ex.Message
                Return
            End Try
            If Not CurrentSpellingMatches() Then
                spellingCancellation?.Cancel()
                spellingSource = Text : spellingLanguage = SpellCheckLanguage : spellingChecker = checker
                spellingRevision = checker.Revision : spellingChangedAt = Environment.TickCount64
                spellingIssues = Array.Empty(Of SpellingIssue)() : spellingComplete = False
            End If
            If spellingTask IsNot Nothing Then
                If Not spellingTask.IsCompleted Then Return
                ' A job only belongs to the text/configuration that scheduled it.
                If spellingTask.IsFaulted Then
                    spellingFailure = spellingTask.Exception.GetBaseException().Message
                ElseIf Not spellingTask.IsCanceled AndAlso taskSource = spellingSource AndAlso taskLanguage = spellingLanguage AndAlso
                    ReferenceEquals(taskChecker, checker) AndAlso taskRevision = spellingRevision Then
                    spellingIssues = spellingTask.Result
                    spellingComplete = True : spellingFailure = Nothing
                End If
                spellingTask = Nothing
                spellingCancellation?.Dispose()
                spellingCancellation = Nothing
            End If
            If Not spellingComplete AndAlso Text.Length > 0 AndAlso Environment.TickCount64 - spellingChangedAt >= 250 Then
                taskSource = spellingSource : taskLanguage = spellingLanguage : taskChecker = checker : taskRevision = spellingRevision
                spellingCancellation = New CancellationTokenSource()
                spellingTask = checker.CheckAsync(taskSource, taskLanguage, spellingCancellation.Token)
                ' Do not retry a failed engine on every frame. Editing or changing configuration retries.
                spellingComplete = True
            End If
        End Sub
        Private taskSource, taskLanguage As String
        Private taskChecker As OfflineSpellChecker
        Private taskRevision As Integer

        ''' <summary>Visible underline regions, using the same wrapping/scrolling layout as selection.</summary>
        Public ReadOnly Property SpellingUnderlineBounds As IReadOnlyList(Of Rectangle)
            Get
                Dim result As New List(Of Rectangle)
                If inputLayout Is Nothing Then Return result
                Dim area As New Rectangle(CInt(Position.X + TextPadding.X), CInt(Position.Y + TextPadding.Y),
                    CInt(Math.Max(0, Size.X - TextPadding.X * 2)), CInt(Math.Max(0, Size.Y - TextPadding.Y * 2)))
                For Each issue In SpellingErrors
                    For Each bounds In inputLayout.SelectionRectangles(issue.Start, issue.Length)
                        bounds.Offset(CInt(TextElement.Position.X), CInt(TextElement.Position.Y))
                        bounds = Rectangle.Intersect(New Rectangle(bounds.X, bounds.Bottom - 3, bounds.Width, 3), area)
                        If bounds.Width > 0 AndAlso bounds.Height > 0 Then result.Add(bounds)
                    Next
                Next
                Return result
            End Get
        End Property
        Private Sub DrawSpellingErrors()
            Dim red = ApplyOpacity(New Color(235, 58, 64))
            For Each bounds In SpellingUnderlineBounds
                For x = 0 To bounds.Width - 1
                    Dim y = If(x Mod 4 = 3, 1, x Mod 4)
                    If y < bounds.Height Then spriteBatch.Draw(Texture, New Rectangle(bounds.X + x, bounds.Y + y, 1, 1), red)
                Next
            Next
        End Sub
        Private Sub AddSpellingMenuItems(point As Point)
            If Not CanSpellCheck Then Return
            Dim absolute = Position + point.ToVector2()
            Dim issue = SpellingErrors.FirstOrDefault(Function(errorItem)
                                                         Return inputLayout.SelectionRectangles(errorItem.Start, errorItem.Length).Any(
                                                             Function(bounds)
                                                                 bounds.Offset(CInt(TextElement.Position.X), CInt(TextElement.Position.Y))
                                                                 Return bounds.Contains(New Point(CInt(absolute.X), CInt(absolute.Y)))
                                                             End Function)
                                                     End Function)
            If issue Is Nothing Then Return
            Dim source = Text
            For Each suggestion In issue.Suggestions
                Dim replacement = suggestion
                AddSpellingItem(replacement, Sub()
                                                 If Not CanSpellCheck OrElse Text <> source Then Return
                                                 [Select](issue.Start, issue.Length)
                                                 ReplaceSelection(replacement)
                                             End Sub)
            Next
            If issue.Suggestions.Count = 0 Then AddSpellingItem("No spelling suggestions", Nothing)
            AddSpellingItem("Add to dictionary", Sub()
                                                     If Not CanSpellCheck OrElse Text <> source Then Return
                                                     Try
                                                         spellingChecker.AddWord(issue.Word)
                                                         spellingFailure = Nothing
                                                     Catch ex As Exception
                                                         spellingFailure = ex.Message
                                                     End Try
                                                 End Sub)
        End Sub
        Private Sub AddSpellingItem(label As String, action As Action)
            Dim item As New MenuItem(Scene) With {.Header = label, .AutoSize = ButtonAutoSize.None,
                .Size = New Vector2(212, 32), .ForegroundColor = Color.White,
                .BackgroundColor = New Color(17, 23, 28), .MouseOverBackgroundColor = New Color(42, 55, 49), .isEnabled = action IsNot Nothing}
            AddHandler item.MouseLeftClick, Sub(p)
                                                editMenu.IsOpen = False
                                                action?.Invoke()
                                                Scene.FocusElement(Me)
                                            End Sub
            editMenu.AddItem(item)
            editMenu.Size = New Vector2(220, editMenu.Size.Y + 32)
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then spellingCancellation?.Cancel()
            If disposing AndAlso editMenu IsNot Nothing Then
                editMenu.IsOpen = False
                editMenu.Dispose()
                editMenu = Nothing
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
