namespace ProjectZ.WpfCompatibility;
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
        using var window=new Window { Title=caption, Owner=owner ?? Application.Current.MainWindow, Width=480, Height=180, ResizeMode=ResizeMode.NoResize, WindowStartupLocation=WindowStartupLocation.CenterOwner, ShowInTaskbar=false };
        var panel=new Controls.StackPanel { Margin=new(16) };
        panel.Children.Add(new Controls.TextBlock { Text=text,TextWrapping=TextWrapping.Wrap,TextAlignment=options.HasFlag(MessageBoxOptions.RightAlign) ? TextAlignment.Right : TextAlignment.Left,Margin=new(0,0,0,16) });
        var actions=new Controls.StackPanel { Orientation=Controls.Orientation.Horizontal };
        MessageBoxResult result=MessageBoxResult.None;
        var choices=buttons switch { MessageBoxButton.OKCancel=>new[]{MessageBoxResult.OK,MessageBoxResult.Cancel}, MessageBoxButton.YesNo=>new[]{MessageBoxResult.Yes,MessageBoxResult.No}, MessageBoxButton.YesNoCancel=>new[]{MessageBoxResult.Yes,MessageBoxResult.No,MessageBoxResult.Cancel}, _=>new[]{MessageBoxResult.OK} };
        var initial=choices.Contains(defaultResult) ? defaultResult : choices[0];
        foreach(var choice in choices) { var button=new Controls.Button { Content=choice.ToString(),Width=96,Height=32,Margin=new(4),IsDefault=choice==initial,IsCancel=choice==MessageBoxResult.Cancel }; button.Click+=(_,_)=>{result=choice;window.DialogResult=choice is MessageBoxResult.OK or MessageBoxResult.Yes;}; actions.Children.Add(button); }
        panel.Children.Add(actions); window.Content=panel; window.ShowDialog();
        return result==MessageBoxResult.None ? MessageBoxResult.Cancel : result;
    }
}
