using System;

namespace ProjectZ.WpfCompatibility
{
    public struct CornerRadius : IEquatable<CornerRadius>
    {
        public double TopLeft { get; set; }
        public double TopRight { get; set; }
        public double BottomRight { get; set; }
        public double BottomLeft { get; set; }

        public CornerRadius(double topLeft, double topRight, double bottomRight, double bottomLeft)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
        }

        public CornerRadius(double value) : this(value, value, value, value) { }

        public override string ToString() => FormattableString.Invariant($"{TopLeft},{TopRight},{BottomRight},{BottomLeft}");

        public bool Equals(CornerRadius other) => TopLeft == other.TopLeft && TopRight == other.TopRight && BottomRight == other.BottomRight && BottomLeft == other.BottomLeft;
    }
}