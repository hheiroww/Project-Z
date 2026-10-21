Imports System

Friend Module Program
    <STAThread>
    Public Sub Main()
        Using game As New GameWindow()
            game.Run()
        End Using
    End Sub
End Module
