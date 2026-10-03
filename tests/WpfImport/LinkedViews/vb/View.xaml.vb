Imports System.Windows
Namespace LinkedVisualBasic
Public Partial Class View
 Inherits Window
 Public Property Calls As Integer
 Public Sub New()
  InitializeComponent()
 End Sub
 Private Sub Run_Click(sender As Object, e As RoutedEventArgs)
  Calls += 1
  Status.Text = DesignerLabel & Calls
 End Sub
End Class
End Namespace
