using System;
using Microsoft.Xna.Framework;

namespace ProjectZ.Shared.Extensions {

    public static class PointExtension {

        public static Point Subtract(this Point p1, Point p2) {
            return new Point(p1.X - p2.X, p1.Y - p2.Y);
        }

        public static Point Subtract(this Point p1, Vector2 p2) {
            return new Point((int)Math.Round(p1.X - p2.X), (int)Math.Round(p1.Y - p2.Y));
        }

        public static Vector2 Subtract(this Vector2 p1, Point p2) {
            return new Vector2((int)Math.Round(p1.X - p2.X), (int)Math.Round(p1.Y - p2.Y));
        }

        public static Vector2 Subtract(this Vector2 p1, Vector2 p2) {
            return new Vector2((int)Math.Round(p1.X - p2.X), (int)Math.Round(p1.Y - p2.Y));
        }

        public static Vector2 ToVector2(this Point p1) {
            return new Vector2(p1.X, p1.Y);
        }

        public static Point ToPoint(this Vector2 p1) {
            return new Point((int)Math.Round(p1.X), (int)Math.Round(p1.Y));
        }

    }

}