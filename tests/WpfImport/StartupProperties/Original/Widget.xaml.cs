namespace StartupProperties;
public partial class Widget : System.Windows.Controls.UserControl, System.IDisposable
{
    public string Value => Label.Text;
    public Widget() { InitializeComponent(); Rows.Items.Add("Queued during construction"); }
    public void Configure(string value) { Label.Text = value; }
    public int DisposeCalls { get; private set; }
    public void Dispose() { DisposeCalls++; }
}
