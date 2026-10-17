using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace AsyncApplication;
public partial class App : Application
{
    async void Ready(object sender, StartupEventArgs args)
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await Task.Delay(100);
        MainWindow = new Window { Title = "Async native startup", Content = new TextBlock { Text = "Scene dispatcher continuation" } };
        MainWindow.Show(); await Task.Delay(100); MainWindow.Close();
        await Task.Delay(100); // Explicit shutdown mode must keep the host alive with no windows.
        MainWindow = new Window { Title = "Native shutdown" }; MainWindow.Show();
        MainWindow.Closing += (_, e) => e.Cancel = true;
        Console.WriteLine("ASYNC_APP_READY"); Shutdown(0);
    }
}
