using System;
using System.Collections.Generic;
namespace ProjectZ.Shared.Drawing.UI.Advanced {
    /// <summary>A vertex produced by Project-Z polygon tessellation.</summary>
    public sealed class PolygonVertex {
        public double X { get; }
        public double Y { get; }
        internal PolygonVertex(double x, double y) { X = x; Y = y; }
    }
    /// <summary>A dependency-neutral triangle, shared with the Windows API.</summary>
    public sealed class PolygonTriangle {
        readonly PolygonVertex[] vertices;
        internal PolygonTriangle(PolygonVertex a, PolygonVertex b, PolygonVertex c) { vertices = new[] { a, b, c }; }
        public PolygonVertex GetVertex(int index) {
            if (index < 0 || index >= vertices.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return vertices[index];
        }
    }
    /// <summary>A read-only mesh with no dependency on the removed Triangle.NET library.</summary>
    public sealed class PolygonMesh {
        public IReadOnlyList<PolygonTriangle> Triangles { get; }
        internal PolygonMesh(IEnumerable<PolygonTriangle> triangles) { Triangles = new List<PolygonTriangle>(triangles).AsReadOnly(); }
    }
}
