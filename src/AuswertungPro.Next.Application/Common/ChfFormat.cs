using System.Globalization;

namespace AuswertungPro.Next.Application.Common;

public static class ChfFormat
{
    private static readonly NumberFormatInfo MoneyFormat = new()
    {
        NumberDecimalSeparator = ".",
        NumberGroupSeparator = "'",
        NumberDecimalDigits = 2
    };

    public static string Money(decimal value, string currency = "CHF")
    {
        var cur = string.IsNullOrWhiteSpace(currency) ? "CHF" : currency.Trim();
        return value.ToString("N2", MoneyFormat) + " " + cur;
    }
}
