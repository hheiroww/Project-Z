using System.Numerics;

namespace ProjectZ.StudioScenes;

internal readonly record struct SceneVertex(Vector3 Position, Vector3 Normal, Vector2 UV, Vector4 Tint);

internal sealed record MeshTexture(int Width, int Height, int[] Pixels, bool ClampEdges = false)
{
    internal Vector4 Sample(Vector2 uv)
    {
        if (!float.IsFinite(uv.X) || !float.IsFinite(uv.Y))
            return Vector4.One;
        var u = ClampEdges ? Math.Clamp(uv.X, 0, 1) : uv.X - MathF.Floor(uv.X);
        var v = ClampEdges ? Math.Clamp(uv.Y, 0, 1) : uv.Y - MathF.Floor(uv.Y);
        var horizontal = u * Width - .5f;
        var vertical = v * Height - .5f;
        var x = (int)MathF.Floor(horizontal);
        var y = (int)MathF.Floor(vertical);
        var left = ClampEdges ? Math.Clamp(x, 0, Width - 1) : (x + Width) % Width;
        var top = ClampEdges ? Math.Clamp(y, 0, Height - 1) : (y + Height) % Height;
        var right = ClampEdges ? Math.Clamp(x + 1, 0, Width - 1) : (left + 1) % Width;
        var bottom = ClampEdges ? Math.Clamp(y + 1, 0, Height - 1) : (top + 1) % Height;
        var upper = Vector4.Lerp(StudioColor.Decode(Pixels[top * Width + left]), StudioColor.Decode(Pixels[top * Width + right]), horizontal - x);
        var lower = Vector4.Lerp(StudioColor.Decode(Pixels[bottom * Width + left]), StudioColor.Decode(Pixels[bottom * Width + right]), horizontal - x);
        return Vector4.Lerp(upper, lower, vertical - y);
    }
}

internal static class StudioColor
{
    internal static Vector4 Decode(int argb) => Decode(unchecked((uint)argb));

    internal static Vector4 Decode(uint packed, bool rgba = false)
    {
        const float unit = 1f / 255f;
        var green = ((packed >> 8) & 255) * unit;
        var low = (packed & 255) * unit;
        var high = ((packed >> 16) & 255) * unit;
        return new Vector4(rgba ? low : high, green, rgba ? high : low, (packed >> 24) * unit);
    }

    internal static int EncodeStraight(Vector4 color)
    {
        color = Vector4.Clamp(color, Vector4.Zero, Vector4.One) * 255f + new Vector4(.5f);
        return ((int)color.W << 24) | ((int)color.X << 16) | ((int)color.Y << 8) | (int)color.Z;
    }

    internal static int Blend(Vector4 straight, int destination)
    {
        if (straight.W >= 1f)
            return EncodeStraight(straight);
        straight = Vector4.Clamp(straight, Vector4.Zero, Vector4.One);
        var alpha = (int)(straight.W * 255f + .5f);
        var destinationColor = unchecked((uint)destination);
        var inverse = 255 - alpha;
        var r = (int)(straight.X * alpha + .5f) + (int)((destinationColor >> 16) & 255) * inverse / 255;
        var g = (int)(straight.Y * alpha + .5f) + (int)((destinationColor >> 8) & 255) * inverse / 255;
        var b = (int)(straight.Z * alpha + .5f) + (int)(destinationColor & 255) * inverse / 255;
        var a = alpha + (int)(destinationColor >> 24) * inverse / 255;
        return (a << 24) | (r << 16) | (g << 8) | b;
    }
}
