using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace Lab3.Converters;

public class LogColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string log)
        {
            if (log.Contains("[ERROR]")) return Colors.Crimson;
            if (log.Contains("[WARN]")) return Colors.Orange;
            if (log.Contains("[INFO]")) return Colors.ForestGreen;
            if (log.Contains("[DEBUG]")) return Colors.Gray;
        }
        return Colors.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}