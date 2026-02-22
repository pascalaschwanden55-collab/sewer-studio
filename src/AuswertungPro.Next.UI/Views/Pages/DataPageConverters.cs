using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows;
using System.Linq;

namespace AuswertungPro.Next.UI.Views.Pages;

public sealed class CountToMarkConverter : IValueConverter
{
    public int MinCount { get; set; } = 1;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int i => i,
            ICollection c => c.Count,
            _ => 0
        };

        return count >= MinCount ? "✔" : "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class StringNotEmptyToMarkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            string s when !string.IsNullOrWhiteSpace(s) => "✔",
            TimeSpan => "✔",
            _ => ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class PhotoIndexToPathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var index = ResolveIndex(parameter);
        if (index < 0)
            return string.Empty;

        if (value is System.Collections.IList list)
        {
            if (index >= list.Count)
                return string.Empty;
            return list[index]?.ToString() ?? string.Empty;
        }

        if (value is IEnumerable<string> seq)
        {
            var path = seq.Skip(index).FirstOrDefault();
            return path ?? string.Empty;
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static int ResolveIndex(object parameter)
    {
        if (parameter is int i)
            return i;
        if (parameter is string s && int.TryParse(s, out var parsed))
            return parsed;
        return 0;
    }
}

public sealed class PhotoIndexToVisibilityConverter : IValueConverter
{
    private static readonly PhotoIndexToPathConverter PathConverter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var path = PathConverter.Convert(value, typeof(string), parameter, culture) as string;
        return string.IsNullOrWhiteSpace(path) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            string s when !string.IsNullOrWhiteSpace(s) => Visibility.Visible,
            TimeSpan => Visibility.Visible,
            _ => Visibility.Collapsed
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class AnyValueToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is null || values.Length == 0)
            return Visibility.Collapsed;

        foreach (var value in values)
        {
            switch (value)
            {
                case string s when !string.IsNullOrWhiteSpace(s):
                    return Visibility.Visible;
                case TimeSpan:
                    return Visibility.Visible;
            }
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => Array.Empty<object>();
}
