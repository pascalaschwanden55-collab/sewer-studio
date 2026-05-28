using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageSearchMatcher
{
    public static bool Matches(HaltungRecord record, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        var term = searchText.Trim();
        var haltung = record.GetFieldValue("Haltungsname") ?? string.Empty;
        if (haltung.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;

        var strasse = record.GetFieldValue("Strasse") ?? string.Empty;
        return strasse.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildResultInfo(string? searchText, int visibleCount, int totalCount)
        => string.IsNullOrWhiteSpace(searchText)
            ? string.Empty
            : $"{visibleCount} von {totalCount} Haltungen";
}
