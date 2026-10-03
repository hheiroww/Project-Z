namespace ProjectZ.WpfCompatibility;

public class RoutedPropertyChangedEventArgs<T>(T oldValue, T newValue) : RoutedEventArgs
{
    public T OldValue { get; } = oldValue;
    public T NewValue { get; } = newValue;
}

public delegate void RoutedPropertyChangedEventHandler<T>(object sender, RoutedPropertyChangedEventArgs<T> e);
