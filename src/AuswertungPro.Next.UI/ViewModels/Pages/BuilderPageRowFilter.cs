using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed record BuilderPageFilterCriteria(
    string? Owner,
    string? ExecutedBy,
    string? Sanieren,
    string? Material,
    string? Status,
    string? Year,
    string? Search,
    bool OnlyWithCost,
    bool OnlyWithMeasures);

public static class BuilderPageRowFilter
{
    public const string AllFilterLabel = "Alle";

    public static List<DruckcenterRowVm> Apply(
        IEnumerable<DruckcenterRowVm> rows,
        BuilderPageFilterCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(criteria);

        IEnumerable<DruckcenterRowVm> query = rows;

        query = ApplyComboFilter(query, criteria.Owner, row => row.Owner);
        query = ApplyComboFilter(query, criteria.ExecutedBy, row => row.ExecutedBy);
        query = ApplyComboFilter(query, criteria.Sanieren, row => row.Sanieren);
        query = ApplyComboFilter(query, criteria.Material, row => row.Material);
        query = ApplyComboFilter(query, criteria.Status, row => row.Status);
        query = ApplyComboFilter(query, criteria.Year, row => row.Year);

        var search = (criteria.Search ?? "").Trim();
        if (search.Length > 0)
        {
            query = query.Where(row => MatchesSearch(row, search));
        }

        if (criteria.OnlyWithCost)
        {
            query = query.Where(row => row.NetCost > 0m);
        }

        if (criteria.OnlyWithMeasures)
        {
            query = query.Where(row => row.HasMeasures);
        }

        return query
            .OrderBy(row => string.IsNullOrWhiteSpace(row.ExecutedBy) ? 1 : 0)
            .ThenBy(row => row.ExecutedBy, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Owner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Holding, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<DruckcenterRowVm> ApplyComboFilter(
        IEnumerable<DruckcenterRowVm> query,
        string? selected,
        Func<DruckcenterRowVm, string> selector)
    {
        if (string.IsNullOrWhiteSpace(selected) || selected.Equals(AllFilterLabel, StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        return query.Where(row => string.Equals(selector(row), selected, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesSearch(DruckcenterRowVm row, string search)
        => row.Holding.Contains(search, StringComparison.OrdinalIgnoreCase) ||
           row.Owner.Contains(search, StringComparison.OrdinalIgnoreCase) ||
           row.ExecutedBy.Contains(search, StringComparison.OrdinalIgnoreCase) ||
           row.Street.Contains(search, StringComparison.OrdinalIgnoreCase) ||
           row.Material.Contains(search, StringComparison.OrdinalIgnoreCase) ||
           row.Status.Contains(search, StringComparison.OrdinalIgnoreCase) ||
           row.Sanieren.Contains(search, StringComparison.OrdinalIgnoreCase) ||
           row.Zustand.Contains(search, StringComparison.OrdinalIgnoreCase) ||
           row.MeasuresPreview.Contains(search, StringComparison.OrdinalIgnoreCase);
}
