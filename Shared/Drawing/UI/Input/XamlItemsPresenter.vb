Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI.Primitives

Namespace [Shared].Drawing.UI.Input
    ''' <summary>Variable-height, pixel-scrolled XAML template rows with bounded live visuals.</summary>
    Public Class XamlItemsPresenter
        Inherits ScrollViewer
        Private ReadOnly surface As RectangleElement
        Private ReadOnly visible As New Dictionary(Of Integer, SceneElement)
        Private ReadOnly retired As New Dictionary(Of SceneElement, Integer)
        Private offsets As Single() = {0}
        Private factory As Func(Of Integer, Single, SceneElement)

        Public Sub New(scene As Scene)
            MyBase.New(scene)
            BackgroundColor = Color.Transparent
            surface = New RectangleElement(scene) With {.BackgroundColor = Color.Transparent, .Padding = New Thickness(0)}
            Content = surface
        End Sub

        Public Sub SetRows(heights As IEnumerable(Of Single), create As Func(Of Integer, Single, SceneElement))
            For Each row In visible.Values
                surface.Children.Remove(row)
                retired(row) = 2
            Next
            visible.Clear()
            factory = create
            Dim sizes = heights.ToArray()
            ReDim offsets(sizes.Length)
            For i = 0 To sizes.Length - 1
                offsets(i + 1) = offsets(i) + sizes(i)
            Next
            surface.Size = New Vector2(Math.Max(1, Size.X - 14), Math.Max(Size.Y, offsets.Last()))
            ScrollTo(Vector2.Zero)
        End Sub

        Public Overrides Sub Tick(time As GameTime)
            MyBase.Tick(time)
            surface.Size = New Vector2(Math.Max(1, Size.X - 14), Math.Max(Size.Y, offsets.Last()))
            Dim first = Math.Max(0, ScrollOffset.Y - 100)
            Dim last = ScrollOffset.Y + Size.Y + 100
            For Each index In visible.Keys.ToArray()
                If offsets(index + 1) < first OrElse offsets(index) > last Then
                    Dim row = visible(index)
                    surface.Children.Remove(row)
                    retired(row) = 2
                    visible.Remove(index)
                End If
            Next
            If factory IsNot Nothing Then
                For i = 0 To offsets.Length - 2
                    If offsets(i + 1) < first OrElse offsets(i) > last OrElse visible.ContainsKey(i) Then Continue For
                    Dim row = factory(i, surface.Size.X)
                    row.Margin = New Thickness(0, CInt(offsets(i)), 0, 0)
                    row.OrientationReserve = DisplayReservation.FloatBoth
                    row.Size = New Vector2(surface.Size.X, offsets(i + 1) - offsets(i) - 3)
                    visible(i) = row
                    surface.Children.Add(row)
                Next
            End If
            For Each row In retired.Keys.ToArray()
                retired(row) -= 1
                If retired(row) = 0 Then
                    row.Dispose()
                    retired.Remove(row)
                End If
            Next
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                For Each row In retired.Keys
                    row.Dispose()
                Next
                retired.Clear()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
