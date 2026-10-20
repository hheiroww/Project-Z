namespace Fixture;
sealed class Control(View owner)
{
    public string Text { get; set; } = "";
    public float Width { get; set; }
    public string Font { get; set; } = "";
    public Microsoft.Xna.Framework.Vector2 Size { get; set; }
    public bool Enabled { get; set; } = true;
    public event Action<object>? Click;
    public void Activate() => Click?.Invoke(this);
}
partial class View
{
    Control? attached;
    void Attach(Control control) => attached = control;
    public void Check(string expected)
    {
        int calls = 0;
        var control = CreateHeading(() => calls++);
        if (!ReferenceEquals(attached, control) || control.Width != 240 || control.Text != expected) throw new Exception("Markup was not compiled into the control.");
        if (control.Font != "fixture_22" || control.Size.Y != 30 || !ProjectZNativeXamlFontSizes.Contains(22)) throw new Exception("Declarative appearance or font metadata was lost.");
        control.Activate(); control.Enabled = false; control.Activate();
        if (calls != 1) throw new Exception("Native action gating failed.");
        var objectControl = CreateObject(() => 125);
        if (objectControl.Text != "Object from XAML" || objectControl.Width != 125) throw new Exception("Object template binding failed.");
        ProjectZCreateAdditionalControls((page, create) => { if (page != "Home") throw new Exception("Page metadata lost."); create(); });
        if (attached?.Text != "Added in XAML" || attached.Width != 80) throw new Exception("XAML-only addition was not created.");
        Console.WriteLine("PASS native markup, attach, edit, enabled action");
    }
}
static class Program
{
    static void Main(string[] args) => new View().Check(args.FirstOrDefault() ?? "Native XAML");
}
