using System;
using System.Globalization;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Pages;

public sealed class ChfAccountingDisplayConverter : IValueConverter
{
    private static readonly CultureInfo ChCulture = CultureInfo.GetCultureInfo("de-CH");
    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString() ?? string.Empty;
        var mode = (parameter as string)?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (!TryParseDecimal(text, out var amount))
        {
            if (string.Equals(mode, "currency", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            return text;
        }

        var abs = Math.Abs(amount).ToString("N2", ChCulture);
        if (string.Equals(mode, "currency", StringComparison.OrdinalIgnoreCase))
            return "CHF";
        if (string.Equals(mode, "amount", StringComparison.OrdinalIgnoreCase))
            return amount < 0m ? $"({abs})" : abs;

        return amount < 0m ? $"(CHF {abs})" : $"CHF {abs}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var raw = value?.ToString() ?? string.Empty;
        var text = raw.Trim();
        if (text.Length == 0)
            return string.Empty;

        var negative = text.StartsWith("(", StringComparison.Ordinal) && text.EndsWith(")", StringComparison.Ordinal);
        text = text.Trim('(', ')');
        text = text.Replace("CHF", string.Empty, StringComparison.OrdinalIgnoreCase)
                   .Replace(" ", string.Empty, StringComparison.Ordinal)
                   .Replace("'", string.Empty, StringComparison.Ordinal)
                   .Trim();

        if (!TryParseDecimal(text, out var amount))
            return raw;

        if (negative)
            amount = -Math.Abs(amount);

        return amount.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static bool TryParseDecimal(string text, out decimal amount)
    {
        if (decimal.TryParse(text, NumberStyles.Any, ChCulture, out amount))
            return true;
        if (decimal.TryParse(text, NumberStyles.Any, DeCulture, out amount))
            return true;
        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
            return true;

        amount = 0m;
        return false;
    }
}
