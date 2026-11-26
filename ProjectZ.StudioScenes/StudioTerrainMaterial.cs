namespace ProjectZ.StudioScenes;

public enum StudioTerrainAltitudePreset { Coastal, Temperate, Highland, Alpine, Arid }

public sealed record StudioTerrainMaterial
{
    public const int MinimumTextureResolution = 64;
    public const int MaximumTextureResolution = 1024;

    public bool Enabled { get; set; } = true;
    public int TextureResolution { get; set; } = 256;
    public float TextureScale { get; set; } = 1.35f;
    public float TextureStrength { get; set; } = .72f;
    public int SandColor { get; set; } = unchecked((int)0xffb49a68);
    public int SoilColor { get; set; } = unchecked((int)0xff765e50);
    public int GrassColor { get; set; } = unchecked((int)0xff546e49);
    public int RockColor { get; set; } = unchecked((int)0xff787a80);
    public float SandAltitude { get; set; } = .30f;
    public float GrassAltitude { get; set; } = .63f;
    public float RockSlope { get; set; } = .56f;
    public float AltitudeBlend { get; set; } = .09f;
    public bool SnowEnabled { get; set; } = true;
    public float SnowAltitude { get; set; } = .72f;
    public float SnowSlope { get; set; } = .48f;
    public float SnowBlend { get; set; } = .065f;

    /// <summary>Applies a set of editable altitude and slope thresholds. Palette and quality are preserved.</summary>
    public void ApplyAltitudePreset(StudioTerrainAltitudePreset preset)
    {
        (SandAltitude, GrassAltitude, RockSlope, AltitudeBlend, SnowEnabled, SnowAltitude, SnowSlope, SnowBlend) = preset switch
        {
            StudioTerrainAltitudePreset.Coastal => (.39f, .72f, .65f, .12f, false, .89f, .43f, .055f),
            StudioTerrainAltitudePreset.Temperate => (.30f, .63f, .56f, .09f, true, .72f, .48f, .065f),
            StudioTerrainAltitudePreset.Highland => (.21f, .58f, .52f, .09f, true, .65f, .54f, .08f),
            StudioTerrainAltitudePreset.Alpine => (.16f, .47f, .48f, .08f, true, .53f, .59f, .07f),
            StudioTerrainAltitudePreset.Arid => (.49f, .56f, .51f, .11f, false, .89f, .36f, .06f),
            _ => throw new ArgumentOutOfRangeException(nameof(preset))
        };
    }

    internal void Validate()
    {
        if (TextureResolution is < MinimumTextureResolution or > MaximumTextureResolution)
            throw new InvalidDataException("The terrain texture size must be between 64 and 1024 samples per side.");
        SceneRanges.Check(TextureScale, .25f, 8, "Terrain texture scale");
        SceneRanges.Check(TextureStrength, 0, 1, "Terrain texture strength");
        SceneRanges.Check(SandAltitude, 0, 1, "Sand altitude");
        SceneRanges.Check(GrassAltitude, 0, 1, "Grass altitude");
        SceneRanges.Check(RockSlope, 0, 1, "Rock slope");
        SceneRanges.Check(AltitudeBlend, .005f, .40f, "Terrain altitude blend");
        SceneRanges.Check(SnowAltitude, 0, 1, "Snow altitude");
        SceneRanges.Check(SnowSlope, 0, 1, "Snow slope");
        SceneRanges.Check(SnowBlend, .005f, .40f, "Snow blend");
    }
}
