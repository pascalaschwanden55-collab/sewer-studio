using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageDropdownOptionSets(
    ObservableCollection<string> SanierenOptions,
    ObservableCollection<string> PruefungsresultatOptions,
    ObservableCollection<string> ReferenzpruefungOptions,
    ObservableCollection<string> EmpfohleneSanierungsmassnahmenOptions,
    ObservableCollection<string> RohrmaterialOptions);

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
            // Materialien, die im Bestand stehen aber in keiner Liste: sichtbar machen,
            // statt sie beim naechsten Oeffnen der Auswahl stillschweigend zu verlieren.
            DropdownOptionList.AddIfMissing(
                options.RohrmaterialOptions,
                record.GetFieldValue(FieldKeys.PipeMaterial));

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
