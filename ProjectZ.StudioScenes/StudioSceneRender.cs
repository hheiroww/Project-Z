using System.Numerics;
using System.Runtime.InteropServices;

namespace ProjectZ.StudioScenes;

/// <summary>Renders a scene into premultiplied BGRA8888 or a native ocean texture without a UI.</summary>
/// <remarks>Create one instance per playback or export worker. Model loading is synchronous.</remarks>
public sealed class StudioSceneRender : IDisposable
{
    private const int MaximumSide = 4096;
    private const int MaximumPixels = 8388608;
    private readonly StudioScene _original;
    private ModelGeometry? _model;
    private readonly SceneRasterizer _raster = new();
    private StudioOceanRender? _ocean;
    private StudioOceanGpu? _oceanReadback;
    private StudioOceanGpu? _oceanOnHost;
    private bool _readbackUnavailable;
    private nint _unsupportedDevice;
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
    public bool LastOceanFrameUsedGpu { get; private set; }
    internal string? OceanGpuError { get; private set; }
    internal bool UseSoftwareOcean { get; set; }

    /// <summary>Returns a view of the completed frame until this instance renders again or is disposed.</summary>
    public ReadOnlySpan<int> Render(int width, int height, double seconds, StudioScene? settings = null, StudioSignal signal = default, StudioClock clock = default) =>
        Render(width, height, seconds, settings, signal, clock, false);

    /// <summary>Renders with denser endless water and up to 16× anisotropic texture filtering when highQuality is enabled.</summary>
    public ReadOnlySpan<int> Render(int width, int height, double seconds, StudioScene? settings, StudioSignal signal,
        StudioClock clock, bool highQuality) =>
        Render(width, height, seconds, settings, signal, clock, highQuality, anisotropicFiltering: highQuality);

    /// <summary>Renders with separate controls for terrain texture filtering and higher quality water.</summary>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <param name="seconds">Scene time in seconds.</param>
    /// <param name="settings">Optional settings for the frame.</param>
    /// <param name="signal">Audio signal for the frame.</param>
    /// <param name="clock">Transport position and tempo for the frame.</param>
    /// <param name="highQuality">Enables denser terrain water and high quality ocean rendering.</param>
    /// <param name="anisotropicFiltering">Enables up to 16× filtering for rasterized scene textures.</param>
    public ReadOnlySpan<int> Render(int width, int height, double seconds, StudioScene? settings, StudioSignal signal,
        StudioClock clock, bool highQuality, bool anisotropicFiltering)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        seconds = ValidateFrame(width, height, seconds);
        var active = settings ?? _original;
        active.Validate();
        if (active.HasModel && (_model is null || !string.Equals(active.Model.Asset, _original.Model.Asset, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Create a new renderer when changing the model asset.", nameof(settings));
        var pose = active.Motion.Sample(active.Camera, seconds, clock);
        var normalized = Normalize(signal);
        LastOceanFrameUsedGpu = false;
        if (active.HasOcean)
            return RenderOcean(width, height, seconds, active, pose, normalized, highQuality);
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
            active.HasTerrain ? _lastTerrain : null, fog, anisotropicFiltering);
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

    /// <summary>Draws the initialized open ocean on the host's Direct3D 11.0+ immediate context.</summary>
    /// <remarks>
    /// Call on the context's owner thread with its compute stage available. Unbind the previous output
    /// from graphics stages before submitting a new frame. The returned RGBA8 UNORM shader texture
    /// is borrowed until the next resize or disposal. A retaining host must take its own reference.
    /// False indicates pending shader compilation, unavailable GPU support, or a non-ocean scene.
    /// </remarks>
    public bool TryRenderOceanTexture(nint device, nint immediateContext, int width, int height, double seconds,
        StudioSignal signal, StudioClock clock, bool highQuality, out nint texture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        texture = 0;
        LastOceanFrameUsedGpu = false;
        seconds = ValidateFrame(width, height, seconds);
        if (!_original.HasOcean || device == 0 || immediateContext == 0 || device == _unsupportedDevice)
            return false;
        var program = ReadyOceanShader(highQuality);
        if (program is null)
            return false;

        try
        {
            if (_oceanOnHost?.Uses(device, immediateContext) != true)
            {
                _oceanOnHost?.Dispose();
                _oceanOnHost = null;
                _oceanOnHost = StudioOceanGpu.Attach(device, immediateContext, program);
            }
            var pose = _original.Motion.Sample(_original.Camera, seconds, clock);
            texture = _oceanOnHost.Draw(width, height, seconds, _original, pose, Normalize(signal), highQuality);
            return LastOceanFrameUsedGpu = texture != 0;
        }
        catch (Exception failure) when (GpuUnavailable(failure))
        {
            _unsupportedDevice = device;
            OceanGpuError = failure.Message;
            _oceanOnHost?.Dispose();
            _oceanOnHost = null;
            return false;
        }
    }

    private ReadOnlySpan<int> RenderOcean(int width, int height, double seconds, StudioScene scene,
        StudioCameraPose pose, StudioSignal signal, bool highQuality)
    {
        var program = _readbackUnavailable ? null : ReadyOceanShader(highQuality);
        if (program is not null)
        {
            try
            {
                _oceanReadback ??= StudioOceanGpu.Create(program);
                _oceanReadback.Draw(width, height, seconds, scene, pose, signal, highQuality);
                var pixels = _oceanReadback.CopyPixels();
                LastOceanFrameUsedGpu = true;
                return pixels;
            }
            catch (Exception failure) when (GpuUnavailable(failure))
            {
                _readbackUnavailable = true;
                OceanGpuError = failure.Message;
                _oceanReadback?.Dispose();
                _oceanReadback = null;
            }
        }
        return (_ocean ??= new StudioOceanRender()).Draw(width, height, seconds, scene, pose, signal, highQuality);
    }

    private byte[][]? ReadyOceanShader(bool wait)
    {
        if (UseSoftwareOcean || !OperatingSystem.IsWindowsVersionAtLeast(10))
            return null;
        var program = OceanShaderProgram.Ready(wait, out var error);
        OceanGpuError = error;
        return program;
    }

    private static bool GpuUnavailable(Exception failure) =>
        failure is SharpDX.SharpDXException or NotSupportedException or DllNotFoundException or BadImageFormatException;

    private static double ValidateFrame(int width, int height, double seconds)
    {
        if (width is <= 0 or > MaximumSide || height is <= 0 or > MaximumSide || (long)width * height > MaximumPixels)
            throw new ArgumentOutOfRangeException(nameof(width), "The viewport must fit within 4096 pixels per side and 8,388,608 pixels total.");
        if (!double.IsFinite(seconds))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        return Math.Clamp(seconds, -1e12, 1e12);
    }

    private static StudioSignal Normalize(StudioSignal signal) =>
        new(Unit(signal.Bass), Unit(signal.Mid), Unit(signal.Treble), Unit(signal.Beat));

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
        _oceanReadback?.Dispose();
        _oceanOnHost?.Dispose();
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
