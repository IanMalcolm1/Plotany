using System.Globalization;

namespace Plotany.Converters;

public class GreaterThanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        if (double.TryParse(value.ToString(), out double val) &&
            double.TryParse(parameter.ToString(), out double param))
        {
            return val > param;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class LessThanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        if (double.TryParse(value.ToString(), out double val) &&
            double.TryParse(parameter.ToString(), out double param))
        {
            return val < param;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}


public class DayOfWeekAbbreviationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            return dt.DayOfWeek switch
            {
                DayOfWeek.Monday => "M",
                DayOfWeek.Tuesday => "T",
                DayOfWeek.Wednesday => "W",
                DayOfWeek.Thursday => "Th",
                DayOfWeek.Friday => "F",
                DayOfWeek.Saturday => "Sa",
                DayOfWeek.Sunday => "Su",
                _ => string.Empty
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolInverterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}