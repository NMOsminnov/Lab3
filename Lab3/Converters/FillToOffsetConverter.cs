using Microsoft.UI.Xaml.Data;
using System;

namespace Lab3.Converters;

public class FillToOffsetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double fill)
        {
            // 20% → 0, 40% → 80, 60% → 160, 80% → 240, 100% → 320
            return (fill / 20.0 - 1) * 80;
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}