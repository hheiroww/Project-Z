using System.Windows;
namespace GridImport;
public partial class ProbeWindow : Window
{
    public ProbeWindow() { InitializeComponent(); }
    public void Constrain() { Layout.ColumnDefinitions[0].MinWidth = 200; }
    public void Limit() { Layout.ColumnDefinitions[0].MinWidth = 0; Layout.ColumnDefinitions[0].MaxWidth = 50; }
    public void Zero() { Layout.ColumnDefinitions[0].Width = new GridLength(0); }
}
