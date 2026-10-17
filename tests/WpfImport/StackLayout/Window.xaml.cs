using System.Windows;
using System.Windows.Controls;
namespace StackLayout;
public partial class ProbeWindow : Window
{
    public ProbeWindow() { InitializeComponent(); }
    public void Vertical() { Layout.Orientation = Orientation.Vertical; }
}
