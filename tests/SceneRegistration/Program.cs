using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using ProjectZ.Shared.Content;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI;
using ProjectZ.Shared.Drawing.UI.Primitives;

internal static class Program {
    static int checks;
    static void Check(bool value,string name) { if (!value) throw new InvalidOperationException(name); checks++; Console.WriteLine("PASS " + name); }
    static void Flush(Scene scene,string name) => typeof(Scene).GetMethod(name,BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene,null);
    static void Attach(SceneElement element,Scene scene) { typeof(SceneElement).GetField("Scene",BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(element,scene); element.GUID = Guid.NewGuid().ToString(); }
    [STAThread] static int Main() {
        try { return Run(); }
        catch (Exception failure) { Console.Error.WriteLine("FAIL " + failure.Message); return 1; }
    }
    static int Run() {
        var scene = new ProbeScene { EnableDesignTimeControls = false };
        var parent = new ProbeElement(); Attach(parent,scene); scene.AddElement(parent);
        var child = new ProbeElement(); Attach(child,scene); parent.Children.Add(child);
        var grandchild = new ProbeElement(); Attach(grandchild,scene); child.Children.Add(grandchild);
        Flush(scene,"CheckForAddedChildren");
        Check(scene.ContainsElement(parent) && scene.ContainsElement(child) && scene.ContainsElement(grandchild),"children and grandchildren appended to a queued parent register in its first frame");
        scene.AddElement(parent); scene.AddElement(child); Flush(scene,"CheckForAddedChildren");
        Check(scene.Elements.Count == 3,"repeated tree registration does not duplicate descendants");
        var detached = new ProbeElement(); Attach(detached,scene);
        var detachedChild = new ProbeElement(); Attach(detachedChild,scene); detached.Children.Add(detachedChild); Flush(scene,"CheckForAddedChildren");
        Check(!scene.ContainsElement(detachedChild),"children of a detached parent do not become independent draw entries");
        scene.AddElement(detached); Flush(scene,"CheckForAddedChildren");
        Check(scene.ContainsElement(detachedChild),"attaching a populated detached tree registers its existing descendants");
        scene.RemoveElement(parent);
        var late = new ProbeElement(); Attach(late,scene); parent.Children.Add(late);
        Flush(scene,"CheckForAddedChildren"); Flush(scene,"CheckForRemovedChildren");
        Check(!scene.ContainsElement(parent) && !scene.ContainsElement(late),"a parent queued for removal cannot leak newly added children into the draw list");
        scene.AddElement(parent); Flush(scene,"CheckForAddedChildren");
        Check(scene.ContainsElement(parent) && scene.ContainsElement(child) && scene.ContainsElement(late),"reattaching registers the complete current tree");
        var unowned = new ProbeElement(); unowned.Children.Add(new ProbeElement());
        Check(unowned.Children.Count == 1,"building a scene-free element tree is null-safe");

        // Only the managed Game service bag is needed for font measurement.
        // Do not construct Game, a window, SpriteBatch or GraphicsDevice.
        var fakeGame = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game)); GC.SuppressFinalize(fakeGame);
        var services = new GameServiceContainer();
        typeof(Game).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(field => field.FieldType == typeof(GameServiceContainer)).SetValue(fakeGame,services);
        using var content = new NoAssetContent(services);
        var resources = new ContentContainer(content,null,Path.Combine(Path.GetTempPath(),"unused-vector-font-root"));
        services.AddService(typeof(ContentContainer),resources);
        typeof(Scene).GetField("_sender",BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scene,fakeGame);
        try {
            Check(resources.TryGetVectorFont("SegoeUI_20",out var font),"real vector font metrics resolve without a graphics device");
            var text = new TextElement(scene,false);
            Attach(text,scene); text.FontScale = 1; text.PpiPerfectText = true; text.MaxSize = new Vector2(float.MaxValue); text.Padding = new Thickness(0);
            text.Font = "SegoeUI_20";
            const string label = "Node label"; float ink = font.Measure(label,true).Y;
            text.BlockLineHeight = true; text.LineHeight = font.LineSpacing + 12;
            Check(ink > 0 && ink < text.LineHeight,"fixture has a measured first line shorter than explicit baseline spacing");
            text.Size = new Vector2(500,ink);
            string Fit(string value) => (string)typeof(TextElement).GetMethod("FitXamlText",BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(text,[value])!;
            Check(Fit(label) == label,"one-line XAML text survives when its ink fits but line spacing does not");
            text.Size = new Vector2(500,Math.Max(0,ink - 1)); Check(Fit(label) == "","a label whose ink does not fit remains clipped");
            text.Size = new Vector2(500,ink + text.LineHeight); Check(Fit(label + "\n" + label) == label + "\n" + label,"two lines use measured height plus one explicit baseline interval");
            text.Padding = new Thickness(4); text.Size = new Vector2(500,ink + 8); Check(Fit(label) == label,"arranged padding is subtracted exactly once from the fitting box");
            text.UseXamlTextLayout = true; text.Size = new Vector2(300,40);
            typeof(TextElement).GetField("_Text",BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(text,"");
            typeof(TextElement).GetMethod("Draw",BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(text,[new GameTime()]);
            Check(text.Size == new Vector2(300,40),"the actual first render's font refresh preserves an arranged XAML box including padding");
            text.PpiPerfectText = false;
            typeof(TextElement).GetMethod("Draw",BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(text,[new GameTime()]);
            Check(text.Size == new Vector2(300,40),"changing pixel alignment preserves the same arranged XAML box");
            Check(content.Loads == 0,"font probes never request a compiled texture/font or graphics device");
        } finally { foreach (var font in resources.VectorFonts.Values) font.Dispose(); }
        Console.WriteLine($"PASS {checks} generic registration/text checks; no window, Game constructor or GraphicsDevice created.");
        return 0;
    }
    sealed class ProbeScene : Scene { }
    sealed class ProbeElement : SceneElement { protected override void Draw(GameTime time) { } }
    sealed class NoAssetContent(IServiceProvider services) : ContentManager(services) {
        public int Loads { get; private set; }
        public override T Load<T>(string assetName) { Loads++; throw new InvalidOperationException("Unexpected asset load."); }
    }
}
