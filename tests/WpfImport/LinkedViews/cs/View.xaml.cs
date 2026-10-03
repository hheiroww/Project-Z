using System.Windows;
namespace LinkedCSharp;
public partial class View : Window {
 public int Calls { get; private set; }
 public View() { InitializeComponent(); }
 private void Run_Click(object sender, RoutedEventArgs e) { Calls++; Status.Text = DesignerLabel + Calls; }
}
