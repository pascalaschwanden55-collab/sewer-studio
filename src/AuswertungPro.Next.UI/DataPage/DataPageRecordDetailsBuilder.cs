using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Baut die Themen der Haltungs-Eingabefelder (Formular-Detailfenster und
/// <see cref="AuswertungPro.Next.UI.Views.Pages.Haltungsansicht.HaltungFelderDrawer"/>).
///
/// Nova-Etappe 2b, Fix-Runde 1: die Prototyp-Liste (Inventar 9.1) ist eine Mindestliste,
/// keine Ausschlussliste. Vier feste Themen mit exakter Reihenfolge — Stammdaten (17),
/// Bewertung (9), Sanierung (11), Kosten und Bemerkungen (3). Drei fachlich zugehoerige
/// Felder, die im Prototyp fehlen, sind bewusst ergaenzt: Schacht_oben/Schacht_unten direkt
/// nach der Strasse (der Haltungsname haengt an den Schaechten), das Gefaelle nach der
/// Haltungslaenge (CLAUDE.md: immer als Stammdaten-Eingabe) und Renovierung_Inliner_Stk
/// direkt nach Renovierung_Inliner_m in der Sanierung. Alle uebrigen Projektfelder (auch die
/// SIA405-Katasterfelder, NR und Primaere_Schaeden) bleiben im fuenften Thema
/// "Weitere Angaben", damit kein Feld verschwindet.
/// </summary>
public static class DataPageRecordDetailsBuilder
{
    // Reihenfolge nach docs/reviews/2026-09-06-nova/wpf-etappe-2/PROTOTYP-INVENTAR.md
    // Abschnitt 9.1 "Stammdaten (14)", ergaenzt um Schacht_oben/Schacht_unten (nach der
    // Strasse) und das Gefaelle (nach der Haltungslaenge) — Fix-Runde 1: 17 Felder.
    private static readonly string[] StammdatenReihenfolge =
    {
        FieldKeys.HoldingName, FieldKeys.Street, "Schacht_oben", "Schacht_unten",
        FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm,
        FieldKeys.ProfileType, FieldKeys.ClearWidthMm, FieldKeys.UsageType, FieldKeys.HoldingLengthMeters,
        FieldKeys.SlopePromille,
        "Inspektionsrichtung", FieldKeys.InspectionYear, FieldKeys.ConstructionYear, FieldKeys.Owner,
        FieldKeys.GeonisId, FieldKeys.CadastreObjectId
    };

    // Inventar 9.1 "Bewertung (9)".
    private static readonly string[] BewertungReihenfolge =
    {
        FieldKeys.ConditionClass, "VSA_Zustandsnote_D", "VSA_Zustandsnote_S", "VSA_Zustandsnote_B",
        "VSA_Geschaetzt", "Pruefungsresultat", "Referenzpruefung", "Gewaesserschutz", "Grundwasserspiegel"
    };

    // Inventar 9.1 "Sanierung (10)", ergaenzt um Renovierung_Inliner_Stk direkt nach
    // Renovierung_Inliner_m — Fix-Runde 1: 11 Felder.
    private static readonly string[] SanierungReihenfolge =
    {
        FieldKeys.RenovationDecision, FieldKeys.RecommendedRehabilitationMeasures, FieldKeys.LinerRenovationMeters,
        FieldKeys.LinerRenovationCount,
        FieldKeys.ConnectionsToGrout, FieldKeys.RepairSleeve, FieldKeys.LinerEndSleeve, FieldKeys.ShortLinerRepair,
        "Erneuerung_Neubau_m", FieldKeys.RehabilitationExecutor, FieldKeys.WorkflowStatus
    };

    // Inventar 9.1 "Kosten und Bemerkungen (3)".
    private static readonly string[] KostenUndBemerkungenReihenfolge =
    {
        FieldKeys.Cost, FieldKeys.Link, FieldKeys.Remarks
    };

    public static List<RecordDetailGroup> Build(
        HaltungRecord record,
        Func<string, RecordDetailItem> createItem,
        IReadOnlySet<string>? excludeFields = null)
    {
        var groups = new List<RecordDetailGroup>();
        var added = new HashSet<string>(StringComparer.Ordinal);
        bool IsExcluded(string field) => excludeFields is not null && excludeFields.Contains(field);

        var itemsByField = new Dictionary<string, RecordDetailItem>(StringComparer.Ordinal);
        // Reihenfolge aller erzeugten Felder (Katalog zuerst, dann freie Projektfelder
        // alphabetisch) — sie bleibt die Anzeigereihenfolge in "Weitere Angaben".
        var alleFelderReihenfolge = new List<string>();

        // Das Projektgefaelle ist auch bei alten Projekten ohne gespeicherten Wert
        // editierbar. Die feste Spaltenfolge fuer CSV/Excel bleibt dabei erhalten.
        foreach (var column in FieldCatalog.ColumnOrder.Append(FieldKeys.SlopePromille).Where(x => added.Add(x)))
        {
            if (IsExcluded(column)) continue;
            var item = createItem(column);
            itemsByField[column] = item;
            alleFelderReihenfolge.Add(column);
        }

        foreach (var extraField in record.Fields.Keys
                     .Where(x => !added.Contains(x))
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (IsExcluded(extraField)) continue;
            var item = createItem(extraField);
            itemsByField[extraField] = item;
            alleFelderReihenfolge.Add(extraField);
        }

        WireSanierungSichtbarkeit(itemsByField);

        var zugewiesen = new HashSet<string>(StringComparer.Ordinal);

        AddThemenGruppe(groups, itemsByField, zugewiesen, "Stammdaten",
            "Identifikation und Lage der Haltung.", RecordDetailGroupKind.MasterData, StammdatenReihenfolge);
        AddThemenGruppe(groups, itemsByField, zugewiesen, "Bewertung",
            "Zustandsklasse, Zustandsnoten und Prüfresultate.", RecordDetailGroupKind.Rating, BewertungReihenfolge);
        AddThemenGruppe(groups, itemsByField, zugewiesen, "Sanierung",
            "Massnahmen und Mengenangaben zur Sanierung.", RecordDetailGroupKind.Renovation, SanierungReihenfolge);
        AddThemenGruppe(groups, itemsByField, zugewiesen, "Kosten und Bemerkungen",
            "Kosten, Video-Link und Bemerkungen.", RecordDetailGroupKind.CostsRemarks, KostenUndBemerkungenReihenfolge);

        var weitereItems = alleFelderReihenfolge
            .Where(f => !zugewiesen.Contains(f))
            .Select(f => itemsByField[f])
            .ToList();
        if (weitereItems.Count > 0)
            groups.Add(new RecordDetailGroup(
                "Weitere Angaben", "Felder ohne klare Zuordnung.", weitereItems, RecordDetailGroupKind.Additional));

        return groups;
    }

    /// <summary>
    /// Liefert den Thementitel eines Feldnamens. Ein unbekanntes Feld liefert
    /// "Weitere Angaben" — dort landen auch alle Projektfelder, die nicht in einer der
    /// vier festen Themenlisten stehen.
    /// </summary>
    public static string ResolveGroup(string fieldName)
    {
        if (Array.IndexOf(StammdatenReihenfolge, fieldName) >= 0) return "Stammdaten";
        if (Array.IndexOf(BewertungReihenfolge, fieldName) >= 0) return "Bewertung";
        if (Array.IndexOf(SanierungReihenfolge, fieldName) >= 0) return "Sanierung";
        if (Array.IndexOf(KostenUndBemerkungenReihenfolge, fieldName) >= 0) return "Kosten und Bemerkungen";
        return "Weitere Angaben";
    }

    /// <summary>
    /// Fuegt eine feste Themen-Gruppe in der vorgegebenen Feldreihenfolge hinzu. Ein Feld,
    /// das (etwa durch <c>excludeFields</c>) keinen Eintrag in <paramref name="itemsByField"/>
    /// hat, wird uebersprungen statt eine Luecke zu erzeugen.
    /// </summary>
    private static void AddThemenGruppe(
        ICollection<RecordDetailGroup> groups,
        IReadOnlyDictionary<string, RecordDetailItem> itemsByField,
        HashSet<string> zugewiesen,
        string title,
        string description,
        RecordDetailGroupKind kind,
        IReadOnlyList<string> reihenfolge)
    {
        var items = new List<RecordDetailItem>(reihenfolge.Count);
        foreach (var feld in reihenfolge)
        {
            if (!itemsByField.TryGetValue(feld, out var item)) continue;
            items.Add(item);
            zugewiesen.Add(feld);
        }

        if (items.Count > 0)
            groups.Add(new RecordDetailGroup(title, description, items, kind));
    }

    // Folgefelder der Sanierungs-Gruppe: nur sinnvoll, wenn ueberhaupt saniert wird.
    // Diese Liste ist bewusst unabhaengig von der Themen-Zuordnung: Auch das in
    // "Kosten und Bemerkungen" stehende Feld "Kosten" bleibt ausgeblendet, solange
    // "Sanieren" = Nein ist — obwohl es nicht mehr im selben Thema wie "Sanieren_JaNein" steht.
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
