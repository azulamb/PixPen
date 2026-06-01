using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PixPen.Services;

namespace PixPen;

[ValueConversion(typeof(object), typeof(bool))]
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>#AARRGGBB 文字列 ↔ WPF Color の相互変換</summary>
[ValueConversion(typeof(string), typeof(Color))]
public class StringToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ColorHelper.FromArgbHex(value as string ?? "#FF000000");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Color c ? ColorHelper.ToArgbHex(c) : "#FF000000";
}
