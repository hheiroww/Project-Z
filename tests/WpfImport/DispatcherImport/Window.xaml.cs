using System;
using System.Windows;
using System.Windows.Threading;

namespace DispatcherImport;
public partial class ProbeWindow : Window
{
    readonly DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background);
    public int Ticks { get; private set; }
    public int Clicks { get; private set; }
    public ProbeWindow()
    {
        InitializeComponent(); timer.Interval = TimeSpan.Zero;
        timer.Tick += (_, _) => { Ticks++; Status.Text = "tick " + Ticks; }; timer.Start();
        Closed += (_, _) => timer.Stop();
    }
    void Clicked(object sender, RoutedEventArgs args) { Clicks++; }
    public DispatcherOperation<int> GetValue() => Dispatcher.InvokeAsync(() => 42, DispatcherPriority.Render);
    public DispatcherOperation<System.Windows.Thickness> GetThickness() => Dispatcher.InvokeAsync(() => new System.Windows.Thickness(3));
    public bool IsComplete(DispatcherOperation<int> operation) => operation.Task.IsCompleted;
}
