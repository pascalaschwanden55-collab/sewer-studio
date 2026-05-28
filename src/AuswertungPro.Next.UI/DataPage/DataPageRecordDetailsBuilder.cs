using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageRecordDetailsBuilder
{
    public static List<RecordDetailGroup> Build(
        HaltungRecord record,
        Func<string, RecordDetailItem> createItem)
    {
        var groups = new List<RecordDetailGroup>();
        var added = new HashSet<string>(StringComparer.Ordinal);
        var buckets = new Dictionary<string, List<RecordDetailItem>>(StringComparer.Ordinal)
        {
            ["Stammdaten"] = new(),
            ["Zustand & Inspektion"] = new(),
            ["Sanierung & Kosten"] = new(),
            ["Dokumente & Medien"] = new(),
            ["Weitere Angaben"] = new()
        };

        foreach (var column in FieldCatalog.ColumnOrder.Where(x => added.Add(x)))
        {
            var groupName = ResolveGroup(column);
            buckets[groupName].Add(createItem(column));
        }

        foreach (var extraField in record.Fields.Keys
                     .Where(x => !added.Contains(x))
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            buckets["Weitere Angaben"].Add(createItem(extraField));
        }

        AddGroup(groups, buckets, "Stammdaten", "Identifikation und Lage der Haltung.");
        AddGroup(groups, buckets, "Zustand & Inspektion", "Bewertung, Schaeden und Pruefresultate.");
        AddGroup(groups, buckets, "Sanierung & Kosten", "Massnahmen, Kosten und Mengenangaben.");
        AddGroup(groups, buckets, "Dokumente & Medien", "Verknuepfte Dateien, PDFs und Links.");
        AddGroup(groups, buckets, "Weitere Angaben", "Felder ohne klare Zuordnung.");

        return groups;
    }

    public static string ResolveGroup(string fieldName)
    {
        return fieldName switch
        {
            "NR" or "Haltungsname" or "Strasse" or "DN_mm" or "Rohrmaterial"
                or "Nutzungsart" or "Haltungslaenge_m" or "Inspektionsrichtung"
                or "Eigentuemer" or "FunktionHierarchisch"
                => "Stammdaten",

            "Zustandsklasse" or "VSA_Zustandsnote_D" or "VSA_Zustandsnote_S"
                or "VSA_Zustandsnote_B" or "Primaere_Schaeden" or "Pruefungsresultat"
                or "Referenzpruefung" or "Datum_Jahr" or "Ausgefuehrt_durch"
                or "Gewaesserschutz" or "Grundwasserspiegel"
                => "Zustand & Inspektion",

            "Sanieren_JaNein" or "Empfohlene_Sanierungsmassnahmen" or "Kosten"
                or "Renovierung_Inliner_Stk" or "Renovierung_Inliner_m"
                or "Anschluesse_verpressen" or "Reparatur_Manschette"
                or "Linerendmanschette_LEM"
                or "Reparatur_Kurzliner" or "Erneuerung_Neubau_m"
                or "Offen_abgeschlossen"
                => "Sanierung & Kosten",

            "Link" => "Dokumente & Medien",

            _ => "Weitere Angaben"
        };
    }

    private static void AddGroup(
        ICollection<RecordDetailGroup> groups,
        IReadOnlyDictionary<string, List<RecordDetailItem>> buckets,
        string title,
        string description)
    {
        if (!buckets.TryGetValue(title, out var items) || items.Count == 0)
            return;

        groups.Add(new RecordDetailGroup(title, description, items));
    }
}
