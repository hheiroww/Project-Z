Namespace RuntimeDiagnostics
    Friend Module ObserverProgram
        <STAThread>
        Sub Main(args As String())
            If Not args.Contains("--diagnostics-watch") Then Return
            Dim target As Integer
            If Not Integer.TryParse(Value(args, "--diagnostics-watch"), target) Then Return
            ApplicationDiagnostics.Watch(target, Value(args, "--diagnostics-session"), args.Contains("--diagnostics-no-dialog"), Value(args, "--application-name"))
        End Sub

        Private Function Value(args As String(), name As String) As String
            For index = 0 To args.Length - 2
                If args(index) = name Then Return args(index + 1)
            Next
            Return String.Empty
        End Function
    End Module
End Namespace
