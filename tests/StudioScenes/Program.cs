using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using ProjectZ.StudioScenes;

internal static class Program
{
    private const int Width = 320;
    private const int Height = 240;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length is < 1 or > 3 || args.Any(path => !Path.IsPathFullyQualified(path)))
                throw new ArgumentException("Supply an absolute fixture directory and optional paths for terrain and Project-Z dialog PNGs.");

            CheckCameraMotion();
            CheckTerrain(args.ElementAtOrDefault(1));
            var fixture = CheckSceneFile(args[0]);
            CheckImportedModel(fixture.Scene, fixture.ModelPath);
            if (args.Length == 3) StudioUiSmoke.Run(args[2]);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void CheckCameraMotion()
    {
        var firstCamera = new StudioCamera { Yaw = 350, Zoom = .85f };
        var secondCamera = new StudioCamera { Yaw = 10, Zoom = 1.15f };
        var motion = new StudioCameraMotion
        {
            Mode = StudioCameraMode.Keyframes, Grid = StudioBeatGrid.Quarter,
            SyncToTrack = true, Tempo = 120, DurationBeats = 8
        };

        var end = new StudioCameraKeyframe { Beat = 4.15, X = 8, Y = 1, Z = 4, Camera = secondCamera };
        motion.SetKey(end);
        motion.SetKey(new StudioCameraKeyframe { Beat = .15, Camera = firstCamera, Z = 0 });
        Require(motion.Keyframes.Select(key => key.Beat).SequenceEqual(new[] { 0d, 4d }),
            "Quarter notes did not snap and sort the authored camera keys.");
        end.Camera.Yaw = 72;
        Require(motion.Keyframes[1].Camera.Yaw == 10, "The saved pose shared mutable authoring state.");

        var atTwo = motion.Sample(firstCamera, 66, new StudioClock(90, 2));
        Require(Math.Abs(atTwo.X - 4) < .0001f && Math.Abs(atTwo.Camera.Zoom - 1) < .0001f &&
            Math.Abs(MathF.IEEERemainder(atTwo.Camera.Yaw, 360)) < .0001f,
            "The camera ignored explicit transport beat or the shortest orientation interpolation.");
        var atTwoAndQuarter = motion.Sample(firstCamera, 66, new StudioClock(90, 2.25));
        Require(atTwoAndQuarter.X > atTwo.X && atTwoAndQuarter.X < 8,
            "The authored beat grid constrained continuous playback to stepped camera poses.");

        motion.Grid = StudioBeatGrid.Eighth;
        motion.SetKey(new StudioCameraKeyframe { Beat = 2.27, X = 6, Camera = firstCamera });
        motion.SetKey(new StudioCameraKeyframe { Beat = 2.35, X = 5, Camera = firstCamera });
        Require(motion.Keyframes.Count == 3 && motion.Keyframes[1].Beat == 2.5 && motion.Keyframes[1].X == 5,
            "An eighth-note capture failed to replace the existing key at the same snapped beat.");

        motion.PhaseOffsetBeats = .25;
        Require(Math.Abs(motion.GetBeat(2, new StudioClock(90)) - 3.25) < .00001,
            "Track tempo without a reported musical position did not use elapsed time.");
        Require(Math.Abs(motion.GetBeat(2, new StudioClock(90, 8)) - 8.25) < .00001,
            "Reported musical position did not take precedence over elapsed time.");
        motion.SyncToTrack = false;
        Require(Math.Abs(motion.GetBeat(2, new StudioClock(90, 8)) - 4.25) < .00001,
            "Local tempo mode still used the host transport.");
        motion.SyncToTrack = true;
        motion.PhaseOffsetBeats = 0;
        motion.Loop = true;
        var beforeWrap = motion.Sample(firstCamera, 0, new StudioClock(100, 7.999));
        var afterWrap = motion.Sample(firstCamera, 0, new StudioClock(100, 8.001));
        Require(Math.Abs(beforeWrap.X - afterWrap.X) < .015f && Math.Abs(beforeWrap.Z - afterWrap.Z) < .015f,
            "The looping camera position jumped across its seam.");

        var scene = new StudioScene
        {
            Camera = firstCamera, Motion = motion,
            Terrain = new StudioTerrain { Endless = true, Grid = 17, ShowWater = false,
                Material = new StudioTerrainMaterial { Enabled = false, TextureResolution = 64 } }
        };
        Require(StudioSceneFile.Deserialize(StudioSceneFile.Serialize(scene)) == scene,
            "A portable scene lost its camera path, track settings, or endless terrain setting.");
        using var renderer = new StudioSceneRender(scene);
        var still = renderer.CopyFrame(160, 100, 0, clock: new StudioClock(100, 0));
        Require(!still.AsSpan().SequenceEqual(renderer.Render(160, 100, 0, clock: new StudioClock(100, 3))),
            "Explicit clock changes did not affect an endless terrain render.");
        Console.WriteLine("PASS quantized camera key replacement, continuous poses, host PPQ/tempo, loop seam, and scene exchange");
    }

    private static void CheckTerrain(string? previewPath)
    {
        int red = unchecked((int)0xffff1820);
        int cyan = unchecked((int)0xff0bf2f9);
        var terrain = new StudioScene
        {
            Kind = StudioSceneKind.Terrain,
            Camera = new StudioCamera { Zoom = .70f },
            Terrain = new StudioTerrain
            {
                Seed = 124512, Grid = 49, Relief = .85f, Roughness = .65f,
                ShowWater = false, LowColor = red, HighColor = red, SnowColor = red,
                Material = new StudioTerrainMaterial { Enabled = false }
            }
        };

        using var renderer = new StudioSceneRender(terrain);
        var first = renderer.CopyFrame(Width, Height, 0);
        var firstArea = CheckAlpha(first, Width, Height, "terrain");
        Require(firstArea.Visible > 1000, "The terrain did not draw a substantial surface.");
        Require(firstArea.MinX > 0 && firstArea.MinY > 0 && firstArea.MaxX < Width - 1 && firstArea.MaxY < Height - 1,
            "The terrain did not fit when the camera was zoomed out.");

        using var repeated = new StudioSceneRender(terrain.Copy());
        Require(first.AsSpan().SequenceEqual(repeated.Render(Width, Height, 0)), "The same terrain seed produced different pixels.");

        var recolor = terrain.Copy();
        recolor.Terrain.LowColor = recolor.Terrain.HighColor = recolor.Terrain.SnowColor = cyan;
        var changed = renderer.CopyFrame(Width, Height, 0, recolor);
        var changedArea = CheckAlpha(changed, Width, Height, "recolored terrain");
        Require(firstArea == changedArea, "Changing only the terrain colors moved its silhouette.");
        var originalColor = DominantColorCounts(first);
        var changedColor = DominantColorCounts(changed);
        Require(originalColor.Red > 1000 && changedColor.Cyan > 1000, "The terrain renderer ignored the custom palette.");

        var material = terrain.Copy();
        material.Terrain.Material.Enabled = true;
        material.Terrain.Material.TextureResolution = 128;
        material.Terrain.Material.ApplyAltitudePreset(StudioTerrainAltitudePreset.Alpine);
        var alpine = renderer.CopyFrame(Width, Height, 0, material);
        Require(!first.AsSpan().SequenceEqual(alpine), "The procedural material was not applied.");
        material.Terrain.Material.SnowEnabled = false;
        var noSnow = renderer.CopyFrame(Width, Height, 0, material);
        Require(!alpine.AsSpan().SequenceEqual(noSnow), "The alpine snow control did not change the terrain.");
        material.Terrain.Material.ApplyAltitudePreset(StudioTerrainAltitudePreset.Coastal);
        Require(!alpine.AsSpan().SequenceEqual(renderer.Render(Width, Height, 0, material)),
            "The coastal and alpine altitude presets produced the same material.");
        Console.WriteLine("PASS procedural terrain materials, altitude presets, and independent snow control");

        var reseeded = terrain.Copy();
        reseeded.Terrain.Seed++;
        Require(!first.AsSpan().SequenceEqual(renderer.Render(Width, Height, 0, reseeded)),
            "Changing the terrain seed did not change the rendered surface.");

        var withWater = terrain.Copy();
        withWater.Terrain.ShowWater = true;
        withWater.Terrain.WaterLevel = .58f;
        withWater.Terrain.WaterColor = unchecked((int)0x7034a1dc);
        var water = CheckAlpha(renderer.Render(Width, Height, 0, withWater), Width, Height, "terrain and water");
        Require(water.Translucent > 0, "A raised translucent water surface had no blended coverage.");

        CheckAlpha(renderer.Render(240, 180, 0, terrain), 240, 180, "resized terrain");
        Throws<ArgumentOutOfRangeException>(() => renderer.CopyFrame(4097, 180, 0), "The renderer accepted an oversized viewport.");
        Throws<ArgumentOutOfRangeException>(() => renderer.CopyFrame(0, 180, 0), "The renderer accepted an empty viewport.");
        Require(first.AsSpan().SequenceEqual(renderer.Render(Width, Height, 0, terrain)),
            "Resizing the renderer changed the original frame.");
        var skyScene = StudioSceneFile.Deserialize(StudioSceneFile.Serialize(terrain with
        {
            Sky = new StudioSky
            {
                Enabled = true,
                TopColor = unchecked((int)0xff143e78),
                HorizonColor = unchecked((int)0xffca7168)
            }
        }));
        var sky = renderer.CopyFrame(Width, Height, 0, skyScene);
        Require(sky[0] == skyScene.Sky.TopColor && sky[^1] == skyScene.Sky.HorizonColor &&
            sky.All(pixel => (uint)pixel >> 24 == 255), "The optional sky did not preserve its gradient endpoints or opacity.");
        Require(first.AsSpan().SequenceEqual(renderer.Render(Width, Height, 0, terrain)),
            "Turning off the sky did not restore the transparent background.");
        Console.WriteLine("PASS optional sky gradient and return to transparent rendering");

        if (previewPath is not null)
        {
            const int imageWidth = 960, imageHeight = 640;
            var preview = new StudioScene
            {
                Camera = new StudioCamera { Yaw = 36, Pitch = 43, Zoom = 1.11f },
                Terrain = new StudioTerrain
                {
                    Seed = 173, Grid = 97, Relief = .82f, WaterLevel = .33f,
                    LowColor = unchecked((int)0xff125b6b), HighColor = unchecked((int)0xff7c9a73),
                    SnowColor = unchecked((int)0xffeafcff), WaterColor = unchecked((int)0xab1d9cac)
                }
            };
            var pixels = renderer.CopyFrame(imageWidth, imageHeight, 0, preview);
            CheckAlpha(pixels, imageWidth, imageHeight, "terrain preview asset");
            SavePng(previewPath, pixels, imageWidth, imageHeight);
        }
        Console.WriteLine("PASS terrain seed determinism, camera bounds, custom palette, alpha, water, and resize");
    }

    private static (StudioScene Scene, string ModelPath) CheckSceneFile(string artifactsRoot)
    {
        var fixtureRoot = Path.Combine(artifactsRoot, "studio-scenes-" + Guid.NewGuid().ToString("N"));
        var assetDirectory = Directory.CreateDirectory(Path.Combine(fixtureRoot, "assets")).FullName;
        var exported = Path.Combine(fixtureRoot, "rig-preview" + StudioSceneFile.Extension);
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "rig.glb");
        var modelPath = Path.Combine(assetDirectory, "rig.glb");
        File.Copy(source, modelPath);

        var portable = StudioSceneFile.MakeModelRelative(exported, modelPath);
        Require(portable == "assets/rig.glb", "Export did not use a portable forward-slash asset path.");
        var scene = new StudioScene
        {
            Kind = StudioSceneKind.Model,
            Camera = new StudioCamera { Pitch = 17, Yaw = 8 },
            Terrain = new StudioTerrain { Seed = -19, Grid = 37, WaterColor = unchecked((int)0x7044e5ff) },
            Model = new StudioModel
            {
                Asset = portable, Speed = 1.25f, Scale = 1.13f, OffsetZ = .12f,
                Ik = new StudioIk { TargetX = 1.2f, TargetY = .8f, TargetZ = .2f, PoleZ = 1f, Weight = .5f }
            }
        };

        StudioSceneFile.Save(exported, scene);
        var reopened = StudioSceneFile.Load(exported);
        Require(reopened == scene, "Saving and reopening changed the scene settings or rig controls.");
        Require(StudioSceneFile.Deserialize(StudioSceneFile.Serialize(scene)) == scene,
            "In-memory manifest export changed the scene settings.");
        Require(StudioSceneFile.ResolveModelPath(exported, reopened) == modelPath, "The exported asset path did not resolve to the fixture.");
        Require(StudioSceneFile.ResolveModelPath(exported, new StudioScene()) is null,
            "A terrain-only scene unexpectedly resolved a model.");

        var movedManifest = Path.Combine(fixtureRoot, "relocated", "rig-preview" + StudioSceneFile.Extension);
        var movedAssets = Directory.CreateDirectory(Path.Combine(fixtureRoot, "relocated", "assets")).FullName;
        var movedModel = Path.Combine(movedAssets, "rig.glb");
        File.Copy(modelPath, movedModel);
        File.Copy(exported, movedManifest);
        Require(StudioSceneFile.ResolveModelPath(movedManifest, StudioSceneFile.Load(movedManifest)) == movedModel,
            "Relocating the portable scene kept a dependency on the old path.");

        foreach (var invalidPath in new[] { modelPath, "../rig.glb", "assets/../rig.glb", "assets//rig.glb", "assets/rig.xyz" })
        {
            var invalid = scene.Copy();
            invalid.Model.Asset = invalidPath;
            Throws<InvalidDataException>(() => StudioSceneFile.Serialize(invalid), "Export accepted a nonportable asset path.");
            Throws<InvalidDataException>(() => StudioSceneFile.ResolveModelPath(exported, invalid),
                "Asset resolution accepted a nonportable path.");
        }
        Throws<InvalidDataException>(() => StudioSceneFile.MakeModelRelative(movedManifest, modelPath),
            "Export accepted a file outside the scene directory.");
        Throws<InvalidDataException>(() => StudioSceneFile.Deserialize("{\"version\":99}"),
            "An unsupported manifest version was accepted.");
        Throws<InvalidDataException>(() => StudioSceneFile.Deserialize("{\"unknownSetting\":true}"),
            "An unknown scene property was accepted.");
        Throws<InvalidDataException>(() => StudioSceneFile.Deserialize(new string(' ', 128 * 1024 + 1)),
            "An oversized manifest was accepted.");

        var invalidSettings = scene.Copy();
        invalidSettings.Camera.Zoom = float.NaN;
        Throws<InvalidDataException>(() => StudioSceneFile.Save(exported, invalidSettings),
            "Export accepted a nonfinite camera value.");
        Require(StudioSceneFile.Load(exported) == scene, "Rejecting an invalid save modified the previous scene.");

        var link = Path.Combine(assetDirectory, "linked.glb");
        try
        {
            File.CreateSymbolicLink(link, source);
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("SKIP symbolic-link fixture creation requires developer mode or link privilege.");
        }
        catch (IOException error) when ((error.HResult & 0xffff) == 1314)
        {
            Console.WriteLine("SKIP symbolic-link fixture creation requires developer mode or link privilege.");
        }

        if (File.Exists(link))
        {
            var linked = scene.Copy();
            linked.Model.Asset = "assets/linked.glb";
            Throws<InvalidDataException>(() => StudioSceneFile.ResolveModelPath(exported, linked),
                "Resolution followed a symbolic link out of the scene directory.");
            Throws<InvalidDataException>(() => StudioSceneFile.MakeModelRelative(exported, link),
                "Export accepted a linked model.");
            Console.WriteLine("PASS symbolic-link rejection");
        }

        Console.WriteLine("PASS manifest round trip, relocation, validation, and relative model paths");
        return (reopened, modelPath);
    }

    private static void CheckImportedModel(StudioScene source, string asset)
    {
        var scene = source.Copy();
        scene.Model.Speed = 1;
        scene.Model.Ik = null;
        using var renderer = new StudioSceneRender(scene, asset);
        var names = renderer.Animations.ToList();
        var swing = names.IndexOf("Swing");
        Require(swing >= 0 && names.Contains("Grow"), "The imported GLB is missing its two authored animations.");
        var joints = renderer.Joints.ToList();
        var upper = joints.FindIndex(joint => joint.Name == "Upper");
        var bend = joints.FindIndex(joint => joint.Name == "Lower");
        var tip = joints.FindIndex(joint => joint.Name == "Tip");
        Require(upper >= 0 && bend > upper && tip > bend && joints[bend].Parent == upper && joints[tip].Parent == bend,
            "The imported GLB did not preserve the three-joint chain.");

        var rest = renderer.CopyFrame(Width, Height, 0, scene);
        var area = CheckAlpha(rest, Width, Height, "bind-pose model");
        Require(area.Visible > 30, "The imported fixture was not drawn.");

        var animated = scene.Copy();
        animated.Model.Animation = swing;
        var keyStart = renderer.CopyFrame(Width, Height, 0, animated);
        var midway = renderer.CopyFrame(Width, Height, 1, animated);
        Require(!keyStart.AsSpan().SequenceEqual(midway), "Changing animation time did not move the skinned model.");
        Require(midway.AsSpan().SequenceEqual(renderer.Render(Width, Height, 3, animated)),
            "The renderer did not loop the fixture's two-second animation.");
        animated.Model.Loop = false;
        Require(!midway.AsSpan().SequenceEqual(renderer.Render(Width, Height, 3, animated)),
            "Disabling loop did not hold the final pose.");

        var inverseKinematics = scene.Copy();
        inverseKinematics.Model.Ik = new StudioIk
        {
            Root = upper, Bend = bend, Tip = tip,
            TargetX = .7f, TargetY = 1.15f, TargetZ = .35f,
            PoleY = .25f, PoleZ = 1f, Weight = 1f
        };
        var posed = renderer.CopyFrame(Width, Height, 0, inverseKinematics);
        Require(!rest.AsSpan().SequenceEqual(posed), "The model was unchanged after applying the IK target.");
        inverseKinematics.Model.Ik.Weight = 0;
        Require(rest.AsSpan().SequenceEqual(renderer.Render(Width, Height, 0, inverseKinematics)),
            "A zero-weight chain changed the bind pose.");

        var blended = scene.Copy();
        blended.Kind = StudioSceneKind.TerrainAndModel;
        var both = CheckAlpha(renderer.Render(Width, Height, 0, blended), Width, Height, "terrain and model");
        Require(both.Visible > area.Visible, "The combined scene did not draw the terrain underneath the model.");
        Console.WriteLine("PASS GLB import, transparent model output, animation timing, looping, IK, and combined preview");
    }

    private static void SavePng(string path, int[] pixels, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        var bits = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
        try
        {
            for (var row = 0; row < height; row++)
                Marshal.Copy(pixels, row * width, nint.Add(bits.Scan0, row * bits.Stride), width);
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }
        bitmap.Save(path, ImageFormat.Png);
    }

    private static SurfaceCoverage CheckAlpha(ReadOnlySpan<int> frame, int width, int height, string name)
    {
        Require(frame.Length == width * height, $"Wrong frame dimensions for {name}.");
        var coverage = new SurfaceCoverage(0, 0, width, height, -1, -1);
        for (var index = 0; index < frame.Length; index++)
        {
            var pixel = (uint)frame[index];
            var alpha = pixel >> 24;
            if ((pixel & 255) > alpha || ((pixel >> 8) & 255) > alpha || ((pixel >> 16) & 255) > alpha)
                throw new InvalidOperationException($"The {name} renderer emitted a non-premultiplied pixel.");
            if (alpha == 0)
                continue;

            var x = index % width;
            var y = index / width;
            coverage = new SurfaceCoverage(coverage.Visible + 1,
                coverage.Translucent + (alpha < 255 ? 1 : 0),
                Math.Min(coverage.MinX, x), Math.Min(coverage.MinY, y),
                Math.Max(coverage.MaxX, x), Math.Max(coverage.MaxY, y));
        }
        Require(coverage.Visible > 0 && coverage.Visible < frame.Length,
            $"The {name} renderer did not produce both visible and transparent pixels.");
        return coverage;
    }

    private static (int Red, int Cyan) DominantColorCounts(ReadOnlySpan<int> frame)
    {
        var red = 0;
        var cyan = 0;
        foreach (var pixel in frame)
        {
            var r = (pixel >> 16) & 255;
            var g = (pixel >> 8) & 255;
            var b = pixel & 255;
            if (r > g * 2 && r > b * 2)
                red++;
            if (g > r * 2 && b > r * 2)
                cyan++;
        }
        return (red, cyan);
    }

    private static void Throws<T>(Action operation, string message) where T : Exception
    {
        try
        {
            operation();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly record struct SurfaceCoverage(int Visible, int Translucent, int MinX, int MinY, int MaxX, int MaxY);
}
