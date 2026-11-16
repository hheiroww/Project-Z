using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing.ThreeD;

namespace ProjectZ.ModelImport;

/// <summary>Assimp adapter loaded on demand by the base runtime.</summary>
public sealed class AssimpModelImporter : IModelImporter3D
{
    public Model3DAsset Load(string path)
    {
        var imported = AssimpModelBridge.Load(path);
        var asset = new Model3DAsset { SourcePath = path, Rig = imported.Rig };
        var materials = imported.Materials.Select(source => LoadMaterial(source, path)).ToArray();
        foreach (var source in imported.Meshes)
        {
            asset.Meshes.Add(new SurfaceMesh3D
            {
                Name = source.Name, Indices = source.Indices,
                Vertices = CreateVertices(source.Positions, source.Normals, source.TextureCoordinates),
                BindVertices = source.BindPositions is { } positions && source.BindNormals is { } normals
                    ? CreateVertices(positions, normals, source.TextureCoordinates) : null,
                Binding = source.Binding,
                Material = source.MaterialIndex >= 0 && source.MaterialIndex < materials.Length
                    ? materials[source.MaterialIndex] : new SurfaceMaterial3D()
            });
        }
        return asset;
    }

    private static SurfaceVertex[] CreateVertices(System.Numerics.Vector3[] positions,
        System.Numerics.Vector3[] normals, System.Numerics.Vector2[] textureCoordinates)
    {
        var vertices = new SurfaceVertex[positions.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var position = positions[i];
            var normal = normals[i];
            var uv = textureCoordinates[i];
            vertices[i] = new SurfaceVertex(new Vector4(position.X, position.Y, position.Z, 1),
                new Vector3(normal.X, normal.Y, normal.Z), new Vector2(uv.X, uv.Y), Color.White);
        }
        return vertices;
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
