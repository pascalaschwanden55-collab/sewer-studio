using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public static class SanierungsMatrixNavigationTarget
{
    public static string? FromRecord(HaltungRecord? record)
    {
        var holding = (record?.GetFieldValue("Haltungsname") ?? "").Trim();
        return string.IsNullOrWhiteSpace(holding) ? null : holding;
    }

    public static SanierungMatrixRowVm? FindRow(
        IEnumerable<SanierungMatrixRowVm> rows,
        string? holding,
        HaltungRecord? targetRecord = null)
    {
        if (targetRecord is not null)
        {
            var byRecord = rows.FirstOrDefault(row => ReferenceEquals(row.Record, targetRecord));
            if (byRecord is not null)
                return byRecord;
        }

        var target = (holding ?? "").Trim();
        if (target.Length == 0)
            return null;

        return rows.FirstOrDefault(row => string.Equals(
            row.Holding,
            target,
            StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<SanierungMatrixRowVm> FilterRows(
        IEnumerable<SanierungMatrixRowVm> rows,
        string? holding,
        bool singleHoldingMode,
        HaltungRecord? targetRecord = null)
    {
        var list = rows.ToList();
        if (!singleHoldingMode)
            return list;

        var row = FindRow(list, holding, targetRecord);
        return row is null ? [] : [row];
    }
}
