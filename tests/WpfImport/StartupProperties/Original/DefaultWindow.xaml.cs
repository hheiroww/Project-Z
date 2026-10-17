using FormsScreen = System.Windows.Forms.Screen;
using System.Windows.Automation;
namespace StartupProperties;
public partial class DefaultWindow : System.Windows.Window
{
    public int ReloadCount;
    public int Screens => FormsScreen.AllScreens.Length;
    public int WindowProcessId(IntPtr handle) => AutomationElement.FromHandle(handle).Current.ProcessId;
    public DefaultWindow() { InitializeComponent(); CustomWidget.Configure("Custom native content"); NamedGradient.Opacity = 0.7; NamedStop.Color = System.Windows.Media.Colors.Blue; NamedMotion.X = 10; System.Windows.Automation.AutomationProperties.SetName(CustomWidget, "Native widget"); AutomationProperties.SetHelpText(CustomWidget, "Native help"); }
    void Reload(object sender, System.Windows.RoutedEventArgs args) { ReloadCount++; HeaderText.Text = "Reloaded"; }
}
