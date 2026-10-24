using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using ProjectZ.Shared.Content;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        int checks = 0;
        void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            checks++;
            Console.WriteLine("PASS " + description);
        }

        string missingRoot = Path.Combine(Path.GetTempPath(), "ProjectZ-vector-only-" + Guid.NewGuid().ToString("N"));
        using var content = new NoAssetContent(new GameServiceContainer()) { RootDirectory = missingRoot };
        var resources = new ContentContainer(content, null, missingRoot);
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            Require(VectorFont.AlignToPixelGrid(new Vector2(10.49f, -2.49f)) == new Vector2(10, -2),
                "pixel alignment rounds positive and negative output coordinates");
            var scaledOrigin = VectorFont.AlignToPixelGrid(new Vector2(1.1f, -2.1f), 1.5f) * 1.5f;
            Require(Math.Abs(scaledOrigin.X - MathF.Round(scaledOrigin.X)) < .001f && Math.Abs(scaledOrigin.Y - MathF.Round(scaledOrigin.Y)) < .001f,
                "pixel alignment respects a noninteger pixels-per-unit transform");
            resources.LoadVectorFontsOnly();
            Require(resources.Fonts.Count == 0 && content.LoadCalls == 0,
                "vector-only loading never reads compiled font assets");
            Require(!Directory.Exists(missingRoot) && resources.VectorFonts.Count == 8,
                "default outline fonts load without creating a content directory");
            Require(resources.TryGetVectorFont("SegoeUI_20", out var twenty),
                "legacy 20-point names resolve to the compiled free font");
            Require(twenty.FilePath is null && twenty.FamilyName == "ProjectZ Sans",
                "default text uses compiled vector data with no font file");
            Require(resources.TryGetVectorFont(Fonts.ProjectZSans.get_GetResourceName(20), out var free)
                && free.FilePath is null && free.Measure("AVATAR").Equals(twenty.Measure("AVATAR")),
                "new and compatibility names have identical free outline metrics");
            var size = twenty.Measure("Create your SocketJack account");
            Require(size.X > 0 && size.Y > 0 && twenty.LineSpacing > 0,
                "20-point font supplies nonzero outline layout and caret metrics");
            Require(resources.TryGetVectorFont("SegoeUI_20", out var repeated) && ReferenceEquals(twenty, repeated),
                "measurement and rendering resolve the same cached font instance");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Require(resources.TryGetVectorFont("SegoeUI_20.5", out var fractional)
                && fractional.Measure("Scale").Y > twenty.Measure("Scale").Y,
                "fractional point sizes resolve with culture-independent names");
            Require(!resources.TryGetVectorFont("SegoeUI_20,5", out var invalid) && invalid is null,
                "culture-specific malformed names are not silently interpreted");
            const string emojiText = "Hello 😂 ❤️ 👍🏽 👨‍👩‍👧‍👦";
            var beforeColor = twenty.Measure(emojiText);
            var meshMethod = typeof(VectorFont).GetMethod("Triangles", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var mesh = meshMethod.Invoke(twenty, new object[] { emojiText, false })!;
            var vertexField = mesh.GetType().GetField("Vertices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var apply = mesh.GetType().GetMethod("Apply", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var vertices = (Microsoft.Xna.Framework.Graphics.VertexPositionColor[])vertexField.GetValue(mesh)!;
            bool Yellow(Color c) => c.R > 140 && c.G > 70 && c.B < 90;
            Require(vertices.Any(v => Yellow(v.Color)), "shaped emoji include their COLR/CPAL yellow layers");
            Require(vertices.Any(v => v.Color.R > 180 && v.Color.G < 100 && v.Color.B < 120), "heart emoji retain their red palette layers");
            var palette = vertices.Where(v => Yellow(v.Color)).Select(v => v.Color).ToArray();
            apply.Invoke(mesh, new object[] { Color.Cyan });
            Require(vertices.Any(v => v.Color == Color.Cyan) && vertices.Count(v => Yellow(v.Color)) == palette.Length,
                "foreground color changes ordinary letters without tinting emoji");
            apply.Invoke(mesh, new object[] { Color.White * .5f });
            Require(vertices.All(v => v.Color.A <= 128 && v.Color.R <= v.Color.A && v.Color.G <= v.Color.A && v.Color.B <= v.Color.A),
                "text opacity applies premultiplied alpha to every color layer");
            Require(twenty.Measure(emojiText) == beforeColor, "color rendering preserves existing layout and caret metrics");
            var rtl = "مرحبا 😂 ❤️";
            var rtlMesh = meshMethod.Invoke(twenty, new object[] { rtl, false })!;
            var rtlVertices = (Microsoft.Xna.Framework.Graphics.VertexPositionColor[])vertexField.GetValue(rtlMesh)!;
            Require(rtlVertices.Any(v => Yellow(v.Color)) && rtlVertices.Min(v => v.Position.X) >= -2 && rtlVertices.Max(v => v.Position.X) <= twenty.Measure(rtl).X + 3,
                "mixed-direction text keeps the shaped color glyph positions");

            foreach (string name in new[] { "SegoeUI_0", "SegoeUI_-20", "SegoeUI_NaN", "SegoeUI_Infinity", "SegoeUI_513", "UnknownFont_20" })
                Require(!resources.TryGetVectorFont(name, out invalid) && invalid is null,
                    "invalid or unregistered font name rejects: " + name);
            var unicode = twenty.Measure("heirow — Δ音楽 😀");
            Require(unicode.X > 0 && unicode.Y > 0,
                "Unicode outside the compiled coverage retains host shaping fallback");
            string customFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "consola.ttf");
            resources.LoadVectorFont("CustomOutline", customFile, 16);
            Require(resources.TryGetVectorFont("CustomOutline", out var custom)
                && custom.FilePath.Equals(customFile, StringComparison.OrdinalIgnoreCase)
                && custom.Measure("123abc").X > 0,
                "explicit custom font files preserve their registered identity and metrics");
            Require(!resources.TryGetVectorFont(null!, out invalid) && invalid is null,
                "missing font names fail resolution without dictionary exceptions");
            resources.LoadAllContent();
            Require(resources.Fonts.Count == 0 && content.LoadCalls == 0 && !Directory.Exists(missingRoot),
                "compatibility loading also permits asset-free native text");
            Require(resources.TryGetVectorFont("SegoeUI_20", out repeated) && ReferenceEquals(twenty, repeated),
                "default loading preserves already resolved font instances");
            Console.WriteLine($"PASS {checks} vector resolver checks; no UI or graphics device was created.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            foreach (var font in resources.VectorFonts.Values) font.Dispose();
        }
    }

    private sealed class NoAssetContent(IServiceProvider services) : ContentManager(services)
    {
        public int LoadCalls { get; private set; }
        public override T Load<T>(string assetName)
        {
            LoadCalls++;
            throw new InvalidOperationException("A vector-only path attempted to load a compiled asset: " + assetName);
        }
    }
}
