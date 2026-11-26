using Compat = ProjectZ.WpfCompatibility;
using Controls = ProjectZ.WpfCompatibility.Controls;
using Color = Microsoft.Xna.Framework.Color;
using Media = ProjectZ.WpfCompatibility.Media;
using Native = ProjectZ.Shared.Drawing.UI.Primitives;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Compat.Hosting.Run(() =>
        {
            var app = new Compat.Application();
            app.Startup += (_, _) => _ = ProbeAsync(app);
            return app;
        }, options: new() { Width = 300, Height = 200 });
    }

    private static async Task ProbeAsync(Compat.Application app)
    {
        try
        {
            var window = new Compat.Window();
            app.MainWindow = window;
            Compat.XamlLoader.Initialize(window, "Probe.xaml");
            var canvas = (Controls.Canvas)window.FindName("Root")!;
            var square = AddBorder(canvas, 115, 62, 0);
            var round = AddBorder(canvas, 165, 62, 6);
            var codeScroll = new Controls.ScrollViewer { Width = 45, Height = 40 };
            Controls.Canvas.SetLeft(codeScroll, 215);
            Controls.Canvas.SetTop(codeScroll, 62);
            canvas.Children.Add(codeScroll);
            var codeStroke = AddBorder(canvas, 125, 117, 6);
            codeStroke.Width = 96;
            codeStroke.Height = 49;
            codeStroke.BorderThickness = new Compat.Thickness(5);
            var rotation = new Media.RotateTransform { CenterX = .5, CenterY = .5 };
            var gradient = new Media.LinearGradientBrush(Media.Color.FromArgb(128, 255, 0, 0), Media.Color.FromArgb(128, 0, 255, 0), new Compat.Point(0, 0), new Compat.Point(1, 0)) { RelativeTransform = rotation };
            codeStroke.BorderBrush = gradient;
            window.Show();
            await Task.Delay(500);
            Color[] pixels = window.CaptureFrame();
            int width = (int)window.Width;
            var cases = new[]
            {
                ("MarkupSquare", (Compat.FrameworkElement)window.FindName("MarkupSquare")!, new Color(128, 0, 32)),
                ("MarkupRound", (Compat.FrameworkElement)window.FindName("MarkupRound")!, new Color(128, 0, 32)),
                ("MarkupOpacity", (Compat.FrameworkElement)window.FindName("MarkupOpacity")!, new Color(128, 0, 32)),
                ("AncestorOpacity", (Compat.FrameworkElement)window.FindName("AncestorOpacity")!, new Color(128, 0, 32)),
                ("ExplicitTransparentScroll", (Compat.FrameworkElement)window.FindName("ExplicitTransparentScroll")!, new Color(0, 0, 64)),
                ("ImplicitTransparentScroll", (Compat.FrameworkElement)window.FindName("ImplicitTransparentScroll")!, new Color(0, 0, 64)),
                ("CodeCreatedTransparentScroll", (Compat.FrameworkElement)codeScroll, new Color(0, 0, 64)),
                ("AdapterSquare", (Compat.FrameworkElement)square, new Color(128, 0, 32)),
                ("AdapterRound", (Compat.FrameworkElement)round, new Color(128, 0, 32))
            };
            bool passed = true;
            foreach (var (name, element, expected) in cases)
            {
                var point = element.NativeElement.Rectangle.Center;
                Color actual = pixels[point.Y * width + point.X];
                bool matches = Math.Abs(actual.R - expected.R) <= 2 && Math.Abs(actual.G - expected.G) <= 2 && Math.Abs(actual.B - expected.B) <= 2 && actual.A == 255;
                passed &= matches;
                Console.WriteLine($"{(matches ? "PASS" : "FAIL")} {name} ({point.X},{point.Y}) actual={actual} expected={expected}");
            }
            var stroke = (Native.RectangleElement)((Controls.Border)window.FindName("GradientStroke")!).NativeElement;
            var area = stroke.Rectangle;
            Color left = At(pixels, area.Left + 12, area.Top + 2);
            Color right = At(pixels, area.Right - 13, area.Top + 2);
            Color center = At(pixels, area.Center.X, area.Center.Y);
            bool strokeBefore = left.R > right.R + 60 && right.G > left.G + 60 && left.B is >= 30 and <= 34 && center == new Color(0, 17, 68);
            Console.WriteLine($"{(strokeBefore ? "PASS" : "FAIL")} gradient stroke without replacing fill left={left} right={right} fill={center}");
            passed &= strokeBefore;

            var nativeBackground = (Controls.Border)window.FindName("MarkupSquare")!;
            var backgroundPoint = nativeBackground.NativeElement.Rectangle.Center;
            var nativeBefore = ((Native.RectangleElement)nativeBackground.NativeElement).BackgroundColor;
            var reconstructedBrush = nativeBackground.Background as Media.SolidColorBrush;
            bool readSafe = reconstructedBrush?.Color is { A: 128, R: 255, G: 0, B: 0 }
                && ((Native.RectangleElement)nativeBackground.NativeElement).BackgroundColor == nativeBefore;
            passed &= readSafe;
            Console.WriteLine($"{(readSafe ? "PASS" : "FAIL")} reading declarative Background preserves native color and recovers straight alpha");

            var codeArea = codeStroke.NativeElement.Rectangle;
            Color codeLeft = At(pixels, codeArea.Left + 12, codeArea.Top + 2);
            Color codeRight = At(pixels, codeArea.Right - 13, codeArea.Top + 2);
            Color codeFill = At(pixels, codeArea.Center.X, codeArea.Center.Y);
            bool roundTrips = ReferenceEquals(codeStroke.BorderBrush, gradient) && ReferenceEquals(codeStroke.BorderBrush, codeStroke.BorderBrush)
                && codeLeft.R > codeRight.R + 60 && codeRight.G > codeLeft.G + 60 && codeFill == new Color(128, 0, 32);
            Console.WriteLine($"{(roundTrips ? "PASS" : "FAIL")} code-created BorderBrush round-trips and draws without replacing solid fill");
            passed &= roundTrips;

            rotation.Angle = 180;
            await Task.Delay(300);
            Color[] afterCodeRotation = window.CaptureFrame();
            Color codeRotatedLeft = At(afterCodeRotation, codeArea.Left + 12, codeArea.Top + 2);
            Color codeRotatedRight = At(afterCodeRotation, codeArea.Right - 13, codeArea.Top + 2);
            bool codeRotation = codeRotatedRight.R > codeRotatedLeft.R + 60 && codeRotatedLeft.G > codeRotatedRight.G + 60;
            bool sameAfterRead = At(pixels, backgroundPoint.X, backgroundPoint.Y) == At(afterCodeRotation, backgroundPoint.X, backgroundPoint.Y);
            Console.WriteLine($"{(codeRotation ? "PASS" : "FAIL")} code-created BorderBrush transform mutation rotates the displayed stroke");
            Console.WriteLine($"{(sameAfterRead ? "PASS" : "FAIL")} Background getter does not change pixels after redraw");
            passed &= codeRotation && sameAfterRead;

            if (stroke.BorderBrush is not null)
            {
                stroke.BorderBrush.RelativeTransform = System.Numerics.Matrix3x2.CreateRotation(MathF.PI, new(.5f, .5f));
                stroke.OwnerScene.RequestRender();
                await Task.Delay(300);
                Color[] rotated = window.CaptureFrame();
                Color rotatedLeft = At(rotated, area.Left + 12, area.Top + 2);
                Color rotatedRight = At(rotated, area.Right - 13, area.Top + 2);
                bool flips = rotatedRight.R > rotatedLeft.R + 60 && rotatedLeft.G > rotatedRight.G + 60;
                Console.WriteLine($"{(flips ? "PASS" : "FAIL")} relative stroke rotation refreshes cache left={rotatedLeft} right={rotatedRight}");
                passed &= flips;
            }
            Color At(Color[] source, int x, int y) => source[y * width + x];
            app.Shutdown(passed ? 0 : 1);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            app.Shutdown(2);
        }
    }

    private static Controls.Border AddBorder(Controls.Canvas canvas, double x, double y, double radius)
    {
        var border = new Controls.Border
        {
            Width = 45, Height = 40, CornerRadius = new Compat.CornerRadius(radius),
            Background = new Media.SolidColorBrush(Media.Color.FromArgb(128, 255, 0, 0))
        };
        Controls.Canvas.SetLeft(border, x);
        Controls.Canvas.SetTop(border, y);
        canvas.Children.Add(border);
        return border;
    }
}
