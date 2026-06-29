using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public static class BuilderPageFilterSummaryBuilder
{
    public static string Build(
        BuilderPageFilterCriteria criteria,
        int filteredRowsCount,
        int totalRows)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var parts = new List<string>();

        AddFilterPart(parts, "Eigentuemer", criteria.Owner);
        AddFilterPart(parts, "Ausgefuehrt durch", criteria.ExecutedBy);
        AddFilterPart(parts, "Sanieren", criteria.Sanieren);
        AddFilterPart(parts, "Material", criteria.Material);
        AddFilterPart(parts, "Status", criteria.Status);
        AddFilterPart(parts, "Jahr", criteria.Year);

        if (criteria.OnlyWithCost)
        {
            parts.Add("nur mit Kosten");
        }

        if (criteria.OnlyWithMeasures)
        {
            parts.Add("nur mit Massnahmen");
        }

        var search = (criteria.Search ?? "").Trim();
        if (search.Length > 0)
        {
            parts.Add($"Suche='{search}'");
        }

        parts.Add($"Treffer={filteredRowsCount}/{totalRows}");
        return string.Join(" | ", parts);
    }

    private static void AddFilterPart(List<string> parts, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Equals(BuilderPageRowFilter.AllFilterLabel, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        parts.Add($"{label}={value}");
    }
}
