Imports Microsoft.Xna.Framework

Friend Module Program
    <STAThread>
    Public Sub Main()
        Using game As New Html5HostGame()
            game.Run()
        End Using
    End Sub
End Module
