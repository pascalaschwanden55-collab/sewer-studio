using System;
using System.Globalization;
using System.Windows.Data;
using AuswertungPro.Next.Application.Export;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Bindet ein <see cref="DistributionVariant"/> an den IsChecked-Zustand eines
/// Umschalt-Buttons. ConverterParameter = Zielvariante ("Normal"/"Sanierung").
/// </summary>
public sealed class DistributionVariantToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DistributionVariant v
           && parameter is string p
           && string.Equals(v.ToString(), p, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
           && parameter is string p
           && Enum.TryParse<DistributionVariant>(p, true, out var v)
            ? v
            : Binding.DoNothing;
}
