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
        "Haltungsname",
        "Strasse",
        "Rohrmaterial",
        "DN_mm",
        "Nutzungsart",
        "Haltungslaenge_m",
        "Inspektionsrichtung",
        "Primaere_Schaeden",
        "Zustandsklasse",
        "VSA_Zustandsnote_D",
        "Pruefungsresultat",
        "Referenzpruefung",
        "Sanieren_JaNein",
        "Empfohlene_Sanierungsmassnahmen",
        "Kosten",
        "Eigentuemer",
        "Ausgefuehrt_durch",
        "Bemerkungen",
        "Link",
        "Renovierung_Inliner_Stk",
        "Renovierung_Inliner_m",
        "Anschluesse_verpressen",
        "Reparatur_Manschette",
        "Reparatur_Kurzliner",
        "Erneuerung_Neubau_m",
        "Offen_abgeschlossen",
        "Datum_Jahr"
    });

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ComboItems =
        new ReadOnlyDictionary<string, IReadOnlyList<string>>(new Dictionary<string, IReadOnlyList<string>>
        {
            ["Rohrmaterial"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "PVC", "PE", "PP", "GFK", "Beton", "Steinzeug", "Guss", "Hartpolyethylen"
            }),
            ["Nutzungsart"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "Schmutzwasser", "Regenwasser", "Mischabwasser"
            }),
            ["Inspektionsrichtung"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "In Fliessrichtung", "Gegen Fliessrichtung"
            }),
            ["Zustandsklasse"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "0", "1", "2", "3", "4", "5"
            }),
            ["Sanieren_JaNein"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "Ja", "Nein"
            }),
            ["Referenzpruefung"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "Ja", "Nein"
            }),
            ["Ausgefuehrt_durch"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "Kanalsanierer", "Baumeister", "Gartenbauer"
            }),
            ["Offen_abgeschlossen"] = new ReadOnlyCollection<string>(new List<string>
            {
                "", "offen", "abgeschlossen"
            })
        });

    public static readonly IReadOnlyDictionary<string, FieldDefinition> Definitions =
        new ReadOnlyDictionary<string, FieldDefinition>(new Dictionary<string, FieldDefinition>
        {
            ["NR"] = new("NR", "NR.", FieldType.Int),
            ["Haltungsname"] = new("Haltungsname", "Haltungsname (ID)", FieldType.Text),
            ["Strasse"] = new("Strasse", "Strasse", FieldType.Text),
            ["Rohrmaterial"] = new("Rohrmaterial", "Rohrmaterial", FieldType.Combo, ComboItems["Rohrmaterial"]),
            ["DN_mm"] = new("DN_mm", "DN mm", FieldType.Int),
            ["Nutzungsart"] = new("Nutzungsart", "Nutzungsart", FieldType.Combo, ComboItems["Nutzungsart"]),
            ["Haltungslaenge_m"] = new("Haltungslaenge_m", "Haltungslänge m", FieldType.Decimal),
            ["Inspektionsrichtung"] = new("Inspektionsrichtung", "Inspektionsrichtung", FieldType.Combo, ComboItems["Inspektionsrichtung"]),
            ["Primaere_Schaeden"] = new("Primaere_Schaeden", "Primäre Schäden", FieldType.Multiline),
            ["Zustandsklasse"] = new("Zustandsklasse", "Zustandsklasse", FieldType.Combo, ComboItems["Zustandsklasse"]),
            ["VSA_Zustandsnote_D"] = new("VSA_Zustandsnote_D", "VSA-Zustandsnote D", FieldType.Decimal),
            ["Pruefungsresultat"] = new("Pruefungsresultat", "Prüfungsresultat", FieldType.Text),
            ["Referenzpruefung"] = new("Referenzpruefung", "Referenzpruefung", FieldType.Combo, ComboItems["Referenzpruefung"]),
            ["Sanieren_JaNein"] = new("Sanieren_JaNein", "Sanieren Ja/Nein", FieldType.Combo, ComboItems["Sanieren_JaNein"]),
            ["Empfohlene_Sanierungsmassnahmen"] = new("Empfohlene_Sanierungsmassnahmen", "Empfohlene Sanierungsmassnahmen", FieldType.Multiline),
            ["Kosten"] = new("Kosten", "Kosten", FieldType.Decimal),
            ["Ausgefuehrt_durch"] = new("Ausgefuehrt_durch", "Ausgefuehrt durch", FieldType.Combo, ComboItems["Ausgefuehrt_durch"]),
            ["Eigentuemer"] = new("Eigentuemer", "Eigentümer", FieldType.Text),
            ["Bemerkungen"] = new("Bemerkungen", "Bemerkungen", FieldType.Multiline),
            ["Link"] = new("Link", "Link", FieldType.Text),
            ["Renovierung_Inliner_Stk"] = new("Renovierung_Inliner_Stk", "Renovierung Inliner Stk.", FieldType.Int),
            ["Renovierung_Inliner_m"] = new("Renovierung_Inliner_m", "Renovierung Inliner m", FieldType.Decimal),
            ["Anschluesse_verpressen"] = new("Anschluesse_verpressen", "Anschlüsse verpressen", FieldType.Int),
            ["Reparatur_Manschette"] = new("Reparatur_Manschette", "Reparatur Manschette", FieldType.Int),
            ["Reparatur_Kurzliner"] = new("Reparatur_Kurzliner", "Reparatur Kurzliner", FieldType.Int),
            ["Erneuerung_Neubau_m"] = new("Erneuerung_Neubau_m", "Erneuerung Neubau m", FieldType.Decimal),
            ["Offen_abgeschlossen"] = new("Offen_abgeschlossen", "offen/abgeschlossen", FieldType.Combo, ComboItems["Offen_abgeschlossen"]),
            ["Datum_Jahr"] = new("Datum_Jahr", "Datum/Jahr", FieldType.Text)
        });

    public static FieldDefinition Get(string fieldName)
        => Definitions.TryGetValue(fieldName, out var def)
            ? def
            : new FieldDefinition(fieldName, fieldName, FieldType.Text);

    public static IReadOnlyList<string> GetComboItems(string fieldName)
        => ComboItems.TryGetValue(fieldName, out var items) ? items : Array.Empty<string>();
}
