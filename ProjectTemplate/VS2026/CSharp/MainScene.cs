using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI.Primitives;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace $safeprojectname$;

public sealed class MainScene : Scene
{
    public MainScene(SceneManager manager) : base(manager)
    {
        BackgroundColor = new Color(18, 25, 35);
        EnableDesignTimeControls = false;
        AddElement(new TextElement(this)
        {
            Text = "Welcome to Project-Z",
            Font = "SegoeUI_24",
            Position = new Vector2(48, 56),
            Size = new Vector2(820, 60)
        });
        AddElement(new TextElement(this)
        {
            Text = "Renderer: " + GraphicsBackendInfo.Current + " | Edit MainScene.cs to build your UI.",
            Font = "SegoeUI_14",
            Position = new Vector2(48, 130),
            Size = new Vector2(850, 44)
        });
    }
}
