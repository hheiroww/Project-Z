using System;
using System.Globalization;
using System.Windows.Data;
namespace ImportSamples;
public sealed class PrefixConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => "Converted: " + value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value.ToString()!.Replace("Converted: ", "");
}
