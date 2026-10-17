namespace ProjectZ.WpfCompatibility.Automation;

/// <summary>Metadata retained on the native element for accessibility hosts.</summary>
public static class AutomationProperties
{
    public static readonly DependencyProperty NameProperty = DependencyProperty.RegisterAttached("AutomationName", typeof(string), typeof(AutomationProperties), new(""));
    public static readonly DependencyProperty HelpTextProperty = DependencyProperty.RegisterAttached("AutomationHelpText", typeof(string), typeof(AutomationProperties), new(""));
    public static void SetName(DependencyObject element, string name) => element.SetValue(NameProperty, name);
    public static string GetName(DependencyObject element) => (string)element.GetValue(NameProperty)!;
    public static void SetHelpText(DependencyObject element, string text) => element.SetValue(HelpTextProperty, text);
    public static string GetHelpText(DependencyObject element) => (string)element.GetValue(HelpTextProperty)!;
}
