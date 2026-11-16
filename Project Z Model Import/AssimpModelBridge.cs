using System.Numerics;
using System.Runtime.InteropServices;
using ProjectZ.Shared.Drawing.ThreeD;
using Silk.NET.Assimp;

namespace ProjectZ.ModelImport;

public sealed class ImportedModel
{
    public ImportedMesh[] Meshes { get; init; } = [];
    public ImportedMaterial[] Materials { get; init; } = [];
    public ModelRig3D? Rig { get; init; }
}

public sealed class ImportedMesh
{
    public string Name { get; init; } = "Surface";
    public Vector3[] Positions { get; init; } = [];
    public Vector3[] Normals { get; init; } = [];
    public Vector2[] TextureCoordinates { get; init; } = [];
    public int[] Indices { get; init; } = [];
    public int MaterialIndex { get; init; }
    public Vector3[]? BindPositions { get; init; }
    public Vector3[]? BindNormals { get; init; }
    public ModelMeshBinding3D? Binding { get; init; }
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

public static unsafe partial class AssimpModelBridge
{
    private const long MaximumInputBytes = 512L * 1024L * 1024L;

    private static Assimp CreateApi()
    {
        // Silk's default loader does not probe the native extraction directory
        // of a single-file host. Use the CLR's paths before its normal fallback.
        var directories = ((AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string) ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Append(AppContext.BaseDirectory);
        var name = Environment.Is64BitProcess ? "Assimp64.dll" : "Assimp32.dll";
        foreach (var directory in directories)
        {
            var nativePath = Path.Combine(directory, name);
            if (System.IO.File.Exists(nativePath)) return new Assimp(Assimp.CreateDefaultContext(new[] { nativePath }));
        }
        return Assimp.GetApi();
    }

    public static ImportedModel Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new FileNotFoundException("3D model file was not found.", file.FullName);
        if (file.Length > MaximumInputBytes)
            throw new InvalidDataException("The model exceeds Project Z's 512 MB import limit.");

        using var api = CreateApi();
        bool keepHierarchy = file.Extension.ToLowerInvariant() is ".fbx" or ".gltf" or ".glb" or ".dae";
        var flags = PostProcessSteps.Triangulate |
                    PostProcessSteps.JoinIdenticalVertices |
                    PostProcessSteps.GenerateSmoothNormals |
                    PostProcessSteps.ImproveCacheLocality |
                    PostProcessSteps.SortByPrimitiveType |
                    PostProcessSteps.FlipUVs |
                    PostProcessSteps.ValidateDataStructure;
        // Assimp 6 flag for the optional per-bone node pointer. Silk's enum
        // exposes the corresponding Bone fields but omits the postprocess flag.
        flags |= keepHierarchy ? (PostProcessSteps)0x4000 : PostProcessSteps.PreTransformVertices;

        Scene* scene = api.ImportFile(file.FullName, (uint)flags);
        if (scene is null)
            throw new InvalidDataException($"Assimp could not import the model: {api.GetErrorStringS()}");

        try
        {
            var materials = ReadMaterials(api, scene);
            if (keepHierarchy)
                return ImportWithHierarchy(scene, materials);
            var meshes = new List<ImportedMesh>((int)scene->MNumMeshes);
            for (uint meshIndex = 0; meshIndex < scene->MNumMeshes; meshIndex++)
            {
                if (ReadMesh(scene->MMeshes[meshIndex], Matrix4x4.Identity, null) is { } mesh)
                    meshes.Add(mesh);
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

    private static ImportedMesh? ReadMesh(Mesh* source, Matrix4x4 bindWorld, ModelMeshBinding3D? binding)
    {
        if (source is null || source->MNumVertices == 0 || source->MNumFaces == 0)
            return null;
        var indices = new List<int>(checked((int)source->MNumFaces * 3));
        for (uint i = 0; i < source->MNumFaces; i++)
        {
            Face face = source->MFaces[i];
            if (face.MNumIndices != 3 || face.MIndices is null)
                continue;
            for (int vertex = 0; vertex < 3; vertex++)
            {
                uint index = face.MIndices[vertex];
                if (index >= source->MNumVertices)
                    throw new InvalidDataException("A triangle references a missing model vertex.");
                indices.Add(checked((int)index));
            }
        }
        if (indices.Count == 0)
            return null;

        var positions = new Vector3[source->MNumVertices];
        var normals = new Vector3[source->MNumVertices];
        var textureCoordinates = new Vector2[source->MNumVertices];
        var bindPositions = binding is null ? null : new Vector3[source->MNumVertices];
        var bindNormals = binding is null ? null : new Vector3[source->MNumVertices];
        var normalTransform = Matrix4x4.Invert(bindWorld, out var inverse) ? Matrix4x4.Transpose(inverse) : bindWorld;
        Vector3* uvChannel = source->MTextureCoords[0];
        for (uint i = 0; i < source->MNumVertices; i++)
        {
            var position = source->MVertices[i];
            var normal = source->MNormals is null ? Vector3.UnitY : source->MNormals[i];
            if (bindPositions is not null && bindNormals is not null)
            {
                bindPositions[i] = position;
                bindNormals[i] = normal;
                position = Vector3.Transform(position, bindWorld);
                normal = Vector3.TransformNormal(normal, normalTransform);
                float lengthSquared = normal.LengthSquared();
                normal = float.IsFinite(lengthSquared) && lengthSquared > 0 ? Vector3.Normalize(normal) : Vector3.UnitY;
            }
            positions[i] = position;
            normals[i] = normal;
            textureCoordinates[i] = uvChannel is null ? Vector2.Zero : new Vector2(uvChannel[i].X, uvChannel[i].Y);
        }

        return new ImportedMesh
        {
            Name = string.IsNullOrWhiteSpace(source->MName.AsString) ? "Surface" : source->MName.AsString,
            Positions = positions,
            Normals = normals,
            BindPositions = bindPositions,
            BindNormals = bindNormals,
            Binding = binding,
            TextureCoordinates = textureCoordinates,
            Indices = indices.ToArray(),
            MaterialIndex = checked((int)source->MMaterialIndex)
        };
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
