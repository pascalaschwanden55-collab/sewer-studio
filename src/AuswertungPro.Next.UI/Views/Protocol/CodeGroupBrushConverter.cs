using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Views.Protocol;

/// <summary>
/// VSA-Code -> Gruppen-Brush aus dem Theme (CodeGroup*Brush).
/// ConverterParameter "subtle" liefert die zarte Hintergrund-Variante fuer Badges.
/// </summary>
public sealed class CodeGroupBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var badge = CodeGroupBadgePolicy.Resolve(value?.ToString());
        var key = string.Equals(parameter?.ToString(), "subtle", StringComparison.OrdinalIgnoreCase)
            ? badge.SubtleBrushKey
            : badge.BrushKey;

        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
