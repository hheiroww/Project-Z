using Native = global::ProjectZ.Shared.Drawing.UI.Input;
using global::ProjectZ.Shared.Drawing.UI;

namespace ProjectZ.WpfCompatibility.Controls;

public class ToolTip : ContentControl
{
    public UIElement? PlacementTarget { get; set; }
    public Primitives.PlacementMode Placement { get; set; }
    public bool StaysOpen { get; set; } = true;
    public ToolTip() : this(new Native.ToolTip(Application.Current.ControlScene)) { }
    internal ToolTip(SceneElement native) : base(native) { }
    public override object? Content { get => ((Native.ToolTip)NativeElement).Content; set => ((Native.ToolTip)NativeElement).Content = value?.ToString() ?? ""; }
    public bool IsOpen { get => NativeElement.isVisible; set { if (value) Show(new(NativeElement.Position.X, NativeElement.Position.Y)); else ((Native.ToolTip)NativeElement).Hide(); } }
    public void Show(Point position) { ((Native.ToolTip)NativeElement).Show(new((float)position.X, (float)position.Y)); Application.Current.ControlScene.AddElement(NativeElement); }
    public override void Dispose() { Application.Current.ControlScene.RemoveElement(NativeElement); base.Dispose(); }
}
