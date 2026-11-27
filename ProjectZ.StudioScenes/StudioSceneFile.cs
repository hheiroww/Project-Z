using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ProjectZ.Shared.Drawing.ThreeD;

namespace ProjectZ.StudioScenes;

public static class StudioSceneFile
{
    public const string Extension = ".pzscene3d";
    private const int MaximumBytes = 128 * 1024;
    private static readonly JsonSerializerOptions Format = new()
    {
        AllowTrailingCommas = false,
        MaxDepth = 16,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                info =>
                {
                    if (info.Type != typeof(StudioScene))
                        return;
                    var ocean = info.Properties.Single(property => property.Name == "ocean");
                    // Continue producing legacy scene data that older Studio hosts can read.
                    ocean.ShouldSerialize = static (scene, _) => ((StudioScene)scene).HasOcean;
                }
            }
        },
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter<StudioSceneKind>(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            new JsonStringEnumConverter<StudioCameraMode>(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            new JsonStringEnumConverter<StudioBeatGrid>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        },
        IgnoreReadOnlyProperties = true
    };

    public static StudioScene Load(string manifestPath)
    {
        using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumBytes)
            throw new InvalidDataException("The Project-Z scene file exceeds 128 KiB.");
        try
        {
            var scene = JsonSerializer.Deserialize<StudioScene>(stream, Format) ??
                        throw new InvalidDataException("The Project-Z scene file is empty.");
            ValidatePortable(scene);
            return scene;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Project-Z scene file is malformed.", exception);
        }
    }

    public static string Serialize(StudioScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ValidatePortable(scene);
        return JsonSerializer.Serialize(scene, Format);
    }

    public static StudioScene Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new InvalidDataException("The Project-Z scene data exceeds 128 KiB.");
        try
        {
            var scene = JsonSerializer.Deserialize<StudioScene>(json, Format) ??
                        throw new InvalidDataException("The Project-Z scene data is empty.");
            ValidatePortable(scene);
            return scene;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Project-Z scene data is malformed.", exception);
        }
    }

    public static void Save(string manifestPath, StudioScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ValidatePortable(scene);
        if (scene.HasModel)
            _ = ResolveModelPath(manifestPath, scene);
        var destination = Path.GetFullPath(manifestPath);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".pzscene-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(stream, scene, Format);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string? ResolveModelPath(string manifestPath, StudioScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!scene.HasModel)
            return null;
        CheckRelativeModel(scene.Model.Asset);
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var candidate = Path.GetFullPath(Path.Combine(directory, scene.Model.Asset));
        CheckRelativeModel(Path.GetRelativePath(directory, candidate));
        CheckModelFile(directory, candidate);
        return candidate;
    }

    public static string MakeModelRelative(string manifestPath, string resolvedModelPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var candidate = Path.GetFullPath(resolvedModelPath);
        var relative = Path.GetRelativePath(directory, candidate);
        CheckRelativeModel(relative);
        CheckModelFile(directory, candidate);
        return relative.Replace('\\', '/');
    }

    private static void CheckModelFile(string directory, string file)
    {
        var relative = Path.GetRelativePath(directory, file);
        var path = directory;
        foreach (var component in relative.Split(Path.DirectorySeparatorChar))
        {
            path = Path.Combine(path, component);
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The scene model path cannot use symbolic links or junctions.");
        }
        if (!File.Exists(file))
            throw new FileNotFoundException("The scene model was not found.", file);
    }

    private static void ValidatePortable(StudioScene scene)
    {
        scene.Validate();
        if (scene.HasModel || !string.IsNullOrEmpty(scene.Model.Asset))
            CheckRelativeModel(scene.Model.Asset);
    }

    private static void CheckRelativeModel(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Length > 512 || Path.IsPathRooted(relative) ||
            relative.Any(char.IsControl) || relative.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || relative.Contains(':') ||
            relative.Split('/', '\\').Any(component => component is "" or "." or ".." ||
                component.EndsWith('.') || component.EndsWith(' ')) ||
            !ModelImporter3D.SupportedFileExtensions.Contains(Path.GetExtension(relative), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Place a supported model next to the scene file or in a subfolder.");
        }
    }
}
