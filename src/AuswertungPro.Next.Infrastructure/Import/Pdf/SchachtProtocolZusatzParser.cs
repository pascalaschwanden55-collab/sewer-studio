using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Liest aus dem Text eines Schachtprotokolls, was bisher liegen blieb: die Anschlusstabelle
/// («Nr Aus/Ein DN mm Tiefe m Material»), Medium, Material von Schacht und Deckel, den
/// Deckeldurchmesser («Deckel DN m 0.66») sowie die Kaestchen «Leiter/Steigeisen» und
/// «Tauchbogen». Reine Textlogik ohne Dateizugriff; was nicht im Text steht, bleibt null.
///
/// Die Tabelle steht als Text im PDF (mit pdftotext geprueft, 120 von 581 Schacht-PDFs).
/// Eine Zeile zaehlt nur mit Nummer, Art, DN und Tiefe; leere Zeilen («5 -  -») und die
/// Skizzenbeschriftungen rechts daneben (A1, E1 …) werden nicht zu Anschluessen.
/// </summary>
internal static class SchachtProtocolZusatzParser
{
    private const RegexOptions Zeilenweise = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // Ein Wert reicht bis zum naechsten Spaltenabstand (zwei oder mehr Leerzeichen): Im
    // Layouttext steht «Material Deckel  Guss und Beton      Deckel DN m  0.66» auf EINER Zeile.
    private static readonly Regex MediumRegex = new(@"^[ \t]*Medium[ \t]+(?<v>\S+(?: \S+)*)", Zeilenweise);
    private static readonly Regex MaterialSchachtRegex = new(@"^[ \t]*Material[ \t]+Schacht[ \t]+(?<v>\S+(?: \S+)*)", Zeilenweise);
    private static readonly Regex MaterialDeckelRegex = new(@"^[ \t]*Material[ \t]+Deckel[ \t]+(?<v>\S+(?: \S+)*)", Zeilenweise);
    private static readonly Regex DeckelDnRegex = new(@"Deckel[ \t]+DN[ \t]*m\b[ \t:]*(?<v>\d+(?:[.,]\d+)?)", Zeilenweise);
    private static readonly Regex TabellenkopfRegex = new(@"^\s*Anschl\S{0,2}sse\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TabellenendeRegex = new(@"^\s*(?:Datum|Visum|Bemerkung(?:en)?|Foto|Seite\s+\d)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Nummer, Art, DN und Tiefe sind Pflicht; das Material sind Woerter aus mindestens zwei
    // Buchstaben mit je EINEM Leerzeichen dazwischen («Guss und Beton»). Die Beschriftungen der
    // Handskizze («E3») stehen nach einem groesseren Abstand und sind nur ein Buchstabe lang.
    private static readonly Regex ZeileRegex = new(
        @"^\s*(?<nr>\d{1,2})\s+(?<art>Auslauf|Einlauf|Ablauf|Zulauf)\b\s+(?<dn>\d{2,4})\s+(?<tiefe>\d{1,3}(?:[.,]\d{1,3})?)" +
        @"(?:\s+(?<mat>\p{L}{2,}[\p{L}./\-]*(?: \p{L}{2,}[\p{L}./\-]*)*))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] SteighilfeWoerter = ["vorhanden", "fehlt", "zu kurz", "verrostet", "defekt"];
    private static readonly string[] TauchbogenWoerter = ["vorhanden", "fehlt", "defekt", "nicht notwendig"];

    internal static SchachtProtocolZusatz Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return SchachtProtocolZusatz.Leer;

        var normalized = SchachtProtocolParser.NormalizeCheckboxGlyphs(SchachtProtocolParser.NormalizePdfText(text));
        var zeilen = normalized.Split('\n');

        return new SchachtProtocolZusatz(
            Anschluesse(zeilen),
            Wert(MediumRegex, normalized),
            Wert(MaterialSchachtRegex, normalized),
            Wert(MaterialDeckelRegex, normalized),
            DeckelDurchmesserMm(normalized),
            Kaestchenwahl(zeilen, @"Leiter/Steigeisen|Leiter|Steigeisen", SteighilfeWoerter),
            Kaestchenwahl(zeilen, @"Tauchbogen", TauchbogenWoerter));
    }

    private static string? Wert(Regex regex, string text)
    {
        var m = regex.Match(text);
        if (!m.Success)
            return null;

        var wert = m.Groups["v"].Value.Trim();
        return wert.Length == 0 || wert == "-" ? null : wert;
    }

    /// <summary>«Deckel DN m 0.66» → 660 (Millimeter). Ein Wert ab 100 gilt bereits als Millimeter.</summary>
    private static string? DeckelDurchmesserMm(string text)
    {
        var m = DeckelDnRegex.Match(text);
        if (!m.Success)
            return null;

        if (!decimal.TryParse(m.Groups["v"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var wert)
            || wert <= 0)
        {
            return null;
        }

        var mm = wert >= 100m ? wert : wert * 1000m;
        return Math.Round(mm, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
    }

    private static List<SchachtAnschluss> Anschluesse(string[] zeilen)
    {
        var liste = new List<SchachtAnschluss>();
        var inTabelle = false;
        foreach (var zeile in zeilen)
        {
            if (!inTabelle)
            {
                inTabelle = TabellenkopfRegex.IsMatch(zeile);
                continue;
            }

            if (TabellenendeRegex.IsMatch(zeile))
                break;

            var m = ZeileRegex.Match(zeile);
            if (!m.Success)
                continue;

            if (!decimal.TryParse(m.Groups["tiefe"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var tiefe))
                continue;

            var art = m.Groups["art"].Value;
            liste.Add(new SchachtAnschluss
            {
                Nr = int.Parse(m.Groups["nr"].Value, CultureInfo.InvariantCulture),
                Art = art.StartsWith("Aus", StringComparison.OrdinalIgnoreCase) || art.StartsWith("Abl", StringComparison.OrdinalIgnoreCase)
                    ? "Auslauf"
                    : "Einlauf",
                DnMm = int.Parse(m.Groups["dn"].Value, CultureInfo.InvariantCulture),
                TiefeM = tiefe,
                Material = m.Groups["mat"].Success ? m.Groups["mat"].Value.Trim() : null,
                Quelle = "PDF"
            });
        }

        return liste;
    }

    /// <summary>
    /// Die Wahl einer Kaestchenzeile: «vorhanden» oder «nicht notwendig» gewinnen, «fehlt»
    /// bleibt «fehlt», ein markierter Schaden (zu kurz, verrostet, defekt) heisst vorhanden —
    /// es gibt das Teil, es ist beschaedigt. Ohne Marke null.
    /// </summary>
    private static string? Kaestchenwahl(string[] zeilen, string zeilenanfang, IReadOnlyList<string> woerter)
    {
        foreach (var zeile in zeilen)
        {
            var m = Regex.Match(zeile, @"^\s*(?:" + zeilenanfang + @")\b(?<tail>.*)$", RegexOptions.IgnoreCase);
            if (!m.Success)
                continue;

            var markiert = new List<string>();
            foreach (var segment in SchachtProtocolKaestchenformular.SegmenteNachMarke(m.Groups["tail"].Value))
            {
                var wort = SchachtProtocolKaestchenformular.WortAmAnfang(woerter, segment);
                if (wort is not null)
                    markiert.Add(wort.ToLowerInvariant());
            }

            if (markiert.Count == 0)
                return null;
            if (markiert.Contains("vorhanden"))
                return "vorhanden";
            if (markiert.Contains("nicht notwendig"))
                return "nicht notwendig";
            if (markiert.Contains("fehlt"))
                return "fehlt";
            return "vorhanden";
        }

        return null;
    }
}
