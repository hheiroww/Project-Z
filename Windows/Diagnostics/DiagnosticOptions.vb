Namespace RuntimeDiagnostics
    Public NotInheritable Class DiagnosticOptions
        Public Property ApplicationName As String
        ''' <summary>Defaults to LocalAppData\Project-Z\application name\Diagnostics.</summary>
        Public Property DirectoryPath As String
        Public Property ShowErrorDialogs As Boolean = True
    End Class
End Namespace
