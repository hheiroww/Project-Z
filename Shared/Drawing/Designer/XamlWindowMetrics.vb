Imports System.Globalization
Imports System.Xml
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI

Namespace [Shared].Drawing.Designer
    ''' <summary>Read host/layout dimensions from linked XAML without rewriting it.</summary>
    Public NotInheritable Class XamlWindowMetrics
        Public Shared Function ReadSize(path As String, fallback As Vector2) As Vector2
            Dim doc As New XmlDocument With {.XmlResolver = Nothing}
            doc.Load(path)
            Return New Vector2(Number(doc.DocumentElement.GetAttribute("Width"), fallback.X), Number(doc.DocumentElement.GetAttribute("Height"), fallback.Y))
        End Function
        Public Shared Function ReadRowHeight(path As String, gridName As String, row As Integer, fallback As Single) As Single
            Dim doc As New XmlDocument With {.XmlResolver = Nothing}
            doc.Load(path)
            Dim grid = doc.SelectNodes("//*").Cast(Of XmlElement)().FirstOrDefault(Function(node) node.GetAttribute("x:Name") = gridName)
            If grid Is Nothing Then Return fallback
            Dim rows = grid.SelectNodes("*[local-name()='Grid.RowDefinitions']/*")
            If row < 0 OrElse row >= rows.Count Then Return fallback
            Return Number(DirectCast(rows(row), XmlElement).GetAttribute("Height"), fallback)
        End Function
        Public Shared Function ReadMargin(path As String, name As String) As Thickness
            Dim doc As New XmlDocument With {.XmlResolver = Nothing}
            doc.Load(path)
            Dim element = doc.SelectNodes("//*").Cast(Of XmlElement)().FirstOrDefault(Function(node) node.GetAttribute("x:Name") = name)
            If element Is Nothing OrElse Not element.HasAttribute("Margin") Then Return New Thickness(0)
            Dim values = element.GetAttribute("Margin").Split(","c).Select(Function(value) CInt(Single.Parse(value, CultureInfo.InvariantCulture))).ToArray()
            If values.Length = 1 Then Return New Thickness(values(0))
            If values.Length = 2 Then Return New Thickness(values(0), values(1), values(0), values(1))
            If values.Length = 4 Then Return New Thickness(values(0), values(1), values(2), values(3))
            Throw New FormatException("XAML Margin requires one, two or four components.")
        End Function
        Private Shared Function Number(value As String, fallback As Single) As Single
            Dim result As Single
            Return If(Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, result) AndAlso Single.IsFinite(result) AndAlso result > 0, result, fallback)
        End Function
    End Class
End Namespace
