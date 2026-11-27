using System.Numerics;
using System.Runtime.InteropServices;

namespace ProjectZ.StudioScenes;

/// <summary>Renders a scene into premultiplied BGRA8888 without creating a UI or graphics device.</summary>
/// <remarks>Create one instance per playback or export worker. Model loading is synchronous.</remarks>
public sealed class StudioSceneRender : IDisposable
{
    private const int MaximumSide = 4096;
    private const int MaximumPixels = 8388608;
    private readonly StudioScene _original;
    private ModelGeometry? _model;
    private readonly SceneRasterizer _raster = new();
    private StudioOceanRender? _ocean;
    private StudioTerrain? _lastTerrain;
    private const float TerrainTileSide = 4;
    private const int MaximumResidentTiles = 144;
    private readonly LinkedList<TerrainGeometry> _terrain = new();
    private readonly Dictionary<TerrainRegion, LinkedListNode<TerrainGeometry>> _terrainByRegion = new();
    private readonly List<TerrainGeometry> _visibleTerrain = new();
    private int[] _landIndices = [];
    private int[] _waterIndices = [];
    private int _waterGrid;
    private Matrix4x4 _visibleCamera;
    private TerrainFog _visibleFog;
    private bool _visibleTerrainValid;
    private bool _disposed;

    public StudioSceneRender(StudioScene scene, string? modelPath = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        scene.Validate();
        _original = scene.Copy();
        if (scene.HasModel)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("Model scenes require a resolved local model path.", nameof(modelPath));
            _model = new ModelGeometry(modelPath);
        }
    }

    public IReadOnlyList<string> Animations => _model?.Animations ?? Array.Empty<string>();
    public IReadOnlyList<StudioJoint> Joints => _model?.Joints ?? Array.Empty<StudioJoint>();

    /// <summary>Returns a view of the completed frame until this instance renders again or is disposed.</summary>
    public ReadOnlySpan<int> Render(int width, int height, double seconds, StudioScene? settings = null, StudioSignal signal = default, StudioClock clock = default) =>
        Render(width, height, seconds, settings, signal, clock, false);

    /// <summary>Renders with denser endless water and up to 16× anisotropic texture filtering when highQuality is enabled.</summary>
    public ReadOnlySpan<int> Render(int width, int height, double seconds, StudioScene? settings, StudioSignal signal,
        StudioClock clock, bool highQuality)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (width is <= 0 or > MaximumSide || height is <= 0 or > MaximumSide || (long)width * height > MaximumPixels)
            throw new ArgumentOutOfRangeException(nameof(width), "The viewport must fit within 4096 pixels per side and 8,388,608 pixels total.");
        if (!double.IsFinite(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        seconds = Math.Clamp(seconds, -1e12, 1e12);
        var active = settings ?? _original;
        active.Validate();
        if (active.HasModel && (_model is null || !string.Equals(active.Model.Asset, _original.Model.Asset, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Create a new renderer when changing the model asset.", nameof(settings));
        var pose = active.Motion.Sample(active.Camera, seconds, clock);
        var normalized = new StudioSignal(Unit(signal.Bass), Unit(signal.Mid), Unit(signal.Treble), Unit(signal.Beat));
        if (active.HasOcean)
            return (_ocean ??= new StudioOceanRender()).Draw(width, height, seconds, active, pose, normalized, highQuality);
        var camera = MakeCamera(pose, width, height, active.HasTerrain ? active.Terrain.Relief : null);
        var fog = active.HasTerrain && active.Terrain.Endless
            ? TerrainFog.ForCamera(camera.Eye, camera.Distance)
            : default;
        TerrainGeometry? modelGround = null;
        if (active.HasTerrain)
        {
            SetTerrainSettings(active.Terrain, highQuality);
            UpdateVisibleTerrain(active.Terrain, camera.ViewProjection, fog);
            if (active.HasModel)
                modelGround = active.Terrain.Endless
                    ? GetTerrain(TileAt(active.Model.OffsetX, active.Model.OffsetZ))
                    : _visibleTerrain[0];
        }
        else
        {
            _visibleTerrain.Clear();
            _visibleTerrainValid = false;
        }

        _raster.Begin(width, height, camera.ViewProjection, active.Sky, _visibleTerrain,
            active.HasTerrain ? _lastTerrain : null, fog, highQuality);
        if (active.HasModel)
            _model!.Draw(_raster, active.Model, seconds, modelGround, normalized);

        var water = TerrainWaterFrame.At(seconds, normalized);
        foreach (var tile in _visibleTerrain)
        {
            if (tile.Water.Length > 0)
                _raster.Draw(tile.Water, tile.WaterIndices, surface: SurfaceLighting.Water, waves: tile.WaterWaves, water: water);
        }
        return _raster.Pixels;
    }

    /// <summary>Returns owned pixels for callers that cannot use spans or want to retain frames.</summary>
    public int[] CopyFrame(int width, int height, double seconds, StudioScene? settings = null, StudioSignal signal = default, StudioClock clock = default) =>
        Render(width, height, seconds, settings, signal, clock).ToArray();

    /// <summary>Writes the premultiplied BGRA frame into a caller-owned byte buffer.</summary>
    public void CopyFrameTo(byte[] destination, int width, int height, double seconds,
        StudioScene? settings = null, StudioSignal signal = default, StudioClock clock = default) =>
        CopyFrameTo(destination, width, height, seconds, settings, signal, clock, false);

    /// <summary>Writes a scene frame into a caller-owned buffer with optional export quality.</summary>
    public void CopyFrameTo(byte[] destination, int width, int height, double seconds,
        StudioScene? settings, StudioSignal signal, StudioClock clock, bool highQuality)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var frame = MemoryMarshal.AsBytes(Render(width, height, seconds, settings, signal, clock, highQuality));
        frame.CopyTo(destination);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _raster.Release();
        _ocean = null;
        _model = null;
        ClearTerrain();
        _landIndices = [];
        _waterIndices = [];
        _lastTerrain = null;
    }

    private void SetTerrainSettings(StudioTerrain settings, bool highQuality)
    {
        var waterGrid = settings.ShowWater ? TerrainGeometry.WaterGrid(settings, highQuality) : 0;
        if (_lastTerrain == settings && _waterGrid == waterGrid) return;
        _waterGrid = waterGrid;
        ClearTerrain();
        _lastTerrain = settings with { Material = settings.Material with { } };
        var grid = TerrainGeometry.LandGrid(settings);
        _landIndices = TerrainGeometry.CreateIndices(grid);
        _waterIndices = !settings.ShowWater ? [] : waterGrid == grid ? _landIndices : TerrainGeometry.CreateIndices(waterGrid);
    }

    private void UpdateVisibleTerrain(StudioTerrain terrain, Matrix4x4 camera, TerrainFog fog)
    {
        if (_visibleTerrainValid && (!terrain.Endless || (_visibleCamera == camera && _visibleFog == fog)))
            return;

        _visibleTerrain.Clear();
        if (terrain.Endless)
            CollectVisibleTiles(camera, fog, terrain.Relief);
        else
            _visibleTerrain.Add(GetTerrain(TerrainRegion.Island));
        _visibleCamera = camera;
        _visibleFog = fog;
        _visibleTerrainValid = true;
    }

    private void ClearTerrain()
    {
        _terrain.Clear();
        _terrainByRegion.Clear();
        _visibleTerrain.Clear();
        _visibleTerrainValid = false;
    }

    private TerrainGeometry GetTerrain(TerrainRegion region)
    {
        if (_terrainByRegion.TryGetValue(region, out var found))
        {
            _terrain.Remove(found);
            _terrain.AddFirst(found);
            return found.Value;
        }

        if (_terrain.Count >= MaximumResidentTiles)
        {
            _terrainByRegion.Remove(_terrain.Last!.Value.Region);
            _terrain.RemoveLast();
        }

        var geometry = new TerrainGeometry(_lastTerrain!, region, _landIndices, _waterIndices, _waterGrid);
        _terrainByRegion.Add(region, _terrain.AddFirst(geometry));
        return geometry;
    }

    private static TerrainRegion TileAt(float x, float z) =>
        Tile((int)MathF.Floor(x / TerrainTileSide), (int)MathF.Floor(z / TerrainTileSide));

    private static TerrainRegion Tile(int x, int z) =>
        new((x + .5f) * TerrainTileSide, (z + .5f) * TerrainTileSide, TerrainTileSide * .5f);

    private void CollectVisibleTiles(Matrix4x4 camera, TerrainFog fog, float relief)
    {
        const float half = TerrainTileSide / 2;
        var radius = fog.End + half * MathF.Sqrt(2);
        var minimum = TileAt(fog.Eye.X - radius, fog.Eye.Y - radius);
        var maximum = TileAt(fog.Eye.X + radius, fog.Eye.Y + radius);
        var y0 = -.42f * relief * 1.45f - .04f;
        var y1 = .58f * relief * 1.45f + .04f;
        for (var z = minimum.Z; z <= maximum.Z; z += TerrainTileSide)
        for (var x = minimum.X; x <= maximum.X; x += TerrainTileSide)
        {
            var nearestX = Math.Clamp(fog.Eye.X, x - half, x + half);
            var nearestZ = Math.Clamp(fog.Eye.Y, z - half, z + half);
            if (Vector2.DistanceSquared(fog.Eye, new Vector2(nearestX, nearestZ)) >= fog.End * fog.End ||
                !VisibleBounds(camera, x, z, half, y0, y1))
                continue;
            _visibleTerrain.Add(GetTerrain(new TerrainRegion(x, z, half)));
        }

        // Submit the nearest ground first so its depth rejects covered terrain and water.
        _visibleTerrain.Sort((left, right) =>
            DistanceFrom(left.Region, fog.Eye).CompareTo(DistanceFrom(right.Region, fog.Eye)));
    }

    private static float DistanceFrom(TerrainRegion region, Vector2 eye) =>
        Vector2.DistanceSquared(new Vector2(region.X, region.Z), eye);

    private static bool VisibleBounds(Matrix4x4 camera, float x, float z, float half, float low, float high)
    {
        var outside = 0b111111;
        for (var corner = 0; corner < 8; corner++)
        {
            var cornerX = x + ((corner & 1) == 0 ? -half : half);
            var cornerY = (corner & 2) == 0 ? low : high;
            var cornerZ = z + ((corner & 4) == 0 ? -half : half);
            var point = Vector4.Transform(new Vector4(cornerX, cornerY, cornerZ, 1), camera);
            var mask = (point.X < -point.W ? 1 : 0) | (point.X > point.W ? 2 : 0) |
                       (point.Y < -point.W ? 4 : 0) | (point.Y > point.W ? 8 : 0) |
                       (point.Z < 0 ? 16 : 0) | (point.Z > point.W ? 32 : 0);
            outside &= mask;
            if (outside == 0) return true;
        }
        return false;
    }
    private readonly record struct SceneCamera(Matrix4x4 ViewProjection, Vector3 Eye, float Distance);

    private static SceneCamera MakeCamera(StudioCameraPose pose, int width, int height, float? terrainRelief)
    {
        const float degrees = MathF.PI / 180f;
        var camera = pose.Camera;
        var aspect = (float)width / height;
        var yaw = camera.Yaw * degrees;
        var pitch = camera.Pitch * degrees;
        var target = new Vector3(pose.X, pose.Y + (terrainRelief.HasValue ? -.08f : 0), pose.Z);
        var unit = new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
        var framing = terrainRelief is { } relief ? 5.05f + Math.Max(0, relief - .65f) * .65f : 3.05f;
        var distance = framing / camera.Zoom / Math.Clamp(aspect, .18f, 1f);
        var eye = target + unit * distance;
        var roll = Quaternion.CreateFromAxisAngle(-unit, camera.Roll * degrees);
        var up = Vector3.Transform(Vector3.UnitY, roll);
        var view = Matrix4x4.CreateLookAt(eye, target, up);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(camera.FieldOfView * degrees, aspect, .05f, Math.Max(80, distance * 2.5f + 32));
        return new SceneCamera(view * projection, eye, distance);
    }
    private static float Unit(float number) => float.IsFinite(number) ? Math.Clamp(number, 0, 1) : 0;
}
