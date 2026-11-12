using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing.ThreeD;
using ProjectZ.Shared.Drawing.UI.Advanced;
using ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing;
using Color = Microsoft.Xna.Framework.Color;

internal static class ProviderChecks
{
    internal static void Run()
    {
        var fonts = new FontProvider();
        VectorFont.Provider = fonts;
        try
        {
            var font = new VectorFont("Fixture Shaping", 400, 12);
            Program.Require(font.FamilyName == "Fixture Shaping" && font.Measure("مرحبا").X == 123, "The custom shaping provider was bypassed.");
            var face = fonts.Last!;
            font.Dispose(); font.Dispose();
            Program.Require(face.Disposals == 1, "Font provider ownership is not idempotent.");
            using var fromFile = new VectorFont(Path.Combine(AppContext.BaseDirectory, "app-owned-font.ttf"), 12);
            Program.Require(fonts.FileRequests == 1 && fromFile.Measure("file").X == 123, "Custom font-file loading did not reach the provider.");
        }
        finally { VectorFont.Provider = null; }
        var dictionary = Path.Combine(AppContext.BaseDirectory, "provider-dictionary.txt");
        File.WriteAllText(dictionary, "# Fixture dictionary\n");
        var checker = new OfflineSpellChecker(dictionary);
        checker.Provider = (text, language, token) => Task.FromResult<IReadOnlyList<SpellingIssue>>([new(0, 4, "hélo", ["hello"])]);
        Program.Require(checker.IsAvailable && checker.CheckAsync("hélo", "en").GetAwaiter().GetResult().Count == 1, "Portable spelling did not reach the provider.");
        checker.AddWord("hélo");
        Program.Require(checker.CheckAsync("hélo", "en").GetAwaiter().GetResult().Count == 0, "Application dictionary was not applied to provider results.");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { checker.CheckAsync("hélo", "en", cancellation.Token).GetAwaiter().GetResult(); throw new Exception("Cancelled provider request succeeded."); }
        catch (OperationCanceledException) { }
        checker.Provider = (text, language, token) => Task.FromResult<IReadOnlyList<SpellingIssue>>([new(20, 4, "bad", [])]);
        try { checker.CheckAsync("text", "en").GetAwaiter().GetResult(); throw new Exception("Invalid provider ranges were accepted."); }
        catch (InvalidDataException) { }
        var path = Path.Combine(AppContext.BaseDirectory, "provider-model.obj");
        File.WriteAllText(path, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        var importer = new ModelProvider(); ModelImporter3D.Provider = importer;
        try { Program.Require(ModelImporter3D.Load(path).Meshes.Count == 1 && importer.Called, "Portable model importer was bypassed."); }
        finally { ModelImporter3D.Provider = null; }
        var plan = ProjectZ.Shared.Composition.LinuxDesktopBlurSetup.PlanForDistribution("ubuntu", true)!;
        Program.Require(plan.SessionChangeRequired && plan.Packages.Contains("xserver-xorg-core") && !plan.Arguments.Any(x => x.Contains(';')), "Managed X11 plan is invalid.");
        Program.Require(ProjectZ.Shared.Composition.LinuxDesktopBlurSetup.PlanForDistribution("unknown; command", true) == null, "Unknown distribution produced an executable plan.");
        Console.WriteLine("PLATFORM_PROVIDERS_OK");
    }
    internal static void CheckMedia(Scene scene, GraphicsDevice device)
    {
        using var media = new MediaElement(scene);
        var controller = new MediaController(media); media.PlaybackController = controller;
        var opened = false; media.MediaOpened += () => opened = true;
        media.Open("fixture://movie"); media.Play(); media.Pause(); media.Seek(TimeSpan.FromSeconds(3)); media.Volume = 0.25;
        using var frame = new Texture2D(device, 2, 2); frame.SetData(new[] { Color.Blue, Color.Blue, Color.Blue, Color.Blue });
        var priorRevision = scene.RenderRevision;
        media.PresentFrame(frame, TimeSpan.FromSeconds(3));
        Program.Require(scene.RenderRevision > priorRevision, "A new video frame did not invalidate scene presentation.");
        priorRevision = scene.RenderRevision;
        frame.SetData(new[] { Color.Red, Color.Red, Color.Red, Color.Red });
        media.PresentFrame(frame, TimeSpan.FromSeconds(4));
        Program.Require(scene.RenderRevision > priorRevision, "A reused video texture did not invalidate scene presentation.");
        Program.Require(opened && controller.Opened && controller.Played && controller.Paused && controller.Position == TimeSpan.FromSeconds(3) && controller.Volume == 0.25, "Media control commands did not reach the application decoder.");
        Program.Require(media.PresentedFrameCount == 2 && !media.IsPlaying, "External video frames changed the paused transport or went uncounted.");
        media.Dispose(); Program.Require(controller.Stopped && !frame.IsDisposed, "External decoder/texture ownership failed.");

        using var image = new ImageElement(scene) { Size = new Vector2(2, 2) };
        priorRevision = scene.RenderRevision;
        image.LoadPixels(2, 2, new[] { Color.Blue, Color.Blue, Color.Blue, Color.Blue });
        Program.Require(scene.RenderRevision > priorRevision, "An image reload at a fixed size did not invalidate scene presentation.");
        var owned = image.Texture;
        image.Texture = owned;
        Program.Require(!owned.IsDisposed, "Reassigning the current image texture disposed it.");
        priorRevision = scene.RenderRevision;
        image.Tint = Color.White;
        Program.Require(scene.RenderRevision == priorRevision, "An unchanged image tint scheduled another presentation.");
        image.Tint = Color.Red;
        Program.Require(scene.RenderRevision > priorRevision, "Changing the image tint did not invalidate scene presentation.");
        image.Dispose();
        Program.Require(owned.IsDisposed, "Self-assignment transferred ownership of the image texture.");
    }
    private sealed class ModelProvider : IModelImporter3D
    {
        internal bool Called;
        public Model3DAsset Load(string path) { Called = true; return Model3DAsset.FromSurface(SurfaceMesh3D.CreateCube()); }
    }
    private sealed class FontProvider : IVectorFontProvider
    {
        internal Face? Last; internal int FileRequests;
        public IVectorFontFace? FromFamily(string family, int weight, float points) => family == "Fixture Shaping" ? Last = new Face() : null;
        public IVectorFontFace FromFile(string path, float points) { FileRequests++; return Last = new Face(); }
    }
    private sealed class Face : IVectorFontFace
    {
        internal int Disposals;
        public string FamilyName => "Fixture Shaping";
        public string FilePath => "";
        public int LineSpacing => 18;
        public Vector2 Measure(string text, bool aligned) => new(123, 18);
        public void Draw(GraphicsDevice device, string text, Vector2 position, Color color, Vector2 offset, float scale, bool aligned) { }
        public void Dispose() => Disposals++;
    }
    private sealed class MediaController(MediaElement owner) : IMediaPlaybackController
    {
        internal bool Opened, Played, Paused, Stopped; internal TimeSpan Position; internal double Volume;
        public void Open(string path, int size) { Opened = true; owner.SignalOpened(TimeSpan.FromSeconds(20)); }
        public void Play() => Played = true;
        public void Pause() => Paused = true;
        public void Stop() => Stopped = true;
        public void Seek(TimeSpan position) => Position = position;
        public void SetVolume(double volume) => Volume = volume;
    }
}
