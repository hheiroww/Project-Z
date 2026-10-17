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
            Console.WriteLine("PASS extraction, named optional arguments, local bindings, constructor/property order");
        }
    }
    static class Program { static void Main() => new View().Check(); }
}
