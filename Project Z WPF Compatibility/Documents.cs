using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;

namespace ProjectZ.WpfCompatibility.Documents;

/// <summary>Document content projected into the native input box and its element host.</summary>
public class TextElement : DependencyObject
{
    public object? ToolTip { get; set; }
    public Media.Brush? Foreground { get; set; }
    public Media.Brush? Background { get; set; }
    public Media.FontFamily FontFamily { get; set; } = new("Segoe UI");
    public double FontSize { get; set; } = 12;
    public FontWeight FontWeight { get; set; } = FontWeights.Normal;
    public TextDecorationCollection TextDecorations { get; set; } = new();
    public virtual string GetCopyText() => "";
    public virtual void Copy() { var text = GetCopyText(); if (text.Length > 0) Clipboard.SetText(text); }
}
public class TextElementCollection<T> : ObservableCollection<T> where T : TextElement { public void AddRange(IEnumerable<T> elements) { foreach (var element in elements) Add(element); } }
public class BlockCollection : TextElementCollection<Block> { }
public class InlineCollection : TextElementCollection<Inline>
{
    public void Add(string text) => Add(new Run(text));
}
public class Block : TextElement
{
    public Thickness Margin { get; set; }
    public Thickness Padding { get; set; }
    public Media.Brush? BorderBrush { get; set; }
    public Thickness BorderThickness { get; set; }
    public double LineHeight { get; set; } = double.NaN;
    public TextAlignment TextAlignment { get; set; }
}
public class Inline : TextElement { }
public class Run : Inline
{
    public Run() { } public Run(string text) => Text = text;
    public string Text { get; set; } = "";
    public override string GetCopyText() => Text;
}
public class LineBreak : Inline { public override string GetCopyText() => "\n"; }
public class Span : Inline
{
    public Span() { } public Span(Inline inline) => Inlines.Add(inline);
    public InlineCollection Inlines { get; } = new();
    public override string GetCopyText() => string.Concat(Inlines.Select(i => i.GetCopyText()));
}
public class Bold : Span { public Bold() { FontWeight = FontWeights.Bold; } public Bold(Inline inline) : this() => Inlines.Add(inline); }
public class Italic : Span { public Italic() { } public Italic(Inline inline) : base(inline) { } }
public class Underline : Span { public Underline() { TextDecorations = global::ProjectZ.WpfCompatibility.TextDecorations.Underline; } public Underline(Inline inline) : this() => Inlines.Add(inline); }
public class Hyperlink : Span
{
    public Hyperlink() { } public Hyperlink(Inline inline) : base(inline) { }
    public Uri? NavigateUri { get; set; } public new object? ToolTip { get; set; }
    public event EventHandler<Navigation.RequestNavigateEventArgs>? RequestNavigate;
    public void Navigate() { if (NavigateUri != null) RequestNavigate?.Invoke(this, new(NavigateUri, "")); }
}
public class Paragraph : Block
{
    public Paragraph() { } public Paragraph(Inline inline) => Inlines.Add(inline);
    public InlineCollection Inlines { get; } = new();
    public override string GetCopyText() => string.Concat(Inlines.Select(i => i.GetCopyText()));
}
public class Section : Block
{
    public Section() { } public Section(Block block) => Blocks.Add(block);
    public BlockCollection Blocks { get; } = new();
    public override string GetCopyText() => string.Join("\n", Blocks.Select(b => b.GetCopyText()));
}
public class ListItem : TextElement
{
    public ListItem() { } public ListItem(Block block) => Blocks.Add(block);
    public BlockCollection Blocks { get; } = new();
    public override string GetCopyText() => string.Join("\n", Blocks.Select(b => b.GetCopyText()));
}
public class List : Block
{
    public TextMarkerStyle MarkerStyle { get; set; } = TextMarkerStyle.Disc;
    public TextElementCollection<ListItem> ListItems { get; } = new();
    public override string GetCopyText() => string.Join("\n", ListItems.Select((item, index) => (MarkerStyle == TextMarkerStyle.Decimal ? (index + 1) + ". " : "• ") + item.GetCopyText()));
}
public class Table : Block
{
    public double CellSpacing { get; set; }
    public TableColumnCollection Columns { get; } = new();
    public TableRowGroupCollection RowGroups { get; } = new();
    public override string GetCopyText() => string.Join("\n", RowGroups.Select(group => group.GetCopyText()));
}
public class TableColumn : TextElement { public GridLength Width { get; set; } }
public class TableRowCollection : TextElementCollection<TableRow> { }
public class TableCellCollection : TextElementCollection<TableCell> { }
public class TableColumnCollection : TextElementCollection<TableColumn> { }
public class TableRowGroupCollection : TextElementCollection<TableRowGroup> { }
public class TableRowGroup : TextElement { public TableRowCollection Rows { get; } = new(); public override string GetCopyText() => string.Join("\n", Rows.Select(row => row.GetCopyText())); }
public class TableRow : TextElement { public TableCellCollection Cells { get; } = new(); public override string GetCopyText() => string.Join("\t", Cells.Select(cell => cell.GetCopyText())); }
public class TableCell : Block
{
    public TableCell() { } public TableCell(Block block) => Blocks.Add(block);
    public BlockCollection Blocks { get; } = new();
    public override string GetCopyText() => string.Join("\n", Blocks.Select(block => block.GetCopyText()));
}
public class BlockUIContainer : Block
{
    public BlockUIContainer() { } public BlockUIContainer(UIElement child) => Child = child;
    public UIElement? Child { get; set; }
    public override string GetCopyText() => Child?.GetCopyText() ?? "";
}
public class InlineUIContainer : Inline
{
    public InlineUIContainer() { } public InlineUIContainer(UIElement child) => Child = child;
    public UIElement? Child { get; set; }
    public override string GetCopyText() => Child?.GetCopyText() ?? "";
}
public class FlowDocument : TextElement
{
    public FlowDocument() { } public FlowDocument(Block block) => Blocks.Add(block);
    public BlockCollection Blocks { get; } = new();
    public Thickness PagePadding { get; set; }
    public double ColumnGap { get; set; }
    public double LineHeight { get; set; } = double.NaN;
    public TextAlignment TextAlignment { get; set; }
    public TextPointer ContentStart => new(this, 0);
    public TextPointer ContentEnd => new(this, GetCopyText().Length);
    public override string GetCopyText() => string.Join("\n", Blocks.Select(block => block.GetCopyText()));
}
public class TextPointer(FlowDocument document, int offset)
{
    internal FlowDocument Document { get; } = document;
    internal int Offset { get; } = offset;
    public void InsertTextInRun(string text)
    {
        var paragraph = Document.Blocks.OfType<Paragraph>().LastOrDefault();
        if (paragraph == null) { paragraph = new Paragraph(); Document.Blocks.Add(paragraph); }
        paragraph.Inlines.Add(new Run(text));
    }
}
public class TextRange(TextPointer start, TextPointer end)
{
    public bool IsEmpty=>start.Document==end.Document && start.Offset==end.Offset;
    public string Text => start.Document.GetCopyText()[Math.Clamp(start.Offset, 0, start.Document.GetCopyText().Length)..Math.Clamp(end.Offset, start.Offset, start.Document.GetCopyText().Length)];
}
public class TextSelection(Controls.RichTextBox owner)
{
    public bool IsEmpty => owner.SelectionLength == 0;
    public string Text { get => owner.SelectedText; set => owner.SelectedText = value; }
    public void Select(TextPointer start, TextPointer end) => owner.Select(start.Offset, end.Offset - start.Offset);
}
