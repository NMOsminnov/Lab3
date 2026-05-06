using Microsoft.UI.Xaml.Data;
using System;

namespace Lab3.Converters;

public class BoolToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is bool b ? (b ? "✅ Обнаружено" : "❌ Не обнаружено") : "?";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}