using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// 1:1 Port der Felddefinitionen aus Models.ps1 (AuswertungPro v2.1.0).
/// </summary>
public static class FieldCatalog
{
    public const string AppVersion = "0.1.0";

    public static readonly IReadOnlyList<string> ColumnOrder = new ReadOnlyCollection<string>(new List<string>
    {
        "NR",
        FieldKeys.HoldingName,
        FieldKeys.Street,
        FieldKeys.PipeMaterial,
        FieldKeys.NominalDiameterMm,
        FieldKeys.UsageType,
        FieldKeys.HoldingLengthMeters,
        "Inspektionsrichtung",
        "Primaere_Schaeden",
        FieldKeys.ConditionClass,
        "VSA_Zustandsnote_D",
        "Pruefungsresultat",
        "Referenzpruefung",
        FieldKeys.RenovationDecision,
        FieldKeys.RecommendedRehabilitationMeasures,
        FieldKeys.Cost,
        FieldKeys.Owner,
        FieldKeys.RehabilitationExecutor,
        FieldKeys.Remarks,
        FieldKeys.Link,
        FieldKeys.LinerRenovationCount,
        FieldKeys.LinerRenovationMeters,
        FieldKeys.ConnectionsToGrout,
        FieldKeys.RepairSleeve,
        FieldKeys.LinerEndSleeve,
        FieldKeys.ShortLinerRepair,
        "Erneuerung_Neubau_m",
        FieldKeys.WorkflowStatus,
        FieldKeys.InspectionYear,
        "VSA_Zustandsnote_S",
        "VSA_Zustandsnote_B",
        "Gewaesserschutz",
        "Grundwasserspiegel",
        "FunktionHierarchisch"
    });

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ComboItems =
        new ReadOnlyDictionary<string, IReadOnlyList<string>>(new Dictionary<string, IReadOnlyList<string>>
        {
            // Rohrmaterial ist ein Auswahlfeld: Was hier fehlt, zeigt das Feld leer an, auch wenn
            // der Wert in den Daten steht. Die Liste muss darum zu dem passen, was
            // XtfValueNormalizer.NormalizeSiaMaterial aus SIA405-Katastern liefert.
            // Ergaenzt 2026-07-17: Epoxydharz, Faserzement, Ton — kommen in echten IKAS-Exporten vor.
            [FieldKeys.PipeMaterial] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "PVC", "PE", "PP", "GFK", "Beton", "Steinzeug", "Guss", "Hartpolyethylen",
                "Zement", "Polyvinylchlorid", "Polyethylen", "Polypropylen", "Normalbeton", "Glasfaser",
                "Epoxydharz", "Faserzement", "Ton"
            }),
            [FieldKeys.UsageType] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "Schmutzwasser", "Regenwasser", "Mischabwasser"
            }),
            ["Inspektionsrichtung"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "In Fliessrichtung", "Gegen Fliessrichtung"
            }),
            [FieldKeys.ConditionClass] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "0", "1", "2", "3", "4", "5"
            }),
            [FieldKeys.RenovationDecision] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "Ja", "Nein"
            }),
            ["Referenzpruefung"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "Ja", "Nein"
            }),
            [FieldKeys.RehabilitationExecutor] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "Kanalsanierer", "Baumeister", "Gartenbauer"
            }),
            [FieldKeys.WorkflowStatus] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "offen", "abgeschlossen"
            }),
            ["Gewaesserschutz"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "S", "Au", "Zu", "Ao"
            }),
            ["Grundwasserspiegel"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "unterhalb", "oberhalb", "unbekannt"
            }),
            ["FunktionHierarchisch"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "PAA.Sammelkanal", "PAA.Hauptsammelkanal", "PAA.Hauptsammelkanal_regional",
                "PAA.Liegenschaftsentwaesserung", "PAA.Sanierungsleitung",
                "PAA.Strassenentwaesserung", "PAA.Gewaesser"
            })
        });

    public static readonly IReadOnlyDictionary<string, FieldDefinition> Definitions =
        new ReadOnlyDictionary<string, FieldDefinition>(new Dictionary<string, FieldDefinition>
        {
            ["NR"] = new("NR", "NR.", FieldType.Int),
            [FieldKeys.HoldingName] = new(FieldKeys.HoldingName, "Haltungsname (ID)", FieldType.Text),
            [FieldKeys.Street] = new(FieldKeys.Street, "Strasse", FieldType.Text),
            [FieldKeys.PipeMaterial] = new(FieldKeys.PipeMaterial, "Rohrmaterial", FieldType.Combo, ComboItems[FieldKeys.PipeMaterial]),
            [FieldKeys.NominalDiameterMm] = new(FieldKeys.NominalDiameterMm, "DN mm", FieldType.Int),
            [FieldKeys.UsageType] = new(FieldKeys.UsageType, "Nutzungsart", FieldType.Combo, ComboItems[FieldKeys.UsageType]),
            [FieldKeys.HoldingLengthMeters] = new(FieldKeys.HoldingLengthMeters, "Haltungslänge m", FieldType.Decimal),
            ["Inspektionsrichtung"] = new("Inspektionsrichtung", "Inspektionsrichtung", FieldType.Combo, ComboItems["Inspektionsrichtung"]),
            ["Primaere_Schaeden"] = new("Primaere_Schaeden", "Primäre Schäden", FieldType.Multiline),
            [FieldKeys.ConditionClass] = new(FieldKeys.ConditionClass, "Zustandsklasse", FieldType.Combo, ComboItems[FieldKeys.ConditionClass]),
            ["VSA_Zustandsnote_D"] = new("VSA_Zustandsnote_D", "VSA-Zustandsnote D", FieldType.Decimal),
            ["Pruefungsresultat"] = new("Pruefungsresultat", "Prüfungsresultat", FieldType.Text),
            ["Referenzpruefung"] = new("Referenzpruefung", "Referenzpruefung", FieldType.Combo, ComboItems["Referenzpruefung"]),
            [FieldKeys.RenovationDecision] = new(FieldKeys.RenovationDecision, "Sanieren Ja/Nein", FieldType.Combo, ComboItems[FieldKeys.RenovationDecision]),
            [FieldKeys.RecommendedRehabilitationMeasures] = new(FieldKeys.RecommendedRehabilitationMeasures, "Empfohlene Sanierungsmassnahmen", FieldType.Multiline),
            [FieldKeys.Cost] = new(FieldKeys.Cost, "Kosten", FieldType.Decimal),
            [FieldKeys.RehabilitationExecutor] = new(FieldKeys.RehabilitationExecutor, "Ausgefuehrt durch", FieldType.Combo, ComboItems[FieldKeys.RehabilitationExecutor]),
            [FieldKeys.Owner] = new(FieldKeys.Owner, "Eigentümer", FieldType.Text),
            [FieldKeys.Remarks] = new(FieldKeys.Remarks, "Bemerkungen", FieldType.Multiline),
            [FieldKeys.Link] = new(FieldKeys.Link, "Link", FieldType.Text),
            [FieldKeys.LinerRenovationCount] = new(FieldKeys.LinerRenovationCount, "Renovierung Inliner Stk.", FieldType.Int),
            [FieldKeys.LinerRenovationMeters] = new(FieldKeys.LinerRenovationMeters, "Renovierung Inliner m", FieldType.Decimal),
            [FieldKeys.ConnectionsToGrout] = new(FieldKeys.ConnectionsToGrout, "Anschlüsse verpressen", FieldType.Int),
            [FieldKeys.RepairSleeve] = new(FieldKeys.RepairSleeve, "Reparatur Manschette", FieldType.Int),
            [FieldKeys.LinerEndSleeve] = new(FieldKeys.LinerEndSleeve, "Linerendmanschette LEM", FieldType.Int),
            [FieldKeys.ShortLinerRepair] = new(FieldKeys.ShortLinerRepair, "Reparatur Kurzliner", FieldType.Int),
            ["Erneuerung_Neubau_m"] = new("Erneuerung_Neubau_m", "Erneuerung Neubau m", FieldType.Decimal),
            [FieldKeys.WorkflowStatus] = new(FieldKeys.WorkflowStatus, "offen/abgeschlossen", FieldType.Combo, ComboItems[FieldKeys.WorkflowStatus]),
            [FieldKeys.InspectionYear] = new(FieldKeys.InspectionYear, "Datum/Jahr", FieldType.Text),
            ["VSA_Zustandsnote_S"] = new("VSA_Zustandsnote_S", "VSA-Zustandsnote S", FieldType.Decimal),
            ["VSA_Zustandsnote_B"] = new("VSA_Zustandsnote_B", "VSA-Zustandsnote B", FieldType.Decimal),
            ["Gewaesserschutz"] = new("Gewaesserschutz", "Gewässerschutz", FieldType.Combo, ComboItems["Gewaesserschutz"]),
            ["Grundwasserspiegel"] = new("Grundwasserspiegel", "Grundwasserspiegel", FieldType.Combo, ComboItems["Grundwasserspiegel"]),
            ["FunktionHierarchisch"] = new("FunktionHierarchisch", "Funktionale Hierarchie", FieldType.Combo, ComboItems["FunktionHierarchisch"])
        });

    public static FieldDefinition Get(string fieldName)
        => Definitions.TryGetValue(fieldName, out var def)
            ? def
            : new FieldDefinition(fieldName, fieldName, FieldType.Text);

    public static IReadOnlyList<string> GetComboItems(string fieldName)
        => ComboItems.TryGetValue(fieldName, out var items) ? items : Array.Empty<string>();
}
