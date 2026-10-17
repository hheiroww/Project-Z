namespace ProjectZ.WpfCompatibility.Input;

public enum MouseButton { Left, Middle, Right }
public enum MouseButtonState { Released, Pressed }
// Values match the native key codes used by Project-Z (not WPF's enum ordinals).
public enum Key { None = 0, Back = 8, Tab = 9, Enter = 13, Return = Enter, Escape = 27, Space = 32, PageUp = 33, PageDown = 34, End = 35, Home = 36, Left = 37, Up = 38, Right = 39, Down = 40, Insert = 45, Delete = 46, D0 = 48, D1, D2, D3, D4, D5, D6, D7, D8, D9, A = 65, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, LWin = 91, RWin = 92, F1 = 112, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24, LeftShift = 160, RightShift = 161, LeftCtrl = 162, RightCtrl = 163, LeftAlt = 164, RightAlt = 165, System = 65535 }
[Flags] public enum ModifierKeys { None = 0, Alt = 1, Control = 2, Shift = 4, Windows = 8 }
public delegate void MouseEventHandler(object sender, MouseEventArgs e);
public delegate void MouseButtonEventHandler(object sender, MouseButtonEventArgs e);
public delegate void MouseWheelEventHandler(object sender, MouseWheelEventArgs e);
public delegate void KeyEventHandler(object sender, KeyEventArgs e);
public delegate void TextCompositionEventHandler(object sender, TextCompositionEventArgs e);
public class InputEventArgs : RoutedEventArgs
{
    public int Timestamp { get; }
    protected InputEventArgs(int timestamp) => Timestamp = timestamp;
}
public sealed class MouseDevice
{
    public Point GetPosition(IInputElement? relativeTo)
    {
        var element = relativeTo as UIElement;
        var scene = element?.NativeElement.OwnerScene ?? Application.Current.ControlScene;
        var position = scene.MousePosition;
        return new(position.X - (element?.NativeElement.Position.X ?? 0), position.Y - (element?.NativeElement.Position.Y ?? 0));
    }
}
public sealed class KeyboardDevice { }
public static class Mouse
{
    public static MouseDevice PrimaryDevice { get; } = new();
    public static IInputElement? Captured => Application.Current.ControlScene.CapturedPointerElement is { } native ? UIElement.FromNative(native) : null;
    public static Point GetPosition(IInputElement? relativeTo) => PrimaryDevice.GetPosition(relativeTo);
    public static bool Capture(IInputElement? element) { if (element != null) return element.CaptureMouse(); Captured?.ReleaseMouseCapture(); return true; }
}
public static class Keyboard
{
    public static KeyboardDevice PrimaryDevice { get; } = new();
    public static readonly RoutedEvent KeyDownEvent = UIElement.KeyDownEvent, KeyUpEvent = UIElement.KeyUpEvent, PreviewKeyDownEvent = UIElement.PreviewKeyDownEvent, PreviewKeyUpEvent = UIElement.PreviewKeyUpEvent;
    public static IInputElement? FocusedElement => Application.Current.ControlScene.FocusedElement is { } native ? UIElement.FromNative(native) : null;
    public static IInputElement? Focus(IInputElement element) => element.Focus() ? element : FocusedElement;
    public static ModifierKeys Modifiers
    {
        get
        {
            var state = Application.Current.ControlScene.InputKeyboardState;
            bool Down(Microsoft.Xna.Framework.Input.Keys left, Microsoft.Xna.Framework.Input.Keys right) => state.IsKeyDown(left) || state.IsKeyDown(right);
            return (Down(Microsoft.Xna.Framework.Input.Keys.LeftAlt, Microsoft.Xna.Framework.Input.Keys.RightAlt) ? ModifierKeys.Alt : 0) |
                (Down(Microsoft.Xna.Framework.Input.Keys.LeftControl, Microsoft.Xna.Framework.Input.Keys.RightControl) ? ModifierKeys.Control : 0) |
                (Down(Microsoft.Xna.Framework.Input.Keys.LeftShift, Microsoft.Xna.Framework.Input.Keys.RightShift) ? ModifierKeys.Shift : 0) |
                (Down(Microsoft.Xna.Framework.Input.Keys.LeftWindows, Microsoft.Xna.Framework.Input.Keys.RightWindows) ? ModifierKeys.Windows : 0);
        }
    }
}
public static class KeyInterop { public static Key KeyFromVirtualKey(int key) => (Key)key; public static int VirtualKeyFromKey(Key key) => (int)key; }
public enum FocusNavigationDirection { Next, Previous, First, Last, Left, Right, Up, Down }
public sealed class TraversalRequest(FocusNavigationDirection focusNavigationDirection) { public FocusNavigationDirection FocusNavigationDirection { get; } = focusNavigationDirection; public bool Wrapped { get; set; } }
public class MouseEventArgs : InputEventArgs
{
    readonly Microsoft.Xna.Framework.Point position;
    public MouseDevice MouseDevice { get; }
    public virtual MouseButtonState LeftButton => (Source as UIElement)?.NativeElement.isMouseDown == true ? MouseButtonState.Pressed : MouseButtonState.Released;
    public MouseEventArgs() : this(default(Microsoft.Xna.Framework.Point)) { }
    internal MouseEventArgs(Microsoft.Xna.Framework.Point point) : base(Environment.TickCount) { position = point; MouseDevice = Mouse.PrimaryDevice; }
    public MouseEventArgs(MouseDevice mouseDevice, int timestamp) : base(timestamp) { MouseDevice = mouseDevice; var point = mouseDevice.GetPosition(null); position = new((int)point.X, (int)point.Y); }
    public Point GetPosition(UIElement? relativeTo) => new(position.X - (relativeTo?.NativeElement.Position.X ?? 0), position.Y - (relativeTo?.NativeElement.Position.Y ?? 0));
}
public class MouseButtonEventArgs : MouseEventArgs
{
    public MouseButton ChangedButton { get; }
    readonly MouseButtonState buttonState;
    public MouseButtonState ButtonState => RoutedEvent?.Name.EndsWith("Up", StringComparison.Ordinal) == true ? MouseButtonState.Released : buttonState;
    public int ClickCount { get; internal set; } = 1;
    public override MouseButtonState LeftButton => ChangedButton == MouseButton.Left ? ButtonState : base.LeftButton;
    internal MouseButtonEventArgs(Microsoft.Xna.Framework.Point point, MouseButtonState state, MouseButton button = MouseButton.Left) : base(point) { buttonState = state; ChangedButton = button; }
    public MouseButtonEventArgs(MouseDevice device, int timestamp, MouseButton button) : base(device, timestamp) { ChangedButton = button; buttonState = MouseButtonState.Pressed; }
}
public class MouseWheelEventArgs : MouseEventArgs
{
    public int Delta { get; }
    internal MouseWheelEventArgs(int delta, Microsoft.Xna.Framework.Point point) : base(point) => Delta = delta;
    public MouseWheelEventArgs(MouseDevice device, int timestamp, int delta) : base(device, timestamp) => Delta = delta;
}
public class KeyEventArgs : InputEventArgs
{
    public Key Key { get; }
    public Key SystemKey { get; init; }
    public KeyboardDevice KeyboardDevice { get; }
    public bool IsRepeat { get; internal set; }
    public KeyEventArgs(Key key) : this(Keyboard.PrimaryDevice, null, Environment.TickCount, key) { }
    public KeyEventArgs(KeyboardDevice device, object? inputSource, int timestamp, Key key) : base(timestamp) { KeyboardDevice = device; Key = key; SystemKey = key; }
}
public sealed class InputManager { public static InputManager Current { get; } = new(); }
public sealed class TextComposition(InputManager inputManager, IInputElement source, string text) { public InputManager InputManager { get; } = inputManager; public string Text { get; } = text; public IInputElement Source { get; } = source; }
public sealed class TextCompositionEventArgs(KeyboardDevice device, TextComposition composition) : InputEventArgs(Environment.TickCount) { public string Text => TextComposition.Text; public TextComposition TextComposition { get; } = composition; public KeyboardDevice Device { get; } = device; }
public static class TextCompositionManager { public static readonly RoutedEvent PreviewTextInputEvent = UIElement.PreviewTextInputEvent, TextInputEvent = UIElement.TextInputEvent; }
