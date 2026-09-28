namespace ProjectZ.Vst;

internal sealed record GranularProfile(
    string Name,
    float[] Wavetable,
    float[]? SourceSamples = null,
    double GrainSizeMs = 85,
    double Density = 22,
    double Position = 50,
    double Spray = 28,
    double PitchSemitones = 0,
    double TableDepth = 55,
    double StereoSpread = 65,
    double Mix = 100,
    double OutputDb = 0,
    int SourceSampleRate = 48000)
{
    public const int TableLength = 2048;

    public static GranularProfile Default { get; } = CreateDefault();

    private static GranularProfile CreateDefault()
    {
        var table = Enumerable.Range(0, TableLength)
            .Select(i => (float)(
                (0.72 * Math.Sin(Math.Tau * i / TableLength)) +
                (0.20 * Math.Sin(Math.Tau * 2 * i / TableLength)) +
                (0.08 * Math.Sin(Math.Tau * 3 * i / TableLength))))
            .ToArray();
        ProfileFactory.Normalize(table);
        return new GranularProfile("Warm harmonic wave", table, ProfileFactory.MakeSource(table));
    }
}
