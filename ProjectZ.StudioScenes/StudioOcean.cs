namespace ProjectZ.StudioScenes;

/// <summary>Physical wind, water and light settings for an open-ocean Studio scene.</summary>
public sealed record StudioOcean
{
    public int Seed { get; set; } = 173;
    public float WindSpeed { get; set; } = 12;
    public float WindDirection { get; set; } = 28;
    public float Fetch { get; set; } = 42_000;
    public float Depth { get; set; } = 70;
    public float Swell { get; set; } = .45f;
    public float Choppiness { get; set; } = 1.28f;
    public float WaveScale { get; set; } = .87f;
    public float Foam { get; set; } = .82f;
    public float AudioResponse { get; set; } = .28f;
    public float EyeHeight { get; set; } = 3.2f;
    public float SunElevation { get; set; } = 17;
    public float SunAzimuth { get; set; } = 18;
    public float Roughness { get; set; } = .14f;

    public void Validate()
    {
        SceneRanges.Check(WindSpeed, 1, 35, "Ocean wind speed");
        SceneRanges.Check(WindDirection, -3600, 3600, "Ocean wind direction");
        SceneRanges.Check(Fetch, 100, 1_000_000, "Ocean wind fetch");
        SceneRanges.Check(Depth, 1, 1000, "Ocean depth");
        SceneRanges.Check(Swell, 0, 1, "Ocean swell");
        SceneRanges.Check(Choppiness, 0, 3, "Ocean choppiness");
        SceneRanges.Check(WaveScale, 0, 3, "Ocean wave scale");
        SceneRanges.Check(Foam, 0, 2, "Ocean foam");
        SceneRanges.Check(AudioResponse, 0, 2, "Ocean audio response");
        SceneRanges.Check(EyeHeight, .5f, 150, "Ocean eye height");
        SceneRanges.Check(SunElevation, -10, 90, "Ocean sun elevation");
        SceneRanges.Check(SunAzimuth, -3600, 3600, "Ocean sun azimuth");
        SceneRanges.Check(Roughness, .025f, .75f, "Ocean roughness");
    }
}
