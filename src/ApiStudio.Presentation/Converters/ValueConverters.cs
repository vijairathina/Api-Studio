using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ApiStudio.Domain.Enums;

namespace ApiStudio.Presentation.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool boolVal = false;
        if (value is bool b)
        {
            boolVal = b;
        }
        else if (value is int intVal && parameter != null && int.TryParse(parameter.ToString(), out var targetInt))
        {
            boolVal = intVal == targetInt;
        }

        if (Invert) boolVal = !boolVal;
        return boolVal ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isVisible = value is Visibility v && v == Visibility.Visible;
        return Invert ? !isVisible : isVisible;
    }
}

public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intVal && parameter != null && int.TryParse(parameter.ToString(), out var targetInt))
        {
            return intVal == targetInt ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class IntToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intVal && parameter != null && int.TryParse(parameter.ToString(), out var targetInt))
        {
            return intVal == targetInt;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null && int.TryParse(parameter.ToString(), out var targetInt))
        {
            return targetInt;
        }
        return Binding.DoNothing;
    }
}

public class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            return parameter.ToString() ?? "";
        }
        return Binding.DoNothing;
    }
}

public class MethodToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GetBrush = new((Color)ColorConverter.ConvertFromString("#16803C"));
    private static readonly SolidColorBrush PostBrush = new((Color)ColorConverter.ConvertFromString("#B7791F"));
    private static readonly SolidColorBrush PutBrush = new((Color)ColorConverter.ConvertFromString("#005EB8"));
    private static readonly SolidColorBrush PatchBrush = new((Color)ColorConverter.ConvertFromString("#6B46C1"));
    private static readonly SolidColorBrush DeleteBrush = new((Color)ColorConverter.ConvertFromString("#D92D20"));
    private static readonly SolidColorBrush DefaultBrush = new((Color)ColorConverter.ConvertFromString("#4A5568"));

    static MethodToBrushConverter()
    {
        GetBrush.Freeze();
        PostBrush.Freeze();
        PutBrush.Freeze();
        PatchBrush.Freeze();
        DeleteBrush.Freeze();
        DefaultBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var methodStr = value?.ToString()?.ToUpperInvariant() ?? "";
        return methodStr switch
        {
            "GET" => GetBrush,
            "POST" => PostBrush,
            "PUT" => PutBrush,
            "PATCH" => PatchBrush,
            "DELETE" => DeleteBrush,
            _ => DefaultBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush SuccessBrush = new((Color)ColorConverter.ConvertFromString("#16803C"));
    private static readonly SolidColorBrush RedirectBrush = new((Color)ColorConverter.ConvertFromString("#005EB8"));
    private static readonly SolidColorBrush WarningBrush = new((Color)ColorConverter.ConvertFromString("#B7791F"));
    private static readonly SolidColorBrush ErrorBrush = new((Color)ColorConverter.ConvertFromString("#D92D20"));
    private static readonly SolidColorBrush NeutralBrush = new((Color)ColorConverter.ConvertFromString("#667085"));

    static StatusToBrushConverter()
    {
        SuccessBrush.Freeze();
        RedirectBrush.Freeze();
        WarningBrush.Freeze();
        ErrorBrush.Freeze();
        NeutralBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int code)
        {
            if (code >= 200 && code < 300) return SuccessBrush;
            if (code >= 300 && code < 400) return RedirectBrush;
            if (code >= 400 && code < 500) return WarningBrush;
            if (code >= 500) return ErrorBrush;
        }
        return NeutralBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isEmpty = string.IsNullOrWhiteSpace(value as string);
        if (Invert) isEmpty = !isEmpty;
        return isEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BytesToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
        return "0 B";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
