namespace ProjectZ.Vst;

public partial class PluginEditorView : System.Windows.Controls.UserControl
{
    private readonly ProjectZDirectXHost _directXHost;

    public PluginEditorView(ProjectZPlugin plugin)
    {
        InitializeComponent();
        Focusable = true;
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
        PreviewMouseDown += (_, _) => _directXHost.ActivateInput();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
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

    internal IntPtr RenderWindowForTesting => _directXHost.RenderWindowForTesting;
    internal string PromptTextForTesting => _directXHost.PromptTextForTesting;
    internal bool PromptSelectedForTesting => _directXHost.PromptSelectedForTesting;
    internal bool PromptMouseOverForTesting => _directXHost.PromptMouseOverForTesting;
    internal Microsoft.Xna.Framework.Rectangle PromptBoundsForTesting => _directXHost.PromptBoundsForTesting;
    internal long HandledInputMessageCountForTesting => _directXHost.HandledInputMessageCountForTesting;
    internal bool LastInputWasInteractiveForTesting => _directXHost.LastInputWasInteractiveForTesting;

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        var virtualKey = System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0)
            return;
        _directXHost.ForwardKeyDown(virtualKey, e.IsRepeat);
        e.Handled = true;
    }

    private void OnPreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        var virtualKey = System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0)
            return;
        _directXHost.ForwardKeyUp(virtualKey);
        e.Handled = true;
    }
}
