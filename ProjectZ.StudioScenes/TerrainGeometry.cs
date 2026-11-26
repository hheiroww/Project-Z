using System.Numerics;

namespace ProjectZ.StudioScenes;

internal readonly record struct TerrainRegion(float X, float Z, float Extent)
{
    internal static TerrainRegion Island => new(0, 0, 1.6f);
}

internal sealed class TerrainGeometry
{
    private readonly float[] _axis;
    private readonly StudioTerrain _settings;
    internal readonly TerrainRegion Region;
    // Water has lower-frequency waves than the land heightfield. Keep its animated pass bounded.
    private const int MaximumWaterGrid = 65;
    private const int MaximumEndlessWaterGrid = 9;
    private readonly float[] _altitudes;
    private readonly float[] _slopes;
    private readonly int _grid;
    private readonly float _relief;
    private readonly float _waterLevel;
    internal readonly SceneVertex[] Land;
    internal readonly int[] Indices;
    internal readonly SceneVertex[] Water;
    internal readonly int[] WaterIndices;
    internal readonly MeshTexture? Material;

    internal TerrainGeometry(StudioTerrain settings, TerrainRegion region)
    {
        _grid = settings.Endless ? Math.Max(5, (settings.Grid - 1) / 4 + 1) : settings.Grid;
        _settings = settings;
        Region = region;
        _axis = new float[_grid];
        for (var index = 0; index < _grid; index++)
            _axis[index] = Axis((float)index / (_grid - 1), Region.Extent);
        _relief = settings.Relief * 1.45f;
        _waterLevel = settings.WaterLevel;
        var count = checked(_grid * _grid);
        _altitudes = new float[count];
        _slopes = new float[count];
        Land = new SceneVertex[count];
        Indices = CreateIndices(_grid);

        var surface = settings.Material;
        var useTexture = surface.Enabled && surface.TextureStrength > 0;
        var low = StudioColor.Decode(settings.LowColor);
        var high = StudioColor.Decode(settings.HighColor);
        var snow = StudioColor.Decode(settings.SnowColor);

        for (var row = 0; row < _grid; row++)
        for (var column = 0; column < _grid; column++)
            _altitudes[row * _grid + column] = Height(settings, Region.X + _axis[column], Region.Z + _axis[row]);

        for (var row = 0; row < _grid; row++)
        for (var column = 0; column < _grid; column++)
        {
            var offset = row * _grid + column;
            var x = Region.X + _axis[column];
            var z = Region.Z + _axis[row];
            var altitude = _altitudes[offset];
            var step = 2 * Region.Extent / (_grid - 1);
            var outside = settings.Endless;
            var west = column == 0 && outside ? Height(settings, x - step, z) : At(_altitudes, column - 1, row);
            var east = column == _grid - 1 && outside ? Height(settings, x + step, z) : At(_altitudes, column + 1, row);
            var north = row == 0 && outside ? Height(settings, x, z - step) : At(_altitudes, column, row - 1);
            var south = row == _grid - 1 && outside ? Height(settings, x, z + step) : At(_altitudes, column, row + 1);
            var spacingX = (column == 0 || column == _grid - 1) && !outside ? step : step * 2;
            var spacingZ = (row == 0 || row == _grid - 1) && !outside ? step : step * 2;
            var normal = Vector3.Normalize(new Vector3((west - east) * _relief / spacingX, 1, (north - south) * _relief / spacingZ));
            var slope = MathF.Acos(Math.Clamp(normal.Y, 0, 1)) * (2f / MathF.PI);
            _slopes[offset] = slope;
            var color = useTexture ? Vector4.One : Vector4.Lerp(BaseColor(low, high, altitude, slope), snow, SnowCover(surface, altitude, slope));
            Land[offset] = new SceneVertex(new Vector3(x, WorldY(altitude), z), normal,
                new Vector2((float)column / (_grid - 1), (float)row / (_grid - 1)),
                color with { W = color.W * BoundaryAlpha(x, z) });
        }

        if (useTexture)
            Material = CreateMaterial(settings, low, high, snow);

        if (settings.ShowWater)
        {
            var grid = Math.Min(_grid, settings.Endless ? MaximumEndlessWaterGrid : MaximumWaterGrid);
            var color = StudioColor.Decode(settings.WaterColor);
            Water = CreateWater(grid, WorldY(_waterLevel), color);
            WaterIndices = grid == _grid ? Indices : CreateIndices(grid);
        }
        else
        {
            Water = [];
            WaterIndices = [];
        }
    }

    internal float SurfaceY(float x, float z, bool includeWater)
    {
        var offsetX = x - Region.X;
        var offsetZ = z - Region.Z;
        var outside = Math.Abs(offsetX) > Region.Extent || Math.Abs(offsetZ) > Region.Extent;
        var altitude = outside && _settings.Endless
            ? Height(_settings, x, z)
            : Sample(_altitudes, AxisToUnit(offsetX), AxisToUnit(offsetZ));
        return WorldY(includeWater && Water.Length > 0 ? Math.Max(altitude, _waterLevel) : altitude);
    }

    private SceneVertex[] CreateWater(int grid, float height, Vector4 waterColor)
    {
        var vertices = new SceneVertex[grid * grid];
        var axis = new float[grid];
        for (var index = 0; index < grid; index++)
            axis[index] = Axis((float)index / (grid - 1), Region.Extent);
        for (var row = 0; row < grid; row++)
        for (var column = 0; column < grid; column++)
        {
            var x = Region.X + axis[column];
            var z = Region.Z + axis[row];
            vertices[row * grid + column] = new SceneVertex(new Vector3(x, height, z), Vector3.UnitY, new Vector2(x, z),
                waterColor with { W = waterColor.W * BoundaryAlpha(x, z) });
        }
        return vertices;
    }

    private static int[] CreateIndices(int grid)
    {
        var indices = new int[checked(6 * (grid - 1) * (grid - 1))];
        var offset = 0;
        for (var row = 0; row < grid - 1; row++)
        for (var column = 0; column < grid - 1; column++)
        {
            var corner = row * grid + column;
            indices[offset++] = corner;
            indices[offset++] = corner + grid;
            indices[offset++] = corner + 1;
            indices[offset++] = corner + 1;
            indices[offset++] = corner + grid;
            indices[offset++] = corner + grid + 1;
        }
        return indices;
    }

    private MeshTexture CreateMaterial(StudioTerrain settings, Vector4 low, Vector4 high, Vector4 snow)
    {
        var surface = settings.Material;
        var resolution = settings.Endless ? Math.Max(24, surface.TextureResolution / 4) : surface.TextureResolution;
        var texture = new int[checked(resolution * resolution)];
        var sand = StudioColor.Decode(surface.SandColor);
        var soil = StudioColor.Decode(surface.SoilColor);
        var grass = StudioColor.Decode(surface.GrassColor);
        var rock = StudioColor.Decode(surface.RockColor);
        var scale = surface.TextureScale;
        for (var row = 0; row < resolution; row++)
        for (var column = 0; column < resolution; column++)
        {
            var u = (column + .5f) / resolution;
            var v = (row + .5f) / resolution;
            var x = (Region.X + Axis(u, Region.Extent)) * scale;
            var z = (Region.Z + Axis(v, Region.Extent)) * scale;
            var sampleHeight = Sample(_altitudes, u, v);
            var sampleSlope = Sample(_slopes, u, v);

            // Frequencies are measured in world space, not in viewport pixels or mesh cells.
            var patches = Noise(x * 3.7f + 7, z * 3.7f + 43, settings.Seed + 619);
            var grain = Noise(x * 32f - 21, z * 32f + 11, settings.Seed - 283);
            var banding = .5f + .5f * MathF.Sin((sampleHeight * 2.1f + x * .39f - z * .65f) * 45f + patches * 4f);
            var altitude = sampleHeight + (patches - .5f) * .11f;
            var slope = sampleSlope + (patches - .5f) * .13f;
            var blend = surface.AltitudeBlend;
            var coast = 1 - Smooth(surface.SandAltitude - blend, surface.SandAltitude + blend, altitude);
            var meadow = Smooth(surface.SandAltitude - .025f, surface.SandAltitude + blend, altitude)
                * (1 - Smooth(surface.GrassAltitude - blend, surface.GrassAltitude + blend, altitude))
                * (1 - Smooth(.25f, .68f, slope));
            meadow *= .68f + .32f * patches;
            var rockMask = Math.Max(Smooth(surface.RockSlope - blend, surface.RockSlope + blend, slope),
                Smooth(surface.GrassAltitude - .025f, surface.GrassAltitude + .22f, altitude) * (.54f + .46f * patches));

            var terrain = Vector4.Lerp(Shade(soil, .79f + grain * .29f + patches * .08f),
                Shade(grass, .74f + grain * .20f + patches * .19f), meadow);
            terrain = Vector4.Lerp(terrain, Shade(sand, .90f + grain * .21f), coast);
            terrain = Vector4.Lerp(terrain, Shade(rock, .69f + banding * .25f + grain * .18f), rockMask);
            terrain = Vector4.Lerp(BaseColor(low, high, sampleHeight, sampleSlope), terrain, surface.TextureStrength);
            var ice = SnowCover(surface, altitude, slope);
            terrain = Vector4.Lerp(terrain, Shade(snow, .88f + .15f * grain), ice);
            texture[row * resolution + column] = StudioColor.EncodeStraight(terrain);
        }
        return new MeshTexture(resolution, resolution, texture, ClampEdges: true);
    }

    private static Vector4 BaseColor(Vector4 low, Vector4 high, float altitude, float slope) =>
        Vector4.Lerp(low, high, Smooth(.26f, .70f, altitude + slope * .15f));

    private static float SnowCover(StudioTerrainMaterial surface, float altitude, float slope)
    {
        if (!surface.SnowEnabled) return 0;
        var elevation = Smooth(surface.SnowAltitude - surface.SnowBlend, surface.SnowAltitude + surface.SnowBlend, altitude);
        var accumulation = 1 - Smooth(surface.SnowSlope - surface.SnowBlend, surface.SnowSlope + surface.SnowBlend, slope);
        return elevation * accumulation;
    }

    private static Vector4 Shade(Vector4 color, float value) => color * new Vector4(value, value, value, 1);

    private float Sample(float[] data, float u, float v)
    {
        var gridX = u * (_grid - 1);
        var gridZ = v * (_grid - 1);
        var column = Math.Min(_grid - 2, (int)gridX);
        var row = Math.Min(_grid - 2, (int)gridZ);
        var blendX = gridX - column;
        var index = row * _grid + column;
        var top = Interpolate(data[index], data[index + 1], blendX);
        var bottom = Interpolate(data[index + _grid], data[index + _grid + 1], blendX);
        return Interpolate(top, bottom, gridZ - row);
    }

    private float At(float[] data, int x, int z) =>
        data[Math.Clamp(z, 0, _grid - 1) * _grid + Math.Clamp(x, 0, _grid - 1)];
    private float WorldY(float altitude) => (altitude - .42f) * _relief;

    private float BoundaryAlpha(float x, float z) =>
        _settings.Endless ? 1 : 1f - Smooth(.85f, 1f, Math.Max(Math.Abs(x), Math.Abs(z)) / Region.Extent);

    private static float Axis(float unit, float extent) => (unit * 2 - 1) * extent;

    private float AxisToUnit(float offset) => Math.Clamp((offset / Region.Extent + 1f) * .5f, 0, 1);
    private static float Height(StudioTerrain settings, float x, float z)
    {
        var frequency = settings.Frequency * .62f;
        var warpX = Noise(x * .91f + 11.3f, z * .91f, settings.Seed - 53) - .5f;
        var warpZ = Noise(x * .88f, z * .88f + 33.7f, settings.Seed + 31) - .5f;
        var amplitude = 1f;
        var sum = 0f;
        var weight = 0f;
        for (var octave = 0; octave < 5; octave++)
        {
            var baseHeight = Noise((x + warpX * .42f) * frequency + 13f,
                (z + warpZ * .42f) * frequency + 17f, settings.Seed + octave * 167);
            var crest = 1 - Math.Abs(2 * baseHeight - 1);
            sum += amplitude * Interpolate(baseHeight, crest * crest, settings.Ridge);
            weight += amplitude;
            frequency *= 2.03f;
            amplitude *= .14f + settings.Roughness * .70f;
        }
        var islandFade = settings.Endless ? 0 : Smooth(.52f, 1.02f, Math.Max(Math.Abs(x), Math.Abs(z)) / TerrainRegion.Island.Extent) * .38f;
        return Math.Clamp((sum / weight - .5f) * 1.58f + .52f - islandFade, 0, 1);
    }

    private static float Noise(float x, float z, int seed)
    {
        var ix = (int)MathF.Floor(x);
        var iz = (int)MathF.Floor(z);
        var dx = Smooth(0, 1, x - ix);
        var dz = Smooth(0, 1, z - iz);
        return Interpolate(Interpolate(Hash(ix, iz, seed), Hash(ix + 1, iz, seed), dx),
            Interpolate(Hash(ix, iz + 1, seed), Hash(ix + 1, iz + 1, seed), dx), dz);
    }

    private static float Hash(int x, int z, int seed)
    {
        var bits = unchecked((uint)x * 0x6c8e9cf5u ^ (uint)z * 0x91e10da5u ^ (uint)seed * 0x85ebca77u);
        bits ^= bits >> 16;
        bits *= 0x7feb352du;
        bits ^= bits >> 15;
        bits *= 0x846ca68bu;
        bits ^= bits >> 16;
        return (bits & 0xffffffu) / 16777215f;
    }

    private static float Smooth(float low, float high, float value)
    {
        var fraction = Math.Clamp((value - low) / (high - low), 0, 1);
        return fraction * fraction * (3 - 2 * fraction);
    }

    private static float Interpolate(float a, float b, float amount) => a + (b - a) * amount;
}
