using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>
/// Welche Spalte des QGIS-Bestands welches Projektfeld fuellt — und wie der Rohwert
/// dabei in die Schreibweise des Programms kommt.
///
/// Die Uebersetzung laeuft durch dieselben Vokabulare wie der XTF-Import. Sonst
/// entstuenden zwei Begriffswelten fuer denselben Kanal: der Import schriebe
/// "Kunststoff", das Nachfuellen "Kunststoff_Polyethylen".
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class QgisFeldKarte
{
    /// <summary>
    /// Ein Wert, der keine Angabe ist. Im Bestand steht er millionenfach: bei
    /// <c>ka_verbindungsart</c> und <c>ka_bettung_umhuellung</c> auf 59 % der
    /// Leitungen. Ein leeres Feld damit zu fuellen macht die Tabelle voller,
    /// nicht besser — und verdeckt, dass die Angabe weiterhin fehlt.
    /// </summary>
    public const string KeineAngabe = "unbekannt";

    private sealed record Zuordnung(string Spalte, string Feld, Func<string, string?> Umsetzung);

    private static readonly Zuordnung[] HaltungZuordnungen =
    [
        new("ha_material", FieldKeys.PipeMaterial, w => Leer(MaterialVokabular.Normalisieren(w))),
        new("ha_lichte_hoehe", FieldKeys.NominalDiameterMm, GanzeZahl),
        new("ha_laengeeffektiv", FieldKeys.HoldingLengthMeters, Dezimalzahl),
        new("ha_lagebestimmung", FieldKeys.PositionAccuracy, w => SiaKanalVokabular.Lagebestimmung.NachNorm(w)),
        new("ha_innenschutz", "Innenschutz", Text),
        new("ka_nutzungsart_ist", FieldKeys.UsageType, w => Leer(NutzungsartVokabular.Normalisieren(w))),
        new("ka_funktionhierarchisch", FieldKeys.HierarchicalFunction, w => SiaKanalVokabular.FunktionHierarchisch.NachNorm(w)),
        new("ka_funktionhydraulisch", FieldKeys.HydraulicFunction, w => SiaKanalVokabular.FunktionHydraulisch.NachNorm(w)),
        new("ka_verbindungsart", FieldKeys.ConnectionType, w => SiaKanalVokabular.Verbindungsart.NachNorm(w)),
        new("ka_bettung_umhuellung", FieldKeys.BeddingEncasement, w => SiaKanalVokabular.BettungUmhuellung.NachNorm(w)),
        new("bw_status", FieldKeys.OperatingStatus, w => SiaKanalVokabular.Status.NachNorm(w)),
        new("bw_sanierungsbedarf", FieldKeys.RehabilitationNeed, w => SiaKanalVokabular.Sanierungsbedarf.NachNorm(w)),
        new("bw_baulicherzustand", FieldKeys.ConditionClass, Zustandsziffer),
        new("bw_baujahr", FieldKeys.ConstructionYear, GanzeZahl),
        new("bw_bruttokosten", FieldKeys.GrossCost, Dezimalzahl),
        new("bw_standortname", FieldKeys.Street, Text),
        new("org_eigentuemer", FieldKeys.Owner, w => Leer(EigentumVokabular.Normalisieren(w))),
        new("obj_id", FieldKeys.CadastreObjectId, Text),
        new("ha_datenherr", FieldKeys.DataOwner, Text),
        new("ha_datenlieferant", FieldKeys.DataSupplier, Text),
        new("ha_letzte_aenderung", FieldKeys.CadastreLastChange, Datum),
        new("datum_upload_sde", FieldKeys.CadastreUpdatedAt, Datum)
    ];

    private static readonly Zuordnung[] SchachtZuordnungen =
    [
        new("ns_funktion", "Funktion", w => Leer(SchachtFunktionVokabular.Normalisieren(w))),
        new("ns_material", "Material", w => Leer(SchachtMaterialVokabular.Normalisieren(w))),
        new("bw_status", FieldKeys.OperatingStatus, w => SiaKanalVokabular.Status.NachNorm(w)),
        new("bw_sanierungsbedarf", FieldKeys.RehabilitationNeed, w => SiaKanalVokabular.Sanierungsbedarf.NachNorm(w)),
        new("bw_baulicherzustand", FieldKeys.ConditionClass, Zustandsziffer),
        new("bw_baujahr", FieldKeys.ConstructionYear, GanzeZahl),
        new("org_eigentuemer", FieldKeys.Owner, w => Leer(EigentumVokabular.Normalisieren(w))),
        new("datenherr", FieldKeys.DataOwner, Text),
        new("datenlieferant", FieldKeys.DataSupplier, Text),
        new("letzte_aenderung", FieldKeys.CadastreLastChange, Datum),
        new("datum_upload_sde", FieldKeys.CadastreUpdatedAt, Datum),

        // Die beiden Masse einzeln. Die GEONIS-Maske schreibt es selbst dazu:
        // Dimension1 ist das groesste, Dimension2 das kleinste Innenmass — es sind
        // also nicht Breite und Laenge in fester Richtung.
        new("ns_dimension1", FieldKeys.ShaftDimension1Mm, GanzeZahl),
        new("ns_dimension2", FieldKeys.ShaftDimension2Mm, GanzeZahl),

        // Zwei Spalten der Ebene, die bisher niemand gelesen hat: die Bemerkung ist
        // bei 16671 Schaechten gefuellt ("Saniert 2018"), die Nutzungsart bei 59961.
        // Die Nutzungsart bleibt ein reines Programmfeld — SIA405 kennt am
        // Normschacht kein solches Attribut.
        new("bw_bemerkung", FieldKeys.Remarks, Text),
        new("ka_nutzungsart", FieldKeys.UsageType, w => Leer(NutzungsartVokabular.Normalisieren(w)))
    ];

    public static IReadOnlyList<string> Felder(BauteilArt art)
        => Tabelle(art).Select(z => z.Feld)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Der Wert, den ein Bauteil fuer ein Projektfeld liefert, oder <c>null</c>.
    ///
    /// <c>null</c> heisst in jedem Fall: nichts eintragen. Die Gruende dafuer sind
    /// leerer Rohwert, <c>unbekannt</c>, ein Wert ausserhalb der Norm oder eine
    /// Zahl, die sich nicht lesen laesst. Geraten wird nie.
    /// </summary>
    public static string? Wert(QgisBauteil bauteil, string feld, BauteilArt art)
    {
        ArgumentNullException.ThrowIfNull(bauteil);

        foreach (var zuordnung in Tabelle(art))
        {
            if (!string.Equals(zuordnung.Feld, feld, StringComparison.Ordinal))
                continue;

            if (!bauteil.Werte.TryGetValue(zuordnung.Spalte, out var roh))
                continue;

            // Zwei Sperren gegen "unbekannt", und beide haben ihren eigenen Grund:
            // Die erste faengt den Rohwert ab, die zweite einen Begriff, der ERST
            // durch die Umsetzung dazu wird (ein Vokabular bildet Synonyme darauf ab).
            // Eine Sabotageprobe am 2026-09-02 zeigt: Wird nur eine entfernt, deckt
            // die andere den Fall weiter ab — genau deshalb steht dieser Hinweis hier,
            // damit keine der beiden als "ueberfluessig" verschwindet.
            var text = (roh ?? "").Trim();
            if (text.Length == 0 || string.Equals(text, KeineAngabe, StringComparison.OrdinalIgnoreCase))
                continue;

            var wert = zuordnung.Umsetzung(text);
            if (!string.IsNullOrWhiteSpace(wert)
                && !string.Equals(wert, KeineAngabe, StringComparison.OrdinalIgnoreCase))
            {
                return wert;
            }
        }

        return null;
    }

    /// <summary>Die Spalten, die der Leser holen muss.</summary>
    public static IReadOnlyList<string> Spalten(BauteilArt art)
        => Tabelle(art).Select(z => z.Spalte)
            .Concat(art == BauteilArt.Schacht ? ["ns_dimension1", "ns_dimension2"] : Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Die Namensspalte der Bauteilart.</summary>
    public static string Namensspalte(BauteilArt art)
        => art == BauteilArt.Haltung ? "ne_bezeichnung" : "bw_bezeichnung";

    private static Zuordnung[] Tabelle(BauteilArt art)
        => art == BauteilArt.Haltung ? HaltungZuordnungen : SchachtZuordnungen;

    private static string? Leer(string? wert)
        => string.IsNullOrWhiteSpace(wert) ? null : wert;

    private static string? Text(string wert) => Leer(wert);

    /// <summary>
    /// Ein Zeitpunkt aus dem Bestand steht dort als <c>2025-02-14T01:00:00</c>.
    /// Im Programm reicht das Datum; die Uhrzeit ist der Zeitpunkt des
    /// Datenbankauszugs und sagt fachlich nichts.
    /// </summary>
    private static string? Datum(string wert)
    {
        return DateTime.TryParse(wert, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var zeitpunkt)
            ? zeitpunkt.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-CH"))
            : Leer(wert);
    }

    /// <summary>
    /// Der bauliche Zustand steht im Bestand als <c>Z2</c>, im Projekt als blosse
    /// Ziffer — dieselbe Uebersetzung wie beim XTF-Import, nur andersherum.
    /// </summary>
    private static string? Zustandsziffer(string wert)
    {
        var text = wert.Trim();
        if (text.Length == 2 && (text[0] is 'Z' or 'z') && text[1] is >= '0' and <= '4')
            return text[1].ToString();

        return text.Length == 1 && text[0] is >= '0' and <= '4' ? text : null;
    }

    /// <summary>
    /// Eine ganze Zahl. Der Bestand schreibt sie teils als <c>300</c>, teils als
    /// <c>300.0</c>; beides ist dieselbe Angabe. Die Null bedeutet dort
    /// "unbekannt" und ist deshalb keine.
    /// </summary>
    private static string? GanzeZahl(string wert)
    {
        if (!decimal.TryParse(wert, NumberStyles.Number, CultureInfo.InvariantCulture, out var zahl))
            return null;

        var ganz = decimal.Truncate(zahl);
        return ganz != zahl || ganz <= 0
            ? null
            : ((long)ganz).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Eine Dezimalzahl in der Schreibweise des Programms.</summary>
    private static string? Dezimalzahl(string wert)
    {
        if (!decimal.TryParse(wert, NumberStyles.Number, CultureInfo.InvariantCulture, out var zahl)
            || zahl <= 0)
        {
            return null;
        }

        return zahl.ToString("0.##", CultureInfo.GetCultureInfo("de-CH"));
    }

}
