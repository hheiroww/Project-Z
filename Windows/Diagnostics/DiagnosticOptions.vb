Namespace RuntimeDiagnostics
    Public NotInheritable Class DiagnosticOptions
        ''' <summary>Defaults to true. Pass false on the first Initialize call, before creating a SceneManager, to disable diagnostic I/O and the observer for this process.</summary>
        Public Property Enabled As Boolean = True
        Public Property ApplicationName As String
        ''' <summary>Defaults to LocalAppData\Project-Z\application name\Diagnostics.</summary>
        Public Property DirectoryPath As String
        Public Property ShowErrorDialogs As Boolean = True
    End Class
End Namespace
