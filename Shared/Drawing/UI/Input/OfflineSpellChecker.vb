Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks

Namespace [Shared].Drawing.UI.Input
    ''' <summary>A spelling error indexed in the original UTF-16 text.</summary>
    Public NotInheritable Class SpellingIssue
        Public ReadOnly Property Start As Integer
        Public ReadOnly Property Length As Integer
        Public ReadOnly Property Word As String
        Public ReadOnly Property Suggestions As IReadOnlyList(Of String)
        Public Sub New(start As Integer, length As Integer, word As String, suggestions As IEnumerable(Of String))
            Me.Start = start : Me.Length = length : Me.Word = word
            Me.Suggestions = Array.AsReadOnly(suggestions.Take(5).ToArray())
        End Sub
    End Class

    ''' <summary>Offline Windows spelling with an application-owned UTF-8 word list.
    ''' The built-in engine does not write or transmit typed text. Application providers own their data policy.</summary>
    Public NotInheritable Class OfflineSpellChecker
#If WINDOWS Then
        Private Shared ReadOnly jobs As New BlockingCollection(Of Action)
        Private Shared ReadOnly worker As Thread = StartWorker()
#End If
        Public Shared ReadOnly Property IsSupported As Boolean
            Get
#If WINDOWS Then
                Return True
#Else
                Return False
#End If
            End Get
        End Property
        Private Shared ReadOnly defaultInstance As New Lazy(Of OfflineSpellChecker)(
            Function() New OfflineSpellChecker(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProjectZ", "Spelling", If(Assembly.GetEntryAssembly()?.GetName().Name, "Application"), "dictionary.txt")))
        Private ReadOnly gate As New Object
        Private words As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Private revisionValue As Integer
        Private providerValue As Func(Of String, String, CancellationToken, Task(Of IReadOnlyList(Of SpellingIssue)))
        ''' <summary>An optional application-owned spelling engine. UTF-16 ranges and cancellation are validated.</summary>
        Public Property Provider As Func(Of String, String, CancellationToken, Task(Of IReadOnlyList(Of SpellingIssue)))
            Get
                SyncLock gate
                    Return providerValue
                End SyncLock
            End Get
            Set(value As Func(Of String, String, CancellationToken, Task(Of IReadOnlyList(Of SpellingIssue))))
                SyncLock gate
                    providerValue = value
                    revisionValue += 1
                End SyncLock
            End Set
        End Property
        Public ReadOnly Property IsAvailable As Boolean
            Get
                Return Provider IsNot Nothing OrElse IsSupported
            End Get
        End Property
        Public Shared ReadOnly Property [Default] As OfflineSpellChecker
            Get
                Return defaultInstance.Value
            End Get
        End Property
        Public ReadOnly Property DictionaryPath As String
        Public ReadOnly Property Revision As Integer
            Get
                SyncLock gate
                    Return revisionValue
                End SyncLock
            End Get
        End Property
        Public Sub New(dictionaryPath As String)
            Me.DictionaryPath = Path.GetFullPath(dictionaryPath)
            ReloadDictionary()
        End Sub
        ''' <summary>Reload after editing the word list externally. One word per line; # starts a comment.</summary>
        Public Sub ReloadDictionary()
            SyncLock gate
                Dim loaded As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                If File.Exists(DictionaryPath) Then
                    For Each line In File.ReadLines(DictionaryPath)
                        Dim word = line.Trim()
                        If word.Length > 0 AndAlso Not word.StartsWith("#", StringComparison.Ordinal) Then loaded.Add(word)
                    Next
                End If
                words = loaded
                revisionValue += 1
            End SyncLock
        End Sub
        Public Function ContainsWord(word As String) As Boolean
            SyncLock gate
                Return words.Contains(word)
            End SyncLock
        End Function
        ''' <summary>Persists before accepting the word. IO errors are reported to the caller.</summary>
        Public Sub AddWord(word As String)
            If String.IsNullOrWhiteSpace(word) OrElse word.Any(Function(c) Char.IsWhiteSpace(c) OrElse Char.IsControl(c)) OrElse word.StartsWith("#", StringComparison.Ordinal) Then
                Throw New ArgumentException("A dictionary entry must be a single word.", NameOf(word))
            End If
            SyncLock gate
                ' Merge external additions before writing; keep other processes' entries.
                ReloadDictionary()
                If words.Contains(word) Then Return
                Directory.CreateDirectory(Path.GetDirectoryName(DictionaryPath))
                ' Appending preserves comments and avoids rewriting an externally edited dictionary.
                File.AppendAllText(DictionaryPath, Environment.NewLine & word & Environment.NewLine, New System.Text.UTF8Encoding(False))
                words.Add(word)
                revisionValue += 1
            End SyncLock
        End Sub
#If WINDOWS Then
        Private Shared Function StartWorker() As Thread
            Dim result As New Thread(Sub()
                                         For Each job In jobs.GetConsumingEnumerable()
                                             job()
                                         Next
                                     End Sub) With {.IsBackground = True, .Name = "ProjectZ offline spelling"}
            result.SetApartmentState(ApartmentState.STA)
            result.Start()
            Return result
        End Function
#End If
        ''' <summary>Runs the local WPF/Windows spelling engine on a dedicated STA, away from rendering.</summary>
        Public Function CheckAsync(text As String, language As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of IReadOnlyList(Of SpellingIssue))
            Dim currentProvider = Provider
            If currentProvider IsNot Nothing Then Return CheckProviderAsync(text, language, currentProvider, cancellationToken)
#If WINDOWS Then
            Dim completion As New TaskCompletionSource(Of IReadOnlyList(Of SpellingIssue))(TaskCreationOptions.RunContinuationsAsynchronously)
            jobs.Add(Sub()
                         Try
                             cancellationToken.ThrowIfCancellationRequested()
                             Dim box As New System.Windows.Controls.TextBox With {
                                 .Language = System.Windows.Markup.XmlLanguage.GetLanguage(language), .Text = text}
                             System.Windows.Controls.SpellCheck.SetIsEnabled(box, True)
                             Dim result As New List(Of SpellingIssue)
                             Dim suggestions As New Dictionary(Of String, String())(StringComparer.OrdinalIgnoreCase)
                             Dim index = 0
                             While index < text.Length
                                 cancellationToken.ThrowIfCancellationRequested()
                                 Dim start = box.GetNextSpellingErrorCharacterIndex(index, System.Windows.Documents.LogicalDirection.Forward)
                                 If start < 0 Then Exit While
                                 Dim length = box.GetSpellingErrorLength(start)
                                 If length <= 0 Then Exit While
                                 Dim word = text.Substring(start, length)
                                 If Not ContainsWord(word) Then
                                     If Not suggestions.ContainsKey(word) Then suggestions(word) = box.GetSpellingError(start).Suggestions.Take(5).ToArray()
                                     result.Add(New SpellingIssue(start, length, word, suggestions(word)))
                                 End If
                                 index = start + length
                             End While
                             System.Windows.Controls.SpellCheck.SetIsEnabled(box, False)
                             box.Clear()
                             completion.SetResult(result.AsReadOnly())
                         Catch ex As OperationCanceledException
                             completion.SetCanceled(cancellationToken)
                         Catch ex As Exception
                             completion.SetException(ex)
                         End Try
                     End Sub)
            Return completion.Task
#Else
            Return Task.FromException(Of IReadOnlyList(Of SpellingIssue))(New PlatformNotSupportedException("The built-in offline spelling engine requires Windows."))
#End If
        End Function

        Private Async Function CheckProviderAsync(text As String, language As String,
                provider As Func(Of String, String, CancellationToken, Task(Of IReadOnlyList(Of SpellingIssue))),
                cancellationToken As CancellationToken) As Task(Of IReadOnlyList(Of SpellingIssue))
            cancellationToken.ThrowIfCancellationRequested()
            Dim issues = Await provider(text, language, cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            If issues Is Nothing Then Throw New InvalidDataException("The spelling provider returned no result.")
            Dim result As New List(Of SpellingIssue)
            For Each issue In issues
                If issue Is Nothing OrElse issue.Start < 0 OrElse issue.Length <= 0 OrElse issue.Start > text.Length - issue.Length Then
                    Throw New InvalidDataException("The spelling provider returned an invalid UTF-16 range.")
                End If
                If Not ContainsWord(issue.Word) Then result.Add(issue)
            Next
            Return result.AsReadOnly()
        End Function
    End Class
End Namespace
