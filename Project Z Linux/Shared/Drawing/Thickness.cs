
namespace ProjectZ.Shared.Drawing {

    public class Thickness {

        public Thickness(int Left, int Top, int Right, int Bottom) {
            this.Left = Left;
            this.Top = Top;
            this.Right = Right;
            this.Bottom = Bottom;
        }

        public Thickness(int UniformLength) {
            Left = UniformLength;
            Top = UniformLength;
            Bottom = UniformLength;
            Right = UniformLength;
        }


        public Thickness() {
            int UniformLength = 3;
            Left = UniformLength;
            Top = UniformLength;
            Bottom = UniformLength;
            Right = UniformLength;
        }

        public int Left { get; set; } = 0;
        public int Top { get; set; } = 0;
        public int Right { get; set; } = 0;
        public int Bottom { get; set; } = 0;

    }

}