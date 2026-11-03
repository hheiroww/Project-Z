using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZStarter;

public sealed partial class MainScene : Scene
{
    public MainScene(SceneManager manager) : base(manager)
    {
        BackgroundColor = new Color(18, 25, 35);
        EnableDesignTimeControls = false;
        CreateHeading();
        CreateDescription();
    }

    private void Attach(TextElement element) => AddElement(element);
}
