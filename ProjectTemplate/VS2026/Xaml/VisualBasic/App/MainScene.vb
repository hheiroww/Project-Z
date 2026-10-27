Imports ProjectZ.Shared.Drawing
Imports ProjectZ.WpfCompatibility

Namespace $ext_safeprojectname$
Public NotInheritable Class MainScene
    Inherits Scene

    Private ReadOnly application As ProjectZ.WpfCompatibility.Application
    Public ReadOnly Property View As MainWindow

    Public Sub New(manager As SceneManager)
        MyBase.New(manager)
        EnableDesignTimeControls = False
        BackgroundColor = New Microsoft.Xna.Framework.Color(18, 25, 35)
        application = New ProjectZ.WpfCompatibility.Application(Me)
        View = New MainWindow()
        View.Show()
    End Sub
End Class
End Namespace
