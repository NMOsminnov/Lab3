using Microsoft.UI.Xaml.Data;
using System;

namespace Lab3.Converters;

public class NotEmptyToBoolConverter : IValueConverter
{
    public NotEmptyToBoolConverter() { }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}