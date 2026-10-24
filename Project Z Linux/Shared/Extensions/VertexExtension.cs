using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing.UI.Advanced;

namespace ProjectZ.Shared.Extensions {

    public static class VertexExtension {

        public static Vector2 Multiply(this Vector2 v, double d) {
            return new Vector2((float)(v.X * d), (float)(v.Y * d));
        }

        public static Vector2 Multiply(this Vector2 v, double x, double y) {
            return new Vector2((float)(v.X * x), (float)(v.Y * y));
        }

        public static Vector2 ToXNAVector2(this PolygonVertex v) {
            return new Vector2((float)v.X, (float)v.Y);
        }

        public static Vector2[] ToXNAVectorArray(this PolygonTriangle t) {
            var P0 = t.GetVertex(0).ToXNAVector2();
            var P1 = t.GetVertex(1).ToXNAVector2();
            var P2 = t.GetVertex(2).ToXNAVector2();
            return new[] { P0, P1, P2 };
        }

        public static PolygonVertex ToPolygonVertex(this Vector2 t) {
            return new PolygonVertex(t.X, t.Y);
        }

    }

}