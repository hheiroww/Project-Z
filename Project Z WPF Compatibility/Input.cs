namespace ProjectZ.WpfCompatibility.Input;

public enum MouseButton { Left, Middle, Right }
public enum MouseButtonState { Released, Pressed }
// Values match the native key codes used by Project-Z (not WPF's enum ordinals).
public enum Key { None = 0, Back = 8, Tab = 9, Enter = 13, Escape = 27, Space = 32, Left = 37, Up = 38, Right = 39, Down = 40, Delete = 46, A = 65, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z }
public class MouseEventArgs : RoutedEventArgs
{
    readonly Microsoft.Xna.Framework.Point position;
    public MouseEventArgs() { }
    internal MouseEventArgs(Microsoft.Xna.Framework.Point point) => position = point;
    public Point GetPosition(UIElement? relativeTo) => new(position.X - (relativeTo?.NativeElement.Position.X ?? 0), position.Y - (relativeTo?.NativeElement.Position.Y ?? 0));
}
public class MouseButtonEventArgs : MouseEventArgs
{
    public MouseButton ChangedButton => MouseButton.Left;
    public MouseButtonState ButtonState { get; }
    internal MouseButtonEventArgs(Microsoft.Xna.Framework.Point point, MouseButtonState state) : base(point) => ButtonState = state;
}
public class MouseWheelEventArgs(int delta, Microsoft.Xna.Framework.Point point) : MouseEventArgs(point) { public int Delta { get; } = delta; }
public class KeyEventArgs(Key key) : RoutedEventArgs { public Key Key { get; } = key; }
