using System.Windows;

namespace $ext_safeprojectname$;

public partial class MainWindow : Window
{
    public int ClickCount { get; private set; }

    public MainWindow() => InitializeComponent();

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        ClickCount++;
        Status.Text = "Button clicked " + ClickCount;
    }
}
