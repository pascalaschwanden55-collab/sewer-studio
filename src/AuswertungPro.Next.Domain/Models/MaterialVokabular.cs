using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Das Rohr- und Bauwerksmaterial — mit den Begriffen der Norm, an einer Stelle.
///
/// Massgebend ist die Modelldatei SIA405_Abwasser_2020_2_d_LV95 (VSA-Modellablage):
/// 24 zulaessige Werte fuer Haltung.Material. Der Kantonsexport von Abwasser Uri
/// (109871 Haltungen) belegt davon 21 - eine Auszaehlung sagt, was vorkommt, nicht
/// was erlaubt ist. Beton_Pressrohrbeton, Ton und Zement kommen in Uri nicht vor,
/// sind aber gueltig.
///
/// Warum es das braucht: Die frueheren Regeln bildeten mehrere Normwerte auf einen
/// ab — vier Betonarten wurden zu "Beton", zwei Gussarten zu "Guss". Das betraf
/// 14510 Haltungen und war nicht umkehrbar. "Asbestzement" wurde sogar zu "Zement",
/// also zu einem anderen Werkstoff mit ganz anderer Sanierung und anderem
/// Arbeitsschutz.
///
/// Aufbau wie <see cref="NutzungsartVokabular"/>: Der Wert im Programm bleibt
/// lesbar und moeglichst so, wie er bisher schon dastand; die Schreibweise fuer die
/// Datei ist davon getrennt. Dadurch aendert sich in bestehenden Projekten nichts,
/// und der Rueckweg in die XTF ist trotzdem zeichengenau.
///
/// Reine Werte-Logik ohne Zustand und ohne Dateizugriff.
/// </summary>
public static class MaterialVokabular
{
    /// <summary>
    /// Ein Werkstoff mit allen Schreibweisen, die dafuer gelesen werden, dem Begriff
    /// im Programm und der Schreibweise in der SIA405-Datei.
    /// </summary>
    /// <summary>
    /// Ein Werkstoff. <paramref name="Norm"/> ist <c>null</c>, wenn die Norm dafuer
    /// keinen Wert kennt — der Begriff bleibt dann waehlbar und lesbar, kann aber
    /// nie in eine XTF geraten.
    /// </summary>
    /// <param name="Bis2015">
    /// Die Schreibweise in SIA405 2015, sofern an echten Dateien belegt. <c>null</c>
    /// heisst nicht "gibt es nicht", sondern "nicht belegt" — und dann wird in eine
    /// 2015-Datei nichts geschrieben. VSA veroeffentlicht das 2015-Modell nicht mehr,
    /// deshalb ist die Liste nur so lang wie die Belege reichen.
    /// </param>
    private sealed record Konzept(string[] Gelesen, string App, string? Norm, string? Bis2015 = null);

    private static readonly Konzept[] Konzepte =
    [
        // --- Kunststoffe ---
        // Die Norm schreibt "Polyvinilchlorid" mit i. Beide Schreibweisen muessen
        // gelesen werden, sonst greift die Regel bei echten Dateien mal nicht.
        new(["kunststoff_polyvinilchlorid", "kunststoff_polyvinylchlorid",
             "polyvinylchlorid", "polyvinilchlorid",
             "polyvinylchlorid (pvc)", "pvc", "kunststoff pvc"],
            "Polyvinylchlorid", "Kunststoff_Polyvinilchlorid", "Polyvinylchlorid"),
        new(["kunststoff_hartpolyethylen", "hartpolyethylen", "hart-polyethylen (hdpe)",
             "hartpolyethylen (hdpe)", "hdpe", "pe-hd", "pehd", "pe_hd"],
            "Hartpolyethylen", "Kunststoff_Hartpolyethylen"),
        new(["kunststoff_polyethylen", "polyethylen", "polyethylen (pe)", "pe", "kunststoff pe"],
            "Polyethylen", "Kunststoff_Polyethylen", "Polyethylen"),
        new(["kunststoff_polypropylen", "polypropylen", "polypropylen (pp)", "pp"],
            "Polypropylen", "Kunststoff_Polypropylen", "Polypropylen"),
        new(["kunststoff_epoxydharz", "epoxydharz", "epoxidharz"],
            "Epoxydharz", "Kunststoff_Epoxydharz"),
        new(["kunststoff_polyester_gup", "polyester gup", "gup"],
            "Polyester GUP", "Kunststoff_Polyester_GUP"),
        new(["kunststoff_unbekannt"],
            "Kunststoff unbekannt", "Kunststoff_unbekannt"),

        // --- Beton: vier eigenstaendige Arten, frueher alle "Beton" ---
        new(["beton_normalbeton", "normalbeton"], "Normalbeton", "Beton_Normalbeton"),
        new(["beton_spezialbeton", "spezialbeton"], "Spezialbeton", "Beton_Spezialbeton"),
        new(["beton_ortsbeton", "ortsbeton"], "Ortsbeton", "Beton_Ortsbeton"),
        new(["beton_pressrohrbeton", "pressrohrbeton"], "Pressrohrbeton", "Beton_Pressrohrbeton"),
        // Ein blosses "Beton" sagt nicht, welche Art. Der Normwert dafuer heisst
        // ausdruecklich "unbekannt" — das ist keine Erfindung, sondern die Aussage.
        new(["beton_unbekannt", "beton"], "Beton", "Beton_unbekannt", "Beton"),

        // --- Mineralisch ---
        new(["steinzeug"], "Steinzeug", "Steinzeug"),
        new(["faserzement"], "Faserzement", "Faserzement"),
        new(["asbestzement", "az"], "Asbestzement", "Asbestzement"),
        new(["gebrannte_steine", "gebrannte steine"], "Gebrannte Steine", "Gebrannte_Steine"),
        new(["ton"], "Ton", "Ton"),
        new(["zement"], "Zement", "Zement", "Zement"),

        // --- Metalle: "Guss" allein bleibt bewusst unaufgeloest ---
        new(["stahl"], "Stahl", "Stahl"),
        new(["stahl_rostfrei", "stahl rostfrei"], "Stahl rostfrei", "Stahl_rostfrei"),
        new(["guss_duktil", "guss duktil", "duktilguss"], "Guss duktil", "Guss_duktil"),
        new(["guss_grauguss", "grauguss"], "Grauguss", "Guss_Grauguss"),

        // --- Sammelwerte der Norm ---
        new(["andere"], "andere", "andere"),
        new(["unbekannt"], "unbekannt", "unbekannt"),

        // --- Altwerte ohne Normziel ---
        // Sie stehen in Bestandsprojekten und bleiben deshalb waehlbar; faellt der
        // Eintrag aus der Liste, zeigt das Feld leer an, obwohl ein Wert gespeichert
        // ist. In eine XTF koennen sie nie geraten: NachNorm liefert null, also
        // wird nichts geschrieben.
        //
        // "Guss" allein sagt nicht, ob duktil oder Grauguss - beide stehen einzeln
        // in der Liste. "GFK" ist nicht dasselbe wie Kunststoff_Polyester_GUP.
        new(["guss"], "Guss", null),
        new(["gfk", "glasfaser", "glasfaserverstaerkter kunststoff",
             "glasfaserverstärkter kunststoff (gfk)"], "GFK", null)
    ];

    /// <summary>
    /// Altwerte der Auswahlliste, denen kein Normwert sicher entspricht. Sie bleiben
    /// waehlbar und lesbar, werden aber nie in eine XTF geschrieben — "Guss" allein
    /// sagt nicht, ob duktil oder Grauguss, und "GFK" ist nicht dasselbe wie
    /// "Kunststoff_Polyester_GUP". Raten waere hier schlimmer als schweigen.
    /// </summary>


    /// <summary>
    /// Die Auswahl im Programm: leer plus genau ein Begriff je Werkstoff.
    /// Keine zweite Schreibweise daneben — "PVC" wird weiterhin gelesen, steht aber
    /// nicht zur Auswahl. Damit kann kein Listeneintrag einen ungueltigen Wert in
    /// eine XTF schreiben.
    /// </summary>
    public static readonly IReadOnlyList<string> Auswahl = new ReadOnlyCollection<string>(
        new[] { "" }
            .Concat(Konzepte.Select(k => k.App))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList());

    /// <summary>
    /// Bringt eine beliebige gelesene Schreibweise auf den Begriff des Programms.
    /// Ein unbekannter Wert bleibt unveraendert stehen: Er koennte eine Angabe
    /// enthalten, die niemand sonst kennt — sie zu loeschen waere schlimmer.
    /// </summary>
    public static string Normalisieren(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return "";

        return Finde(text)?.App ?? text;
    }

    /// <summary>
    /// Die in SIA405 gueltige Schreibweise, oder <c>null</c>, wenn der Wert dort zu
    /// keinem Begriff gehoert. Dann wird nichts geschrieben statt geraten.
    /// </summary>
    public static string? NachNorm(string? wert) => Finde((wert ?? "").Trim())?.Norm;

    /// <summary>
    /// Die in der Zielfassung gueltige Schreibweise, oder <c>null</c>, wenn sie dort
    /// nicht belegt ist. Dann wird nichts geschrieben statt geraten.
    ///
    /// Die 2015-Fassung fuehrt die Werkstoffe ohne Kategorie-Praefix: <c>Polyethylen</c>
    /// statt <c>Kunststoff_Polyethylen</c>, <c>Beton</c> statt <c>Beton_unbekannt</c>.
    /// Sie ist ausserdem groeber — aus dem 2015-Wert <c>Polyethylen</c> wurde 2020 sowohl
    /// <c>Kunststoff_Polyethylen</c> (18x) als auch <c>Kunststoff_Hartpolyethylen</c> (9x),
    /// gemessen an den 77 Haltungen, die in beiden Fassungen vorliegen. Der Rueckweg
    /// waere deshalb ein Informationsverlust und wird nicht gegangen.
    ///
    /// Belegt sind nur die fuenf Werkstoffe, die in echten 2015-Kundendateien vorkommen.
    /// Fuer alle uebrigen bleibt <c>Bis2015</c> leer: VSA veroeffentlicht das 2015-Modell
    /// nicht mehr, und "sieht eindeutig aus" hat mich beim Zement schon einmal in die
    /// Irre gefuehrt.
    /// </summary>
    /// <param name="ab2020">
    /// <c>true</c> fuer SIA405 2020 und neuer, <c>false</c> fuer aeltere Fassungen,
    /// <c>null</c>, wenn die Fassung der Datei nicht erkennbar ist.
    /// </param>
    public static string? NachModell(string? wert, bool? ab2020)
    {
        var konzept = Finde((wert ?? "").Trim());
        if (konzept is null)
            return null;

        if (konzept.Norm is not null && string.Equals(konzept.Norm, konzept.Bis2015, StringComparison.Ordinal))
            return konzept.Norm;

        // Nur hier entscheidet die Fassung — ohne sie waere jede Wahl geraten.
        return ab2020 switch
        {
            true => konzept.Norm,
            false => konzept.Bis2015,
            _ => null
        };
    }

    private static Konzept? Finde(string text)
    {
        if (text.Length == 0)
            return null;

        var klein = text.ToLowerInvariant();

        // Leerzeichen wie Unterstrich behandeln. In echten Projekten steht
        // "Beton Normalbeton" - der alte Normalisierer ersetzte Unterstriche durch
        // Leerzeichen, wenn er einen Wert nicht kannte. Diese Werte muessen lesbar
        // bleiben, sonst faellt ein Bestandsprojekt aus der Auswahlliste.
        var mitUnterstrich = klein.Replace(' ', '_');

        // Der Begriff des Programms zaehlt immer als gelesene Schreibweise. Sonst
        // muesste er in jeder Leseliste doppelt stehen, und ein vergessener Eintrag
        // machte den eigenen App-Wert unlesbar.
        return Konzepte.FirstOrDefault(k =>
            k.Gelesen.Contains(klein)
            || k.Gelesen.Contains(mitUnterstrich)
            || string.Equals(k.App, text, StringComparison.OrdinalIgnoreCase));
    }
}
