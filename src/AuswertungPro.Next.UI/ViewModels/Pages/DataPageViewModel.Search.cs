using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// DataGrid-Filter und Resultat-Info: Suche ueber Haltungsname und Strasse,
// Anzahl-Anzeige im Filter-Banner.
public sealed partial class DataPageViewModel
{
    /// <summary>
    /// Filter predicate for the DataGrid's CollectionView.
    /// Matches if the Haltungsname contains the search term (either side of the pair).
    /// </summary>
    public bool MatchesSearch(HaltungRecord record)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;

        var term = SearchText.Trim();
        var haltung = record.GetFieldValue("Haltungsname") ?? "";
        // Match against full haltungsname or individual shaft numbers
        if (haltung.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;

        // Also check Strasse
        var strasse = record.GetFieldValue("Strasse") ?? "";
        if (strasse.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Updates the search result info text.
    /// </summary>
    public void UpdateSearchResultInfo(int visibleCount)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            SearchResultInfo = string.Empty;
        else
            SearchResultInfo = $"{visibleCount} von {Records.Count} Haltungen";
    }
}
