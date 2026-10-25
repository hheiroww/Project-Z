Imports ProjectSettingsProbe

Module Program
    <STAThread>
    Sub Main(args As String())
        If QualityProbe.CheckMetadata(args) Then Return
        Using game As New EntryGame(args)
            game.Run()
        End Using
    End Sub
End Module

NotInheritable Class EntryGame
    Inherits QualityProbe

    Public Sub New(args As String())
        MyBase.New(args)
    End Sub
End Class
