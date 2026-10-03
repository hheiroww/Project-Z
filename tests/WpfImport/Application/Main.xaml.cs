using System;
using System.Threading.Tasks;
using System.Windows;
namespace ApplicationSample;
public partial class Main : Window
{
    public Main() { InitializeComponent(); }
    private async void Ready(object sender, RoutedEventArgs e)
    {
        await Task.Delay(300);
        Dispatcher.Invoke(() => { Console.WriteLine("IMPORTED_APP_READY"); Close(); });
    }
}
