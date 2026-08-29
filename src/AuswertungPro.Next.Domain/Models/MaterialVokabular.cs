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
    private sealed record Konzept(string[] Gelesen, string App, string Norm);

    private static readonly Konzept[] Konzepte =
    [
        // --- Kunststoffe ---
        // Die Norm schreibt "Polyvinilchlorid" mit i. Beide Schreibweisen muessen
        // gelesen werden, sonst greift die Regel bei echten Dateien mal nicht.
        new(["kunststoff_polyvinilchlorid", "kunststoff_polyvinylchlorid",
             "polyvinylchlorid", "polyvinilchlorid",
             "polyvinylchlorid (pvc)", "pvc", "kunststoff pvc"],
            "Polyvinylchlorid", "Kunststoff_Polyvinilchlorid"),
        new(["kunststoff_hartpolyethylen", "hartpolyethylen", "hart-polyethylen (hdpe)",
             "hartpolyethylen (hdpe)", "hdpe", "pe-hd", "pehd", "pe_hd"],
            "Hartpolyethylen", "Kunststoff_Hartpolyethylen"),
        new(["kunststoff_polyethylen", "polyethylen", "polyethylen (pe)", "pe", "kunststoff pe"],
            "Polyethylen", "Kunststoff_Polyethylen"),
        new(["kunststoff_polypropylen", "polypropylen", "polypropylen (pp)", "pp"],
            "Polypropylen", "Kunststoff_Polypropylen"),
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
        new(["beton_unbekannt", "beton"], "Beton", "Beton_unbekannt"),

        // --- Mineralisch ---
        new(["steinzeug"], "Steinzeug", "Steinzeug"),
        new(["faserzement"], "Faserzement", "Faserzement"),
        new(["asbestzement", "az"], "Asbestzement", "Asbestzement"),
        new(["gebrannte_steine", "gebrannte steine"], "Gebrannte Steine", "Gebrannte_Steine"),
        new(["ton"], "Ton", "Ton"),
        new(["zement"], "Zement", "Zement"),

        // --- Metalle: "Guss" allein bleibt bewusst unaufgeloest ---
        new(["stahl"], "Stahl", "Stahl"),
        new(["stahl_rostfrei", "stahl rostfrei"], "Stahl rostfrei", "Stahl_rostfrei"),
        new(["guss_duktil", "guss duktil", "duktilguss"], "Guss duktil", "Guss_duktil"),
        new(["guss_grauguss", "grauguss"], "Grauguss", "Guss_Grauguss"),

        // --- Sammelwerte der Norm ---
        new(["andere"], "andere", "andere"),
        new(["unbekannt"], "unbekannt", "unbekannt")
    ];

    /// <summary>
    /// Altwerte der Auswahlliste, denen kein Normwert sicher entspricht. Sie bleiben
    /// waehlbar und lesbar, werden aber nie in eine XTF geschrieben — "Guss" allein
    /// sagt nicht, ob duktil oder Grauguss, und "GFK" ist nicht dasselbe wie
    /// "Kunststoff_Polyester_GUP". Raten waere hier schlimmer als schweigen.
    /// </summary>
    private static readonly string[] AltwerteOhneNorm =
        ["Guss", "GFK", "Glasfaser"];

    /// <summary>
    /// Kurzformen, die bis heute in der Auswahlliste standen und in bestehenden
    /// Projekten gespeichert sind. Sie bleiben waehlbar, sonst zeigte ein altes
    /// Projekt an dieser Stelle nichts mehr an. Ueber die Leselisten finden sie
    /// trotzdem ihren Normwert.
    /// </summary>
    private static readonly string[] AltweisenMitNorm = ["PVC", "PE", "PP"];

    /// <summary>
    /// Die Auswahl im Programm: leer, die Begriffe aller Konzepte und die Altwerte
    /// ohne Normentsprechung. Letztere muessen drinbleiben, sonst zeigt ein
    /// bestehendes Projekt an dieser Stelle nichts mehr an.
    /// </summary>
    public static readonly IReadOnlyList<string> Auswahl = new ReadOnlyCollection<string>(
        new[] { "" }
            .Concat(Konzepte.Select(k => k.App))
            .Concat(AltwerteOhneNorm)
            .Concat(AltweisenMitNorm)
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

    private static Konzept? Finde(string text)
    {
        if (text.Length == 0)
            return null;

        var klein = text.ToLowerInvariant();

        // Der Begriff des Programms zaehlt immer als gelesene Schreibweise. Sonst
        // muesste er in jeder Leseliste doppelt stehen, und ein vergessener Eintrag
        // machte den eigenen App-Wert unlesbar.
        return Konzepte.FirstOrDefault(k =>
            k.Gelesen.Contains(klein)
            || string.Equals(k.App, text, StringComparison.OrdinalIgnoreCase));
    }
}
