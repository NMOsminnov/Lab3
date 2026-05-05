using Microsoft.UI.Xaml.Data;
using System;

namespace Lab3.Converters; // 👈 Должно совпадать с xmlns в App.xaml

public class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double percent)
        {
            // Масштабирование: 100% = 300 пикселей
            return Math.Min(percent * 3.0, 300.0);
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}