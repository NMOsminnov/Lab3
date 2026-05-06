using Microsoft.UI.Xaml.Data;
using System;

namespace Lab3.Converters;

public class PercentToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double percent)
            return Math.Max(2, percent * 2.3); // 100% → ~230px (чтобы влезало в 250px)
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}