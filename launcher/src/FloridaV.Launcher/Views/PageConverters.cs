using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FloridaV.Launcher.Models;

namespace FloridaV.Launcher.Views;

/// <summary>
/// AppPage -> Visibility. ConverterParameter = имя страницы ("Play"/"News"/"Settings").
/// Используется, чтобы показывать только активную страницу контента.
/// </summary>
public sealed class PageVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AppPage page && parameter is string target &&
            Enum.TryParse<AppPage>(target, out var wanted))
        {
            return page == wanted ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// AppPage -> "Active"/null для Tag кнопки NavButton (подсветка активного пункта меню).
/// ConverterParameter = имя страницы.
/// </summary>
public sealed class PageActiveTagConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AppPage page && parameter is string target &&
            Enum.TryParse<AppPage>(target, out var wanted) && page == wanted)
        {
            return "Active";
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
