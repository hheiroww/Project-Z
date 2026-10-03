using System.Windows;
using WpfWindow = System.Windows.Window;
namespace ImportSamples;
public partial class Demo : WpfWindow
{
    public Model Model { get; } = new();
    public int Clicks { get; private set; }
    public Demo() { InitializeComponent(); DataContext = Model; }
    private void Save_Click(object sender, RoutedEventArgs e) { Clicks++; Model.Person.Name = "saved " + Clicks; }
}
