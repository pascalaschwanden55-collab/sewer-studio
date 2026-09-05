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
        // Profilform und beide Innenmasse stehen zusammen wie in der GEONIS-Maske.
        // Bei runden Rohren sind Hoehe und Breite gleich, bei Ei-, Maul- und
        // Rechteckprofilen koennen sie verschieden sein.
        FieldKeys.ProfileType,
        FieldKeys.ClearWidthMm,
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
        // Markiert Haltungen, deren Note aus einem Standard-Schaetzwert je
        // Schadenscode stammt, weil die Quantifizierung fehlte. Ohne diese
        // Spalte sah eine geschaetzte Note aus wie eine gerechnete
        // (Codeaudit 2026-08-17).
        "VSA_Geschaetzt",
        "Gewaesserschutz",
        "Grundwasserspiegel",
        FieldKeys.HierarchicalFunction,
        // Ergaenzt 2026-09-02 fuer die revidierte XTF. Beide haben in SIA405 ein Ziel
        // (Kanal.Verbindungsart und Kanal.Bettung_Umhuellung). Profiltyp und Breite
        // stehen bereits oben direkt beim Hoehenmass.
        FieldKeys.ConnectionType,
        FieldKeys.BeddingEncasement,
        // Die uebrigen Felder der Kataster-Infobox, ergaenzt 2026-09-02. Die ersten
        // sechs haben in SIA405 ein Ziel; die sechs Herkunftsangaben danach nicht —
        // sie sind Nachweis, keine Aussage von SewerStudio.
        FieldKeys.OperatingStatus,
        FieldKeys.RehabilitationNeed,
        FieldKeys.HydraulicFunction,
        FieldKeys.PositionAccuracy,
        FieldKeys.ConstructionYear,
        FieldKeys.GrossCost,
        FieldKeys.CadastreObjectId,
        FieldKeys.GeonisId,
        FieldKeys.DataOwner,
        FieldKeys.DataSupplier,
        FieldKeys.CadastreOrganisation,
        FieldKeys.CadastreLastChange,
        FieldKeys.CadastreUpdatedAt
    });

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ComboItems =
        new ReadOnlyDictionary<string, IReadOnlyList<string>>(new Dictionary<string, IReadOnlyList<string>>
        {
            // Rohrmaterial ist ein Auswahlfeld: Was hier fehlt, zeigt das Feld leer an, auch wenn
            // der Wert in den Daten steht. Die Liste muss darum zu dem passen, was
            // XtfValueNormalizer.NormalizeSiaMaterial aus SIA405-Katastern liefert.
            // Ergaenzt 2026-07-17: Epoxydharz, Faserzement, Ton — kommen in echten IKAS-Exporten vor.
            [FieldKeys.PipeMaterial] = new ReadOnlyCollection<string>(
                MaterialVokabular.Auswahl.ToList()),
            // Die Begriffe der Norm, gefuehrt in NutzungsartVokabular — keine zweite Liste.
            [FieldKeys.UsageType] = new ReadOnlyCollection<string>(
                NutzungsartVokabular.Auswahl.ToList()),
            ["Inspektionsrichtung"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "In Fliessrichtung", "Gegen Fliessrichtung"
            }),
            // Die Zustandsklasse geht nach VSA von Z0 (schlechtester Zustand) bis Z4.
            // Bis 2026-09-03 stand hier zusaetzlich eine "5": In SIA405 gibt es sie nicht,
            // sie waere beim Export verloren gegangen. In 21 Projekten kam sie kein
            // einziges Mal vor — weder bei Haltungen noch bei Schaechten.
            [FieldKeys.ConditionClass] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "0", "1", "2", "3", "4"
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
            // Belastungsklassen der Schachtabdeckung nach EN 124. Feste Liste ohne
            // Freitext: eine erfundene Klasse waere eine Aussage ueber die Tragfaehigkeit.
            [FieldKeys.LoadClass] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "A15", "B125", "C250", "D400", "E600", "F900"
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
            // Bis 2026-09-02 standen hier sieben handgepflegte PAA-Werte. Die Liste
            // kommt jetzt aus SiaKanalVokabular und enthaelt alle 14 Blaetter des
            // Modells — die sekundaere Abwasseranlage (SAA) fehlte ganz, obwohl der
            // Kataster sie fuehrt. Kein bisheriger Wert faellt weg.
            [FieldKeys.HierarchicalFunction] = new ReadOnlyCollection<string>(
                SiaKanalVokabular.FunktionHierarchisch.Auswahl.ToList()),
            [FieldKeys.ConnectionType] = new ReadOnlyCollection<string>(
                SiaKanalVokabular.Verbindungsart.Auswahl.ToList()),
            [FieldKeys.BeddingEncasement] = new ReadOnlyCollection<string>(
                SiaKanalVokabular.BettungUmhuellung.Auswahl.ToList()),
            [FieldKeys.ProfileType] = new ReadOnlyCollection<string>(
                ProfiltypVokabular.Auswahl.ToList()),
            [FieldKeys.HydraulicFunction] = new ReadOnlyCollection<string>(
                SiaKanalVokabular.FunktionHydraulisch.Auswahl.ToList()),
            [FieldKeys.OperatingStatus] = new ReadOnlyCollection<string>(
                SiaKanalVokabular.Status.Auswahl.ToList()),
            [FieldKeys.RehabilitationNeed] = new ReadOnlyCollection<string>(
                SiaKanalVokabular.Sanierungsbedarf.Auswahl.ToList()),
            [FieldKeys.PositionAccuracy] = new ReadOnlyCollection<string>(
                SiaKanalVokabular.Lagebestimmung.Auswahl.ToList())
        });

    public static readonly IReadOnlyDictionary<string, FieldDefinition> Definitions =
        new ReadOnlyDictionary<string, FieldDefinition>(new Dictionary<string, FieldDefinition>
        {
            ["NR"] = new("NR", "NR.", FieldType.Int),
            [FieldKeys.HoldingName] = new(FieldKeys.HoldingName, "Haltungsname (ID)", FieldType.Text),
            [FieldKeys.Street] = new(FieldKeys.Street, "Strasse", FieldType.Text),
            [FieldKeys.PipeMaterial] = new(FieldKeys.PipeMaterial, "Rohrmaterial", FieldType.Combo, ComboItems[FieldKeys.PipeMaterial]),
            // Bei nicht runden Profilen ist DN fachlich die lichte Hoehe. Der
            // Excel-Export ordnet diesen genaueren UI-Namen weiter dem bestehenden
            // Vorlagenkopf "DN mm" zu.
            [FieldKeys.NominalDiameterMm] = new(FieldKeys.NominalDiameterMm, "Lichte Höhe / DN mm", FieldType.Int),
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
            ["VSA_Geschaetzt"] = new("VSA_Geschaetzt", "Note geschätzt", FieldType.Text),
            ["Gewaesserschutz"] = new("Gewaesserschutz", "Gewässerschutz", FieldType.Combo, ComboItems["Gewaesserschutz"]),
            ["Grundwasserspiegel"] = new("Grundwasserspiegel", "Grundwasserspiegel", FieldType.Combo, ComboItems["Grundwasserspiegel"]),
            [FieldKeys.HierarchicalFunction] = new(FieldKeys.HierarchicalFunction, "Funktionale Hierarchie", FieldType.Combo, ComboItems[FieldKeys.HierarchicalFunction]),
            [FieldKeys.ConnectionType] = new(FieldKeys.ConnectionType, "Verbindungsart", FieldType.Combo, ComboItems[FieldKeys.ConnectionType]),
            [FieldKeys.BeddingEncasement] = new(FieldKeys.BeddingEncasement, "Bettung/Umhüllung", FieldType.Combo, ComboItems[FieldKeys.BeddingEncasement]),
            [FieldKeys.ProfileType] = new(FieldKeys.ProfileType, "Profilform", FieldType.Combo, ComboItems[FieldKeys.ProfileType]),
            [FieldKeys.ClearWidthMm] = new(FieldKeys.ClearWidthMm, "Lichte Breite mm", FieldType.Int),
            [FieldKeys.OperatingStatus] = new(FieldKeys.OperatingStatus, "Status", FieldType.Combo, ComboItems[FieldKeys.OperatingStatus]),
            [FieldKeys.RehabilitationNeed] = new(FieldKeys.RehabilitationNeed, "Sanierungsbedarf", FieldType.Combo, ComboItems[FieldKeys.RehabilitationNeed]),
            [FieldKeys.HydraulicFunction] = new(FieldKeys.HydraulicFunction, "Funktion hydraulisch", FieldType.Combo, ComboItems[FieldKeys.HydraulicFunction]),
            [FieldKeys.PositionAccuracy] = new(FieldKeys.PositionAccuracy, "Lagebestimmung", FieldType.Combo, ComboItems[FieldKeys.PositionAccuracy]),
            [FieldKeys.ConstructionYear] = new(FieldKeys.ConstructionYear, "Baujahr", FieldType.Int),
            [FieldKeys.GrossCost] = new(FieldKeys.GrossCost, "Bruttokosten (Kataster)", FieldType.Decimal),
            [FieldKeys.CadastreObjectId] = new(FieldKeys.CadastreObjectId, "Objekt-ID (Lisag)", FieldType.Text),
            [FieldKeys.GeonisId] = new(FieldKeys.GeonisId, "GEONIS-Kennung", FieldType.Text),
            [FieldKeys.DataOwner] = new(FieldKeys.DataOwner, "Datenherr", FieldType.Text),
            [FieldKeys.DataSupplier] = new(FieldKeys.DataSupplier, "Datenlieferant", FieldType.Text),
            [FieldKeys.CadastreOrganisation] = new(FieldKeys.CadastreOrganisation, "Organisation", FieldType.Text),
            [FieldKeys.CadastreLastChange] = new(FieldKeys.CadastreLastChange, "Letzte Änderung", FieldType.Text),
            [FieldKeys.CadastreUpdatedAt] = new(FieldKeys.CadastreUpdatedAt, "Aktualisierungsdatum", FieldType.Text),
            [FieldKeys.ShaftShape] = new(
                FieldKeys.ShaftShape,
                "Schachtform",
                FieldType.Combo,
                SchachtformVokabular.Auswahl),
            [FieldKeys.ShaftDimension1Mm] = new(FieldKeys.ShaftDimension1Mm, "Grösstes Innenmass mm", FieldType.Int),
            [FieldKeys.ShaftDimension2Mm] = new(FieldKeys.ShaftDimension2Mm, "Kleinstes Innenmass mm", FieldType.Int)
        });

    public static FieldDefinition Get(string fieldName)
        => Definitions.TryGetValue(fieldName, out var def)
            ? def
            : new FieldDefinition(fieldName, fieldName, FieldType.Text);

    public static IReadOnlyList<string> GetComboItems(string fieldName)
        => ComboItems.TryGetValue(fieldName, out var items) ? items : Array.Empty<string>();
}
