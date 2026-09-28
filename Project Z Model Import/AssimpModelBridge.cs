using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Assimp;

namespace ProjectZ.ModelImport;

public sealed class ImportedModel
{
    public ImportedMesh[] Meshes { get; init; } = [];
    public ImportedMaterial[] Materials { get; init; } = [];
}

public sealed class ImportedMesh
{
    public string Name { get; init; } = "Surface";
    public Vector3[] Positions { get; init; } = [];
    public Vector3[] Normals { get; init; } = [];
    public Vector2[] TextureCoordinates { get; init; } = [];
    public int[] Indices { get; init; } = [];
    public int MaterialIndex { get; init; }
}

public sealed class ImportedMaterial
{
    public string Name { get; init; } = "Material";
    public Vector4 DiffuseColor { get; init; } = Vector4.One;
    public string? TextureReference { get; init; }
    public byte[]? EmbeddedTextureData { get; init; }
    public byte[]? EmbeddedTextureRgba { get; init; }
    public int EmbeddedTextureWidth { get; init; }
    public int EmbeddedTextureHeight { get; init; }
}

public static unsafe class AssimpModelBridge
{
    private const long MaximumInputBytes = 512L * 1024L * 1024L;

    public static ImportedModel Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new FileNotFoundException("3D model file was not found.", file.FullName);
        if (file.Length > MaximumInputBytes)
            throw new InvalidDataException("The model exceeds Project Z's 512 MB import limit.");

        using var api = Assimp.GetApi();
        var flags = PostProcessSteps.Triangulate |
                    PostProcessSteps.JoinIdenticalVertices |
                    PostProcessSteps.GenerateSmoothNormals |
                    PostProcessSteps.PreTransformVertices |
                    PostProcessSteps.ImproveCacheLocality |
                    PostProcessSteps.SortByPrimitiveType |
                    PostProcessSteps.FlipUVs |
                    PostProcessSteps.ValidateDataStructure;

        Scene* scene = api.ImportFile(file.FullName, (uint)flags);
        if (scene is null)
            throw new InvalidDataException($"Assimp could not import the model: {api.GetErrorStringS()}");

        try
        {
            var materials = ReadMaterials(api, scene);
            var meshes = new List<ImportedMesh>((int)scene->MNumMeshes);
            for (uint meshIndex = 0; meshIndex < scene->MNumMeshes; meshIndex++)
            {
                Mesh* source = scene->MMeshes[meshIndex];
                if (source is null || source->MNumVertices == 0 || source->MNumFaces == 0)
                    continue;

                var positions = new Vector3[source->MNumVertices];
                var normals = new Vector3[source->MNumVertices];
                var textureCoordinates = new Vector2[source->MNumVertices];
                Vector3* uvChannel = source->MTextureCoords[0];
                for (uint i = 0; i < source->MNumVertices; i++)
                {
                    positions[i] = source->MVertices[i];
                    normals[i] = source->MNormals is null ? Vector3.UnitY : source->MNormals[i];
                    textureCoordinates[i] = uvChannel is null ? Vector2.Zero : new Vector2(uvChannel[i].X, uvChannel[i].Y);
                }

                var indices = new List<int>((int)source->MNumFaces * 3);
                for (uint i = 0; i < source->MNumFaces; i++)
                {
                    Face face = source->MFaces[i];
                    if (face.MNumIndices != 3 || face.MIndices is null)
                        continue;
                    indices.Add(checked((int)face.MIndices[0]));
                    indices.Add(checked((int)face.MIndices[1]));
                    indices.Add(checked((int)face.MIndices[2]));
                }
                if (indices.Count == 0)
                    continue;

                meshes.Add(new ImportedMesh
                {
                    Name = string.IsNullOrWhiteSpace(source->MName.AsString) ? "Surface" : source->MName.AsString,
                    Positions = positions,
                    Normals = normals,
                    TextureCoordinates = textureCoordinates,
                    Indices = indices.ToArray(),
                    MaterialIndex = checked((int)source->MMaterialIndex)
                });
            }

            if (meshes.Count == 0)
                throw new InvalidDataException("The model contains no triangle surfaces.");
            return new ImportedModel { Meshes = meshes.ToArray(), Materials = materials };
        }
        finally
        {
            api.ReleaseImport(scene);
        }
    }

    private static ImportedMaterial[] ReadMaterials(Assimp api, Scene* scene)
    {
        var result = new ImportedMaterial[scene->MNumMaterials];
        for (uint i = 0; i < scene->MNumMaterials; i++)
        {
            Material* material = scene->MMaterials[i];
            var name = default(AssimpString);
            api.GetMaterialString(material, Assimp.MatkeyName, 0, 0, ref name);

            var diffuse = Vector4.One;
            api.GetMaterialColor(material, Assimp.MatkeyColorDiffuse, 0, 0, ref diffuse);

            string? textureReference = null;
            byte[]? compressed = null;
            byte[]? rgba = null;
            var width = 0;
            var height = 0;
            if (api.GetMaterialTextureCount(material, TextureType.Diffuse) > 0)
            {
                var texturePath = default(AssimpString);
                Return textureResult = api.GetMaterialTexture(material, TextureType.Diffuse, 0, &texturePath,
                    null, null, null, null, null, null);
                if (textureResult == Return.Success)
                {
                    textureReference = texturePath.AsString;
                    if (textureReference.StartsWith('*') &&
                        int.TryParse(textureReference.AsSpan(1), out int embeddedIndex) &&
                        embeddedIndex >= 0 && embeddedIndex < scene->MNumTextures)
                    {
                        Texture* texture = scene->MTextures[embeddedIndex];
                        if (texture is not null && texture->PcData is not null)
                        {
                            if (texture->MHeight == 0)
                            {
                                compressed = new byte[texture->MWidth];
                                Marshal.Copy((nint)texture->PcData, compressed, 0, compressed.Length);
                            }
                            else
                            {
                                width = checked((int)texture->MWidth);
                                height = checked((int)texture->MHeight);
                                rgba = new byte[checked(width * height * 4)];
                                for (int p = 0; p < width * height; p++)
                                {
                                    Texel texel = texture->PcData[p];
                                    int offset = p * 4;
                                    rgba[offset] = texel.R;
                                    rgba[offset + 1] = texel.G;
                                    rgba[offset + 2] = texel.B;
                                    rgba[offset + 3] = texel.A;
                                }
                            }
                        }
                    }
                }
            }

            result[i] = new ImportedMaterial
            {
                Name = string.IsNullOrWhiteSpace(name.AsString) ? $"Material {i}" : name.AsString,
                DiffuseColor = diffuse,
                TextureReference = textureReference,
                EmbeddedTextureData = compressed,
                EmbeddedTextureRgba = rgba,
                EmbeddedTextureWidth = width,
                EmbeddedTextureHeight = height
            };
        }
        return result;
    }
}
