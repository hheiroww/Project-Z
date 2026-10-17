namespace ProjectZ.WpfCompatibility.Shell;

/// <summary>Project-Z window decoration metadata, with the rounded surface owned by the native renderer.</summary>
public class WindowChrome : DependencyObject
{
    public double CaptionHeight { get; set; }
    public Thickness ResizeBorderThickness { get; set; }
    public Thickness GlassFrameThickness { get; set; }
    public CornerRadius CornerRadius { get; set; }
    public bool UseAeroCaptionButtons { get; set; }
    public static readonly DependencyProperty WindowChromeProperty = DependencyProperty.Register("WindowChrome", typeof(WindowChrome), typeof(WindowChrome), new(null, (owner, args) =>
    {
        if (owner is Window window)
        {
            if (args.NewValue is WindowChrome chrome && window.NativeElement is global::ProjectZ.Shared.Drawing.UI.Primitives.RectangleElement rectangle)
                rectangle.CornerRadii = global::ProjectZ.Shared.Drawing.UI.Primitives.CornerRadii.Parse(chrome.CornerRadius.ToString());
            window.ApplyStyles();
        }
    }));
    public static readonly DependencyProperty IsHitTestVisibleInChromeProperty = DependencyProperty.Register("IsHitTestVisibleInChrome", typeof(bool), typeof(WindowChrome), new(false));
    public static void SetWindowChrome(Window window, WindowChrome? value) => window.SetValue(WindowChromeProperty, value);
    public static WindowChrome? GetWindowChrome(Window window) => window.GetValue(WindowChromeProperty) as WindowChrome;
    public static void SetIsHitTestVisibleInChrome(UIElement element, bool value) => element.SetValue(IsHitTestVisibleInChromeProperty, value);
    public static bool GetIsHitTestVisibleInChrome(UIElement element) => (bool)element.GetValue(IsHitTestVisibleInChromeProperty)!;
}
