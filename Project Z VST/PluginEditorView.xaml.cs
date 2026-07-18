namespace ProjectZ.Vst;

public partial class PluginEditorView : System.Windows.Controls.UserControl
{
    private readonly ProjectZDirectXHost _directXHost;

    public PluginEditorView(ProjectZPlugin plugin)
    {
        InitializeComponent();
        Width = plugin.EditorWidth;
        Height = plugin.EditorHeight;
        _directXHost = new ProjectZDirectXHost(plugin)
        {
            Width = plugin.EditorWidth,
            Height = plugin.EditorHeight,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = System.Windows.VerticalAlignment.Stretch
        };
        RenderSurface.Children.Add(_directXHost);
        PreviewDragEnter += OnPreviewDragEnter;
        PreviewDragOver += OnPreviewDragEnter;
        PreviewDrop += OnPreviewDrop;
    }

    private static void OnPreviewDragEnter(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnPreviewDrop(object sender, System.Windows.DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths)
            await _directXHost.ImportDroppedFilesAsync(paths);
    }

    public Task LoadFilesAsync(IEnumerable<string> paths) =>
        _directXHost.ImportDroppedFilesAsync(paths.Where(System.IO.File.Exists).ToArray());
}
