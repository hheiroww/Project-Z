using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Native = ProjectZ.Shared.Drawing.UI.Input;
using Primitive = ProjectZ.Shared.Drawing.UI.Primitives;

namespace ProjectZ.WpfCompatibility.Dialogs;

/// <summary>Project-Z-owned file selection. Windows common dialogs and WPF are not used.</summary>
public abstract class FileDialog
{
    public string Title { get; set; } = "";
    public string InitialDirectory { get; set; } = "";
    public string FileName { get; set; } = "";
    public string[] FileNames { get; internal set; } = [];
    public string SafeFileName => Path.GetFileName(FileName);
    public string[] SafeFileNames => FileNames.Select(Path.GetFileName).ToArray()!;
    public string Filter { get; set; } = "All files|*.*";
    public int FilterIndex { get; set; } = 1;
    public string DefaultExt { get; set; } = "";
    public bool AddExtension { get; set; } = true;
    public bool CheckFileExists { get; set; }
    public bool CheckPathExists { get; set; } = true;
    public bool ValidateNames { get; set; } = true;
    public bool RestoreDirectory { get; set; }
    public virtual bool Multiselect { get; set; }
    public bool? ShowDialog() => ShowDialog(null);
    public bool? ShowDialog(Window? owner)
    {
        var ownerHandle = GetActiveWindow();
        using var window = CreateWindow();
        window.Surface.SetOwner(ownerHandle);
        if (ownerHandle != IntPtr.Zero) EnableWindow(ownerHandle, false);
        try { while (window.Result == null && !window.Surface.IsClosed) { window.Surface.RenderFrame(); Threading.Dispatcher.CurrentDispatcher.Pump(); Thread.Sleep(8); } return window.Result == true; }
        finally { if (ownerHandle != IntPtr.Zero) { EnableWindow(ownerHandle, true); SetForegroundWindow(ownerHandle); } }
    }
    public FileDialogWindow CreateWindow() => new(this);
    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] static extern bool EnableWindow(IntPtr handle, bool enabled);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
}
public sealed class OpenFileDialog : FileDialog { public OpenFileDialog() { CheckFileExists = true; } }
public sealed class SaveFileDialog : FileDialog { public bool OverwritePrompt { get; set; } = true; public bool CreatePrompt { get; set; } }
public sealed class OpenFolderDialog : FileDialog
{
    public string FolderName { get => FileName; set => FileName = value; }
    public string[] FolderNames => FileNames;
    public string SafeFolderName => SafeFileName;
}

public sealed class FileDialogWindow : IDisposable
{
    readonly FileDialog options;
    readonly List<(string Label, string Pattern)> filters = new();
    readonly HashSet<string> selected = new(StringComparer.OrdinalIgnoreCase);
    readonly List<string> paths = new();
    readonly Primitive.TextElement status;
    readonly Native.Button overwrite;
    string[]? pendingOverwrite;
    bool refreshing;
    public NativeWindowSurface Surface { get; }
    public Native.Textbox DirectoryBox { get; }
    public Native.ListBox Entries { get; }
    public Native.Textbox NameBox { get; }
    public Native.ComboBox FilterBox { get; }
    public Native.Button AcceptButton { get; }
    public Native.Button CancelButton { get; }
    public Native.Button UpButton { get; }
    public bool? Result { get; private set; }
    public string CurrentDirectory { get; private set; } = "";
    public IReadOnlyList<string> EntryPaths => paths;
    public string Status => status.Text;
    public FileDialogWindow(FileDialog options)
    {
        this.options = options;
        Surface = new(options.Title.Length > 0 ? options.Title : options is SaveFileDialog ? "Save file" : options is OpenFolderDialog ? "Choose folder" : "Open file", 760, 540);
        var scene = Surface.Scene;
        scene.BackgroundColor = new Color(20, 26, 36);
        DirectoryBox = new(scene) { Position = new(18, 16), Size = new(575, 32), TextPadding = new(6, 5) };
        UpButton = Button("Up", 600, 16, 60);
        var go = Button("Go", 670, 16, 72);
        Entries = new(scene) { Position = new(18, 62), Size = new(724, 338), Clip = true };
        NameBox = new(scene) { Position = new(18, 414), Size = new(510, 32), Text = Path.GetFileName(options.FileName) };
        FilterBox = new(scene) { Position = new(540, 414), Size = new(202, 32) };
        var parts = options.Filter.Split('|');
        for (int i = 0; i + 1 < parts.Length; i += 2) filters.Add((parts[i], parts[i + 1]));
        if (filters.Count == 0) filters.Add(("All files", "*.*"));
        FilterBox.Items = filters.Select(f => f.Label).ToList();
        FilterBox.SelectedIndex = Math.Clamp(options.FilterIndex - 1, 0, filters.Count - 1);
        status = new(scene) { Position = new(18, 455), Font = global::ProjectZ.Shared.Content.ContentContainer.SystemFontName("Segoe UI", 400, 9), UseXamlTextLayout = true, ForegroundColor = new Color(200, 210, 225), MaxSize = new(510, float.MaxValue) };
        DirectoryBox.OnKeyDown += (key, _) => { if (key == Microsoft.Xna.Framework.Input.Keys.Enter) Navigate(DirectoryBox.Text); };
        NameBox.OnKeyDown += (key, _) => { if (key == Microsoft.Xna.Framework.Input.Keys.Enter) Accept(); };
        AcceptButton = Button(options is SaveFileDialog ? "Save" : options is OpenFolderDialog ? "Choose" : "Open", 540, 482, 96);
        CancelButton = Button("Cancel", 648, 482, 94);
        AcceptButton.IsDefault = true; CancelButton.IsCancel = true;
        overwrite = Button("Confirm overwrite", 330, 482, 198); overwrite.isVisible = false;
        foreach (var element in new global::ProjectZ.Shared.Drawing.UI.SceneElement[] { DirectoryBox, UpButton, go, Entries, NameBox, FilterBox, status, AcceptButton, CancelButton, overwrite }) scene.AddElement(element);
        UpButton.MouseLeftClick += _ => { var parent = Directory.GetParent(CurrentDirectory); if (parent != null) Navigate(parent.FullName); };
        go.MouseLeftClick += _ => Navigate(DirectoryBox.Text);
        FilterBox.SelectionChanged += (_, _) => { if (!refreshing) Navigate(CurrentDirectory); };
        Entries.SelectionChanged += (_, index) =>
        {
            if (refreshing || index < 0 || index >= paths.Count) return;
            var path = paths[index];
            if (!options.Multiselect || !Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl)) selected.Clear();
            if (File.Exists(path) || options is OpenFolderDialog) { if (!selected.Add(path)) selected.Remove(path); NameBox.Text = string.Join(" ", selected.Select(p => "\"" + Path.GetFileName(p) + "\"")); }
            else NameBox.Text = Path.GetFileName(path) + Path.DirectorySeparatorChar;
        };
        AcceptButton.MouseLeftClick += _ => Accept(); CancelButton.MouseLeftClick += _ => Cancel(); overwrite.MouseLeftClick += _ => ConfirmOverwrite();
        var initial = options.InitialDirectory;
        if (initial.Length == 0 && Path.IsPathRooted(options.FileName)) initial = Path.GetDirectoryName(options.FileName) ?? "";
        if (!Directory.Exists(initial)) initial = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        Navigate(initial);
        Native.Button Button(string text, float x, float y, float width) => new(scene) { AutoSize = Native.ButtonAutoSize.None, Text = text, Position = new(x, y), Size = new(width, 32) };
    }
    public void Navigate(string directory)
    {
        try
        {
            var absolute = Path.GetFullPath(directory);
            var directories = Directory.EnumerateDirectories(absolute).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            var files = options is OpenFolderDialog ? Enumerable.Empty<string>() : Directory.EnumerateFiles(absolute).Where(MatchesFilter).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            var next = directories.Concat(files).ToList();
            refreshing = true;
            CurrentDirectory = absolute; DirectoryBox.Text = absolute; paths.Clear(); paths.AddRange(next); selected.Clear(); Entries.Items = paths.Select(p => (Directory.Exists(p) ? "[Folder]  " : "") + Path.GetFileName(p)).ToList(); Entries.SelectedIndex = -1; status.Text = $"{paths.Count} entries";
            Surface.Scene.AddElement(Entries);
            pendingOverwrite = null; overwrite.isVisible = false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { status.Text = error.Message; }
        finally { refreshing = false; }
    }
    bool MatchesFilter(string path) => filters[Math.Clamp(FilterBox.SelectedIndex, 0, filters.Count - 1)].Pattern.Split(';').Any(pattern => pattern is "*" or "*.*" || System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, Path.GetFileName(path), true));
    public void Accept()
    {
        try
        {
            if (options is OpenFolderDialog && string.IsNullOrWhiteSpace(NameBox.Text)) { Complete([CurrentDirectory]); return; }
            var names = Regex.Matches(NameBox.Text, "\"([^\"]+)\"|([^\"]+?)(?=\\s*\"|$)").Select(m => (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim()).Where(n => n.Length > 0).ToArray();
            if (names.Length == 0) { status.Text = "Enter a file name or select an entry."; return; }
            if (!options.Multiselect && names.Length != 1) { status.Text = "Choose one file."; return; }
            var chosen = names.Select(name => Path.GetFullPath(Path.Combine(CurrentDirectory, name))).ToArray();
            if (options.ValidateNames && chosen.Any(path => Path.GetFileName(path).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) { status.Text = "The file name contains an invalid character."; return; }
            if (chosen.Length == 1 && Directory.Exists(chosen[0]) && options is not OpenFolderDialog) { NameBox.Text = ""; Navigate(chosen[0]); return; }
            if (options is SaveFileDialog && options.AddExtension && !Path.HasExtension(chosen[0]) && options.DefaultExt.Length > 0) chosen[0] += "." + options.DefaultExt.TrimStart('.');
            if (options.CheckPathExists && chosen.Any(p => !Directory.Exists(Path.GetDirectoryName(p)))) { status.Text = "The destination folder does not exist."; return; }
            if (options is OpenFolderDialog && chosen.Any(p => !Directory.Exists(p)) || options.CheckFileExists && chosen.Any(p => !File.Exists(p))) { status.Text = "The selected entry does not exist."; return; }
            if (options is SaveFileDialog { OverwritePrompt: true } && File.Exists(chosen[0])) { pendingOverwrite = chosen; overwrite.isVisible = true; status.Text = "This file exists. Confirm overwrite or choose another name."; return; }
            Complete(chosen);
        }
        catch (Exception error) when (error is IOException or ArgumentException or NotSupportedException) { status.Text = error.Message; }
    }
    public void ConfirmOverwrite() { if (pendingOverwrite != null) Complete(pendingOverwrite); }
    void Complete(string[] chosen) { options.FileNames = chosen; options.FileName = chosen.FirstOrDefault() ?? ""; options.FilterIndex = FilterBox.SelectedIndex + 1; Result = true; }
    public void Cancel() => Result = false;
    public void Dispose() => Surface.Dispose();
}
