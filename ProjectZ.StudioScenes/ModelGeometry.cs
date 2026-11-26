using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using ProjectZ.Shared.Drawing.ThreeD;

namespace ProjectZ.StudioScenes;

internal sealed class ModelGeometry
{
    private const int TriangleLimit = 400000;
    private const int TextureSideLimit = 2048;
    private const int VertexLimit = 1200000;
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-.46f, .74f, .48f));
    private readonly Model3DAsset _asset;
    private readonly Mesh[] _meshes;
    private readonly Vector3 _pivot;
    private readonly float _scale;
    private readonly float _floor;

    private sealed record Mesh(SurfaceMesh3D Source, SceneVertex[] Vertices, Vector4 Color, MeshTexture? Texture);

    internal IReadOnlyList<string> Animations { get; }
    internal IReadOnlyList<StudioJoint> Joints { get; }

    internal ModelGeometry(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException("Select a local model file using a complete path.", nameof(path));
        _asset = ModelImporter3D.Load(path);
        if (_asset.Meshes.Sum(mesh => (long)mesh.Indices.Length) > TriangleLimit * 3L ||
            _asset.Meshes.Sum(mesh => (long)mesh.Vertices.Length) > VertexLimit)
            throw new InvalidDataException("Use a model with at most 400,000 triangles and 1,200,000 vertices.");

        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        Dictionary<SurfaceMaterial3D, MeshTexture?> textures = new();
        var loaded = new List<Mesh>(_asset.Meshes.Count);
        foreach (var mesh in _asset.Meshes)
        {
            if (mesh.Vertices.Length == 0 || mesh.Indices.Length == 0) continue;
            if (mesh.Indices.Length % 3 != 0 || mesh.Indices.Any(index => (uint)index >= mesh.Vertices.Length))
                throw new InvalidDataException("The model contains invalid triangle indices.");
            foreach (var vertex in mesh.Vertices)
            {
                var point = Position(vertex);
                if (!Finite(point)) throw new InvalidDataException("The model contains invalid vertex positions.");
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }
            var material = mesh.Material ?? new SurfaceMaterial3D();
            if (!textures.TryGetValue(material, out var texture))
            {
                texture = DecodeTexture(material);
                textures.Add(material, texture);
            }
            loaded.Add(new Mesh(mesh, new SceneVertex[mesh.Vertices.Length], StudioColor.Decode(material.DiffuseColor.PackedValue, rgba: true), texture));
        }
        if (loaded.Count == 0)
            throw new InvalidDataException("The model contains no drawable mesh.");
        _meshes = loaded.ToArray();
        _pivot = (minimum + maximum) * .5f;
        _scale = 2.05f / Math.Max(.00001f, (maximum - minimum).Length());
        _floor = (minimum.Y - _pivot.Y) * _scale;
        Animations = Array.AsReadOnly(_asset.Rig?.Animations.Select(clip => clip.Name).ToArray() ?? []);
        Joints = Array.AsReadOnly(_asset.Rig?.Joints.Select(joint => new StudioJoint(joint.Name, joint.Parent)).ToArray() ?? []);
    }

    internal void Draw(SceneRasterizer raster, StudioModel settings, double seconds, TerrainGeometry? terrain, StudioSignal signal)
    {
        var rig = _asset.Rig;
        var animation = settings.Animation >= 0 && settings.Animation < Animations.Count ? settings.Animation : -1;
        var pose = rig?.Sample(seconds * settings.Speed, animation, settings.Loop);
        if (pose is not null && rig is not null && settings.Ik is { } chain && chain.Weight > 0 &&
            chain.Tip < Joints.Count && Joints[chain.Bend].Parent == chain.Root && Joints[chain.Tip].Parent == chain.Bend)
        {
            pose.ApplyTwoBoneIk(chain.Root, chain.Bend, chain.Tip, new Vector3(chain.TargetX, chain.TargetY, chain.TargetZ),
                new Vector3(chain.PoleX, chain.PoleY, chain.PoleZ), chain.Weight);
        }
        const float degrees = MathF.PI / 180f;
        var rotation = Matrix4x4.CreateFromYawPitchRoll(settings.RotationY * degrees,
            settings.RotationX * degrees, settings.RotationZ * degrees);
        var size = settings.Scale * (1f + signal.Beat * .045f) * (terrain is null ? 1f : .61f);
        var translation = new Vector3(settings.OffsetX, settings.OffsetY, settings.OffsetZ);
        if (terrain is not null)
            translation.Y += terrain.SurfaceY(translation.X, translation.Z, true) - _floor * size;

        foreach (var mesh in _meshes)
        {
            var source = mesh.Source.DeformVertices(pose);
            for (var index = 0; index < source.Length; index++)
            {
                var vertex = source[index];
                var normal = Vector3.TransformNormal(new Vector3(vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z), rotation);
                if (!Finite(normal) || normal.LengthSquared() < .0001f)
                    normal = Vector3.UnitY;
                else
                    normal = Vector3.Normalize(normal);
                var position = Vector3.Transform((Position(vertex) - _pivot) * (_scale * size), rotation) + translation;
                var tint = mesh.Color * StudioColor.Decode(vertex.Color.PackedValue, rgba: true);
                var lighting = .46f + .54f * Math.Max(0, Vector3.Dot(normal, Light));
                tint *= new Vector4(lighting, lighting, lighting, 1);
                mesh.Vertices[index] = new SceneVertex(position, normal, new Vector2(vertex.TextureCoordinate.X, vertex.TextureCoordinate.Y), tint);
            }
            raster.Draw(mesh.Vertices, mesh.Source.Indices, mesh.Texture);
        }
    }

    private static Vector3 Position(SurfaceVertex vertex) => new(vertex.Position.X, vertex.Position.Y, vertex.Position.Z);

    private static bool Finite(Vector3 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);

    private static MeshTexture? DecodeTexture(SurfaceMaterial3D material)
    {
        if (material.TexturePixels is { Length: > 0 } source && material.TextureWidth is > 0 and <= TextureSideLimit &&
            material.TextureHeight is > 0 and <= TextureSideLimit &&
            material.TextureWidth * material.TextureHeight == source.Length)
        {
            return new MeshTexture(material.TextureWidth, material.TextureHeight,
                source.Select(color => unchecked((int)(((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B))).ToArray());
        }
        if (material.TextureData is not { Length: > 0 and <= 67108864 } bytes)
            return null;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var image = Image.FromStream(stream, false, true);
            var scale = Math.Min(1f, (float)TextureSideLimit / Math.Max(image.Width, image.Height));
            var width = Math.Max(1, (int)(image.Width * scale));
            var height = Math.Max(1, (int)(image.Height * scale));
            using var surface = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(surface))
                graphics.DrawImage(image, 0, 0, width, height);
            var pixels = new int[width * height];
            var bits = surface.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (var row = 0; row < height; row++)
                    Marshal.Copy(nint.Add(bits.Scan0, row * bits.Stride), pixels, row * width, width);
            }
            finally { surface.UnlockBits(bits); }
            return new MeshTexture(width, height, pixels);
        }
        catch (ArgumentException) { return null; }
        catch (ExternalException) { return null; }
    }
}
