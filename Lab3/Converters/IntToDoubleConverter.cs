using Microsoft.UI.Xaml.Data;
using System;

namespace Lab3.Converters;

public class IntToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int i) return (double)i;
        if (value is long l) return (double)l;
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is double d) return (int)d;
        return 0;
    }
}