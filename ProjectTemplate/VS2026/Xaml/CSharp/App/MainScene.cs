using ProjectZ.Shared.Drawing;
using ProjectZ.WpfCompatibility;
using Application = ProjectZ.WpfCompatibility.Application;

namespace $ext_safeprojectname$;

public sealed class MainScene : Scene
{
    private readonly Application application;
    public MainWindow View { get; }

    public MainScene(SceneManager manager) : base(manager)
    {
        EnableDesignTimeControls = false;
        BackgroundColor = new Microsoft.Xna.Framework.Color(18, 25, 35);
        application = new Application(this);
        View = new MainWindow();
        View.Show();
    }
}
