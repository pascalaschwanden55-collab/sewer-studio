using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

public static class SchaechteSearchMatcher
{
    public static bool Matches(SchachtRecord record, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        var term = searchText.Trim();
        if (term.Length == 0)
            return true;

        return record.Fields.Any(kvp =>
            (!string.IsNullOrWhiteSpace(kvp.Key) && kvp.Key.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(kvp.Value) && kvp.Value.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    public static string BuildResultInfo(string? searchText, int visibleCount, int totalCount)
        => string.IsNullOrWhiteSpace(searchText)
            ? string.Empty
            : $"{visibleCount} von {totalCount} Schaechten";
}
