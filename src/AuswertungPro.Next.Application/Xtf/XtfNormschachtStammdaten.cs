using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Ein Normschacht aus der SIA405-XTF, so wie er in der Datei steht.
/// Rohwerte ohne jede Umrechnung — die macht <see cref="XtfNormschachtStammdaten"/>.
/// </summary>
public sealed record XtfNormschachtElement(
    string Bezeichnung,
    string? Funktion = null,
    string? Material = null,
    string? Dimension1 = null,
    string? Dimension2 = null,
    string? Eigentuemer = null,
    string? BaulicherZustand = null,
    string? Bemerkung = null,
    string? Status = null,
    string? Sanierungsbedarf = null,
    string? Baujahr = null,
    string? Datenherr = null,
    string? Datenlieferant = null,
    string? Tid = null);

/// <summary>
/// Bildet einen Normschacht der SIA405-XTF auf die Schachtfelder von SewerStudio ab.
///
/// Uebernommen wird, was das Programm am Schacht fuehrt und was der Export wieder
/// hinausschreibt: Nummer, Funktion, Material, die zwei Masse, Eigentuemer, Datenherr,
/// Datenlieferant, Zustand, Bemerkung, Status, Sanierungsbedarf und Baujahr. Sohlenkote,
/// Lagebestimmung und die Deckelangaben bleiben draussen; sie stehen im Protokoll.
///
/// Status, Sanierungsbedarf, Baujahr und Bemerkung fehlten bis 2026-09-03. Die
/// Rundreise Export, Import, Vergleich zeigte: Was SewerStudio selbst hinausschreibt,
/// kam nicht zurueck. Beim Import gewinnt die Datei, nur so sind GEONIS und
/// SewerStudio nach einem Austausch identisch.
///
/// Die Masse landen in den zwei Zahlenfeldern von <see cref="Schacht.SchachtMasse"/>,
/// nicht mehr im alten Textfeld <c>Dimension</c>.
///
/// Die Feldnamen folgen der Konvention des PDF-, WinCan- und SchachtPro-Imports
/// (<c>Schachtnummer</c>, <c>NR.</c>, <c>Nr.</c>, <c>Funktion</c>, <c>Material</c>),
/// damit ein Schacht aus verschiedenen Quellen derselbe bleibt.
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class XtfNormschachtStammdaten
{
    /// <summary>
    /// Das Feld, in dem die Schachtnummer steht — und nur dieses.
    ///
    /// <c>NR.</c> und <c>Nr.</c> sehen aus wie Nummernfelder, sind es aber nicht: In den
    /// 17 echten Projekten tragen sie bei 257 von 257 Schaechten eine LAUFENDE Nummer
    /// (1, 2, 3 ...) und in keinem einzigen Fall die Schachtnummer. Die Schachtnummer
    /// dort hineinzuschreiben wuerde die Durchnummerierung zerstoeren.
    ///
    /// Dass der SchachtPro-Import beide Felder mit der Schachtnummer fuellt, ist kein
    /// Gegenbeweis — SchachtPro liefert keine laufende Nummer, und diese Faelle sind in
    /// der Messung gar nicht enthalten.
    /// </summary>
    public static readonly IReadOnlyList<string> Nummernfelder = ["Schachtnummer"];

    /// <summary>
    /// Die Feldpaare fuer einen Schachtdatensatz, in stabiler Reihenfolge.
    ///
    /// Leere und nicht abbildbare Auswahlwerte fehlen einfach. Bei Funktion, Material,
    /// Zustand und Status ist <c>unbekannt</c> keine Angabe: Im Kantonsexport von
    /// Abwasser Uri steht es bei 211 von 295 Schaechten beim Material. In einer
    /// Bemerkung ist dasselbe Wort dagegen echter Freitext. Bei Organisationsfeldern
    /// entscheidet <see cref="EigentumVokabular"/>; dort darf es nicht vorher pauschal
    /// entfernt werden.
    /// </summary>
    /// <summary>
    /// "Z0" bis "Z4" aus der XTF zur Ziffer, die das Programm fuehrt. Alles andere —
    /// "unbekannt", leer, ein unerwarteter Text — liefert <c>null</c> und setzt nichts.
    ///
    /// Dieselbe Regel gilt fuer Haltungen; sie steht hier, damit beide Wege sie teilen.
    /// </summary>
    public static string? Zustandsklasse(string? ausDerDatei)
    {
        var wert = (ausDerDatei ?? "").Trim();
        if (wert.Length != 2 || (wert[0] != 'Z' && wert[0] != 'z'))
            return null;

        return wert[1] is >= '0' and <= '4' ? wert[1].ToString() : null;
    }

    public static IReadOnlyList<KeyValuePair<string, string>> Feldpaare(XtfNormschachtElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var paare = new List<KeyValuePair<string, string>>();
        var nummer = (element.Bezeichnung ?? "").Trim();
        if (nummer.Length == 0)
            return paare;

        foreach (var feld in Nummernfelder)
            paare.Add(new(feld, nummer));

        // SIA405 kennt keine separate OBJ_ID. Die TID ist die Katasteridentitaet und
        // erlaubt eine spaetere Revision am richtigen Normschacht. Der eigenstaendige
        // Neu-Export vergibt dagegen bewusst einen vollstaendigen eigenen TID-Verbund.
        Ergaenze(FieldKeys.CadastreObjectId, element.Tid);

        Ergaenze("Funktion", SchachtFunktionVokabular.Normalisieren(element.Funktion));
        Ergaenze("Material", SchachtMaterialVokabular.Normalisieren(element.Material));

        // Dimension1/Dimension2 tragen laut Modell den Typ Abmessung (Millimeter, Null =
        // unbekannt). Fehlt eines, gilt das vorhandene fuer beide Richtungen.
        var masse = Schacht.SchachtMasse.AusZwei(element.Dimension1, element.Dimension2);
        if (masse is not null)
        {
            paare.Add(new(FieldKeys.ShaftDimension1Mm, masse.Value.Dimension1));
            paare.Add(new(FieldKeys.ShaftDimension2Mm, masse.Value.Dimension2));
        }

        Ergaenze(
            FieldKeys.Owner,
            EigentumVokabular.Normalisieren(element.Eigentuemer),
            unbekanntIstLeer: false);
        Ergaenze(FieldKeys.ConditionClass, Zustandsklasse(element.BaulicherZustand));
        Ergaenze(FieldKeys.Remarks, element.Bemerkung, unbekanntIstLeer: false);
        Ergaenze(FieldKeys.OperatingStatus, element.Status);
        Ergaenze(FieldKeys.RehabilitationNeed, element.Sanierungsbedarf);
        Ergaenze(FieldKeys.ConstructionYear, element.Baujahr);
        Ergaenze(
            FieldKeys.DataOwner,
            EigentumVokabular.Normalisieren(element.Datenherr),
            unbekanntIstLeer: false);
        Ergaenze(
            FieldKeys.DataSupplier,
            EigentumVokabular.Normalisieren(element.Datenlieferant),
            unbekanntIstLeer: false);

        return paare;

        void Ergaenze(string feld, string? wert, bool unbekanntIstLeer = true)
        {
            var text = (wert ?? "").Trim();
            if (text.Length == 0
                || (unbekanntIstLeer
                    && string.Equals(text, "unbekannt", StringComparison.OrdinalIgnoreCase)))
                return;

            paare.Add(new(feld, text));
        }
    }
}
