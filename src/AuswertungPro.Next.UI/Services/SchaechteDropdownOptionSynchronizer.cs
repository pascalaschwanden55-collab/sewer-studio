using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

public sealed record SchaechteDropdownOptionSets(
    ObservableCollection<string> SanierenOptions,
    ObservableCollection<string> PruefungsresultatOptions,
    ObservableCollection<string> ReferenzpruefungOptions,
    ObservableCollection<string> AusgefuehrtDurchOptions);

public static class SchaechteDropdownOptionSynchronizer
{
    public static void SyncFromRecords(
        IEnumerable<SchachtRecord> records,
        SchaechteDropdownOptionSets options)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var record in records)
        {
            DropdownOptionList.AddIfMissing(options.SanierenOptions, ResolveFieldValue(record, "sanieren"));
            DropdownOptionList.AddIfMissing(options.PruefungsresultatOptions, ResolveFieldValue(record, "pruefungsresultat"));
            DropdownOptionList.AddIfMissing(options.ReferenzpruefungOptions, ResolveFieldValue(record, "referenzpruefung"));
            DropdownOptionList.AddIfMissing(options.AusgefuehrtDurchOptions, ResolveFieldValue(record, "ausgefuehrt_durch"));
        }
    }

    private static string ResolveFieldValue(SchachtRecord record, string logicalField)
    {
        foreach (var kvp in record.Fields)
        {
            var normalized = NormalizeKey(kvp.Key);
            if (logicalField == "sanieren" && normalized.Contains("sanieren", StringComparison.Ordinal))
                return kvp.Value ?? "";
            if (logicalField == "pruefungsresultat" &&
                (normalized.Contains("pruefung", StringComparison.Ordinal)
                 || normalized.Contains("dichtheit", StringComparison.Ordinal)
                 || normalized.Contains("dichtigkeit", StringComparison.Ordinal)))
                return kvp.Value ?? "";
            if (logicalField == "referenzpruefung" &&
                normalized.Contains("referenz", StringComparison.Ordinal)
                && normalized.Contains("pruefung", StringComparison.Ordinal))
                return kvp.Value ?? "";
            if (logicalField == "ausgefuehrt_durch" &&
                (normalized.Contains("ausgefuehrt", StringComparison.Ordinal)
                 || normalized.Contains("ausgefuhrt", StringComparison.Ordinal))
                && normalized.Contains("durch", StringComparison.Ordinal))
                return kvp.Value ?? "";
        }

        return "";
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("\u00e4", "ae", StringComparison.Ordinal)
            .Replace("\u00f6", "oe", StringComparison.Ordinal)
            .Replace("\u00fc", "ue", StringComparison.Ordinal)
            .Replace("\u00df", "ss", StringComparison.Ordinal)
            .Replace("\u00c3\u00a4", "ae", StringComparison.Ordinal)
            .Replace("\u00c3\u00b6", "oe", StringComparison.Ordinal)
            .Replace("\u00c3\u00bc", "ue", StringComparison.Ordinal)
            .Replace("\u00c3\u0178", "ss", StringComparison.Ordinal)
            .Replace("\u00e3\u00a4", "ae", StringComparison.Ordinal)
            .Replace("\u00e3\u00b6", "oe", StringComparison.Ordinal)
            .Replace("\u00e3\u00bc", "ue", StringComparison.Ordinal)
            .Replace("\u00e3\u0178", "ss", StringComparison.Ordinal)
            .Replace("\u00c3\u0192\u00c2\u00a4", "ae", StringComparison.Ordinal)
            .Replace("\u00c3\u0192\u00c2\u00b6", "oe", StringComparison.Ordinal)
            .Replace("\u00c3\u0192\u00c2\u00bc", "ue", StringComparison.Ordinal)
            .Replace("\u00c3\u0192\u00c5\u00b8", "ss", StringComparison.Ordinal)
            .Replace("\u00e3\u0192\u00e2\u00a4", "ae", StringComparison.Ordinal)
            .Replace("\u00e3\u0192\u00e2\u00b6", "oe", StringComparison.Ordinal)
            .Replace("\u00e3\u0192\u00e2\u00bc", "ue", StringComparison.Ordinal)
            .Replace("\u00e3\u0192\u00e5\u00b8", "ss", StringComparison.Ordinal);
    }
}
