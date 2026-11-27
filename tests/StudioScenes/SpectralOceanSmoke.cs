using System.Diagnostics;
using System.Numerics;
using ProjectZ.StudioScenes;

internal static class SpectralOceanSmoke
{
    private const int Width = 160;
    private const int Height = 96;
    private const double Time = 8.25;
    private static readonly Vector2[] Points =
    [
        new(1.25f, -11f), new(-23f, 31.5f), new(34.75f, 14.25f), new(68f, -53.5f)
    ];

    internal static void Run()
    {
        CheckSpectrum();
        CheckFrames();
        Console.WriteLine("PASS spectral ocean repeat, seek, audio, alpha, camera motion, and legacy terrain");
    }

    internal static int[] CapturePreview(int width, int height)
    {
        var scene = CreateScene();
        using var renderer = new StudioSceneRender(scene);
        var signal = new StudioSignal(.7f, .25f, .18f, .85f);
        renderer.Render(640, 360, Time, signal: signal);
        var clock = Stopwatch.StartNew();
        for (var frame = 1; frame <= 3; frame++)
            renderer.Render(640, 360, Time + frame / 30d, signal: signal);
        var previewMs = clock.Elapsed.TotalMilliseconds / 3;
        renderer.Render(width, height, Time, settings: null, signal, default, highQuality: true);
        clock.Restart();
        var capture = renderer.Render(width, height, Time + .125, settings: null, signal, default, highQuality: true).ToArray();
        Console.WriteLine($"Ocean CPU preview 640×360: {previewMs:F1} ms/frame. Capture {width}×{height}: {clock.Elapsed.TotalMilliseconds:F1} ms.");
        return capture;
    }

    private static void CheckSpectrum()
    {
        var settings = CreateScene().Ocean;
        var waves = new SpectralOcean();
        waves.Update(settings, Time);
        var before = Sample(waves);
        Require(before.Any(sample => MathF.Abs(sample.Height) > .001f), "The populated FFT returned flat water.");
        foreach (var (point, field) in Points.Zip(before))
        {
            Require(MathF.Abs(waves.SampleHeight(point.X, point.Y) - field.Height) < .00001f && field.Normal.Y > 0 &&
                    MathF.Abs(field.Normal.Length() - 1f) < .00001f && float.IsFinite(field.Jacobian) && field.Foam is >= 0 and <= 1,
                "An ocean sample had inconsistent height, a nonphysical normal, or invalid compression/foam.");
        }

        var other = new SpectralOcean();
        other.Update(settings with { }, Time);
        Require(before.SequenceEqual(Sample(other)), "Fresh equal ocean spectra differed at the same time and seed.");
        other.Update(settings with { Seed = settings.Seed + 1 }, Time);
        Require(!before.SequenceEqual(Sample(other)), "Changing the ocean seed did not alter the spectral field.");

        waves.Update(settings, Time + .25);
        Require(before.Zip(Sample(waves)).Any(pair => MathF.Abs(pair.First.Height - pair.Second.Height) > .001f),
            "The spectral field did not evolve over time.");
        waves.Update(settings, Time);
        Require(before.SequenceEqual(Sample(waves)), "A backward ocean seek retained unrelated foam history.");
        waves.Update(settings, Time + 200);
        Require(before.SequenceEqual(Sample(waves)), "A nonsequential seek through the 200-second spectral cycle changed the field.");

        waves.Update(settings, Time, new StudioSignal(Bass: 1, Beat: 1));
        var pulsed = Sample(waves);
        Require(before.Zip(pulsed).All(pair =>
                MathF.Abs(pair.Second.Height - pair.First.Height * (1f + settings.AudioResponse)) < .0002f),
            "Current host audio did not rescale the continuous spectral height as configured.");
        var still = new SpectralOcean();
        still.Update(settings with { Choppiness = 0 }, Time);
        Require(Sample(still).All(sample => sample.Displacement.X == 0 && sample.Displacement.Z == 0 &&
                    sample.Jacobian == 1 && sample.Foam == 0),
            "Disabling choppy displacement left horizontal deflection or breaking foam in a fresh wave field.");

        var left = waves.Sample(191.999f, 7.25f);
        var right = waves.Sample(192.001f, 7.25f);
        Require(Vector3.Distance(left.Displacement, right.Displacement) < .02f,
            "A world-space cascade boundary introduced a visible displacement discontinuity.");
        var filtered = waves.Sample(12, 56, footprint: 100);
        Require(filtered.Displacement == Vector3.Zero && filtered.Normal == Vector3.UnitY && filtered.Foam == 0,
            "Subpixel waves survived minification into a fully filtered footprint.");
    }

    private static void CheckFrames()
    {
        // This compact version-one fixture represents scenes saved before the ocean kind existed.
        var terrain = StudioSceneFile.Deserialize("""{"version":1,"kind":"terrain","terrain":{"grid":17,"showWater":false}}""");
        Require(terrain.HasTerrain && !terrain.HasOcean && terrain.Terrain.Grid == 17 &&
                !StudioSceneFile.Serialize(terrain).Contains("\"ocean\"", StringComparison.Ordinal),
            "Legacy terrain lost its authored settings or serialized an unrecognized ocean block.");
        using var renderer = new StudioSceneRender(terrain);
        var originalTerrain = renderer.CopyFrame(Width, Height, Time);
        Require(originalTerrain.Any(pixel => (pixel >>> 24) != 0), "The legacy terrain fixture was empty.");

        var ocean = CreateScene();
        var roundTrip = StudioSceneFile.Deserialize(StudioSceneFile.Serialize(ocean));
        Require(roundTrip == ocean && roundTrip.HasOcean && !roundTrip.HasTerrain &&
                !ReferenceEquals(ocean.Copy().Ocean, ocean.Ocean),
            "Ocean copy or portable scene data lost its independent physical settings.");

        var opaque = renderer.CopyFrame(Width, Height, Time, roundTrip);
        var noSky = ocean with { Sky = ocean.Sky with { Enabled = false } };
        var masked = renderer.CopyFrame(Width, Height, Time, noSky);
        var waterPixels = 0;
        var skyPixels = 0;
        var minimum = 255;
        var maximum = 0;
        for (var index = 0; index < opaque.Length; index++)
        {
            Require((opaque[index] >>> 24) == 255, "Enabled ocean sky left an uncovered pixel.");
            if (masked[index] == 0)
            {
                skyPixels++;
                continue;
            }
            Require((masked[index] >>> 24) == 255 && masked[index] == opaque[index],
                "Hiding the sky changed the shaded ocean or produced non-premultiplied transparency.");
            waterPixels++;
            var pixel = masked[index];
            var brightness = (((pixel >>> 16) & 255) + ((pixel >>> 8) & 255) + (pixel & 255)) / 3;
            minimum = Math.Min(minimum, brightness);
            maximum = Math.Max(maximum, brightness);
        }
        Require(skyPixels > Width * Height / 5 && waterPixels > Width * Height / 5 && maximum - minimum > 30,
            "The seeded ocean frame lacked a stable sky horizon or distinct water luminance.");

        var later = renderer.CopyFrame(Width, Height, Time + .25, ocean);
        Require(CountChangedWater(opaque, later, masked) > waterPixels / 5,
            "Approaching wave fronts did not visibly change the frame over time.");
        var driven = renderer.CopyFrame(Width, Height, Time + .25, ocean, new StudioSignal(Bass: 1f, Beat: 1f));
        Require(CountChangedWater(later, driven, masked) > waterPixels / 5,
            "Beat-driven wave strength did not reach the rendered ocean.");
        Require(opaque.AsSpan().SequenceEqual(renderer.Render(Width, Height, Time, ocean)),
            "Returning to the original ocean time did not recreate its frame.");

        var moving = ocean with
        {
            Motion = new StudioCameraMotion { Mode = StudioCameraMode.Flythrough, DistancePerBeat = 12 }
        };
        Require(opaque.AsSpan().SequenceEqual(renderer.Render(Width, Height, Time, moving, clock: new StudioClock(120, 0))),
            "The zero-position world camera changed the original ocean view.");
        var translated = renderer.CopyFrame(Width, Height, Time, moving, clock: new StudioClock(120, 4));
        Require(CountChangedWater(opaque, translated, masked) > waterPixels / 5,
            "Camera translation sampled the same tile instead of the shared world-space ocean.");
        Require(translated.AsSpan(0, Width).SequenceEqual(opaque.AsSpan(0, Width)),
            "Camera translation moved the fixed horizon sky as if it were projected onto the water.");

        var capture = renderer.Render(Width, Height, Time, ocean, default, default, highQuality: true).ToArray();
        Require(CountChangedWater(opaque, capture, masked) > waterPixels / 20,
            "Capture quality did not change ocean sampling.");
        Require(opaque.AsSpan().SequenceEqual(renderer.Render(Width, Height, Time, ocean)),
            "Changing quality left the preview on a different wave field.");
        Require(originalTerrain.AsSpan().SequenceEqual(renderer.Render(Width, Height, Time)),
            "Returning from the ocean renderer altered the legacy terrain frame.");
    }

    private static StudioScene CreateScene() => new()
    {
        Kind = StudioSceneKind.Ocean,
        Camera = new StudioCamera { Yaw = 16, Pitch = 3, FieldOfView = 49 },
        Ocean = new StudioOcean { Seed = 613, WindSpeed = 15, AudioResponse = 1.2f, SunAzimuth = 14, SunElevation = 12 },
        Sky = new StudioSky { Enabled = true, TopColor = unchecked((int)0xff304f86), HorizonColor = unchecked((int)0xffb3c3d8) }
    };

    private static StudioOceanSample[] Sample(SpectralOcean waves) =>
        Points.Select(point => waves.Sample(point.X, point.Y)).ToArray();

    private static int CountChangedWater(ReadOnlySpan<int> before, ReadOnlySpan<int> after, ReadOnlySpan<int> mask)
    {
        var count = 0;
        for (var i = 0; i < before.Length; i++)
            if (mask[i] != 0 && before[i] != after[i])
                count++;
        return count;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
