using Microsoft.Xna.Framework;
using Microsoft.Win32;
using ProjectZ.Shared.Drawing;
using ProjectZ.Shared.Drawing.Designer;
using ProjectZ.Shared.Drawing.UI.Input;
using ProjectZ.Shared.Drawing.UI.Primitives;
using System.Globalization;
using System.IO;
using System.Reflection;
using PzButton = ProjectZ.Shared.Drawing.UI.Input.Button;
using PzCheckBox = ProjectZ.Shared.Drawing.UI.Input.CheckBox;
using PzPoint = Microsoft.Xna.Framework.Point;
using PzTextbox = ProjectZ.Shared.Drawing.UI.Input.Textbox;

namespace ProjectZ.Vst;

internal sealed class PluginEditorScene : Scene
{
    private const string SceneResourceName = "ProjectZ.Vst.PluginScene.xaml";
    private readonly ProjectZPlugin _plugin;
    private readonly bool _editorPreview = string.Equals(
        Environment.GetEnvironmentVariable("PROJECTZ_EDITOR_PREVIEW"), "1", StringComparison.Ordinal);
    private readonly Dictionary<AudioPlugSharp.AudioPluginParameter, double> _previewValues = [];
    private readonly List<ParameterControl> _controls = [];
    private readonly PzCheckBox _hold;
    private readonly PzCheckBox _mute;
    private readonly PzTextbox _prompt;
    private readonly TextElement _profileName;
    private readonly TextElement _status;
    private string? _previewProfileName;
    private bool _syncing;

    public PluginEditorScene(SceneManager manager, ProjectZPlugin plugin) : base(manager)
    {
        _plugin = plugin;
        var parser = new SceneXamlParser(this);
        var root = parser.Parse(ReadSceneXaml());
        foreach (var child in root.Children)
            AddElement(child);

        Bind(parser, "GrainSize", plugin.GrainSizeParameter, value => $"{value:0} ms");
        Bind(parser, "Density", plugin.DensityParameter, value => $"{value:0}/s");
        Bind(parser, "Position", plugin.PositionParameter, Percent);
        Bind(parser, "Spray", plugin.SprayParameter, Percent);
        Bind(parser, "Pitch", plugin.PitchParameter, value => $"{value:0.0} st");
        Bind(parser, "TableDepth", plugin.TableDepthParameter, Percent);
        Bind(parser, "Stereo", plugin.StereoSpreadParameter, Percent);
        Bind(parser, "Level", plugin.LevelParameter, Percent);
        Bind(parser, "Output", plugin.OutputParameter, value => $"{value:0.0} dB");

        _hold = Require<PzCheckBox>(parser, "HoldCheckBox");
        _mute = Require<PzCheckBox>(parser, "MuteCheckBox");
        _prompt = Require<PzTextbox>(parser, "PromptTextBox");
        _profileName = Require<TextElement>(parser, "ProfileNameText");
        _status = Require<TextElement>(parser, "StatusText");
        _hold.CheckedChanged += (_, value) => { if (!_syncing) WriteValue(plugin.HoldParameter, value == true ? 1 : 0); };
        _mute.CheckedChanged += (_, value) => { if (!_syncing) WriteValue(plugin.MuteParameter, value == true ? 1 : 0); };
        Require<PzButton>(parser, "ResetButton").MouseLeftClick += OnReset;
        Require<PzButton>(parser, "LoadAudioButton").MouseLeftClick += OnLoadAudio;
        Require<PzButton>(parser, "LoadImageButton").MouseLeftClick += OnLoadImage;
        Require<PzButton>(parser, "LoadTextButton").MouseLeftClick += OnLoadText;
        Require<PzButton>(parser, "GenerateButton").MouseLeftClick += OnGenerate;
        Synchronize(force: true);
    }

    public override void Tick(GameTime gameTime)
    {
        Synchronize(force: false);
        base.Tick(gameTime);
    }

    public async Task ImportDroppedFilesAsync(IReadOnlyList<string> paths)
    {
        var files = paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Length == 0)
        {
            _status.Text = "Drop WAV, MP3, an image, text, or any file onto the editor";
            return;
        }

        var loaded = 0;
        foreach (var path in files)
        {
            try
            {
                _status.Text = $"Loading dropped file {loaded + 1}/{files.Length}: {Path.GetFileName(path)}...";
                var profile = await _plugin.ImportFileAsync(path, CancellationToken.None);
                loaded++;
                ApplyProfile(profile, $"Dropped {Path.GetFileName(path)}");
                AudioPlugSharp.Logger.Log($"Project Z loaded dropped file '{path}'.");
            }
            catch (Exception ex)
            {
                _status.Text = $"Could not load {Path.GetFileName(path)}: {ex.Message}";
                AudioPlugSharp.Logger.Log($"Project Z could not load dropped file '{path}': {ex}");
            }
        }

        if (loaded > 1)
            _status.Text = $"Loaded {loaded} dropped files; {Path.GetFileName(files[^1])} is active";
    }

    private void Bind(SceneXamlParser parser, string prefix, AudioPlugSharp.AudioPluginParameter parameter, Func<double, string> format)
    {
        var slider = Require<Trackbar>(parser, prefix + "Slider");
        var text = Require<TextElement>(parser, prefix + "Value");
        slider.FixedInterval = false;
        slider.ValueChanged += value =>
        {
            if (!_syncing)
                WriteValue(parameter, value);
            text.Text = format(value);
        };
        _controls.Add(new ParameterControl(slider, text, parameter, format));
    }

    private async void OnLoadAudio(PzPoint _) => await ImportAsync(
        "Load sound", "Audio|*.wav;*.mp3;*.aiff;*.aif;*.flac;*.wma|All files|*.*", _plugin.ImportAudioAsync);

    private async void OnLoadImage(PzPoint _) => await ImportAsync(
        "Load picture", "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*", _plugin.ImportImageAsync);

    private async void OnLoadText(PzPoint _) => await ImportAsync(
        "Load text", "Text|*.txt;*.md;*.json;*.xml;*.csv|All files|*.*", _plugin.ImportTextAsync);

    private async Task ImportAsync(string title, string filter, Func<string, CancellationToken, Task<GranularProfile>> importer)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        if (dialog.ShowDialog() != true)
            return;
        try
        {
            _status.Text = "Building profile...";
            var profile = await importer(dialog.FileName, CancellationToken.None);
            ApplyProfile(profile, $"Loaded {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            _status.Text = "Import failed: " + ex.Message;
        }
    }

    private async void OnGenerate(PzPoint _)
    {
        try
        {
            _status.Text = "SocketJack LlmRuntime is designing the profile...";
            var result = await _plugin.GenerateProfileAsync(_prompt.Text);
            ApplyProfile(result.Profile, result.Status);
        }
        catch (Exception ex)
        {
            _status.Text = "Generation failed: " + ex.Message;
        }
    }

    private void ApplyProfile(GranularProfile profile, string status)
    {
        if (_editorPreview)
        {
            WriteValue(_plugin.GrainSizeParameter, profile.GrainSizeMs);
            WriteValue(_plugin.DensityParameter, profile.Density);
            WriteValue(_plugin.PositionParameter, profile.Position);
            WriteValue(_plugin.SprayParameter, profile.Spray);
            WriteValue(_plugin.PitchParameter, profile.PitchSemitones);
            WriteValue(_plugin.TableDepthParameter, profile.TableDepth);
            WriteValue(_plugin.StereoSpreadParameter, profile.StereoSpread);
            WriteValue(_plugin.LevelParameter, profile.Mix);
            WriteValue(_plugin.OutputParameter, profile.OutputDb);
            _previewProfileName = profile.Name;
        }
        else
        {
            _plugin.ApplyProfile(profile);
        }
        _profileName.Text = profile.Name;
        _status.Text = status;
        Synchronize(force: true);
    }

    private void OnReset(PzPoint _)
    {
        foreach (var control in _controls)
            WriteValue(control.Parameter, control.Parameter.DefaultValue);
        WriteValue(_plugin.HoldParameter, 0);
        WriteValue(_plugin.MuteParameter, 0);
        Synchronize(force: true);
        _status.Text = "Controls reset; current source profile retained";
    }

    private void Synchronize(bool force)
    {
        _syncing = true;
        try
        {
            foreach (var control in _controls)
            {
                var value = ReadValue(control.Parameter);
                if (force || Math.Abs(control.Slider.Value - value) > 0.001)
                    control.Slider.Value = value;
                control.ValueText.Text = control.Format(value);
            }
            var hold = ReadValue(_plugin.HoldParameter) >= 0.5;
            var mute = ReadValue(_plugin.MuteParameter) >= 0.5;
            if (force || _hold.IsChecked != hold) _hold.IsChecked = hold;
            if (force || _mute.IsChecked != mute) _mute.IsChecked = mute;
            _profileName.Text = _previewProfileName ?? _plugin.ProfileName;
        }
        finally { _syncing = false; }
    }

    private double ReadValue(AudioPlugSharp.AudioPluginParameter parameter)
    {
        if (!_editorPreview)
            return parameter.EditValue;
        if (_previewValues.TryGetValue(parameter, out var value))
            return value;
        value = parameter.DefaultValue;
        _previewValues[parameter] = value;
        return value;
    }

    private void WriteValue(AudioPlugSharp.AudioPluginParameter parameter, double value)
    {
        if (_editorPreview)
            _previewValues[parameter] = Math.Clamp(value, parameter.MinValue, parameter.MaxValue);
        else
            parameter.EditValue = value;
    }

    private static string Percent(double value) => value.ToString("0'%'", CultureInfo.InvariantCulture);

    private static T Require<T>(SceneXamlParser parser, string name) where T : global::ProjectZ.Shared.Drawing.UI.SceneElement =>
        parser.FindName<T>(name) ?? throw new InvalidOperationException($"PluginScene.xaml is missing '{name}' or it has the wrong type.");

    private static string ReadSceneXaml()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SceneResourceName)
            ?? throw new InvalidOperationException($"Embedded scene resource '{SceneResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record ParameterControl(Trackbar Slider, TextElement ValueText, AudioPlugSharp.AudioPluginParameter Parameter, Func<double, string> Format);
}
