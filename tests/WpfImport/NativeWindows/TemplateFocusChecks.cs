using ProjectZ.WpfCompatibility;
using ProjectZ.WpfCompatibility.Controls;
using ProjectZ.WpfCompatibility.Threading;

internal static class TemplateFocusChecks
{
    internal static void Run() => Hosting.Run(static () => new FocusApplication());

    private sealed class FocusApplication : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var markup = Path.GetFullPath("artifacts/wpf-import/template-focus-effect.xaml");
            File.WriteAllText(markup, """
                <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        Width="280" Height="170" WindowStyle="None" ShowActivated="False">
                    <Window.Resources>
                        <Style x:Key="GlowingField" TargetType="TextBox">
                            <Setter Property="Template">
                                <Setter.Value>
                                    <ControlTemplate TargetType="{x:Type TextBox}">
                                        <Border x:Name="Chrome" Background="#20242E">
                                            <Border.Effect>
                                                <DropShadowEffect x:Name="FocusShadow" BlurRadius="8" ShadowDepth="0" Opacity="0"/>
                                            </Border.Effect>
                                            <ScrollViewer x:Name="PART_ContentHost"/>
                                        </Border>
                                        <ControlTemplate.Triggers>
                                            <Trigger Property="IsKeyboardFocusWithin" Value="True">
                                                <Trigger.EnterActions><BeginStoryboard><Storyboard>
                                                    <DoubleAnimation Storyboard.TargetName="FocusShadow"
                                                                     Storyboard.TargetProperty="Opacity" To="0.7" Duration="0:0:0.08"/>
                                                </Storyboard></BeginStoryboard></Trigger.EnterActions>
                                                <Trigger.ExitActions><BeginStoryboard><Storyboard>
                                                    <DoubleAnimation Storyboard.TargetName="FocusShadow"
                                                                     Storyboard.TargetProperty="Opacity" To="0" Duration="0:0:0.08"/>
                                                </Storyboard></BeginStoryboard></Trigger.ExitActions>
                                            </Trigger>
                                        </ControlTemplate.Triggers>
                                    </ControlTemplate>
                                </Setter.Value>
                            </Setter>
                        </Style>
                    </Window.Resources>
                    <StackPanel Margin="20">
                        <TextBox x:Name="First" Style="{StaticResource GlowingField}" Height="28" Text="First"/>
                        <TextBox x:Name="Second" Style="{StaticResource GlowingField}" Height="28" Text="Second"/>
                        <TextBox x:Name="Invalid" Height="28" Text="Invalid">
                            <TextBox.Triggers>
                                <Trigger Property="IsKeyboardFocusWithin" Value="True">
                                    <Trigger.EnterActions><BeginStoryboard><Storyboard>
                                        <DoubleAnimation Storyboard.TargetName="MissingPart"
                                                         Storyboard.TargetProperty="Opacity" To="0.5" Duration="0:0:0.05"/>
                                    </Storyboard></BeginStoryboard></Trigger.EnterActions>
                                </Trigger>
                            </TextBox.Triggers>
                        </TextBox>
                    </StackPanel>
                </Window>
                """);
            var window = new Window();
            XamlLoader.Initialize(window, markup);
            var first = (TextBox)window.FindName("First")!;
            var second = (TextBox)window.FindName("Second")!;
            var invalid = (TextBox)window.FindName("Invalid")!;
            window.Show();

            int tick = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            timer.Tick += (_, _) =>
            {
                if (++tick == 2)
                    Require(first.Focus(), "focus reaches the imported textbox with a named shadow");
                if (tick == 8)
                {
                    Require(first.NativeElement.VisualEffect?.Opacity > 0.65f && second.NativeElement.VisualEffect?.Opacity == 0,
                        "the named template shadow animates on only the focused textbox");
                    Require(second.Focus(), "focus transfers to another instance of the same template");
                }
                if (tick < 14) return;
                timer.Stop();
                Require(first.NativeElement.VisualEffect?.Opacity < 0.05f && second.NativeElement.VisualEffect?.Opacity > 0.65f,
                    "focus exit and entry independently animate the two native shadows");
                bool rejected = false;
                try { invalid.Focus(); }
                catch (NotSupportedException error) when (error.Message.Contains("unknown TargetName: MissingPart", StringComparison.Ordinal))
                {
                    rejected = true;
                }
                Require(rejected, "an unknown storyboard target still reports the exact missing name");
                window.Close();
            };
            timer.Start();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
