using System.IO;
using System.Windows;
using AudioPlugSharp;
using ProjectZ.Vst;

namespace ProjectZ.Vst.SmokeHost;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var capturePath = Path.Combine(AppContext.BaseDirectory, "granulizer-scene.png");
        Environment.SetEnvironmentVariable("PROJECTZ_CAPTURE_SCENE", capturePath);
        Environment.SetEnvironmentVariable("PROJECTZ_EDITOR_PREVIEW", "1");
        var plugin = new ProjectZPlugin();
        SeedEditorParameters(plugin);
        var editor = plugin.GetEditorView();

        var application = new System.Windows.Application();
        var window = new System.Windows.Window
        {
            Title = "Project Z Granulizer - DirectX Scene Smoke Test",
            Width = plugin.EditorWidth,
            Height = plugin.EditorHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = editor
        };

        var testDropPath = Environment.GetEnvironmentVariable("PROJECTZ_TEST_DROP");
        if (editor is PluginEditorView pluginEditor && File.Exists(testDropPath))
            window.Loaded += async (_, _) => await pluginEditor.LoadFilesAsync([testDropPath]);
        if (string.Equals(Environment.GetEnvironmentVariable("PROJECTZ_REOPEN_TEST"), "1", StringComparison.Ordinal))
        {
            window.Loaded += async (_, _) =>
            {
                await Task.Delay(1500);
                window.Content = null;
                await Task.Delay(500);
                var reopenedPlugin = new ProjectZPlugin();
                SeedEditorParameters(reopenedPlugin);
                window.Content = reopenedPlugin.GetEditorView();
            };
        }

        application.Run(window);
    }

    private static void SeedEditorParameters(ProjectZPlugin plugin)
    {
        SetParameter(plugin, "GrainSizeParameter", "grain-size", "Grain Size", 5, 500, 85, "{0:0} ms");
        SetParameter(plugin, "DensityParameter", "density", "Density", 1, 120, 22, "{0:0}/s");
        SetParameter(plugin, "PositionParameter", "position", "Position", 0, 100, 50, "{0:0}%");
        SetParameter(plugin, "SprayParameter", "spray", "Spray", 0, 100, 28, "{0:0}%");
        SetParameter(plugin, "PitchParameter", "pitch", "Pitch", -24, 24, 0, "{0:0.0} st");
        SetParameter(plugin, "TableDepthParameter", "table-depth", "Table Depth", 0, 100, 55, "{0:0}%");
        SetParameter(plugin, "StereoSpreadParameter", "stereo-spread", "Stereo Spread", 0, 100, 65, "{0:0}%");
        SetParameter(plugin, "LevelParameter", "level", "Grain Level", 0, 100, 100, "{0:0}%");
        SetParameter(plugin, "OutputParameter", "output", "Output", -24, 12, 0, "{0:0.0} dB");
        SetParameter(plugin, "HoldParameter", "hold", "Hold Notes", 0, 1, 0, "{0:0}");
        SetParameter(plugin, "MuteParameter", "mute", "Mute", 0, 1, 0, "{0:0}");
    }

    private static void SetParameter(ProjectZPlugin plugin, string propertyName, string id, string name,
        double min, double max, double defaultValue, string format)
    {
        var parameter = new AudioPluginParameter
        {
            ID = id,
            Name = name,
            MinValue = min,
            MaxValue = max,
            DefaultValue = defaultValue,
            ValueFormat = format
        };
        typeof(ProjectZPlugin).GetProperty(propertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(plugin, parameter);
    }
}
