using ProjectZ.RuntimeDiagnostics;
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
            if (model.Meshes.Count != 1 || model.Meshes[0].Vertices.Length != 3 || model.Meshes[0].Indices.Length != 3)
                throw new Exception("The optional Assimp importer did not load a real triangle.");
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
        Console.WriteLine("PACKAGE_RUNTIME_OK");
    }
}
