using Microsoft.Xna.Framework;
using TriangleNet.Topology;

namespace ProjectZ.Shared.Extensions {

    public static class TriangleExtension {

        public static bool Contains(this Triangle t, Point p) {
            var p0 = t.GetVertex(0).ToXNAVector2();
            var p1 = t.GetVertex(1).ToXNAVector2();
            var p2 = t.GetVertex(2).ToXNAVector2();
            float A = ((p1.Y - p2.Y) * (p.X - p2.X) + (p2.X - p1.X) * (p.Y - p2.Y)) / ((p1.Y - p2.Y) * (p0.X - p2.X) + (p2.X - p1.X) * (p0.Y - p2.Y));
            float B = ((p2.Y - p0.Y) * (p.X - p2.X) + (p0.X - p2.X) * (p.Y - p2.Y)) / ((p1.Y - p2.Y) * (p0.X - p2.X) + (p2.X - p1.X) * (p0.Y - p2.Y));
            float G = 1.0f - A - B;
            return A > 0f & B > 0f & G > 0f;
        }

    }

}