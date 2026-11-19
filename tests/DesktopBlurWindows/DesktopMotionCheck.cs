using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Composition;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

internal sealed class DesktopMotionCheck
{
    private const int SourceRate = 60;
    private const int FrameCount = SourceRate * 3;
    private readonly Stopwatch time = new();
    private readonly double[] sourceTimes = Enumerable.Repeat(double.NaN, FrameCount).ToArray();
    private readonly List<double> latencies = new();
    private readonly List<double> intervals = new();
    private readonly Color[] sample = new Color[1];
    private long initialCaptures;
    private double lastObservedTime;
    private int lastPainted = -1;
    private int lastObserved = -1;
    private int produced;
    public bool Started => time.IsRunning;

    public void Start(DesktopBackdrop backdrop)
    {
        initialCaptures = backdrop.CapturedFrameCount;
        // Flat, separated RGB code values let the probe recognize source
        // frames after Gaussian blur without recording desktop pixels.
        backdrop.Settings.Saturation = 1;
        time.Start();
    }

    public void UpdateSource(NativeBackdropFixture? source)
    {
        if (!Started || source is null) return;
        var elapsed = time.Elapsed.TotalSeconds;
        var sequence = Math.Min((int)(elapsed * SourceRate), FrameCount - 1);
        if (sequence == lastPainted) return;
        source.SetColor(System.Drawing.Color.FromArgb(140 + sequence % 16 * 5, 160, 40 + sequence / 16 * 12));
        sourceTimes[sequence] = time.Elapsed.TotalSeconds;
        lastPainted = sequence;
        produced++;
    }

    public bool CheckFrame(GraphicsDevice device, DesktopBackdrop backdrop)
    {
        device.GetBackBufferData(new Rectangle(240, 160, 1, 1), sample, 0, sample.Length);
        var elapsed = time.Elapsed.TotalSeconds;
        var sequence = Decode(sample[0]);
        if (sequence > lastObserved && !double.IsNaN(sourceTimes[sequence]))
        {
            latencies.Add((elapsed - sourceTimes[sequence]) * 1000);
            if (lastObserved >= 0) intervals.Add((elapsed - lastObservedTime) * 1000);
            lastObservedTime = elapsed;
            lastObserved = sequence;
        }
        if (elapsed < FrameCount / (double)SourceRate + 0.25) return false;

        var captured = backdrop.CapturedFrameCount - initialCaptures;
        var latency = Percentile95(latencies);
        var interval = Percentile95(intervals);
        Console.WriteLine($"NATIVE_DESKTOP_MOTION produced={produced} observed={latencies.Count} captured={captured} " +
            $"latencyP95Ms={latency:F2} frameIntervalP95Ms={interval:F2}");
        Program.Require(produced >= 90 && latencies.Count >= produced * 0.7 && lastObserved >= FrameCount - 3 &&
            latency <= 120 && interval <= 100, "The native blur did not follow the controlled animation.");
        return true;
    }

    private static int Decode(Color color)
    {
        var column = (int)Math.Round((color.R - 140) / 5.0);
        var row = (int)Math.Round((color.B - 40) / 12.0);
        var sequence = row * 16 + column;
        return column is >= 0 and < 16 && sequence is >= 0 and < FrameCount && Math.Abs(color.G - 160) <= 2 &&
            Math.Abs(color.R - (140 + column * 5)) <= 2 && Math.Abs(color.B - (40 + row * 12)) <= 2 ? sequence : -1;
    }

    private static double Percentile95(List<double> values)
    {
        if (values.Count == 0) return double.PositiveInfinity;
        values.Sort();
        return values[(int)Math.Ceiling(values.Count * 0.95) - 1];
    }
}
