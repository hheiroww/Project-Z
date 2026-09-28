using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using AudioPlugSharp;
using Microsoft.Xna.Framework;
using ProjectZ.Shared.Drawing.UI.Advanced;
using ProjectZ.Shared.Drawing.ThreeD;
using ProjectZ.Vst;

namespace ProjectZ.Vst.SmokeHost;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        VerifyTriangulation();
        VerifyProceduralSurface();
        if (args.Length > 0)
        {
            foreach (var path in args)
            {
                var model = ModelImporter3D.Load(path);
                var triangles = model.Meshes.Sum(mesh => mesh.Indices.Length / 3);
                Console.WriteLine($"MODEL_OK {Path.GetExtension(path)} meshes={model.Meshes.Count} triangles={triangles}");
            }
            return;
        }

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
        if (editor is PluginEditorView inputEditor &&
            string.Equals(Environment.GetEnvironmentVariable("PROJECTZ_INPUT_SMOKE"), "1", StringComparison.Ordinal))
        {
            window.Loaded += async (_, _) => await VerifyEmbeddedInputAsync(window, inputEditor);
        }
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

    private static async Task VerifyEmbeddedInputAsync(Window window, PluginEditorView editor)
    {
        await Task.Delay(750);
        var handle = editor.RenderWindowForTesting;
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("Embedded input smoke test could not resolve the DirectX HWND.");

        var before = editor.PromptTextForTesting;
        if (!GetClientRect(handle, out var clientRect))
            throw new InvalidOperationException("Embedded input smoke test could not read the DirectX client rectangle.");
        var clientWidth = clientRect.Right - clientRect.Left;
        var clientHeight = clientRect.Bottom - clientRect.Top;
        var promptBounds = editor.PromptBoundsForTesting;
        var promptX = (promptBounds.X + promptBounds.Width / 2) * clientWidth / 1040;
        var promptY = (promptBounds.Y + promptBounds.Height / 2) * clientHeight / 700;
        SendPointerMove(handle, promptX, promptY);
        if (!editor.PromptMouseOverForTesting)
            throw new InvalidOperationException("Embedded hover smoke test did not hit the textbox.");
        SendPointerClick(handle, promptX, promptY);
        SendKey(handle, 0x5A); // Z
        SendKey(handle, 0x39); // 9
        await Task.Delay(100);
        var afterTyping = editor.PromptTextForTesting;
        if (afterTyping.Length != before.Length + 2 || !afterTyping.Contains("z9", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Embedded keyboard smoke test failed. Before='{before}', after='{afterTyping}', " +
                $"selected={editor.PromptSelectedForTesting}, hover={editor.PromptMouseOverForTesting}, " +
                $"handled={editor.HandledInputMessageCountForTesting}, interactive={editor.LastInputWasInteractiveForTesting}, " +
                $"client={clientWidth}x{clientHeight}, prompt={promptBounds}, click={promptX},{promptY}.");

        window.WindowState = WindowState.Minimized;
        await Task.Delay(250);
        SendPointerMove(handle, promptX, promptY);
        SendPointerClick(handle, promptX, promptY);
        SendKey(handle, 0x58); // X must be ignored while minimized.
        await Task.Delay(100);
        if (!string.Equals(afterTyping, editor.PromptTextForTesting, StringComparison.Ordinal))
            throw new InvalidOperationException("The minimized editor accepted synthetic mouse/keyboard input.");
        if (editor.PromptMouseOverForTesting)
            throw new InvalidOperationException("The minimized editor retained a synthetic hover target.");

        Console.WriteLine("EMBEDDED_INPUT_OK coordinates=true keyboard=true minimizedHitTest=true");
        window.Close();
    }

    private static void SendPointerClick(IntPtr handle, int x, int y)
    {
        var point = new IntPtr((y << 16) | (x & 0xFFFF));
        SendMessage(handle, 0x0201, new IntPtr(1), point);
        SendMessage(handle, 0x0202, IntPtr.Zero, point);
    }

    private static void SendPointerMove(IntPtr handle, int x, int y)
    {
        var point = new IntPtr((y << 16) | (x & 0xFFFF));
        SendMessage(handle, 0x0200, IntPtr.Zero, point);
    }

    private static void SendKey(IntPtr handle, int virtualKey)
    {
        SendMessage(handle, 0x0100, new IntPtr(virtualKey), IntPtr.Zero);
        SendMessage(handle, 0x0101, new IntPtr(virtualKey), new IntPtr(1L << 30));
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);

    private static void VerifyProceduralSurface()
    {
        var cube = SurfaceMesh3D.CreateCube(2.0f);
        if (cube.Vertices.Length != 8 || cube.Indices.Length != 36)
            throw new InvalidOperationException("XYZW procedural cube smoke test failed.");
        if (cube.Vertices.Any(vertex => Math.Abs(vertex.Position.W - 1.0f) > 0.0001f))
            throw new InvalidOperationException("XYZW procedural cube contains an invalid W component.");
    }

    private static void VerifyTriangulation()
    {
        AssertTriangleCount(
            [new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 100), new Vector2(0, 100)],
            2,
            "convex square");
        AssertTriangleCount(
            [new Vector2(0, 0), new Vector2(100, 0), new Vector2(45, 45), new Vector2(100, 100), new Vector2(0, 100)],
            3,
            "concave arrow");
    }

    private static void AssertTriangleCount(Vector2[] points, int expected, string shape)
    {
        var polygon = new PolygonElement();
        polygon.AddVectorPoints(points);
        polygon.ApplyGeometryChanges();
        var actual = polygon.MeshData?.Triangles.Count ?? 0;
        if (actual != expected)
            throw new InvalidOperationException(
                $"Triangulation smoke test failed for {shape}: expected {expected} triangles, got {actual}.");
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
