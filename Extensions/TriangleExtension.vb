Imports System.Runtime.CompilerServices
Imports Microsoft.Xna.Framework
Imports ProjectZ.Shared.Drawing.UI.Advanced


Public Module TriangleExtension

    <Extension()>
    Public Function Contains(t As PolygonTriangle, p As Point) As Boolean
        Dim p0 As Vector2 = ToXNAVector2(t.GetVertex(0))
        Dim p1 As Vector2 = ToXNAVector2(t.GetVertex(1))
        Dim p2 As Vector2 = ToXNAVector2(t.GetVertex(2))
        Dim A As Single = ((p1.Y - p2.Y) * (p.X - p2.X) +
                                  (p2.X - p1.X) * (p.Y - p2.Y)) /
                                  ((p1.Y - p2.Y) * (p0.X - p2.X) +
                                  (p2.X - p1.X) * (p0.Y - p2.Y))
        Dim B As Single = ((p2.Y - p0.Y) * (p.X - p2.X) +
                                 (p0.X - p2.X) * (p.Y - p2.Y)) /
                                 ((p1.Y - p2.Y) * (p0.X - p2.X) +
                                 (p2.X - p1.X) * (p0.Y - p2.Y))
        Dim G As Single = 1.0F - A - B
        Return A > 0 And B > 0 And G > 0
    End Function

End Module

