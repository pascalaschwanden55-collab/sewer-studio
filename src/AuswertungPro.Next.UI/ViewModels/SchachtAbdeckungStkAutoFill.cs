using System;
using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels;

internal static class SchachtAbdeckungStkAutoFill
{
    public const string FieldName = "Abdeckung Stk.";
    private const string RahmenDeckelMeasureId = "SCHACHT_RAHMEN_DECKEL";

    public static bool TryApplyForMeasure(SchachtRecord record, string? measureId, string? measureName)
    {
        if (!IsRahmenDeckelMeasure(measureId, measureName))
            return false;

        return TrySetDefault(record);
    }

    public static bool IsRahmenDeckelMeasure(string? measureId, string? measureName)
    {
        if (string.Equals(measureId?.Trim(), RahmenDeckelMeasureId, StringComparison.OrdinalIgnoreCase))
            return true;

        var normalized = Normalize(measureName);
        return normalized.Contains("rahmen", StringComparison.Ordinal)
               && normalized.Contains("deckel", StringComparison.Ordinal)
               && (normalized.Contains("ersetzen", StringComparison.Ordinal)
                   || normalized.Contains("ersatz", StringComparison.Ordinal));
    }

    private static bool TrySetDefault(SchachtRecord record)
    {
        var current = record.GetFieldValue(FieldName);
        if (!ShouldReplace(current))
            return false;

        return record.SetFieldValue(FieldName, "1") == FeldSchreibErgebnis.Geschrieben;
    }

    private static bool ShouldReplace(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return true;

        return TryParseDecimal(text, out var parsed) && parsed <= 0m;
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
               || decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("de-CH"), out value)
               || decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out value);
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal)
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("-", " ", StringComparison.Ordinal);
}
