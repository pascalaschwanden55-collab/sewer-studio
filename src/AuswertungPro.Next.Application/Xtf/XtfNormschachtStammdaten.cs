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
    string? BaulicherZustand = null);

/// <summary>
/// Bildet einen Normschacht der SIA405-XTF auf die Schachtfelder von SewerStudio ab.
///
/// Uebernommen wird nur, was in der Schachttabelle wirklich gebraucht wird: Nummer,
/// Funktion, Material, Dimension und Eigentuemer. Status, Sanierungsbedarf, Baujahr,
/// Sohlenkote, Lagebestimmung und die Deckelangaben bleiben bewusst draussen — sie sind
/// informativ und stehen im Protokoll.
///
/// Die Feldnamen folgen der Konvention des PDF-, WinCan- und SchachtPro-Imports
/// (<c>Schachtnummer</c>, <c>NR.</c>, <c>Nr.</c>, <c>Funktion</c>, <c>Material</c>,
/// <c>Dimension</c>), damit ein Schacht aus verschiedenen Quellen derselbe bleibt.
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
    /// Leere und nicht abbildbare Werte fehlen einfach — ein Feld, das nichts aussagt,
    /// wird nicht gesetzt. Insbesondere <c>unbekannt</c> ist keine Angabe: Im
    /// Kantonsexport von Abwasser Uri steht es bei 211 von 295 Schaechten beim Material.
    /// Es wuerde die Spalte fuellen, ohne etwas zu sagen, und dabei einen spaeteren
    /// besseren Wert aus einer anderen Quelle blockieren.
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

        Ergaenze("Funktion", SchachtFunktionVokabular.Normalisieren(element.Funktion));
        Ergaenze("Material", SchachtMaterialVokabular.Normalisieren(element.Material));
        Ergaenze("Dimension", Dimension(element.Dimension1, element.Dimension2));
        Ergaenze(FieldKeys.Owner, EigentumVokabular.Normalisieren(element.Eigentuemer));
        Ergaenze(FieldKeys.ConditionClass, Zustandsklasse(element.BaulicherZustand));

        return paare;

        void Ergaenze(string feld, string? wert)
        {
            var text = (wert ?? "").Trim();
            if (text.Length == 0 || string.Equals(text, "unbekannt", StringComparison.OrdinalIgnoreCase))
                return;

            paare.Add(new(feld, text));
        }
    }

    /// <summary>
    /// Die Schachtdimension als Text, in der Schreibweise der Bestandsdaten:
    /// <c>600 mm</c> beim runden Schacht, <c>1100 x 900 mm</c> beim eckigen.
    ///
    /// Das Format stammt aus dem PDF-Import, der in den echten Projekten
    /// <c>600 mm</c> und <c>100 x 100 mm</c> geschrieben hat. Ein zweites Format
    /// daneben waere in derselben Excel-Spalte nur verwirrend.
    ///
    /// <c>Dimension1</c> und <c>Dimension2</c> tragen laut Modell den Typ
    /// <c>SIA405_Base_Abwasser.Abmessung</c> und stehen damit in Millimetern; die Null
    /// bedeutet unbekannt. <c>null</c> heisst hier: keine brauchbare Angabe.
    /// </summary>
    public static string? Dimension(string? erstes, string? zweites)
    {
        var a = SiaAbmessung.NachMillimeter(erstes);
        var b = SiaAbmessung.NachMillimeter(zweites);

        // Fehlt eines der beiden Masse, gilt das vorhandene fuer beide Richtungen.
        a ??= b;
        b ??= a;
        if (a is null || b is null)
            return null;

        return a == b ? $"{a} mm" : $"{a} x {b} mm";
    }
}
