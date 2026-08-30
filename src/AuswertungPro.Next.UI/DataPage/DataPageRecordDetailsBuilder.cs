using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageRecordDetailsBuilder
{
    public static List<RecordDetailGroup> Build(
        HaltungRecord record,
        Func<string, RecordDetailItem> createItem,
        IReadOnlySet<string>? excludeFields = null)
    {
        var groups = new List<RecordDetailGroup>();
        var added = new HashSet<string>(StringComparer.Ordinal);
        bool IsExcluded(string field) => excludeFields is not null && excludeFields.Contains(field);
        var buckets = new Dictionary<string, List<RecordDetailItem>>(StringComparer.Ordinal)
        {
            ["Stammdaten"] = new(),
            ["Zustand & Inspektion"] = new(),
            ["Sanierung & Kosten"] = new(),
            ["Dokumente & Medien"] = new(),
            ["Weitere Angaben"] = new()
        };

        var itemsByField = new Dictionary<string, RecordDetailItem>(StringComparer.Ordinal);

        foreach (var column in FieldCatalog.ColumnOrder.Where(x => added.Add(x)))
        {
            if (IsExcluded(column)) continue;
            var groupName = ResolveGroup(column);
            var item = createItem(column);
            itemsByField[column] = item;
            buckets[groupName].Add(item);
        }

        foreach (var extraField in record.Fields.Keys
                     .Where(x => !added.Contains(x))
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (IsExcluded(extraField)) continue;
            var item = createItem(extraField);
            itemsByField[extraField] = item;
            // Auch freie Projektfelder laufen durch die Gruppenregel; alles
            // Unbekannte liefert sie weiterhin als "Weitere Angaben" zurueck.
            buckets[ResolveGroup(extraField)].Add(item);
        }

        WireSanierungSichtbarkeit(itemsByField);

        AddGroup(groups, buckets, "Stammdaten", "Identifikation und Lage der Haltung.", RecordDetailGroupKind.MasterData);
        AddGroup(groups, buckets, "Zustand & Inspektion", "Bewertung, Schaeden und Pruefresultate.", RecordDetailGroupKind.Condition);
        AddGroup(groups, buckets, "Sanierung & Kosten", "Massnahmen, Kosten und Mengenangaben.", RecordDetailGroupKind.RenovationCosts);
        AddGroup(groups, buckets, "Dokumente & Medien", "Verknuepfte Dateien, PDFs und Links.", RecordDetailGroupKind.Documents);
        AddGroup(groups, buckets, "Weitere Angaben", "Felder ohne klare Zuordnung.", RecordDetailGroupKind.Additional);

        return groups;
    }

    public static string ResolveGroup(string fieldName)
    {
        return fieldName switch
        {
            "NR" or "Haltungsname" or "Strasse" or "DN_mm" or "Rohrmaterial"
                or "Nutzungsart" or "Haltungslaenge_m" or "Inspektionsrichtung"
                or "Eigentuemer" or "FunktionHierarchisch"
                // Anfangs- und Endschacht stehen nicht im Feldkatalog, gehoeren
                // fachlich aber zu den Stammdaten der Haltung.
                or "Schacht_oben" or "Schacht_unten"
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
        string description,
        RecordDetailGroupKind kind)
    {
        if (!buckets.TryGetValue(title, out var items) || items.Count == 0)
            return;

        groups.Add(new RecordDetailGroup(title, description, items, kind));
    }

    // Folgefelder der Sanierungs-Gruppe: nur sinnvoll, wenn ueberhaupt saniert wird.
    private static readonly string[] SanierungFolgeFelder =
    {
        "Empfohlene_Sanierungsmassnahmen", "Kosten",
        "Renovierung_Inliner_Stk", "Renovierung_Inliner_m",
        "Anschluesse_verpressen", "Reparatur_Manschette", "Linerendmanschette_LEM",
        "Reparatur_Kurzliner", "Erneuerung_Neubau_m", "Offen_abgeschlossen"
    };

    /// <summary>
    /// Blendet die Sanierungs-Folgefelder aus, solange "Sanieren = Nein" gewaehlt ist — nur das
    /// Feld "Sanieren_JaNein" bleibt dann sichtbar. Reagiert live auf Aenderungen des Feldes.
    /// </summary>
    internal static void WireSanierungSichtbarkeit(IReadOnlyDictionary<string, RecordDetailItem> itemsByField)
    {
        if (!itemsByField.TryGetValue("Sanieren_JaNein", out var sanieren))
            return;

        var folge = SanierungFolgeFelder
            .Where(itemsByField.ContainsKey)
            .Select(f => itemsByField[f])
            .ToList();
        if (folge.Count == 0)
            return;

        void Apply()
        {
            var sichtbar = !string.Equals(sanieren.Value?.Trim(), "Nein", StringComparison.OrdinalIgnoreCase);
            foreach (var item in folge)
                item.IsVisible = sichtbar;
        }

        Apply();
        sanieren.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RecordDetailItem.Value))
                Apply();
        };
    }
}
