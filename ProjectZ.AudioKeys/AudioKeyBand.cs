namespace ProjectZ.AudioKeys;

public enum AudioKeyMode { Rms, Peak, Trigger, Onset }

public sealed record AudioKeyBand
{
    public double LowHz { get; init; }
    public double HighHz { get; init; } = 250;
    public AudioKeyMode Mode { get; init; }
    public double Threshold { get; init; }
    public double Gain { get; init; } = 1;
    public double AttackSeconds { get; init; } = .02;
    public double ReleaseSeconds { get; init; } = .12;

    public AudioKeyBand Validate(int sampleRate)
    {
        if (!double.IsFinite(LowHz) || !double.IsFinite(HighHz) || LowHz < 0 ||
            HighHz <= LowHz || HighHz > sampleRate * .5 || !Enum.IsDefined(Mode) ||
            !double.IsFinite(Threshold) || Threshold is < 0 or > 1 ||
            !double.IsFinite(Gain) || Gain is < 0 or > 100 ||
            !double.IsFinite(AttackSeconds) || AttackSeconds is < 0 or > 10 ||
            !double.IsFinite(ReleaseSeconds) || ReleaseSeconds is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(AudioKeyBand), "Check the frequency boundaries, gain, threshold, and envelope times.");
        return this;
    }
}
