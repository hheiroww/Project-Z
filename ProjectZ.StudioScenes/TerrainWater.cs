using System.Numerics;

namespace ProjectZ.StudioScenes;

internal readonly record struct TerrainWaterVertex(float Primary, float Cross, float Strength)
{
    internal static TerrainWaterVertex At(float x, float z, StudioTerrain terrain)
    {
        var scale = terrain.Endless ? 1f : 2.25f;
        x *= scale;
        z *= scale;
        var warp = TerrainGeometry.Noise(x * .34f + 29f, z * .32f - 13f, terrain.Seed + 7103) - .5f;
        var drift = TerrainGeometry.Noise(x * .69f - 7f, z * .71f + 41f, terrain.Seed - 3581) - .5f;
        var strength = .62f + .38f * TerrainGeometry.Noise(x * .49f, z * .52f + 17f, terrain.Seed ^ 48371);
        var primary = x * 4.57 + z * 3.26 + 6.8 * warp + 2.5 * drift;
        var cross = x * 2.23 - z * 5.05 + 5.1 * drift - 2.6 * warp;
        return new TerrainWaterVertex((float)Math.IEEERemainder(primary, Math.Tau),
            (float)Math.IEEERemainder(cross, Math.Tau), strength);
    }
}

internal readonly record struct TerrainWaterFrame(float Primary, float Cross, float Height, float Light)
{
    internal const float MaximumDisplacement = .02f;

    internal static TerrainWaterFrame At(double seconds, StudioSignal signal) => new(
        (float)Math.IEEERemainder(seconds * 3.4, Math.Tau) + .5f * signal.Mid,
        (float)Math.IEEERemainder(-seconds * 3.9, Math.Tau),
        .0038f + .01f * signal.Bass,
        .55f + .35f * signal.Beat + .10f * signal.Treble);

    internal void Apply(TerrainWaterVertex wave, ref Vector3 point, ref Vector4 color)
    {
        var ripple = (MathF.Sin(wave.Primary + Primary) + .38f * MathF.Sin(wave.Cross + Cross)) * wave.Strength;
        point.Y += ripple * Height;
        var crest = Math.Clamp(.5f + ripple * .32f, 0, 1);
        crest *= crest;
        crest *= crest;
        var highlight = new Vector3(color.X, color.Y, color.Z) * (.61f + .45f * crest)
            + new Vector3(.22f, .34f, .43f) * crest * Light;
        color = new Vector4(highlight, color.W);
    }
}
