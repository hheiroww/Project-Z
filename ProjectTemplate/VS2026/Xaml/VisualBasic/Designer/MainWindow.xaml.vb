Imports System.Windows

Namespace $ext_safeprojectname$
Public Partial Class MainWindow
    Inherits Window

    Public Property ClickCount As Integer

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub Run_Click(sender As Object, e As RoutedEventArgs)
        ClickCount += 1
        Status.Text = "Button clicked " & ClickCount
    End Sub
End Class
End Namespace
