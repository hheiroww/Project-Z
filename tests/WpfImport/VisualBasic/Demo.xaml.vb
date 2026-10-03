Public Partial Class Demo
    Public ReadOnly Property Model As New ImportSamples.Model()
    Public Property Clicks As Integer
    Public Sub New()
        InitializeComponent()
        DataContext = Model
    End Sub
    Private Sub Save_Click(sender As Object, e As RoutedEventArgs) Handles Save.Click
        Clicks += 1
        Model.Person.Name = "saved VB " & Clicks
    End Sub
End Class
