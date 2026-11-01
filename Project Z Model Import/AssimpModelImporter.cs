using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing.ThreeD;

namespace ProjectZ.ModelImport;

/// <summary>Assimp adapter loaded on demand by the base runtime.</summary>
public sealed class AssimpModelImporter : IModelImporter3D
{
    public Model3DAsset Load(string path)
    {
        var imported = AssimpModelBridge.Load(path);
        var asset = new Model3DAsset { SourcePath = path };
        var materials = imported.Materials.Select(source => LoadMaterial(source, path)).ToArray();
        foreach (var source in imported.Meshes)
        {
            var vertices = new SurfaceVertex[source.Positions.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var position = source.Positions[i];
                var normal = source.Normals[i];
                var uv = source.TextureCoordinates[i];
                vertices[i] = new SurfaceVertex(new Vector4(position.X, position.Y, position.Z, 1),
                    new Vector3(normal.X, normal.Y, normal.Z), new Vector2(uv.X, uv.Y), Color.White);
            }
            asset.Meshes.Add(new SurfaceMesh3D
            {
                Name = source.Name, Vertices = vertices, Indices = source.Indices,
                Material = source.MaterialIndex >= 0 && source.MaterialIndex < materials.Length
                    ? materials[source.MaterialIndex] : new SurfaceMaterial3D()
            });
        }
        return asset;
    }

    private static SurfaceMaterial3D LoadMaterial(ImportedMaterial source, string modelPath)
    {
        var color = source.DiffuseColor;
        var material = new SurfaceMaterial3D
        {
            Name = source.Name,
            DiffuseColor = new Color(Math.Clamp(color.X, 0, 1), Math.Clamp(color.Y, 0, 1), Math.Clamp(color.Z, 0, 1), Math.Clamp(color.W, 0, 1)),
            TexturePath = source.TextureReference, TextureData = source.EmbeddedTextureData,
            TextureWidth = source.EmbeddedTextureWidth, TextureHeight = source.EmbeddedTextureHeight
        };
        if (source.EmbeddedTextureRgba is { } rgba)
        {
            var pixels = new Color[checked(source.EmbeddedTextureWidth * source.EmbeddedTextureHeight)];
            for (var i = 0; i < pixels.Length; i++)
            {
                var offset = checked(i * 4);
                pixels[i] = new Color(rgba[offset], rgba[offset + 1], rgba[offset + 2], rgba[offset + 3]);
            }
            material.TexturePixels = pixels;
        }
        else if (!string.IsNullOrWhiteSpace(source.TextureReference) && !source.TextureReference.StartsWith('*'))
        {
            var reference = source.TextureReference.Replace('/', Path.DirectorySeparatorChar);
            var texturePath = Path.IsPathRooted(reference) ? reference : Path.Combine(Path.GetDirectoryName(modelPath)!, reference);
            if (File.Exists(texturePath))
            {
                material.TexturePath = Path.GetFullPath(texturePath);
                material.TextureData = File.ReadAllBytes(texturePath);
            }
        }
        return material;
    }
}
