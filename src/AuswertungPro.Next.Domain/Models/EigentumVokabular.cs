using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Der Eigentuemer einer Haltung oder eines Schachts — an einer Stelle.
///
/// Massgebend sind die amtlichen Begriffe des Kantons Uri. Im QGIS-Export Zone 1.17
/// stehen an den 295 Normschaechten "Privat" 204x, "Abwasser Uri" 68x und
/// "Kanton Uri" 17x; das Abwassernetz des Kantons liefert dieselben Begriffe.
///
/// Bis 2026-08-31 uebersetzte diese Klasse sie in die Kurzformen "AWU" und "Kanton",
/// weil die Excel-Vorlagen ihre Eigentuemerspalte ueber einen EXAKTEN Vergleich
/// faerben und nur diese fuenf Woerter kannten. Beide Vorlagen tragen jetzt je eine
/// Regel fuer den amtlichen Begriff UND fuer die Kurzform, und die Kennzahlen zaehlen
/// ueber beide. Damit gibt es keinen Grund mehr, einen eingelesenen Wert umzuschreiben.
/// Gespeicherte Kurzformen aus Altprojekten bleiben gueltig, gefaerbt und gezaehlt.
///
/// <see cref="Costs.OwnershipAwuFilter"/> bleibt davon unberuehrt: Er erkennt sowohl
/// "AWU" als auch jeden Freitext mit "Abwasser Uri" und arbeitet nach der Uebersetzung
/// genauso weiter.
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class EigentumVokabular
{
    /// <summary>
    /// Ein Eigentumsverhaeltnis mit allen Schreibweisen, die dafuer gelesen werden, und
    /// dem Begriff, den die Excel-Vorlage faerbt.
    /// </summary>
    private sealed record Konzept(string[] Gelesen, string App, string? Organisationstyp = null);

    private static readonly Konzept[] Konzepte =
    [
        // Der amtliche Begriff gewinnt. Frueher stand hier die Kurzform "AWU",
        // weil die Excel-Vorlage nur sie faerbte — seit 2026-08-31 faerbt und
        // zaehlt sie beide Schreibweisen, also darf der eingelesene Wert stehen
        // bleiben. Die Kurzformen werden weiter gelesen; Altprojekte behalten
        // ihren gespeicherten Wert unveraendert.
        // Der Organisationstyp stammt aus SIA405_Base_Abwasser_1_LV95 (18.10.2023) und ist
        // dort ein Pflichtfeld mit sieben Werten: Abwasserverband, Bund, Gemeinde,
        // Gemeindeabteilung, Genossenschaft_Korporation, Kanton, Privat.
        //
        // "Abwasser Uri" traegt im Kantonsexport zwar den Typ "Kanton", ist aber ein
        // Zweckverband. Abwasser Uri hat das am 2026-09-02 auf "Abwasserverband"
        // korrigiert; SewerStudio folgt derselben Entscheidung, damit beide Seiten
        // dieselbe Organisation meinen.
        new(["awu", "abwasser uri", "abwasser uri (awu)", "abwasseruri"], "Abwasser Uri", "Abwasserverband"),
        new(["kanton", "kanton uri", "kanton_uri"], "Kanton Uri", "Kanton"),
        new(["bund", "eidgenossenschaft"], "Bund", "Bund"),
        new(["gemeinde", "einwohnergemeinde", "korporationsgemeinde"], "Gemeinde", "Gemeinde"),
        new(["privat", "private", "privatperson"], "Privat", "Privat")
    ];

    /// <summary>
    /// Die 19 Gemeinden des Kantons Uri, klein und ohne Sonderzeichen.
    ///
    /// Der Kataster schreibt drei davon mit Kantonszusatz ("Altdorf (UR)",
    /// "Buerglen (UR)", "Seedorf (UR)"), weil es diese Namen auch in anderen Kantonen
    /// gibt. Die Faltung in <see cref="Falte"/> entfernt Zusatz und Umlaute nur fuer
    /// den Vergleich — der Name selbst bleibt unangetastet und geht zeichengleich in
    /// die XTF.
    /// </summary>
    private static readonly string[] UrnerGemeinden =
    [
        "altdorf", "andermatt", "attinghausen", "burglen", "erstfeld", "fluelen",
        "goschenen", "gurtnellen", "hospental", "isenthal", "realp", "schattdorf",
        "seedorf", "seelisberg", "silenen", "sisikon", "spiringen", "unterschachen",
        "wassen"
    ];

    /// <summary>
    /// Benannte Organisationen des Bestands, die kein Konzept des Programms sind.
    /// Gemessen am Abwassernetz des Kantons Uri (110297 Haltungen, 69197 Schaechte).
    /// </summary>
    private static readonly (string Teil, string Typ)[] BenannteOrganisationen =
    [
        // "ASTRA - Bundesamt fuer Strassen", 14497 Haltungen.
        ("astra", "Bund"),
        ("bundesamt", "Bund"),
        // "Korporation Uri" (908) und "Meliorationsgenossenschaft Reussebene Uri" (1041).
        ("korporation", "Genossenschaft_Korporation"),
        ("genossenschaft", "Genossenschaft_Korporation"),
        // "Meliorationsgesellschaft Seedorf", 608 Haltungen. Das Modell sagt zu
        // Genossenschaft_Korporation: "Koerperschaft oeffentlichen Rechts. Falls
        // privaten Rechtes dann als Privat abbilden." Ob diese Gesellschaft
        // oeffentlich- oder privatrechtlich ist, ist offen — bis dahin gilt dieselbe
        // Einordnung wie beim QGIS-Exporter, damit beide Seiten nicht auseinanderlaufen.
        ("meliorationsgesellschaft", "Genossenschaft_Korporation")
    ];

    /// <summary>
    /// Der Organisationstyp nach SIA405 zu einem Eigentuemer, oder <c>null</c>.
    ///
    /// Der Name selbst wird dabei NIE veraendert — er geht zeichengleich in die XTF,
    /// samt Umlauten und Kantonszusatz. Hier wird nur der Typ bestimmt, den das Modell
    /// zusaetzlich zum Namen verlangt.
    ///
    /// Fail-closed: Wofuer kein Typ belegt ist, entsteht keine Organisation. Ohne Typ
    /// darf in der XTF keine entstehen — <c>Organisationstyp</c> ist dort ein
    /// Pflichtfeld, und ein geratener Wert waere eine Aussage, die niemand getroffen hat.
    ///
    /// <c>unbekannt</c> ist die einzige erzwungene Wahl: Das Modell kennt bei
    /// <c>Organisationstyp</c> kein "unbekannt", und die Assoziation ist mit
    /// Kardinalitaet 1 zwingend. "Privat" ist dabei die schwaechste der sieben
    /// Behauptungen; die Bezeichnung sagt weiterhin, was Sache ist.
    /// </summary>
    public static string? NachOrganisationstyp(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return null;

        var klein = text.ToLowerInvariant();
        var konzept = Konzepte.FirstOrDefault(k =>
            k.Gelesen.Contains(klein)
            || string.Equals(k.App, text, StringComparison.OrdinalIgnoreCase));
        if (konzept?.Organisationstyp is { } typ)
            return typ;

        if (string.Equals(klein, "unbekannt", StringComparison.Ordinal))
            return "Privat";

        var gefaltet = Falte(text);
        if (UrnerGemeinden.Contains(gefaltet, StringComparer.Ordinal))
            return "Gemeinde";

        foreach (var (teil, benannt) in BenannteOrganisationen)
        {
            if (klein.Contains(teil, StringComparison.Ordinal))
                return benannt;
        }

        return null;
    }

    /// <summary>
    /// Bringt einen Namen nur fuer den Vergleich auf eine schlichte Form: Kantonszusatz
    /// weg, Umlaute aufgeloest, klein. Der Rueckgabewert wird nie ausgegeben.
    /// </summary>
    private static string Falte(string text)
    {
        var ohneZusatz = text;
        var klammer = ohneZusatz.IndexOf('(');
        if (klammer > 0)
            ohneZusatz = ohneZusatz[..klammer];

        return ohneZusatz
            .Trim()
            .ToLowerInvariant()
            .Replace("ä", "a", StringComparison.Ordinal)
            .Replace("ö", "o", StringComparison.Ordinal)
            .Replace("ü", "u", StringComparison.Ordinal)
            .Replace("é", "e", StringComparison.Ordinal)
            .Replace("è", "e", StringComparison.Ordinal)
            .Replace("ss", "ss", StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Auswahl im Programm — leer plus genau die fuenf Werte, welche die Excel-Vorlage
    /// faerbt. Ein sechster Eintrag hier waere eine farblose Zelle dort.
    /// </summary>
    public static readonly IReadOnlyList<string> Auswahl = new ReadOnlyCollection<string>(
        new[] { "" }
            .Concat(Konzepte.Select(k => k.App))
            .ToList());

    /// <summary>
    /// Bringt eine beliebige gelesene Schreibweise auf den Begriff des Programms.
    ///
    /// Ein unbekannter Wert bleibt unveraendert stehen. Eine Korporation oder
    /// Genossenschaft ist eine echte Angabe — sie zu loeschen waere schlimmer, als sie
    /// ohne Farbe stehen zu lassen.
    /// </summary>
    public static string Normalisieren(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return "";

        var klein = text.ToLowerInvariant();
        var treffer = Konzepte.FirstOrDefault(k =>
            k.Gelesen.Contains(klein)
            || string.Equals(k.App, text, StringComparison.OrdinalIgnoreCase));

        return treffer?.App ?? text;
    }
}
