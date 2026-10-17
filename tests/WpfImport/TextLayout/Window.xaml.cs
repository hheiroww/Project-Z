using System.Windows;
using System.Windows.Media;
namespace TextLayout;
public partial class ProbeWindow : Window
{
    public ProbeWindow() { InitializeComponent(); }
    public void Change() { Wrapped.TextWrapping = TextWrapping.NoWrap; Wrapped.FontSize = 18; Trimmed.TextTrimming = TextTrimming.CharacterEllipsis; }
    public void ChangeTypeface() { Typeface.FontFamily = new FontFamily("Segoe UI"); Typeface.FontWeight = FontWeights.Normal; }
}
