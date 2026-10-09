using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Northpad.Core.Modules;

namespace Northpad.App.Converters;

public sealed class DestinationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        parameter?.ToString() ?? string.Empty;
}

public sealed class ThemeMatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class BoolToVisConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class IconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var data = (value as string) switch
        {
            KnownModules.Notes => "M7,3 H14 L19,8 V21 H7 Z M14,3 V8 H19 M10,12 H16 M10,16 H14",
            KnownModules.Todo => "M5,4 H19 V20 H5 Z M8,12 L11,15 L16,9",
            KnownModules.Calendar => "M5,5 H19 V20 H5 Z M5,9 H19 M9,3 V7 M15,3 V7",
            KnownModules.Reminders => "M12,4 V6 M8,8 H16 V18 H8 Z M8,12 H16",
            KnownModules.Mail => "M4,7 H20 V18 H4 Z M4,7 L12,13 L20,7",
            KnownModules.Browser => "M4,6 H20 V18 H4 Z M4,10 H20",
            KnownModules.Photos => "M5,7 H19 V18 H5 Z M8,15 L11,12 L14,16 L17,13",
            KnownModules.Maps => "M4,7 L9,5 L15,8 L20,6 V17 L15,19 L9,16 L4,18 Z",
            KnownModules.WebSearch => "M8,8 H14 V14 H8 Z M14,14 L19,19",
            KnownModules.Video => "M5,7 H15 V17 H5 Z M15,10 L20,8 V16 L15,14",
            KnownModules.Passwords => "M8,11 V9 H16 V11 H18 V19 H6 V11 Z",
            KnownModules.Drive => "M5,8 H11 L13,6 H19 V18 H5 Z",
            KnownModules.Sheets => "M5,4 H19 V20 H5 Z M5,9 H19 M5,14 H19 M12,4 V20",
            KnownModules.Translate => "M4,6 H14 V14 H4 Z M12,10 H20 V18 H10",
            KnownModules.Wallet => "M4,8 H20 V18 H4 Z M14,12 H18",
            _ => "M4,4 H20 V20 H4 Z",
        };
        return Geometry.Parse(data);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
