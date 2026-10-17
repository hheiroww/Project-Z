using System.Xml.Linq;
using ProjectZ.WpfCompatibility.Data;
using ProjectZ.WpfCompatibility.Documents;

namespace ProjectZ.WpfCompatibility;

/// <summary>Loads original document declarations into native editable text and hosted elements.</summary>
public static class DocumentMarkup
{
    public static TextElement Load(XElement node, Func<XElement, FrameworkElement>? loadElement = null)
    {
        var type = typeof(TextElement).Assembly.GetType(typeof(TextElement).Namespace + "." + node.Name.LocalName) ?? throw new NotSupportedException("Document type " + node.Name);
        var value = (TextElement)Activator.CreateInstance(type)!;
        foreach (var attribute in node.Attributes().Where(n => !n.IsNamespaceDeclaration && n.Name.NamespaceName.Length == 0))
        {
            var property = type.GetProperty(attribute.Name.LocalName);
            if (property?.CanWrite != true) continue;
            if (attribute.Value.StartsWith('{')) throw new NotSupportedException("Document property expression " + attribute.Value);
            property.SetValue(value, BindingExpression.Coerce(attribute.Value, property.PropertyType));
        }
        foreach (var child in node.Nodes())
        {
            if (child is XText text)
            {
                if (value is Run run) run.Text += text.Value;
                else if (!string.IsNullOrWhiteSpace(text.Value)) Add(new Run(text.Value));
            }
            else if (child is XElement element)
            {
                if (value is BlockUIContainer blockHost) blockHost.Child = loadElement?.Invoke(element);
                else if (value is InlineUIContainer inlineHost) inlineHost.Child = loadElement?.Invoke(element);
                else if (element.Name.LocalName.Contains('.')) foreach (var item in element.Elements()) Add(Load(item, loadElement));
                else Add(Load(element, loadElement));
            }
        }
        return value;
        void Add(TextElement child)
        {
            switch (value)
            {
                case FlowDocument document when child is Block block: document.Blocks.Add(block); break;
                case Paragraph paragraph when child is Inline inline: paragraph.Inlines.Add(inline); break;
                case Span span when child is Inline inline: span.Inlines.Add(inline); break;
                case Section section when child is Block block: section.Blocks.Add(block); break;
                case ListItem item when child is Block block: item.Blocks.Add(block); break;
                case Documents.List list when child is ListItem item: list.ListItems.Add(item); break;
                case Table table when child is TableColumn column: table.Columns.Add(column); break;
                case Table table when child is TableRowGroup group: table.RowGroups.Add(group); break;
                case TableRowGroup group when child is TableRow row: group.Rows.Add(row); break;
                case TableRow row when child is TableCell cell: row.Cells.Add(cell); break;
                case TableCell cell when child is Block block: cell.Blocks.Add(block); break;
                default: throw new InvalidDataException($"{value.GetType().Name} cannot contain {child.GetType().Name}.");
            }
        }
    }
}
