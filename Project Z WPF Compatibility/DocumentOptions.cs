namespace ProjectZ.WpfCompatibility
{
    public enum TextAlignment { Left, Right, Center, Justify }
    public enum TextMarkerStyle { None, Disc, Circle, Square, Box, LowerRoman, UpperRoman, LowerLatin, UpperLatin, Decimal }
}
namespace ProjectZ.WpfCompatibility.Navigation
{
    public class RequestNavigateEventArgs(Uri uri, string target) : RoutedEventArgs { public Uri Uri { get; } = uri; public string Target { get; } = target; }
}
