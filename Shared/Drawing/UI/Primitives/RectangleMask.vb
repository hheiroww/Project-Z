Imports Microsoft.Xna.Framework

Namespace [Shared].Drawing.UI.Primitives
    ''' <summary>Non-overlapping background fragments after excluding rectangular masks.</summary>
    Public NotInheritable Class RectangleMask
        Private Sub New()
        End Sub

        Public Shared Function Exclude(bounds As Rectangle, cutouts As IEnumerable(Of Rectangle)) As IReadOnlyList(Of Rectangle)
            Dim regions As New List(Of Rectangle)
            If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return regions
            regions.Add(bounds)
            If cutouts Is Nothing Then Return regions
            For Each cutout In cutouts
                Dim nextRegions As New List(Of Rectangle)
                For Each region In regions
                    Dim intersection = Rectangle.Intersect(region, cutout)
                    If intersection.Width <= 0 OrElse intersection.Height <= 0 Then
                        nextRegions.Add(region)
                        Continue For
                    End If
                    If intersection.Top > region.Top Then nextRegions.Add(New Rectangle(region.Left, region.Top, region.Width, intersection.Top - region.Top))
                    If intersection.Bottom < region.Bottom Then nextRegions.Add(New Rectangle(region.Left, intersection.Bottom, region.Width, region.Bottom - intersection.Bottom))
                    If intersection.Left > region.Left Then nextRegions.Add(New Rectangle(region.Left, intersection.Top, intersection.Left - region.Left, intersection.Height))
                    If intersection.Right < region.Right Then nextRegions.Add(New Rectangle(intersection.Right, intersection.Top, region.Right - intersection.Right, intersection.Height))
                Next
                regions = nextRegions
                If regions.Count = 0 Then Exit For
            Next
            Return regions
        End Function
    End Class
End Namespace
