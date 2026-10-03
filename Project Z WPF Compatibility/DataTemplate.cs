using System.Xml.Linq;
namespace ProjectZ.WpfCompatibility;

public sealed class DataTemplate
{
    readonly Func<FrameworkElement> factory;
    public DataTemplate(Func<FrameworkElement> factory) => this.factory = factory;
    internal DataTemplate(XElement root, System.Reflection.Assembly assembly) => factory = () => XamlLoader.LoadTemplate(new XElement(root), assembly);
    public FrameworkElement LoadContent() => factory();
}
