using System.Diagnostics;
using System.Runtime.InteropServices;
using ProjectZ.StudioScenes;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

internal static class StudioOceanGpuSmoke
{
    internal static int[] Run(int imageWidth, int imageHeight)
    {
        const double time = 8.25;
        var scene = StudioScene.CreateOcean();
        scene.Camera.Pitch = -0.8f;
        scene.Ocean.EyeHeight = 2.6f;
        scene.Ocean.WindSpeed = 14;
        scene.Ocean.AudioResponse = 1;
        using var renderer = new StudioSceneRender(scene);
        var clock = Stopwatch.StartNew();
        while (!renderer.LastOceanFrameUsedGpu && clock.Elapsed.TotalSeconds < 35)
        {
            renderer.Render(192, 108, time);
            if (renderer.OceanGpuError is { } error)
                throw new InvalidOperationException(error);
            if (!renderer.LastOceanFrameUsedGpu)
                Thread.Sleep(20);
        }
        Require(renderer.LastOceanFrameUsedGpu, "The hardware ocean did not become available.");
        var original = renderer.CopyFrame(192, 108, time);
        Require(original.Distinct().Count() > 2_000, "The ocean shader did not produce normal texture and color variation.");

        var transparent = scene with { Sky = scene.Sky with { Enabled = false } };
        var mask = renderer.CopyFrame(192, 108, time, transparent);
        var sky = mask.Count(value => value == 0);
        Require(sky is > 6000 and < 14000 && mask.Zip(original).All(pair => pair.First == 0 || pair.First == pair.Second),
            "The hardware path did not preserve the water and transparent sky pixels.");
        var clearSky = scene with { Ocean = scene.Ocean with { CloudCoverage = 0, SunIntensity = .2f } };
        var relit = renderer.CopyFrame(192, 108, time, clearSky);
        Require(original.Zip(relit).Zip(mask).Count(pair => pair.Second == 0 && pair.First.First != pair.First.Second) > 2000,
            "Cloud and sunlight settings did not change the GPU sky.");
        var evolved = renderer.CopyFrame(192, 108, time + .33);
        var driven = renderer.CopyFrame(192, 108, time + .33, signal: new StudioSignal(Bass: .95f, Beat: .9f));
        Require(Difference(original, evolved, mask) > 4000, "The inverse FFT did not animate the water.");
        Require(Difference(evolved, driven, mask) > 4000, "Live audio did not drive the hardware wave field.");
        Require(original.AsSpan().SequenceEqual(renderer.Render(192, 108, time)), "A backward seek did not rebuild the exact wave and foam frame.");
        Require(original.AsSpan().SequenceEqual(renderer.Render(192, 108, time + 200)), "The spectrum did not repeat on its 200-second period.");
        var nextSeed = scene with { Ocean = scene.Ocean with { Seed = 991 } };
        Require(Difference(original, renderer.CopyFrame(192, 108, time, nextSeed), mask) > 4000, "Re-seeding did not regenerate the wave field.");
        Require(renderer.LastOceanFrameUsedGpu, "An ocean regression silently fell back to software.");

        using var device = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport, FeatureLevel.Level_11_0);
        using var context = device.ImmediateContext;
        using var external = new StudioSceneRender(scene);
        Require(external.TryRenderOceanTexture(device.NativePointer, context.NativePointer, 320, 180, time,
            default, default, false, out var address), "Could not borrow the host device's ocean texture.");
        Marshal.AddRef(address);
        using var texture = new Texture2D(address);
        Require(texture.Description.Format == Format.R8G8B8A8_UNorm
            && texture.Description.BindFlags.HasFlag(BindFlags.ShaderResource)
            && texture.Description.Width == 320 && texture.Description.Height == 180,
            "The native handoff did not return the agreed RGBA8 shader texture.");
        Require(external.TryRenderOceanTexture(device.NativePointer, context.NativePointer, 320, 180, time + .1,
            default, default, false, out var again) && address == again, "Same-size playback did not reuse the host texture.");

        var widths = new[] { 640, 1280 };
        foreach (int width in widths)
        {
            int height = width * 9 / 16;
            for (var warmup = 4; warmup > 0; warmup--)
                renderer.Render(width, height, time - warmup / 30d);
            var milliseconds = new double[32];
            for (int frame = 0; frame < milliseconds.Length; frame++)
            {
                long start = Stopwatch.GetTimestamp();
                renderer.Render(width, height, time + frame / 30d);
                milliseconds[frame] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            Array.Sort(milliseconds);
            Console.WriteLine($"GPU ocean {width}×{height}, including BGRA readback, p50 {milliseconds[15]:F2} ms, p95 {milliseconds[30]:F2} ms.");
        }
        var image = renderer.Render(imageWidth, imageHeight, time, scene,
            new StudioSignal(Bass: .38f, Beat: .44f), default, highQuality: true).ToArray();
        Require(renderer.LastOceanFrameUsedGpu, "Capture quality did not run on the hardware ocean.");
        Console.WriteLine("PASS on-device 2D inverse FFT, host-owned texture reuse, audio, sky alpha, resolution, repeat, seek and reseed.");
        return image;
    }

    private static int Difference(int[] a, int[] b, int[] waterMask) =>
        a.Zip(b).Zip(waterMask).Count(pair => pair.Second != 0 && pair.First.First != pair.First.Second);

    private static void Require(bool result, string message)
    {
        if (!result)
            throw new InvalidOperationException(message);
    }
}
