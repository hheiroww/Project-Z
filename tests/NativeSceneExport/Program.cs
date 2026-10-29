namespace ProjectZ.Shared.Drawing.UI
{
    class SceneElement { }
}
namespace Fixture
{
    class Control : ProjectZ.Shared.Drawing.UI.SceneElement
    {
        public Control(View owner) { owner.Trace.Add("constructor"); }
        public string Text { get; set; } = "";
        public float Width { get; set; }
        public float BackgroundBlur { get; set; }
        public float? DropDownBackgroundBlur { get; set; }
    }
    partial class View
    {
        public List<string> Trace = [];
        Control Label(string text, float width = 42) => new Control(this) { Text = text, Width = width };
        public void Check()
        {
            var first = Label("literal", 24);
            string text = "local";
            var second = Label(text, width: 48);
            float Width() { Trace.Add("property"); return 32; }
            Trace.Clear();
            var third = new Control(this) { Text = "object", Width = Width() };
            if (first.Text != "literal" || first.Width != 24 || second.Text != "local" || second.Width != 48 || third.Text != "object" || third.Width != 32 || string.Join(",", Trace) != "constructor,property") throw new Exception("Scene extraction changed native control semantics.");
            var explicitZero = new Control(this) { Text = "Popup off", BackgroundBlur = 12, DropDownBackgroundBlur = 0 };
            var inherited = new Control(this) { Text = "Inherited popup", BackgroundBlur = 24, DropDownBackgroundBlur = null };
            if (explicitZero.BackgroundBlur != 12 || explicitZero.DropDownBackgroundBlur != 0 || inherited.BackgroundBlur != 24 || inherited.DropDownBackgroundBlur != null) throw new Exception("Native extraction changed popup blur zero/null semantics.");
            Console.WriteLine("PASS extraction, named optional arguments, local bindings, constructor/property order, blur zero/null roundtrip");
        }
        public void CheckPopupAssignments()
        {
            var off = new Control(this);
            off.BackgroundBlur = 12;
            off.DropDownBackgroundBlur = 0;
            var inherited = new Control(this);
            inherited.BackgroundBlur = 24;
            inherited.DropDownBackgroundBlur = null;
            if (off.BackgroundBlur != 12 || off.DropDownBackgroundBlur != 0 || inherited.BackgroundBlur != 24 || inherited.DropDownBackgroundBlur != null) throw new Exception("Standalone blur assignments changed during native appearance extraction.");
            Console.WriteLine("PASS standalone appearance extraction, explicit zero and null resource values");
        }
    }
    static class Program
    {
        static void Main(string[] args)
        {
            var view = new View();
            if (!args.Contains("--appearance-map-only")) view.Check();
            view.CheckPopupAssignments();
        }
    }
}
