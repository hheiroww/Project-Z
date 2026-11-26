#nullable enable
using System.Diagnostics;
using System.Runtime.InteropServices;
using ProjectZ.WpfCompatibility;
using ProjectZ.WpfCompatibility.Controls;
using NativeTextBlock = ProjectZ.Shared.Drawing.UI.Primitives.TextElement;
using NativeTextWrapping = ProjectZ.Shared.Drawing.UI.Primitives.TextWrapping;

internal static class MessageBoxChecks
{
    internal static void Run(Window owner)
    {
        RunDialog(owner, "Open this sample?", "Short message probe", MessageBoxButton.YesNo,
            MessageBoxResult.No, 13, MessageBoxResult.No, dialog =>
            {
                var panel = (Canvas)dialog.Content!;
                var actions = panel.Children.OfType<Button>().Where(button => (string?)button.Content != "×").ToArray();
                Require(dialog.Height < 160 && dialog.WindowStyle == WindowStyle.None,
                    "short message boxes fit their content and use native custom chrome");
                Require(actions.Select(button => button.Content).SequenceEqual(new object[] { "Yes", "No" }),
                    "confirmation shows its requested actions in order");
                var pixels = dialog.CaptureFrame();
                dialog.SaveFrame(Path.GetFullPath("artifacts/wpf-import/native-message-box.png"));
                int width = (int)dialog.Width;
                Require(pixels[6 * width + 6].B > 20 && pixels[6 * width + 6].R < 45,
                    "the frame renders the dark dialog surface");
                foreach (var action in actions)
                {
                    var label = action.NativeElement.Children.OfType<NativeTextBlock>().Single();
                    var bounds = action.NativeElement.Rectangle;
                    var labelRows = Enumerable.Range(bounds.Top + 4, bounds.Height - 8).Count(y =>
                        Enumerable.Range(bounds.Left + 6, bounds.Width - 12).Any(x =>
                            pixels[y * width + x] is { R: > 170, G: > 170, B: > 170 }));
                    Console.WriteLine($"Modal action {action.Content}: {bounds}, label {label.Rectangle}, {label.TextWrapping}, bright rows {labelRows}");
                    Require(label.TextWrapping == NativeTextWrapping.NoWrap && bounds.Width >= 70 && labelRows is > 0 and < 20,
                        "message action labels render on a single line");
                }
            });

        string longMessage = string.Join('\n', Enumerable.Repeat("A longer localized message can contain many paragraphs.", 30));
        RunDialog(owner, longMessage, "Long message probe", MessageBoxButton.OKCancel,
            MessageBoxResult.OK, 27, MessageBoxResult.Cancel, dialog =>
            {
                var panel = (Canvas)dialog.Content!;
                var scroll = panel.Children.OfType<ScrollViewer>().Single();
                var actions = panel.Children.OfType<Button>().Where(button => (string?)button.Content != "×").ToArray();
                Require(scroll.ScrollableHeight > 0 && actions.All(action =>
                        action.NativeElement.Rectangle.Top >= scroll.NativeElement.Rectangle.Bottom),
                    "long messages scroll while their action row remains visible");
            });
    }

    private static void RunDialog(Window owner, string message, string caption, MessageBoxButton buttons,
        MessageBoxResult defaultResult, int nativeKey, MessageBoxResult expected, Action<Window> verify)
    {
        var timeout = Stopwatch.StartNew();
        Exception? failure = null;
        int passes = 0;
        bool inputSent = false;
        using var timer = new System.Windows.Forms.Timer { Interval = 25 };
        timer.Tick += (_, _) =>
        {
            var dialog = Application.Current.Windows.SingleOrDefault(window => window.Title == caption);
            if (dialog is null) return;
            if (timeout.Elapsed > TimeSpan.FromSeconds(4))
            {
                failure = new TimeoutException("The synthetic modal did not handle its key input.");
                dialog.Close();
                return;
            }
            if (inputSent || ++passes < 3) return;
            try
            {
                Require(!IsWindowEnabled(owner.NativeHandle), "the owner is disabled while its modal is visible");
                verify(dialog);
                inputSent = true;
                SendMessage(dialog.NativeHandle, 0x100, new IntPtr(nativeKey), new IntPtr(1));
                SendMessage(dialog.NativeHandle, 0x101, new IntPtr(nativeKey), new IntPtr(unchecked((int)0xC0000001)));
            }
            catch (Exception error)
            {
                failure = error;
                dialog.Close();
            }
        };
        timer.Start();
        var result = MessageBox.Show(owner, message, caption, buttons, MessageBoxImage.None, defaultResult);
        timer.Stop();
        if (failure is not null) throw failure;
        Require(result == expected && IsWindowEnabled(owner.NativeHandle),
            "modal keyboard actions return the expected result and re-enable the owner");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
