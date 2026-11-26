using System.Runtime.InteropServices;
using ProjectZ.StudioScenes;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

internal static class StudioUiSmoke
{
    public static void Run(string screenshot)
    {
        CheckCanvas();
        using var dialog = new StudioSceneDialog(new StudioScene());
        using var driver = new System.Windows.Forms.Timer { Interval = 180 };
        var stage = 0;
        var startup = System.Diagnostics.Stopwatch.StartNew();
        Exception? failure = null;
        driver.Tick += (_, _) =>
        {
            try
            {
                Check(dialog.Controls.Count == 1, "The editor created visible native widgets.");
                var surface = dialog.Controls[0];
                if (stage == 0 && !HasPresentedCanvas(surface))
                {
                    Check(startup.Elapsed < TimeSpan.FromSeconds(5), "The dialog never presented its first Project-Z frame.");
                    return;
                }
                switch (stage++)
                {
                    case 0:
                        var before = dialog.Scene.Camera.Yaw;
                        Drag(surface, 180, 220, 216, 230);
                        Check(dialog.Scene.Camera.Yaw != before, "The Project-Z preview did not receive orbit input.");
                        var width = Math.Clamp((int)(surface.Width * .388), 316, 430);
                        var x = surface.Width - 20 - width;
                        var tabWidth = (width - 25) / 6;
                        Click(surface, x + tabWidth + 5 + tabWidth / 2, 114);
                        break;
                    case 1:
                        Click(surface, surface.Width - 80, 200);
                        break;
                    case 2:
                        Click(surface, surface.Width - 80, 331);
                        Check(dialog.Scene.Terrain.Grid == 129 && dialog.Scene.Terrain.Material.TextureResolution == 512,
                            "The custom terrain menu did not apply its High quality preset.");
                        break;
                    case 3:
                        TypeNumber(surface, 237, "-288");
                        Check(dialog.Scene.Terrain.Seed == -288, "Project-Z numeric editing did not commit the typed seed.");
                        break;
                    case 4:
                        Click(surface, surface.Width - 80, 465);
                        Check(dialog.Scene.Terrain.Endless, "The terrain pane did not enable endless terrain.");
                        Click(surface, surface.Width - 47, 114);
                        break;
                    case 5:
                        Click(surface, surface.Width - 80, 162);
                        break;
                    case 6:
                        Click(surface, surface.Width - 80, 292);
                        Check(dialog.Scene.Motion.Mode == StudioCameraMode.Keyframes, "The Path pane did not enter keyframe authoring.");
                        break;
                    case 7:
                        TypeNumber(surface, 237, "2.26");
                        break;
                    case 8:
                        Click(surface, surface.Width - 80, 350);
                        Check(dialog.Scene.Motion.Keyframes.Count == 1 && dialog.Scene.Motion.Keyframes[0].Beat == 2,
                            "The Path pane did not capture the first key at the snapped beat.");
                        break;
                    case 9:
                        Click(surface, surface.Width - 80, 313);
                        break;
                    case 10:
                        TypeNumber(surface, 237, "6.6");
                        Drag(surface, 186, 231, 289, 242);
                        break;
                    case 11:
                        Click(surface, surface.Width - 80, 350);
                        var keys = dialog.Scene.Motion.Keyframes;
                        Check(keys.Count == 2 && keys[1].Beat == 7 &&
                            Math.Abs(keys[1].Camera.Yaw - keys[0].Camera.Yaw) > 10,
                            "The Path pane did not capture a second distinct camera pose.");
                        Click(surface, surface.Width - 80, 199);
                        break;
                    default:
                        using (var preview = new Bitmap(surface.Width, surface.Height))
                        {
                            surface.DrawToBitmap(preview, new Rectangle(Point.Empty, surface.Size));
                            Check(preview.GetPixel(2, 2).ToArgb() == unchecked((int)0xff0f1219),
                                "The native shell did not present the Project-Z editor frame.");
                            preview.Save(screenshot);
                        }
                        driver.Stop();
                        Click(surface, surface.Width - 77, surface.Height - 27);
                        break;
                }
            }
            catch (Exception error)
            {
                failure = error;
                driver.Stop();
                dialog.DialogResult = DialogResult.Cancel;
            }
        };
        dialog.Shown += (_, _) => driver.Start();
        var result = dialog.ShowDialog();
        if (failure is not null) throw failure;
        Check(result == DialogResult.OK && stage >= 13, "The Project-Z dialog did not apply its edited scene.");
        Console.WriteLine("PASS Project-Z painter, text, texture composition, native shell, orbit, terrain quality and endless toggle, two snapped camera keys, playback and modal Apply");
    }

    private static bool HasPresentedCanvas(Control surface)
    {
        using var sample = new Bitmap(surface.Width, surface.Height);
        surface.DrawToBitmap(sample, new Rectangle(Point.Empty, surface.Size));
        return sample.GetPixel(31, 129).ToArgb() == unchecked((int)0xff121721);
    }

    private static void TypeNumber(Control surface, int y, string text)
    {
        Click(surface, surface.Width - 138, y);
        foreach (var character in text)
            SendMessage(surface.Handle, 0x0102, character, 0);
        var confirm = Message.Create(surface.Handle, 0x0100, (nint)Keys.Enter, 0);
        Check(surface.PreProcessMessage(ref confirm), "The canvas did not accept numeric Enter.");
    }

    private static void CheckCanvas()
    {
        using var owner = new Form { Text = "Project-Z canvas focus fixture", ClientSize = new Size(220, 120) };
        owner.Show();
        owner.Activate();
        Application.DoEvents();
        var foreground = GetForegroundWindow();
        using var canvas = new ProjectZCanvas(painter =>
        {
            painter.Fill(new XnaRectangle(0, 0, painter.Width, painter.Height), new XnaColor(15, 18, 25));
            painter.Panel(new XnaRectangle(5, 5, 48, 42), new XnaColor(20, 70, 120), XnaColor.CornflowerBlue, 8);
            painter.Circle(26, 88, 11, XnaColor.DeepSkyBlue);
            painter.Line(12, 62, 47, 62, 2, XnaColor.LightBlue);
            painter.Text("Project-Z", 72, 19, XnaColor.White, StudioTextStyle.Heading);
            painter.Image(new[] { unchecked((int)0xffff0000) }, 1, 1, new XnaRectangle(129, 62, 64, 33));
        });
        var pixels = canvas.Render(230, 115);
        if (foreground == owner.Handle)
        {
            Check(GetForegroundWindow() == owner.Handle, "The offscreen Project-Z canvas stole foreground from its host.");
            Console.WriteLine("PASS offscreen Project-Z canvas preserved the active host window");
        }
        Check(pixels.Length == 230 * 115 && pixels[0] == unchecked((int)0xff0f1219),
            "The canvas background or offscreen dimensions did not match.");
        Check(pixels[73 * 230 + 150] == unchecked((int)0xffff0000), "The canvas blit changed packed BGRA pixels.");
        var textPixels = 0;
        for (var y = 19; y < 51; y++)
        for (var x = 72; x < 206; x++)
            if ((pixels[y * 230 + x] & 0x00ffffff) == 0x00ffffff) textPixels++;
        Check(textPixels > 15, "Project-Z vector text produced no readable glyph coverage.");
    }

    private static void Click(Control target, int x, int y)
    {
        SendMessage(target.Handle, 0x0201, 1, PointWord(x, y));
        SendMessage(target.Handle, 0x0202, 0, PointWord(x, y));
    }

    private static void Drag(Control target, int x, int y, int toX, int toY)
    {
        SendMessage(target.Handle, 0x0201, 1, PointWord(x, y));
        SendMessage(target.Handle, 0x0200, 1, PointWord(toX, toY));
        SendMessage(target.Handle, 0x0202, 0, PointWord(toX, toY));
    }

    private static nint PointWord(int x, int y) => (y << 16) | (x & 0xffff);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint SendMessage(nint window, uint message, nuint wparam, nint lparam);
}
