using System.Numerics;

namespace ProjectZ.StudioScenes;

internal enum SurfaceLighting { Model, Land, Water }

internal readonly record struct TerrainFog(Vector2 Eye, float Start, float End)
{
    internal static TerrainFog ForCamera(Vector3 eye, float orbitDistance)
    {
        var start = Math.Clamp(orbitDistance * .85f, 11f, 44f);
        return new TerrainFog(new Vector2(eye.X, eye.Z), start, start + 13f);
    }

    internal float Coverage(Vector3 point)
    {
        var horizontal = new Vector2(point.X, point.Z);
        var factor = Math.Clamp((Vector2.Distance(horizontal, Eye) - Start) / (End - Start), 0, 1);
        return 1f - factor * factor * (3 - 2 * factor);
    }
}

internal sealed class SceneRasterizer
{
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-.48f, .77f, .42f));
    private Matrix4x4 _camera;
    private TerrainFog _fog;
    private int _width;
    private int _height;
    private bool _highQuality;
    private int[] _pixels = [];
    private float[] _depth = [];
    private const int MaximumCachedTerrainPixels = 2073600;
    private ClipVertex[] _projected = [];
    private PixelVertex[] _screen = [];
    private int[] _clipMasks = [];
    private StudioTerrain? _cachedTerrainSettings;
    private StudioSky? _cachedSky;
    private Matrix4x4 _cachedCamera;
    private TerrainFog _cachedFog;
    private bool _cachedHighQuality;
    private (int Width, int Height) _cachedSize;
    private int[] _cachedPixels = [];
    private float[] _cachedDepth = [];

    internal ReadOnlySpan<int> Pixels => _pixels;

    private readonly record struct ClipVertex(Vector4 Position, Vector2 UV, Vector4 Tint);
    private readonly record struct PixelVertex(float X, float Y, float Depth, float ReciprocalW, Vector2 UV, Vector4 Tint);

    internal void Begin(int width, int height, Matrix4x4 camera, StudioSky sky, IReadOnlyList<TerrainGeometry> terrain,
        StudioTerrain? terrainSettings, TerrainFog fog, bool highQuality)
    {
        _width = width;
        _height = height;
        _camera = camera;
        _fog = fog;
        _highQuality = highQuality;
        var count = checked(width * height);
        if (count != _pixels.Length)
        {
            _pixels = new int[count];
            _depth = new float[count];
        }
        if (terrainSettings is not null && ReferenceEquals(terrainSettings, _cachedTerrainSettings) && sky == _cachedSky &&
            camera == _cachedCamera && fog == _cachedFog && _cachedSize == (width, height) &&
            highQuality == _cachedHighQuality)
        {
            _cachedPixels.CopyTo(_pixels, 0);
            _cachedDepth.CopyTo(_depth, 0);
            return;
        }
        _cachedTerrainSettings = null;

        if (sky.Enabled)
        {
            var top = StudioColor.Decode(sky.TopColor);
            var horizon = StudioColor.Decode(sky.HorizonColor);
            for (var row = 0; row < _height; row++)
            {
                var fraction = (float)row / Math.Max(1, _height - 1);
                var tint = StudioColor.Blend(Vector4.Lerp(top, horizon, fraction), 0);
                Array.Fill(_pixels, tint, row * _width, _width);
            }
        }
        else
            Array.Clear(_pixels);
        Array.Fill(_depth, float.PositiveInfinity);
        if (terrainSettings is null)
            return;

        foreach (var chunk in terrain)
            Draw(chunk.Land, chunk.Indices, chunk.Material, SurfaceLighting.Land);
        if (count > MaximumCachedTerrainPixels)
            return;
        if (_cachedPixels.Length != count)
        {
            _cachedPixels = new int[count];
            _cachedDepth = new float[count];
        }
        _pixels.CopyTo(_cachedPixels, 0);
        _depth.CopyTo(_cachedDepth, 0);
        _cachedTerrainSettings = terrainSettings;
        _cachedSky = sky with { };
        _cachedCamera = camera;
        _cachedFog = fog;
        _cachedSize = (width, height);
        _cachedHighQuality = highQuality;
    }

    internal void Release()
    {
        _pixels = [];
        _depth = [];
        _projected = [];
        _screen = [];
        _clipMasks = [];
        _cachedTerrainSettings = null;
        _cachedSky = null;
        _cachedPixels = [];
        _cachedDepth = [];
    }

    internal void Draw(ReadOnlySpan<SceneVertex> vertices, ReadOnlySpan<int> indices, MeshTexture? texture = null,
        SurfaceLighting surface = SurfaceLighting.Model, ReadOnlySpan<TerrainWaterVertex> waves = default,
        TerrainWaterFrame water = default)
    {
        if (_projected.Length < vertices.Length)
        {
            _projected = new ClipVertex[vertices.Length];
            _screen = new PixelVertex[vertices.Length];
            _clipMasks = new int[vertices.Length];
        }
        for (var index = 0; index < vertices.Length; index++)
        {
            var vertex = vertices[index];
            var point = vertex.Position;
            var color = vertex.Tint;
            if (surface == SurfaceLighting.Land)
            {
                var slope = Math.Max(0, Vector3.Dot(vertex.Normal, Light));
                var shade = .37f + .63f * slope;
                color *= new Vector4(shade * .94f, shade, shade, 1);
            }
            else if (surface == SurfaceLighting.Water)
            {
                water.Apply(waves[index], ref point, ref color);
            }
            if (_fog.End > 0 && surface != SurfaceLighting.Model)
                color.W *= _fog.Coverage(point);
            var projected = new ClipVertex(Vector4.Transform(new Vector4(point, 1), _camera), vertex.UV, color);
            _projected[index] = projected;
            var position = projected.Position;
            _clipMasks[index] = (position.X < -position.W ? 1 : 0) | (position.X > position.W ? 2 : 0) |
                               (position.Y < -position.W ? 4 : 0) | (position.Y > position.W ? 8 : 0) |
                               (position.Z < 0 ? 16 : 0) | (position.Z > position.W ? 32 : 0) | (color.W <= 1f / 255f ? 128 : 0);
            if (position.Z >= 0 && !TryScreen(projected, out _screen[index]))
                _clipMasks[index] |= 64;
        }

        for (var index = 0; index < indices.Length; index += 3)
        {
            var a = indices[index];
            var b = indices[index + 1];
            var c = indices[index + 2];
            var first = _clipMasks[a];
            var second = _clipMasks[b];
            var third = _clipMasks[c];
            if ((first & second & third) != 0 || ((first | second | third) & 64) != 0)
                continue;
            if (((first | second | third) & 16) == 0)
                RasterizePixels(_screen[a], _screen[b], _screen[c], texture);
            else
                ClipTriangle(_projected[a], _projected[b], _projected[c], texture);
        }
    }

    private void ClipTriangle(ClipVertex a, ClipVertex b, ClipVertex c, MeshTexture? texture)
    {
        Span<ClipVertex> corners = stackalloc ClipVertex[] { a, b, c };
        Span<ClipVertex> clipped = stackalloc ClipVertex[4];
        var count = 0;
        var previous = corners[^1];
        foreach (var current in corners)
        {
            var before = previous.Position.Z >= 0;
            var after = current.Position.Z >= 0;
            if (before != after)
            {
                var fraction = previous.Position.Z / (previous.Position.Z - current.Position.Z);
                clipped[count++] = new ClipVertex(Vector4.Lerp(previous.Position, current.Position, fraction),
                    Vector2.Lerp(previous.UV, current.UV, fraction), Vector4.Lerp(previous.Tint, current.Tint, fraction));
            }
            if (after) clipped[count++] = current;
            previous = current;
        }
        for (var index = 1; index + 1 < count; index++)
            Rasterize(clipped[0], clipped[index], clipped[index + 1], texture);
    }

    private void Rasterize(ClipVertex first, ClipVertex second, ClipVertex third, MeshTexture? texture)
    {
        if (TryScreen(first, out var a) && TryScreen(second, out var b) && TryScreen(third, out var c))
            RasterizePixels(a, b, c, texture);
    }

    private void RasterizePixels(PixelVertex a, PixelVertex b, PixelVertex c, MeshTexture? texture)
    {
        var left = (int)Math.Clamp(MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), 0, _width - 1);
        var right = (int)Math.Clamp(MathF.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), -1, _width - 1);
        var top = (int)Math.Clamp(MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), 0, _height - 1);
        var bottom = (int)Math.Clamp(MathF.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))), -1, _height - 1);
        if (left > right || top > bottom) return;

        var area = Edge(a.X, a.Y, b, c);
        if (!float.IsFinite(area) || Math.Abs(area) < .0001f) return;
        var oneOver = 1f / area;
        var stepA = (c.Y - b.Y) * oneOver;
        var stepB = (a.Y - c.Y) * oneOver;
        var stepC = (b.Y - a.Y) * oneOver;
        var deltaA = (b.X - c.X) * oneOver;
        var deltaB = (c.X - a.X) * oneOver;
        var deltaC = (a.X - b.X) * oneOver;
        var rowA = Edge(left + .5f, top + .5f, b, c) * oneOver;
        var rowB = Edge(left + .5f, top + .5f, c, a) * oneOver;
        var rowC = Edge(left + .5f, top + .5f, a, b) * oneOver;
        Vector2 textureA = default, textureB = default, textureC = default, dx = default, dy = default;
        float dwx = 0, dwy = 0;
        var filter = texture is not null && _highQuality;
        if (filter)
        {
            textureA = a.UV * a.ReciprocalW;
            textureB = b.UV * b.ReciprocalW;
            textureC = c.UV * c.ReciprocalW;
            dx = textureA * stepA + textureB * stepB + textureC * stepC;
            dy = textureA * deltaA + textureB * deltaB + textureC * deltaC;
            dwx = a.ReciprocalW * stepA + b.ReciprocalW * stepB + c.ReciprocalW * stepC;
            dwy = a.ReciprocalW * deltaA + b.ReciprocalW * deltaB + c.ReciprocalW * deltaC;
        }
        for (var y = top; y <= bottom; y++)
        {
            var wA = rowA;
            var wB = rowB;
            var wC = rowC;
            var destination = y * _width + left;
            for (var x = left; x <= right; x++, destination++)
            {
                if (wA >= -.00003f && wB >= -.00003f && wC >= -.00003f)
                {
                    var z = a.Depth * wA + b.Depth * wB + c.Depth * wC;
                    if (z is >= 0 and <= 1 && z < _depth[destination])
                    {
                        var pA = wA * a.ReciprocalW;
                        var pB = wB * b.ReciprocalW;
                        var pC = wC * c.ReciprocalW;
                        var inverse = 1f / (pA + pB + pC);
                        var color = (a.Tint * pA + b.Tint * pB + c.Tint * pC) * inverse;
                        if (texture is not null)
                        {
                            if (filter)
                            {
                                var uv = (textureA * wA + textureB * wB + textureC * wC) * inverse;
                                color *= texture.Sample(uv, (dx - uv * dwx) * inverse, (dy - uv * dwy) * inverse);
                            }
                            else
                            {
                                color *= texture.Sample((a.UV * pA + b.UV * pB + c.UV * pC) * inverse);
                            }
                        }
                        if (color.W > 1f / 255f)
                        {
                            _depth[destination] = z;
                            _pixels[destination] = StudioColor.Blend(color, _pixels[destination]);
                        }
                    }
                }
                wA += stepA;
                wB += stepB;
                wC += stepC;
            }
            rowA += deltaA;
            rowB += deltaB;
            rowC += deltaC;
        }
    }

    private bool TryScreen(ClipVertex point, out PixelVertex pixel)
    {
        pixel = default;
        var position = point.Position;
        if (!float.IsFinite(position.W) || position.W <= .00001f) return false;
        var inverse = 1f / position.W;
        var x = (position.X * inverse + 1) * _width * .5f;
        var y = (1f - position.Y * inverse) * _height * .5f;
        if (!float.IsFinite(x) || !float.IsFinite(y)) return false;
        pixel = new PixelVertex(x, y, position.Z * inverse, inverse, point.UV, point.Tint);
        return true;
    }

    private static float Edge(float x, float y, PixelVertex a, PixelVertex b) =>
        (x - a.X) * (b.Y - a.Y) - (y - a.Y) * (b.X - a.X);
}
