using Microsoft.Xna.Framework;

namespace ProjectZ.Shared.Extensions {

    static class ColorExtension {

        public static byte[] ToByteArray(this Color Color) {
            return new byte[4] { Color.R, Color.G, Color.B, Color.A };
        }

    }

}