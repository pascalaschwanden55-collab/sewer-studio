using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Lookup;

/// <summary>
/// Die GEONIS-Kennungen eines Bauteils, so wie die Kennungstabelle sie fuehrt.
///
/// Ein Datensatz traegt je nach Bauteilart entweder die Haltungs- oder die
/// Schachtkennungen; die uebrigen Felder bleiben leer. Ein gemeinsamer Typ, weil
/// Leser, Planer und Bericht fuer beide Arten dieselben sind.
/// </summary>
public sealed record KatasterKennung(
    string Name,
    string? Gemeinde,
    string? Haltung,
    string? Kanal,
    string? VonPunkt,
    string? VonPunktBezeichnung,
    string? NachPunkt,
    string? NachPunktBezeichnung,
    string? Rohrprofil,
    string? RohrprofilTyp,
    string? Knoten,
    string? Bauwerk,
    DateTime? GeonisGeaendert = null)
{
    /// <summary>Die Hauptkennung: bei Haltungen die der Haltung, bei Schaechten die des Knotens.</summary>
    public string? Hauptkennung => Haltung ?? Knoten;

    public static KatasterKennung FuerHaltung(
        string name, string? gemeinde, string haltung, string? kanal,
        string? vonPunkt, string? vonPunktBezeichnung,
        string? nachPunkt, string? nachPunktBezeichnung,
        string? rohrprofil, string? rohrprofilTyp,
        DateTime? geonisGeaendert = null)
        => new(name, gemeinde, haltung, kanal, vonPunkt, vonPunktBezeichnung,
               nachPunkt, nachPunktBezeichnung, rohrprofil, rohrprofilTyp, null, null, geonisGeaendert);

    public static KatasterKennung FuerSchacht(
        string name, string? gemeinde, string knoten, string? bauwerk, DateTime? geonisGeaendert = null)
        => new(name, gemeinde, null, null, null, null, null, null, null, null, knoten, bauwerk, geonisGeaendert);
}

/// <summary>
/// Die Form einer SIA405-Objektkennung (<c>STANDARDOID</c>): genau sechzehn Zeichen,
/// Buchstaben und Ziffern, beginnend mit einem Buchstaben. Alles andere weist der
/// ilivalidator ab — und GEONIS wuerde es nicht wiedererkennen.
/// </summary>
public static class SiaObjektkennung
{
    private static readonly Regex Form = new("^[A-Za-z][A-Za-z0-9]{15}$", RegexOptions.CultureInvariant);

    public static bool IstGueltig(string? kennung)
        => kennung is not null && Form.IsMatch(kennung);
}

/// <summary>
/// Die Kennungstabelle einer Bauteilart, nachschlagbar ueber den Namen.
///
/// <see cref="Mehrdeutig"/> nennt die Namen, die mehrfach vorkommen. Sie liefern
/// bewusst KEINE Kennung: In der GEONIS-Kopie vom Dezember 2024 tragen 389 echte
/// Haltungsnamen und 467 echte Schachtnamen mehr als ein Objekt. Einen davon zu
/// nehmen hiesse, eine fremde Haltung im Kataster zu ueberschreiben.
/// </summary>
public sealed record KatasterKennungBestand(
    BauteilArt Art,
    IReadOnlyDictionary<string, KatasterKennung> JeName,
    IReadOnlySet<string> Mehrdeutig,
    int GeleseneObjekte,
    string Stand)
{
    public KatasterKennung? Finde(string? name)
    {
        var text = (name ?? "").Trim();
        return text.Length > 0 && JeName.TryGetValue(text, out var kennung) ? kennung : null;
    }

    public bool IstMehrdeutig(string? name)
        => Mehrdeutig.Contains((name ?? "").Trim());
}

/// <summary>
/// Liest die Kennungstabelle. Ausschliesslich lesend; die Datei wird nie veraendert.
/// </summary>
public interface IKatasterKennungLeser
{
    /// <summary>
    /// Liest die Kennungen einer Bauteilart. Wirft bei einer fehlenden oder
    /// unlesbaren Datei — eine leere Antwort waere von "nichts gefunden" nicht zu
    /// unterscheiden.
    /// </summary>
    KatasterKennungBestand Lies(BauteilArt art);

    /// <summary>Der Pfad, aus dem gelesen wird — fuer den Bericht.</summary>
    string Quellpfad();
}
