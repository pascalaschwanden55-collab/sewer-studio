using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Gemeinsamer Filterzustand der Haltungsseite. Suche, Filterchips und ein
/// Dashboard-Startfilter gelten gleichzeitig und werden mit UND verknuepft.
/// </summary>
public sealed record DataPageCombinedFilter(
    string? SearchText,
    DataPageFilter ChipFilter,
    DataPageStartFilter? StartFilter)
{
    public static readonly DataPageCombinedFilter Aus = new(null, DataPageFilter.Aus, null);

    public bool IstAktiv
        => !string.IsNullOrWhiteSpace(SearchText)
           || ChipFilter.IstAktiv
           || StartFilter is not null;

    public bool Passt(HaltungRecord? record)
    {
        if (record is null)
            return false;

        return DataPageSearchMatcher.Matches(record, SearchText)
               && ChipFilter.Passt(record)
               && (StartFilter?.Matches(record) ?? true);
    }

    public DataPageCombinedFilter WithSearchText(string? searchText)
        => this with { SearchText = searchText };

    public DataPageCombinedFilter WithChipFilter(DataPageFilter chipFilter)
        => this with { ChipFilter = chipFilter };

    public DataPageCombinedFilter WithStartFilter(DataPageStartFilter? startFilter)
        => this with { StartFilter = startFilter };

    public DataPageCombinedFilter WithoutStartFilter()
        => this with { StartFilter = null };
}
