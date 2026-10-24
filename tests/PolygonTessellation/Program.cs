using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing.UI.Advanced;
using ProjectZ.Shared.Extensions;

var points = new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 4), new Vector2(4, 4), new Vector2(4, 10), new Vector2(0, 10) };
foreach (var contour in new[] { points, points.Reverse().ToArray() })
{
    var polygon = new PolygonElement { Vectors = contour };
    polygon.ApplyGeometryChanges();
    var mesh = polygon.MeshData ?? throw new Exception("No mesh for concave contour");
    double area = 0;
    foreach (var triangle in mesh.Triangles)
    {
        var a = triangle.GetVertex(0); var b = triangle.GetVertex(1); var c = triangle.GetVertex(2);
        area += Math.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X)) / 2;
        if (triangle.Contains(new Point(7, 7))) throw new Exception("Tessellation filled the concave notch");
    }
    if (Math.Abs(area - 64) > .001) throw new Exception($"Incorrect concave area: {area}");
    if (!mesh.Triangles.Any(t => t.Contains(new Point(1, 7)))) throw new Exception("Interior hit testing failed");
    Console.WriteLine($"PASS concave contour, winding {Math.Sign(contour[1].X - contour[0].X)}, area {area}, {mesh.Triangles.Count} triangles");
}
Console.WriteLine("PASS Linux polygon tessellation and hit testing without TriangleNet or a graphics device");
