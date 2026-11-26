using System.Diagnostics;
using System.Numerics;
using ProjectZ.StudioScenes;

internal static class EndlessChunkSmoke
{
    internal static void Run()
    {
        var terrain = new StudioTerrain { Endless = true, Seed = 917, Grid = 49, WaterLevel = .76f };
        var landIndices = TerrainGeometry.CreateIndices(TerrainGeometry.LandGrid(terrain));
        var waterIndices = TerrainGeometry.CreateIndices(TerrainGeometry.WaterGrid(terrain));
        CheckBoundary(terrain, new TerrainRegion(-2, 2, 2), landIndices, waterIndices);
        CheckBoundary(terrain, new TerrainRegion(500002, -499998, 2), landIndices, waterIndices);
        CheckBoundary(terrain, new TerrainRegion(-499998, 500002, 2), landIndices, waterIndices);

        var surface = TerrainWaterVertex.At(500002.5f, -499999.25f, terrain);
        Require(surface == TerrainWaterVertex.At(500002.5f, -499999.25f, terrain),
            "A seeded water sample changed when regenerated.");
        Require(surface != TerrainWaterVertex.At(500006.5f, -499999.25f, terrain),
            "Neighboring water chunks repeat the same local phase pattern.");
        Require(surface != TerrainWaterVertex.At(500002.5f, -499999.25f, terrain with { Seed = terrain.Seed + 1 }),
            "Changing the terrain seed did not alter water ripples.");
        var signal = new StudioSignal(.87f, .35f, .64f, .85f);
        var closeBefore = new Vector3(.75f, 0, .5f);
        var closeAfter = closeBefore;
        var beforeTint = Vector4.One;
        var afterTint = beforeTint;
        TerrainWaterFrame.At(16383.999, signal).Apply(surface, ref closeBefore, ref beforeTint);
        TerrainWaterFrame.At(16384.001, signal).Apply(surface, ref closeAfter, ref afterTint);
        Require(Vector3.Distance(closeBefore, closeAfter) < .001f && Vector4.Distance(beforeTint, afterTint) < .02f,
            "Water motion jumped across the old time wrap.");

        var drySettings = terrain with
        {
            WaterLevel = 0, Frequency = .45f, Ridge = .55f,
            Material = new StudioTerrainMaterial { Enabled = false }
        };
        var highestWater = -.42f * drySettings.Relief * 1.45f + TerrainWaterFrame.MaximumDisplacement;
        var dry = Enumerable.Range(0, 16)
            .Select(index => new TerrainGeometry(drySettings, new TerrainRegion(2 + index * 4, -2, 2), landIndices, waterIndices))
            .FirstOrDefault(chunk => chunk.Land.All(vertex => vertex.Position.Y > highestWater));
        Require(dry is { Water.Length: 0, WaterWaves.Length: 0 },
            "A completely dry chunk still allocated animated water.");

        CheckFrames(terrain, signal);
        Console.WriteLine("PASS seeded water phases, dry-chunk cull, exact height/normal/material/water edges and no high-quality seam");
    }

    private static void CheckBoundary(StudioTerrain terrain, TerrainRegion leftRegion, int[] landIndices, int[] waterIndices)
    {
        var left = new TerrainGeometry(terrain, leftRegion, landIndices, waterIndices);
        var right = new TerrainGeometry(terrain, leftRegion with { X = leftRegion.X + 4 }, landIndices, waterIndices);
        var landGrid = TerrainGeometry.LandGrid(terrain);
        var waterGrid = TerrainGeometry.WaterGrid(terrain);
        Require(ReferenceEquals(left.Indices, right.Indices) && ReferenceEquals(left.WaterIndices, right.WaterIndices),
            "Neighboring chunks made duplicate index buffers.");
        for (var row = 0; row < landGrid; row++)
        {
            var a = left.Land[row * landGrid + landGrid - 1];
            var b = right.Land[row * landGrid];
            Require(a.Position == b.Position && a.Normal == b.Normal, "Terrain edge shape or normals diverged.");
        }
        Require(left.WaterWaves.Length == waterGrid * waterGrid && right.WaterWaves.Length == waterGrid * waterGrid,
            "Water level did not wet both terrain chunks in this fixture.");
        for (var row = 0; row < waterGrid; row++)
        {
            var leftEdge = row * waterGrid + waterGrid - 1;
            var rightEdge = row * waterGrid;
            Require(left.Water[leftEdge].Position == right.Water[rightEdge].Position &&
                left.WaterWaves[leftEdge] == right.WaterWaves[rightEdge],
                "Water waves or geometry diverged at an adjacent world edge.");
        }

        var aMaterial = left.Material!;
        var bMaterial = right.Material!;
        for (var row = 0; row < aMaterial.Height; row++)
        {
            Require(aMaterial.Pixels[row * aMaterial.Width + aMaterial.Width - 1] == bMaterial.Pixels[row * bMaterial.Width],
                "Adjacent terrain generated different boundary texels.");
        }
        for (var sample = 0; sample <= 12; sample++)
        {
            var v = sample / 12f;
            var dx = new Vector2(.22f, .035f);
            var dy = new Vector2(.02f, .07f);
            var a = aMaterial.Sample(new Vector2(1, v), dx, dy);
            var b = bMaterial.Sample(new Vector2(0, v), dx, dy);
            Require(Vector4.Distance(a, b) < .00001f, "Minified terrain sampling exposed a material edge.");
        }
    }

    private static void CheckFrames(StudioTerrain terrain, StudioSignal signal)
    {
        var scene = new StudioScene
        {
            Terrain = terrain,
            Sky = new StudioSky { Enabled = true },
            Motion = new StudioCameraMotion
            {
                Mode = StudioCameraMode.Keyframes, SyncToTrack = true, Loop = false
            }
        };
        scene.Motion.SetKey(new StudioCameraKeyframe { Beat = 0, Camera = scene.Camera });
        scene.Motion.SetKey(new StudioCameraKeyframe { Beat = 4, X = 500000, Z = -500000, Camera = scene.Camera });
        using var renderer = new StudioSceneRender(scene);
        var initial = renderer.CopyFrame(720, 405, 1, signal: signal, clock: new StudioClock(120, 0));
        var far = renderer.CopyFrame(720, 405, 1, signal: signal, clock: new StudioClock(120, 4));
        Require(!initial.AsSpan().SequenceEqual(far), "Distant terrain repeated the origin frame.");
        Require(!far.AsSpan().SequenceEqual(renderer.Render(720, 405, 1.18, signal: signal, clock: new StudioClock(120, 4))),
            "Distant water did not animate at a fixed camera.");
        Require(initial.AsSpan().SequenceEqual(renderer.Render(720, 405, 1, signal: signal, clock: new StudioClock(120, 0))),
            "Returning from a distant scene did not reconstruct the original frame.");

        var watch = Stopwatch.StartNew();
        for (var index = 0; index < 32; index++)
            renderer.Render(720, 405, index / 30d, signal: signal, clock: new StudioClock(120, 0));
        watch.Stop();
        Console.WriteLine($"Stationary 720×405 CPU terrain with animated water averaged {watch.Elapsed.TotalMilliseconds / 32:0.0} ms per frame.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
