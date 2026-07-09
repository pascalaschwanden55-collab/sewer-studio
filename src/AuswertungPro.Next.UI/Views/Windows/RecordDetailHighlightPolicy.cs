namespace AuswertungPro.Next.UI.Views.Windows;

public static class RecordDetailHighlightPolicy
{
    public static RecordDetailHighlightKind Resolve(string? fieldName)
    {
        var normalized = Normalize(fieldName);
        if (IsSanierenField(normalized))
            return RecordDetailHighlightKind.Sanieren;

        if (IsAusgefuehrtDurchField(normalized))
            return RecordDetailHighlightKind.AusgefuehrtDurch;

        return RecordDetailHighlightKind.None;
    }

    private static bool IsSanierenField(string normalized)
    {
        var compact = normalized.Replace("/", " ", StringComparison.Ordinal);
        while (compact.Contains("  ", StringComparison.Ordinal))
            compact = compact.Replace("  ", " ", StringComparison.Ordinal);

        return compact.Equals("ja nein", StringComparison.Ordinal)
               || (compact.Contains("sanieren", StringComparison.Ordinal)
                   && (compact.Contains("ja", StringComparison.Ordinal)
                       || compact.Contains("nein", StringComparison.Ordinal)));
    }

    private static bool IsAusgefuehrtDurchField(string normalized)
        => (normalized.Contains("ausgefuehrt", StringComparison.Ordinal)
            || normalized.Contains("ausgefuhrt", StringComparison.Ordinal)
            || normalized.Contains("sanieren", StringComparison.Ordinal)
            || normalized.Contains("sanierung", StringComparison.Ordinal))
           && normalized.Contains("durch", StringComparison.Ordinal);

    private static string Normalize(string? value)
        => (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal)
            .Replace("Ã¤", "ae", StringComparison.Ordinal)
            .Replace("Ã¶", "oe", StringComparison.Ordinal)
            .Replace("Ã¼", "ue", StringComparison.Ordinal)
            .Replace("ÃŸ", "ss", StringComparison.Ordinal)
            .Replace("_", " ", StringComparison.Ordinal);
}
