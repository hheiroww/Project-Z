using System.Numerics;

namespace ProjectZ.StudioScenes;

internal readonly record struct SceneVertex(Vector3 Position, Vector3 Normal, Vector2 UV, Vector4 Tint);

internal sealed record MeshTexture(int Width, int Height, int[] Pixels, bool ClampEdges = false, bool SharedBorder = false)
{
    private MeshTexture[]? _mipmaps;

    internal Vector4 Sample(Vector2 uv, Vector2 dx, Vector2 dy)
    {
        var size = SharedBorder ? new Vector2(Width - 1, Height - 1) : new Vector2(Width, Height);
        var texelsX = dx * size;
        var texelsY = dy * size;
        var xx = texelsX.LengthSquared();
        var yy = texelsY.LengthSquared();
        var xy = Vector2.Dot(texelsX, texelsY);
        var difference = MathF.Sqrt((xx - yy) * (xx - yy) + 4 * xy * xy);
        var major = MathF.Sqrt(.5f * (xx + yy + difference));
        if (!float.IsFinite(major) || major <= 1)
            return Sample(uv);

        const int maxAnisotropy = 16;
        var minor = MathF.Sqrt(MathF.Max(0, .5f * (xx + yy - difference)));
        var width = MathF.Max(1, MathF.Max(minor, major / maxAnisotropy));
        var count = Math.Clamp((int)MathF.Ceiling(major / width), 1, maxAnisotropy);
        var angle = .5f * MathF.Atan2(2 * xy, xx - yy);
        var axis = dx * MathF.Cos(angle) + dy * MathF.Sin(angle);
        if (SharedBorder)
        {
            // Restrict taps to this region and filter along the common boundary at its edge.
            var extent = 2 * Vector2.Max(Vector2.Zero, Vector2.Min(uv, Vector2.One - uv));
            axis = Vector2.Clamp(axis, -extent, extent);
        }
        var levels = _mipmaps ??= BuildMipmaps();
        var lod = Math.Clamp(MathF.Log2(width), 0, levels.Length - 1);
        var first = (int)lod;
        var second = Math.Min(levels.Length - 1, first + 1);
        var sum = Vector4.Zero;
        for (var tap = 0; tap < count; tap++)
        {
            var point = uv + axis * ((tap + .5f) / count - .5f);
            var color = levels[first].Sample(point);
            if (second != first && lod > first)
                color = Vector4.Lerp(color, levels[second].Sample(point), lod - first);
            sum += color;
        }
        return sum / count;
    }

    private MeshTexture[] BuildMipmaps()
    {
        var levels = new List<MeshTexture> { this };
        var minimumSize = SharedBorder ? 2 : 1;
        while (levels[^1] is { } source && (source.Width > minimumSize || source.Height > minimumSize))
        {
            var width = Math.Max(minimumSize, (source.Width + 1) / 2);
            var height = Math.Max(minimumSize, (source.Height + 1) / 2);
            var pixels = new int[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (SharedBorder)
                {
                    pixels[y * width + x] = source.FilterTerrainSample(x, y, width, height);
                    continue;
                }
                var left = x * 2;
                var top = y * 2;
                var right = Math.Min(source.Width - 1, left + 1);
                var bottom = Math.Min(source.Height - 1, top + 1);
                var color = StudioColor.Decode(source.Pixels[top * source.Width + left]) +
                            StudioColor.Decode(source.Pixels[top * source.Width + right]) +
                            StudioColor.Decode(source.Pixels[bottom * source.Width + left]) +
                            StudioColor.Decode(source.Pixels[bottom * source.Width + right]);
                pixels[y * width + x] = StudioColor.EncodeStraight(color * .25f);
            }
            levels.Add(new MeshTexture(width, height, pixels, ClampEdges, SharedBorder));
        }
        return levels.ToArray();
    }

    private int FilterTerrainSample(int x, int y, int width, int height)
    {
        var u = (float)x / (width - 1);
        var v = (float)y / (height - 1);
        var dx = x == 0 || x == width - 1 ? 0 : .5f / (Width - 1);
        var dy = y == 0 || y == height - 1 ? 0 : .5f / (Height - 1);
        var sum = Sample(new Vector2(u - dx, v - dy)) + Sample(new Vector2(u + dx, v - dy)) +
                  Sample(new Vector2(u - dx, v + dy)) + Sample(new Vector2(u + dx, v + dy));
        return StudioColor.EncodeStraight(sum * .25f);
    }

    internal Vector4 Sample(Vector2 uv)
    {
        if (!float.IsFinite(uv.X) || !float.IsFinite(uv.Y))
            return Vector4.One;
        var u = ClampEdges ? Math.Clamp(uv.X, 0, 1) : uv.X - MathF.Floor(uv.X);
        var v = ClampEdges ? Math.Clamp(uv.Y, 0, 1) : uv.Y - MathF.Floor(uv.Y);
        var horizontal = SharedBorder ? u * (Width - 1) : u * Width - .5f;
        var vertical = SharedBorder ? v * (Height - 1) : v * Height - .5f;
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
