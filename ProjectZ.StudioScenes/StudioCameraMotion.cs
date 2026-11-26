namespace ProjectZ.StudioScenes;

public enum StudioCameraMode { Static, Flythrough, Orbit, Keyframes }
public enum StudioBeatGrid { Free, Bar, Half, Quarter, Eighth, Sixteenth }

/// <summary>Quarter-note transport position and tempo. Hosts can omit position when only elapsed time is available.</summary>
public readonly record struct StudioClock(double Tempo = 0, double? BeatPosition = null);
public readonly record struct StudioCameraPose(StudioCamera Camera, float X, float Y, float Z);

public sealed record StudioCameraKeyframe
{
    public double Beat { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public StudioCamera Camera { get; set; } = new();

    internal StudioCameraKeyframe Copy() => this with { Camera = Camera with { } };
}

public sealed record StudioCameraMotion
{
    private const int MaximumKeys = 64;
    private const double MaximumBeat = 16384;

    public StudioCameraMode Mode { get; set; }
    public StudioBeatGrid Grid { get; set; } = StudioBeatGrid.Quarter;
    public bool SyncToTrack { get; set; } = true;
    public bool Loop { get; set; }
    public double Tempo { get; set; } = 120;
    public double DurationBeats { get; set; } = 16;
    public double PhaseOffsetBeats { get; set; }
    public int BeatsPerBar { get; set; } = 4;
    public float DistancePerBeat { get; set; } = .42f;
    public List<StudioCameraKeyframe> Keyframes { get; set; } = [];

    public StudioCameraMotion Copy() => this with { Keyframes = Keyframes.Select(key => key.Copy()).ToList() };

    public bool Equals(StudioCameraMotion? other) => ReferenceEquals(this, other) ||
        other is not null && Mode == other.Mode && Grid == other.Grid && SyncToTrack == other.SyncToTrack &&
        Loop == other.Loop && Tempo == other.Tempo && DurationBeats == other.DurationBeats &&
        PhaseOffsetBeats == other.PhaseOffsetBeats && BeatsPerBar == other.BeatsPerBar &&
        DistancePerBeat == other.DistancePerBeat && Keyframes.SequenceEqual(other.Keyframes);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Mode); hash.Add(Grid); hash.Add(SyncToTrack); hash.Add(Loop); hash.Add(Tempo);
        hash.Add(DurationBeats); hash.Add(PhaseOffsetBeats); hash.Add(BeatsPerBar); hash.Add(DistancePerBeat);
        foreach (var key in Keyframes)
            hash.Add(key);
        return hash.ToHashCode();
    }

    public double Quantize(double beat)
    {
        if (!double.IsFinite(beat))
            throw new ArgumentOutOfRangeException(nameof(beat), "Enter a finite beat position.");
        beat = Math.Clamp(beat, 0, MaximumBeat);
        var step = Grid switch
        {
            StudioBeatGrid.Bar => Math.Clamp(BeatsPerBar, 1, 32),
            StudioBeatGrid.Half => 2d,
            StudioBeatGrid.Quarter => 1d,
            StudioBeatGrid.Eighth => .5d,
            StudioBeatGrid.Sixteenth => .25d,
            _ => 0d
        };
        return step <= 0 ? beat : Math.Min(MaximumBeat, Math.Round(beat / step, MidpointRounding.AwayFromZero) * step);
    }

    public void SetKey(StudioCameraKeyframe key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Camera is null)
            throw new InvalidDataException("A camera key needs a pose.");
        var stored = key.Copy();
        stored.Beat = Quantize(stored.Beat);
        ValidateKey(stored);
        var existing = Keyframes.FindIndex(item => Math.Abs(item.Beat - stored.Beat) < .000001);
        if (existing >= 0)
        {
            Keyframes[existing] = stored;
        }
        else
        {
            if (Keyframes.Count >= MaximumKeys)
                throw new InvalidOperationException($"A scene can contain up to {MaximumKeys} camera keys.");
            var next = Keyframes.FindIndex(item => item.Beat > stored.Beat);
            Keyframes.Insert(next < 0 ? Keyframes.Count : next, stored);
        }
    }

    public double GetBeat(double seconds, StudioClock clock = default)
    {
        var fallback = double.IsFinite(Tempo) ? Math.Clamp(Tempo, 20, 400) : 120;
        var tempo = SyncToTrack && clock.Tempo is >= 20 and <= 400 ? clock.Tempo : fallback;
        var supplied = SyncToTrack && clock.BeatPosition is { } beat && double.IsFinite(beat)
            ? beat
            : (double.IsFinite(seconds) ? seconds : 0) * (tempo / 60d);
        return Math.Clamp(supplied + PhaseOffsetBeats, -1e9, 1e9);
    }

    /// <summary>Samples a continuous camera pose. Beat snapping is applied to authored keys, leaving playback smooth.</summary>
    public StudioCameraPose Sample(StudioCamera baseCamera, double seconds, StudioClock clock = default)
    {
        if (Mode == StudioCameraMode.Static)
            return new(baseCamera, 0, 0, 0);
        var beat = GetBeat(seconds, clock);
        if (Mode == StudioCameraMode.Flythrough)
        {
            var yaw = baseCamera.Yaw * (MathF.PI / 180);
            var distance = (float)Math.Clamp(beat * DistancePerBeat, -1_000_000, 1_000_000);
            return new(baseCamera, -MathF.Sin(yaw) * distance, 0, -MathF.Cos(yaw) * distance);
        }
        if (Mode == StudioCameraMode.Orbit)
        {
            var progress = beat / Math.Max(DurationBeats, .25);
            var turn = Loop ? progress - Math.Floor(progress) : Math.Clamp(progress, 0, 1);
            return new(baseCamera with { Yaw = baseCamera.Yaw + (float)(turn * 360) }, 0, 0, 0);
        }
        if (Keyframes.Count == 0)
            return new(baseCamera, 0, 0, 0);
        if (Keyframes.Count == 1)
            return Pose(Keyframes[0]);

        var first = Keyframes[0];
        var last = Keyframes[^1];
        if (!Loop)
        {
            if (beat <= first.Beat) return Pose(first);
            if (beat >= last.Beat) return Pose(last);
        }

        var cycle = Math.Max(DurationBeats, last.Beat + .25);
        if (Loop)
            beat -= Math.Floor(beat / cycle) * cycle;
        var next = Keyframes.FindIndex(key => key.Beat > beat);
        if (next < 0) next = Keyframes.Count;
        var p0 = TimedKey(next - 2, cycle);
        var p1 = TimedKey(next - 1, cycle);
        var p2 = TimedKey(next, cycle);
        var p3 = TimedKey(next + 1, cycle);
        var span = p2.Beat - p1.Beat;
        var t = Math.Clamp((beat - p1.Beat) / span, 0, 1);
        var ease = (float)(t * t * (3 - 2 * t));
        var a = p1.Key.Camera;
        var b = p2.Key.Camera;
        var camera = new StudioCamera
        {
            Yaw = MoveAngle(a.Yaw, b.Yaw, ease),
            Pitch = Lerp(a.Pitch, b.Pitch, ease),
            Roll = MoveAngle(a.Roll, b.Roll, ease),
            Zoom = Lerp(a.Zoom, b.Zoom, ease),
            FieldOfView = Lerp(a.FieldOfView, b.FieldOfView, ease)
        };
        return new(camera,
            SmoothPosition(p0, p1, p2, p3, t, static key => key.X),
            SmoothPosition(p0, p1, p2, p3, t, static key => key.Y),
            SmoothPosition(p0, p1, p2, p3, t, static key => key.Z));
    }

    public void Validate()
    {
        if (!Enum.IsDefined(Mode) || !Enum.IsDefined(Grid) || Keyframes is null || Keyframes.Count > MaximumKeys ||
            BeatsPerBar is < 1 or > 32 || !double.IsFinite(Tempo) || Tempo is < 20 or > 400 ||
            !double.IsFinite(DurationBeats) || DurationBeats is < .25 or > MaximumBeat ||
            !double.IsFinite(PhaseOffsetBeats) || Math.Abs(PhaseOffsetBeats) > MaximumBeat)
            throw new InvalidDataException("The camera mode, tempo, grid or duration is invalid.");
        SceneRanges.Check(DistancePerBeat, 0, 128, "Camera distance per beat");
        var previous = -1d;
        foreach (var key in Keyframes)
        {
            if (key is null || key.Beat - previous < .000001)
                throw new InvalidDataException("Camera keys must be in distinct ascending beat order.");
            ValidateKey(key);
            previous = key.Beat;
        }
    }

    private static void ValidateKey(StudioCameraKeyframe key)
    {
        if (!double.IsFinite(key.Beat) || key.Beat is < 0 or > MaximumBeat || key.Camera is null)
            throw new InvalidDataException("The camera key time or pose is invalid.");
        SceneRanges.Check(key.X, -1_000_000, 1_000_000, "Camera key X");
        SceneRanges.Check(key.Y, -1_000_000, 1_000_000, "Camera key Y");
        SceneRanges.Check(key.Z, -1_000_000, 1_000_000, "Camera key Z");
        SceneRanges.Check(key.Camera.Yaw, -3600, 3600, "Camera key yaw");
        SceneRanges.Check(key.Camera.Pitch, -89, 89, "Camera key pitch");
        SceneRanges.Check(key.Camera.Roll, -3600, 3600, "Camera key roll");
        SceneRanges.Check(key.Camera.Zoom, .2f, 3, "Camera key zoom");
        SceneRanges.Check(key.Camera.FieldOfView, 18, 90, "Camera key field of view");
    }

    private readonly record struct Timed(StudioCameraKeyframe Key, double Beat);

    private Timed TimedKey(int index, double cycle)
    {
        if (!Loop)
        {
            var clamped = Keyframes[Math.Clamp(index, 0, Keyframes.Count - 1)];
            return new(clamped, clamped.Beat);
        }
        var wrap = (int)Math.Floor((double)index / Keyframes.Count);
        var key = Keyframes[index - wrap * Keyframes.Count];
        return new(key, key.Beat + wrap * cycle);
    }

    private static float SmoothPosition(Timed p0, Timed p1, Timed p2, Timed p3, double t,
        Func<StudioCameraKeyframe, float> get)
    {
        var a = get(p1.Key);
        var b = get(p2.Key);
        var duration = p2.Beat - p1.Beat;
        var m1 = (get(p2.Key) - get(p0.Key)) * duration / (p2.Beat - p0.Beat);
        var m2 = (get(p3.Key) - get(p1.Key)) * duration / (p3.Beat - p1.Beat);
        var t2 = t * t;
        var t3 = t2 * t;
        return (float)Math.Clamp((2 * t3 - 3 * t2 + 1) * a + (t3 - 2 * t2 + t) * m1 +
            (-2 * t3 + 3 * t2) * b + (t3 - t2) * m2, -1_000_000, 1_000_000);
    }

    private static StudioCameraPose Pose(StudioCameraKeyframe key) => new(key.Camera, key.X, key.Y, key.Z);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float MoveAngle(float a, float b, float t) => a + MathF.IEEERemainder(b - a, 360) * t;
}
