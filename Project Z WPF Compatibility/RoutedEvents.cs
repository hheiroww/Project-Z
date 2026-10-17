using System.Reflection;
using System.Runtime.ExceptionServices;
using ProjectZ.Shared.Drawing.UI;

namespace ProjectZ.WpfCompatibility;

public interface IInputElement
{
    bool IsEnabled { get; }
    bool Focusable { get; set; }
    bool IsKeyboardFocused { get; }
    bool IsMouseCaptured { get; }
    bool Focus();
    bool CaptureMouse();
    void ReleaseMouseCapture();
    void RaiseEvent(RoutedEventArgs args);
}

public partial class UIElement
{
    public static readonly RoutedEvent MouseDoubleClickEvent = new("MouseDoubleClick");
    public event Input.MouseButtonEventHandler? MouseDoubleClick
    {
        add { if (value != null) AddHandler(MouseDoubleClickEvent, value); }
        remove { if (value != null) RemoveHandler(MouseDoubleClickEvent, value); }
    }
    public static readonly RoutedEvent LoadedEvent = new("Loaded"), UnloadedEvent = new("Unloaded"), GotFocusEvent = new("GotFocus"), LostFocusEvent = new("LostFocus");
    public static readonly RoutedEvent MouseEnterEvent = new("MouseEnter"), MouseLeaveEvent = new("MouseLeave"), MouseMoveEvent = new("MouseMove"), PreviewMouseMoveEvent = new("PreviewMouseMove");
    public static readonly RoutedEvent MouseDownEvent = new("MouseDown"), MouseUpEvent = new("MouseUp"), PreviewMouseDownEvent = new("PreviewMouseDown"), PreviewMouseUpEvent = new("PreviewMouseUp");
    public static readonly RoutedEvent MouseLeftButtonDownEvent = new("MouseLeftButtonDown"), MouseLeftButtonUpEvent = new("MouseLeftButtonUp"), PreviewMouseLeftButtonDownEvent = new("PreviewMouseLeftButtonDown"), PreviewMouseLeftButtonUpEvent = new("PreviewMouseLeftButtonUp");
    public static readonly RoutedEvent MouseRightButtonDownEvent = new("MouseRightButtonDown"), MouseRightButtonUpEvent = new("MouseRightButtonUp"), PreviewMouseRightButtonDownEvent = new("PreviewMouseRightButtonDown"), PreviewMouseRightButtonUpEvent = new("PreviewMouseRightButtonUp");
    public static readonly RoutedEvent MouseWheelEvent = new("MouseWheel"), PreviewMouseWheelEvent = new("PreviewMouseWheel");
    public static readonly RoutedEvent KeyDownEvent = new("KeyDown"), KeyUpEvent = new("KeyUp"), PreviewKeyDownEvent = new("PreviewKeyDown"), PreviewKeyUpEvent = new("PreviewKeyUp");
    public static readonly RoutedEvent TextInputEvent = new("TextInput"), PreviewTextInputEvent = new("PreviewTextInput"), GotMouseCaptureEvent = new("GotMouseCapture"), LostMouseCaptureEvent = new("LostMouseCapture");
    sealed class HandlerRegistration(RoutedEvent routedEvent, Delegate handler, bool handledEventsToo)
    {
        public RoutedEvent Event = routedEvent;
        public Delegate Handler = handler;
        public bool HandledEventsToo = handledEventsToo;
        public IDisposable? Subscription;
    }
    readonly List<HandlerRegistration> routedHandlers = [];
    public bool IsLoaded { get; private set; }
    public bool Focusable { get => NativeElement.CanSelect; set => NativeElement.CanSelect = value; }
    public bool IsKeyboardFocused => NativeElement.isSelected;
    public bool IsKeyboardFocusWithin
    {
        get { for (var node = NativeElement.OwnerScene?.FocusedElement; node != null; node = node.RoutedParent) if (node == NativeElement) return true; return false; }
    }
    public bool IsMouseCaptured => NativeElement.OwnerScene?.CapturedPointerElement == NativeElement;
    public bool CaptureMouse()
    {
        if (IsDisposed || !IsEnabled || !IsVisible || NativeElement.OwnerScene == null) return false;
        var scene = NativeElement.OwnerScene;
        scene.CapturePointer(NativeElement);
        return true;
    }
    public void ReleaseMouseCapture()
    {
        if (!IsMouseCaptured) return;
        var scene = NativeElement.OwnerScene;
        scene.ReleasePointerCapture(NativeElement);
    }
    public IInputElement? InputHitTest(Point point)
    {
        var scene = NativeElement.OwnerScene;
        if (scene == null) return null;
        var target = scene.PointToElement(new((int)(point.X + NativeElement.Position.X), (int)(point.Y + NativeElement.Position.Y)));
        return target == null ? null : FromNative(target);
    }
    public bool MoveFocus(Input.TraversalRequest request) => NativeElement.OwnerScene?.MoveFocus(request.FocusNavigationDirection == Input.FocusNavigationDirection.Previous) == true;
    public void AddHandler(RoutedEvent routedEvent, Delegate handler) => AddHandler(routedEvent, handler, false);
    public void AddHandler(RoutedEvent routedEvent, Delegate handler, bool handledEventsToo)
    {
        ArgumentNullException.ThrowIfNull(routedEvent); ArgumentNullException.ThrowIfNull(handler);
        Dispatcher.VerifyAccess();
        var entry = new HandlerRegistration(routedEvent, handler, handledEventsToo);
        routedHandlers.Add(entry); Bind(entry);
    }
    public void RemoveHandler(RoutedEvent routedEvent, Delegate handler)
    {
        Dispatcher.VerifyAccess();
        var index = routedHandlers.FindLastIndex(entry => entry.Event.Native == routedEvent.Native && entry.Handler.Equals(handler));
        if (index < 0) return;
        routedHandlers[index].Subscription?.Dispose(); routedHandlers.RemoveAt(index);
    }
    void Bind(HandlerRegistration entry)
    {
        entry.Subscription?.Dispose();
        entry.Subscription = SceneEvents.AddHandler(NativeElement, entry.Event.Native, (_, native) =>
        {
            var args = EventArgs(native, entry.Event);
            Invoke(entry.Handler, args); native.Handled = args.Handled;
        }, entry.HandledEventsToo);
    }
    void AttachRoutedHandlers()
    {
        foreach (var entry in routedHandlers) Bind(entry);
    }
    RoutedEventArgs EventArgs(SceneRoutedEventArgs native, RoutedEvent routedEvent)
    {
        var args = native.Payload as RoutedEventArgs ?? (native.Payload is ScenePastingContext paste ? new DataObjectPastingEventArgs(paste) : routedEvent.Name is "GotMouseCapture" or "LostMouseCapture" ? new Input.MouseEventArgs(native.Source.OwnerScene?.MousePosition ?? default) : new RoutedEventArgs(routedEvent));
        args.RoutedEvent = routedEvent;
        args.Source ??= FromNative(native.Source); args.OriginalSource ??= FromNative(native.OriginalSource);
        args.Handled = native.Handled;
        return args;
    }
    void Invoke(Delegate handler, RoutedEventArgs args)
    {
        try { handler.DynamicInvoke(this, args); }
        catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
    }
    void ClearRoutedHandlers() { foreach (var handler in routedHandlers) handler.Subscription?.Dispose(); routedHandlers.Clear(); }
}
