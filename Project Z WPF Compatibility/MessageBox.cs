namespace ProjectZ.WpfCompatibility;

using Controls;
using Media;
using NativeButton = global::ProjectZ.Shared.Drawing.UI.Input.Button;
using NativeButtonAutoSize = global::ProjectZ.Shared.Drawing.UI.Input.ButtonAutoSize;
using NativeTextBlock = global::ProjectZ.Shared.Drawing.UI.Primitives.TextElement;

public enum MessageBoxButton { OK, OKCancel, YesNoCancel, YesNo }
public enum MessageBoxResult { None, OK, Cancel, Yes, No }
public enum MessageBoxImage { None, Error, Hand=Error, Stop=Error, Question, Exclamation, Warning=Exclamation, Asterisk, Information=Asterisk }
[Flags] public enum MessageBoxOptions { None=0, DefaultDesktopOnly=0x20000, RightAlign=0x80000, RtlReading=0x100000, ServiceNotification=0x200000 }
public static class MessageBox
{
    public static MessageBoxResult Show(string text) => Show(null,text,"",MessageBoxButton.OK,MessageBoxImage.None);
    public static MessageBoxResult Show(string text,string caption) => Show(null,text,caption,MessageBoxButton.OK,MessageBoxImage.None);
    public static MessageBoxResult Show(string text,string caption,MessageBoxButton buttons) => Show(null,text,caption,buttons,MessageBoxImage.None);
    public static MessageBoxResult Show(string text,string caption,MessageBoxButton buttons,MessageBoxImage image) => Show(null,text,caption,buttons,image);
    public static MessageBoxResult Show(string text,string caption,MessageBoxButton buttons,MessageBoxImage image,MessageBoxResult defaultResult) => Show(null,text,caption,buttons,image,defaultResult,MessageBoxOptions.None);
    public static MessageBoxResult Show(string text,string caption,MessageBoxButton buttons,MessageBoxImage image,MessageBoxResult defaultResult,MessageBoxOptions options) => Show(null,text,caption,buttons,image,defaultResult,options);
    public static MessageBoxResult Show(Window? owner,string text) => Show(owner,text,"",MessageBoxButton.OK,MessageBoxImage.None);
    public static MessageBoxResult Show(Window? owner,string text,string caption) => Show(owner,text,caption,MessageBoxButton.OK,MessageBoxImage.None);
    public static MessageBoxResult Show(Window? owner,string text,string caption,MessageBoxButton buttons) => Show(owner,text,caption,buttons,MessageBoxImage.None);
    public static MessageBoxResult Show(Window? owner,string text,string caption,MessageBoxButton buttons,MessageBoxImage image) => Show(owner,text,caption,buttons,image,MessageBoxResult.None,MessageBoxOptions.None);
    public static MessageBoxResult Show(Window? owner,string text,string caption,MessageBoxButton buttons,MessageBoxImage image,MessageBoxResult defaultResult) => Show(owner,text,caption,buttons,image,defaultResult,MessageBoxOptions.None);
    public static MessageBoxResult Show(Window? owner,string text,string caption,MessageBoxButton buttons,MessageBoxImage image,MessageBoxResult defaultResult,MessageBoxOptions options)
    {
        const double margin = 18, captionHeight = 40, actionHeight = 34, actionGap = 8;

        owner ??= Application.Current.MainWindow;
        using var window = new Window
        {
            Title = caption,
            Owner = owner,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Background = Paint(18, 25, 35),
            BorderBrush = Paint(61, 80, 100),
            BorderThickness = new(1)
        };
        var panel = new Canvas();
        var message = new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = Paint(225, 234, 245),
            TextAlignment = options.HasFlag(MessageBoxOptions.RightAlign) ? TextAlignment.Right : TextAlignment.Left
        };
        var workArea = SystemParameters.WorkArea;
        double maxWidth = Math.Clamp(workArea.Width - 36, 260, 560);
        double width = Math.Clamp(Math.Ceiling(Measure(message, maxWidth).X) + margin * 2, Math.Min(352, maxWidth), maxWidth);
        double contentWidth = width - margin * 2;
        double messageTop = captionHeight + 9;
        double maxMessageHeight = Math.Clamp(workArea.Height - messageTop - actionHeight - margin * 2, 70, 360);

        message.TextWrapping = TextWrapping.Wrap;
        double messageHeight = Math.Max(20, Math.Ceiling(Measure(message, contentWidth).Y) + 4);
        if (messageHeight > maxMessageHeight)
        {
            message.Width = contentWidth - 16;
            message.Height = Math.Ceiling(Measure(message, message.Width).Y) + 4;
            Add(new ScrollViewer
            {
                Width = contentWidth,
                Height = maxMessageHeight,
                Background = Brushes.Transparent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = message
            }, margin, messageTop);
            messageHeight = maxMessageHeight;
        }
        else
        {
            message.Width = contentWidth;
            message.Height = messageHeight;
            Add(message, margin, messageTop);
        }

        var title = new TextBlock
        {
            Text = caption,
            Width = contentWidth - 38,
            Height = 26,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Paint(248, 251, 255),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        title.MouseLeftButtonDown += (_, _) => window.DragMove();
        Add(title, margin, 14);

        var choices = buttons switch
        {
            MessageBoxButton.OKCancel => new[] { MessageBoxResult.OK, MessageBoxResult.Cancel },
            MessageBoxButton.YesNo => new[] { MessageBoxResult.Yes, MessageBoxResult.No },
            MessageBoxButton.YesNoCancel => new[] { MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel },
            _ => new[] { MessageBoxResult.OK }
        };
        var initial = choices.Contains(defaultResult) ? defaultResult : choices[0];
        var close = CreateButton("×", 30, 29, emphasized: false);
        close.IsCancel = !choices.Contains(MessageBoxResult.Cancel);
        close.Click += (_, _) => window.Close();
        Add(close, width - margin - close.Width, 8);

        var result = MessageBoxResult.None;
        double actionTop = messageTop + messageHeight + margin;
        double actionWidth = Math.Min(96, (contentWidth - (choices.Length - 1) * actionGap) / choices.Length);
        double actionLeft = width - margin - choices.Length * actionWidth - (choices.Length - 1) * actionGap;
        foreach (var choice in choices)
        {
            var button = CreateButton(choice.ToString(), actionWidth, actionHeight, choice == initial);
            button.IsDefault = choice == initial;
            button.IsCancel = choice == MessageBoxResult.Cancel;
            button.Click += (_, _) =>
            {
                result = choice;
                window.DialogResult = choice is MessageBoxResult.OK or MessageBoxResult.Yes;
            };
            Add(button, actionLeft, actionTop);
            actionLeft += actionWidth + actionGap;
        }
        window.Width = width;
        window.Height = actionTop + actionHeight + margin;
        window.Content = panel;
        window.ShowDialog();
        return result == MessageBoxResult.None ? MessageBoxResult.Cancel : result;

        void Add(FrameworkElement element, double left, double top)
        {
            Canvas.SetLeft(element, left);
            Canvas.SetTop(element, top);
            panel.Children.Add(element);
        }
    }

    private static global::Microsoft.Xna.Framework.Vector2 Measure(TextBlock text, double width) =>
        ((NativeTextBlock)text.NativeElement).MeasureXamlText((float)width);

    private static SolidColorBrush Paint(byte red, byte green, byte blue) => new(Color.FromRgb(red, green, blue));

    private static Button CreateButton(string label, double width, double height, bool emphasized)
    {
        var button = new Button
        {
            Padding = new(8, 0, 8, 0),
            CornerRadius = new(6),
            BorderThickness = new(1),
            BorderBrush = emphasized ? Paint(65, 157, 174) : Paint(64, 83, 103),
            Background = emphasized ? Paint(34, 93, 109) : Paint(32, 44, 57),
            Foreground = Paint(246, 250, 255),
            FontSize = 13,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var native = (NativeButton)button.NativeElement;
        native.AutoSize = NativeButtonAutoSize.None;
        native.AutoSizeWidthOnTextOverflow = true;
        native.MouseOverBackgroundColor = emphasized ? new(42, 112, 130) : new(43, 61, 77);
        native.MouseDownBackgroundColor = emphasized ? new(27, 76, 89) : new(27, 38, 49);
        button.Width = width;
        button.Height = height;
        button.Content = label;
        return button;
    }
}
