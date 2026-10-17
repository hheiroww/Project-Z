namespace UnsupportedSample;
public partial class View : System.Windows.Window
{
    public System.Windows.Media.Media3D.PerspectiveCamera Camera { get; } = new();
    public View() { InitializeComponent(); }
}
