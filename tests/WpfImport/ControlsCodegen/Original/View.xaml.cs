using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace ControlsCodegen;
public partial class View : UserControl
{
    public int Actions;
    public View() { InitializeComponent(); Editor.AppendText("Editable native text"); BlankTabs.Items.Add(new TabItem { Header = "Codebehind blank", IsSelected = true }); Clipped.SetValue(UIElement.OpacityProperty, 1d); }
    void OnAction(object sender, RoutedEventArgs args) { Actions++; Action.FontSize = 21; }
    public void SetDocument() { Editor.Document = new FlowDocument(new Paragraph(new Run("Document text"))); Editor.Document.Blocks.Add(new BlockUIContainer(new TextBlock { Text = "Embedded vector element", FontSize = 15 })); }
    public object OpenPicker() => new Microsoft.Win32.OpenFileDialog { Title = "Generated picker", Filter = "Text|*.txt", Multiselect = true };
    public object SavePicker() => new Microsoft.Win32.SaveFileDialog { DefaultExt = "txt", OverwritePrompt = true };
    public object FolderPicker() => new Microsoft.Win32.OpenFolderDialog { Title = "Generated folder picker" };
    public object NativeScale() => new ScaleTransform(1.25, 1.25);
    public void ResizeRow() => MainRow.Height = new GridLength(1, GridUnitType.Star);
    public void LoadImage(string path) { var image = new System.Windows.Media.Imaging.BitmapImage(); image.BeginInit(); image.UriSource = new System.Uri(path); image.EndInit(); Picture.Source = image; }
    public string ClipboardText() { System.Windows.IDataObject? value = Clipboard.GetDataObject(); return value?.GetData(DataFormats.UnicodeText) as string ?? ""; }
}
