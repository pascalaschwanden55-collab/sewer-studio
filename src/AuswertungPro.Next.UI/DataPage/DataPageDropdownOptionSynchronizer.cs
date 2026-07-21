using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageDropdownOptionSets(
    ObservableCollection<string> SanierenOptions,
    ObservableCollection<string> PruefungsresultatOptions,
    ObservableCollection<string> ReferenzpruefungOptions,
    ObservableCollection<string> EmpfohleneSanierungsmassnahmenOptions);

public static class DataPageDropdownOptionSynchronizer
{
    public static void SyncFromRecords(
        IEnumerable<HaltungRecord> records,
        DataPageDropdownOptionSets options)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var record in records)
        {
            DropdownOptionList.AddIfMissing(
                options.SanierenOptions,
                record.GetFieldValue("Sanieren_JaNein"));
            DropdownOptionList.AddIfMissing(
                options.PruefungsresultatOptions,
                record.GetFieldValue("Pruefungsresultat"));
            DropdownOptionList.AddIfMissing(
                options.ReferenzpruefungOptions,
                record.GetFieldValue("Referenzpruefung"));

            foreach (var entry in ParseRecommendedTemplates(
                         record.GetFieldValue("Empfohlene_Sanierungsmassnahmen")))
            {
                DropdownOptionList.AddIfMissing(
                    options.EmpfohleneSanierungsmassnahmenOptions,
                    entry);
            }
        }
    }

    public static IReadOnlyList<string> ParseRecommendedTemplates(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw.Split(
                new[] { '\r', '\n', ';', ',', '|' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(DataPageSanierungCostMapper.NormalizeRecommendationEntry)
            .Where(entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
