using System.Globalization;
using System.Windows;
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

/// <summary>
/// 2 つの Color が一致するとき Visibility.Visible を返す。
/// スウォッチの選択状態インジケーターに使用。
/// </summary>
public class ColorMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is Color c1 && values[1] is Color c2)
            return c1 == c2 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
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
