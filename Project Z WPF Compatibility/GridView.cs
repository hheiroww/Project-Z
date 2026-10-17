using System.Collections.ObjectModel;
using System.Reflection;
using System.Xml.Linq;
using ProjectZ.WpfCompatibility.Data;

namespace ProjectZ.WpfCompatibility.Controls;

public class GridViewColumn : DependencyObject
{
    public object? Header { get; set; }
    public double Width { get; set; } = double.NaN;
    public Binding? DisplayMemberBinding { get; set; }
    public DataTemplate? CellTemplate { get; set; }
    internal XElement? CellMarkup;
    internal string? DisplayMemberExpression;
}
public class GridViewColumnCollection : ObservableCollection<GridViewColumn> { }
public class GridView
{
    internal Assembly? SourceAssembly;
    internal FrameworkElement? ResourceOwner;
    public GridViewColumnCollection Columns { get; } = new();
    public bool AllowsColumnReorder { get; set; } = true;
    internal DataTemplate CreateTemplate(Assembly assembly, bool header = false) => new(CreateRowMarkup(header), assembly, resourceOwner: ResourceOwner);
    public XElement CreateRowMarkup(bool header = false)
    {
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var row = new XElement(ns + "Grid", new XAttribute("Height", "24"));
        var definitions = new XElement(ns + "Grid.ColumnDefinitions"); row.Add(definitions);
        for (var index = 0; index < Columns.Count; index++)
        {
            var column = Columns[index];
            definitions.Add(new XElement(ns + "ColumnDefinition", new XAttribute("Width", double.IsNaN(column.Width) ? "*" : column.Width.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            XElement cell;
            if (!header && column.CellMarkup != null) cell = new(column.CellMarkup);
            else
            {
                cell = new(ns + "TextBlock", new XAttribute("Margin", "4,2"));
                if (header) { cell.SetAttributeValue("Text", column.Header?.ToString() ?? ""); cell.SetAttributeValue("FontWeight", "Bold"); }
                else if (column.DisplayMemberBinding != null) cell.SetAttributeValue("Text", column.DisplayMemberExpression ?? "{Binding " + column.DisplayMemberBinding.Path.Path + "}");
                else cell.SetAttributeValue("Text", "{Binding}");
            }
            cell.SetAttributeValue("Grid.Column", index); row.Add(cell);
        }
        return row;
    }
}
