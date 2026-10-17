using System.Windows;
using System.Windows.Controls;
namespace BinaryWpf;
public class Widget : UserControl { }
public class DerivedWidget : Widget { }
public static class Contract
{
    public static IEnumerable<FrameworkElement> Elements() => System.Array.Empty<FrameworkElement>();
    public static FrameworkElement[] Array() => System.Array.Empty<FrameworkElement>();
    public class Nested { public List<Window> Windows { get; } = new(); }
}
