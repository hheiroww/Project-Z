using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ.Shared.Content {

    public class Textures {

        public GraphicsDevice GraphicsDevice { get; set; }

        public static Texture2D CreateSolidTexture(GraphicsDevice GraphicsDevice, Color Color) {
            var t = new Texture2D(GraphicsDevice, 1, 1);
            t.SetData(new[] { Color });
            return t;
        }

        public Textures(GraphicsDevice GraphicsDevice) {
            this.GraphicsDevice = GraphicsDevice;
        }
    }

}