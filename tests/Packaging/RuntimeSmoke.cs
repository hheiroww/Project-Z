using System.Runtime.InteropServices;
using ProjectZ.RuntimeDiagnostics;
using ProjectZ.ShaderEditor;
using ProjectZ.Shared.Drawing.ThreeD;
using ProjectZ.Shared.Drawing.UI.Primitives;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        var directory = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(directory);
        ApplicationDiagnostics.Initialize(new DiagnosticOptions { DirectoryPath = directory, ShowErrorDialogs = false });
        ApplicationDiagnostics.Report("Package managed logging", new InvalidOperationException("Managed report survived"));
        var log = File.ReadAllText(ApplicationDiagnostics.SessionPath);
        if (!log.Contains("Managed report survived") || log.Contains("Diagnostic initialization failed"))
            throw new Exception("Base managed diagnostics failed.");
        var target = new RectangleElement();
        var source = new RectangleElement { BorderThickness = 7, CornerRadius = 3 };
        target.UpdateElement(source);
        if (target.BorderThickness != 7 || target.CornerRadius != 3)
            throw new Exception("Element property updates depend on the removed networking package.");
        if (Model3DAsset.FromSurface(SurfaceMesh3D.CreateCube()).Meshes.Count != 1)
            throw new Exception("Procedural 3D requires an optional importer.");
        var modelPath = Path.Combine(directory, "triangle.obj");
        File.WriteAllText(modelPath, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        if (args.Contains("--model"))
        {
            var model = await ModelImporter3D.LoadAsync(modelPath);
            if (model.Meshes.Count != 1 || model.Meshes[0].Vertices.Length != 3 || model.Meshes[0].Indices.Length != 3 ||
                model.Rig is not null || model.Meshes[0].Binding is not null)
                throw new Exception("The optional Assimp importer did not load a real triangle.");
            await VerifyRigFixtures(args[Array.IndexOf(args, "--model") + 1]);
        }
        else
        {
            try { ModelImporter3D.Load(modelPath); throw new Exception("Model loading unexpectedly succeeded without the co-package."); }
            catch (InvalidOperationException error) when (error.Message.Contains("ProjectZ.ModelImport NuGet")) { }
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try { await ModelImporter3D.LoadAsync(modelPath, cancellation.Token); throw new Exception("Canceled import was accepted."); }
        catch (OperationCanceledException) { }
        if (args.Contains("--native"))
        {
            foreach (var payload in new[] { "mgruntime.dll", "ProjectZDesktopCapture.dll" })
                NativeLibrary.Free(NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, payload)));

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var compiler = Path.Combine(AppContext.BaseDirectory, "ProjectZ.ShaderCompiler");
            var shader = ShaderGenerator.Generate(ShaderDocument.Create());
            var result = await ShaderCompiler.CompileAsync(shader, ShaderBackend.DirectX12, compiler,
                Path.Combine(directory, "shader"), timeout.Token);
            if (!result.Success)
                throw new InvalidOperationException($"Packaged DX12 shader compilation failed: {result.Diagnostics}");
        }
        Console.WriteLine("PACKAGE_RUNTIME_OK");
    }

    private static async Task VerifyRigFixtures(string directory)
    {
        foreach (var extension in new[] { "fbx", "gltf", "glb", "dae" })
        {
            var model = await ModelImporter3D.LoadAsync(Path.Combine(directory, $"rig.{extension}"));
            var rig = model.Rig ?? throw new InvalidOperationException($"No rig imported from {extension}.");
            var joints = rig.Joints.Select(joint => joint.Name).ToList();
            int upper = joints.IndexOf("Upper"), lower = joints.IndexOf("Lower"), tip = joints.IndexOf("Tip");
            if (upper < 0 || lower < 0 || tip < 0 || rig.Joints[lower].Parent != upper || rig.Joints[tip].Parent != lower)
                throw new InvalidOperationException($"{extension} lost the parent-ordered chain.");

            var mesh = model.Meshes.Single(surface => surface.Binding.Bones.Length > 0);
            var rigid = model.Meshes.Single(surface => surface.Binding.Bones.Length == 0);
            var upperBone = mesh.Binding.Bones.Single(bone => bone.Joint == upper);
            var lowerBone = mesh.Binding.Bones.Single(bone => bone.Joint == lower);
            if (mesh.Binding.Bones.Length != 3 || !upperBone.Weights.Any(weight => Close(weight.Weight, 0.25f)) ||
                !lowerBone.Weights.Any(weight => Close(weight.Weight, 0.75f)) ||
                !Close(lowerBone.Offset.Translation.X, -1) || !Close(rigid.Vertices.Min(v => v.Position.X), 4) ||
                !Close(rigid.BindVertices.Min(v => v.Position.X), 0))
                throw new InvalidOperationException($"{extension} lost skin weights, joint offsets, or the rigid mesh transform.");

            int vertex = Array.FindIndex(mesh.BindVertices, v => Close(v.Position.X, 2) && Close(v.Position.Y, 1));
            var swing = rig.Animations[0];
            if (!swing.Name.Contains("Swing", StringComparison.Ordinal) || Math.Abs(swing.Duration - 2) > 0.001 ||
                swing.Tracks.Length != 2 || vertex < 0)
                throw new InvalidOperationException($"{extension} lost the animated translation or rotation clip.");
            var midpoint = rig.Sample(1, 0, false);
            var position = mesh.DeformVertices(midpoint)[vertex].Position;
            if (!Close(position.X, 1) || !Close(position.Y, 0.5f + MathF.Sqrt(2)) ||
                !Close(rigid.DeformVertices(midpoint).Min(v => v.Position.X), 4))
                throw new InvalidOperationException($"{extension} did not render the hierarchy and skin at the clip midpoint.");

            if (extension != "fbx")
            {
                if (rig.Animations.Count != 2 || !rig.Animations[1].Name.Contains("Grow", StringComparison.Ordinal))
                    throw new InvalidOperationException($"{extension} lost the separate scale clip.");
                var scaled = mesh.DeformVertices(rig.Sample(1, 1, false))[vertex].Position;
                if (!Close(scaled.X, 2) || !Close(scaled.Y, 1.5f))
                    throw new InvalidOperationException($"{extension} did not import the joint scale channel.");
            }
        }
    }

    private static bool Close(float actual, float expected) => MathF.Abs(actual - expected) < 0.001f;
}
