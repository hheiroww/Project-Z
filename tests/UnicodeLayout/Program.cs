using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI.Primitives;
using ProjectZ.Windows.Composition;

internal static class Program
{
    [STAThread] private static int Main()
    {
        using var fixture = new Fixture();
        fixture.Run();
        return fixture.Result;
    }
    private sealed class Fixture : Game
    {
        private readonly GraphicsDeviceManager graphics;
        public int Result { get; private set; }
        public Fixture() { graphics = new(this) { PreferredBackBufferWidth = 420, PreferredBackBufferHeight = 240 }; Window.Title = "Project-Z Unicode layout acceptance"; }
        protected override void LoadContent()
        {
            try
            {
                var content = new ContentContainer(Content, GraphicsDevice); content.LoadVectorFontsOnly();
                Services.AddService(typeof(ContentContainer), content);
                Services.AddService(typeof(GraphicsDevice), GraphicsDevice);
                Services.AddService(typeof(SpriteBatch), new SpriteBatch(GraphicsDevice));
                var manager = new SceneManager(this) { UseExternalInput = true };
                using var scene = new TestScene(manager);
                using var text = new ProbeText(scene) { Font = "ProjectZSans_18", TextWrapping = TextWrapping.Wrap, MaxWidth = 1 };
                foreach (var sample in new[] { "😂", "🫱🏿", "👨‍👩‍👧‍👦", "🇺🇸", "1️⃣", "👩🏽‍💻", "e\u0301", "𝛼", "✅", "→", "∑", "│", "└" })
                {
                    text.Text = sample;
                    if (text.Wrapped != sample) throw new Exception("Wrapping split a Unicode grapheme: " + sample);
                }
                text.MaxWidth = 1000; text.UseXamlTextLayout = true; text.TextTrimming = TextTrimming.CharacterEllipsis;
                var source = "A👨‍👩‍👧‍👦Z";
                text.Text = source; text.Size = new Vector2(scene.MeasureText(text.Font, "A…").X + 8, 60);
                var fitted = (string)typeof(TextElement).GetMethod("FitXamlText", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(text, [source])!;
                var prefix = fitted.EndsWith('…') ? fitted[..^1] : fitted;
                if (!fitted.EndsWith('…')) throw new Exception("Ellipsis fixture did not overflow its arranged box.");
                if (!StringInfo.ParseCombiningCharacters(source).Append(source.Length).Contains(prefix.Length)) throw new Exception("Ellipsis cut a joined emoji.");
                if (prefix.EnumerateRunes().Any(r => r.Value == 0xFFFD)) throw new Exception("Ellipsis produced a replacement character.");
                var hwnd = WindowsComposition.ResolveHwnd(this);
                if (hwnd == IntPtr.Zero) throw new Exception("Native HWND was not resolved.");
                var timer = Stopwatch.StartNew();
                for (int n = 0; n < 1000; n++) if (WindowsComposition.ResolveHwnd(this) != hwnd) throw new Exception("Cached native HWND changed.");
                if (timer.ElapsedMilliseconds > 250) throw new Exception("Repeated native HWND lookup blocks input: " + timer.ElapsedMilliseconds + " ms.");
                Console.WriteLine("PASS native grapheme wrapping, ellipsis, README symbols, and 1000 cached HWND lookups in " + timer.ElapsedMilliseconds + " ms.");
            }
            catch (Exception error) { Console.Error.WriteLine(error); Result = 1; }
            finally { Exit(); }
        }
    }
    private sealed class TestScene(SceneManager manager) : Scene(manager);
    private sealed class ProbeText(Scene scene) : TextElement(scene) { public string Wrapped => WrappedText; }
}
