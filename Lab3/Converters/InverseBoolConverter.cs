using Microsoft.UI.Xaml.Data;
using System;

namespace Lab3.Converters;  // 🔥 Должно совпадать с xmlns в App.xaml!

public class InverseBoolConverter : IValueConverter  // 🔥 Не abstract, не generic!
{
    // 🔥 Публичный конструктор без параметров (по умолчанию есть, если нет других)
    public InverseBoolConverter() { }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is bool b && !b;
    }
}