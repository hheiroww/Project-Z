namespace Fixture;
sealed class Control(View owner)
{
    public string Text { get; set; } = "";
    public float Width { get; set; }
    public string Font { get; set; } = "";
    public Microsoft.Xna.Framework.Vector2 Size { get; set; }
    public bool Enabled { get; set; } = true;
    public float BackgroundBlur { get; set; }
    public float? DropDownBackgroundBlur { get; set; }
    public ProjectZ.Shared.Drawing.SceneCursor? CursorOverride { get; set; } = ProjectZ.Shared.Drawing.SceneCursor.Hand;
    public bool RigidBody { get; set; }
    public bool RidgidBody { get => RigidBody; set => RigidBody = value; }
    public bool FreeMove { get; set; }
    public bool CollisionEnabled { get; set; }
    public double Mass { get; set; }
    public double Weight { get => Mass; set => Mass = value; }
    public double Bounce { get; set; }
    public double Friction { get; set; }
    public ProjectZ.Shared.Physics.FrictionType FrictionType { get; set; }
    public ProjectZ.Shared.Drawing.UI.PhysicsMotion PhysicsMotion { get; set; }
    public Microsoft.Xna.Framework.Vector2 Gravity { get; set; }
    public Microsoft.Xna.Framework.Vector2 Velocity { get; set; }
    public Microsoft.Xna.Framework.Vector3 Velocity3D { get; set; }
    public Microsoft.Xna.Framework.Vector3 Gravity3D { get; set; }
    public Microsoft.Xna.Framework.Vector3 Position3D { get; set; }
    public Microsoft.Xna.Framework.Vector3 PhysicsSize3D { get; set; }
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
        if (control.BackgroundBlur != 12 || control.DropDownBackgroundBlur != 0) throw new Exception("Popup blur zero must override inherited background blur.");
        var inherited = CreateInheritedBlur(() => { });
        if (inherited.BackgroundBlur != 24 || inherited.DropDownBackgroundBlur != null) throw new Exception("Popup blur null fallback was lost in native XAML.");
        if (control.CursorOverride != ProjectZ.Shared.Drawing.SceneCursor.Arrow || inherited.CursorOverride != null)
            throw new Exception("Native XAML must preserve explicit Arrow and clearing a factory cursor override.");
        var physics = CreatePhysics(() => { });
        if (!physics.RigidBody || !physics.FreeMove || !physics.CollisionEnabled || physics.Weight != 4.5 || physics.Bounce != 0.35 || physics.Friction != 0.2)
            throw new Exception("Compiled markup lost physics flags or double-valued parameters.");
        if (physics.FrictionType != ProjectZ.Shared.Physics.FrictionType.Ice || physics.PhysicsMotion != ProjectZ.Shared.Drawing.UI.PhysicsMotion.RenderTranslation)
            throw new Exception("Compiled markup lost physics enum values.");
        if (physics.Gravity != new Microsoft.Xna.Framework.Vector2(0, 980) || physics.Velocity != new Microsoft.Xna.Framework.Vector2(120, -15) ||
            physics.Velocity3D != new Microsoft.Xna.Framework.Vector3(1, 2, 3) || physics.Gravity3D != new Microsoft.Xna.Framework.Vector3(0, -9.81f, 0) ||
            physics.Position3D != new Microsoft.Xna.Framework.Vector3(-1, 0, 2) || physics.PhysicsSize3D != new Microsoft.Xna.Framework.Vector3(2, 3, 4))
            throw new Exception("Compiled markup lost 2D/3D physics vectors.");
        var alias = CreatePhysicsAlias(() => { });
        if (!alias.RigidBody || alias.Mass != 2 || alias.PhysicsMotion != ProjectZ.Shared.Drawing.UI.PhysicsMotion.Position)
            throw new Exception("Compiled markup lost RidgidBody spelling or physics position mode.");
        control.Activate(); control.Enabled = false; control.Activate();
        if (calls != 1) throw new Exception("Native action gating failed.");
        var objectControl = CreateObject(() => 125);
        if (objectControl.Text != "Object from XAML" || objectControl.Width != 125) throw new Exception("Object template binding failed.");
        ProjectZCreateAdditionalControls((page, create) => { if (page != "Home") throw new Exception("Page metadata lost."); create(); });
        if (attached?.Text != "Added in XAML" || attached.Width != 80) throw new Exception("XAML-only addition was not created.");
        Console.WriteLine("PASS native markup, attach, edit, enabled action, popup blur inheritance and explicit zero, physics flags, aliases, doubles, enums and 2D/3D vectors");
    }
}
static class Program
{
    static void Main(string[] args) => new View().Check(args.FirstOrDefault() ?? "Native XAML");
}
