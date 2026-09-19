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
///
/// Der SchachtPro-Export (GKS Cahenzli, Goeschenen 2026) hat eine eigene Tabelle: Kennung
/// (A1, E1 …), Uhrzeit, Tiefe, Durchmesser, Typ, Medium, Material, Zustand. pdftotext schiebt
/// dort die Zellen «150 mm Auslauf» um eine Zeile nach unten; die Durchmesser kommen deshalb
/// aus der Skizzenlegende («A1 DN150») und erst ersatzweise aus der verschobenen Spalte in
/// Tabellenreihenfolge. Die Uhrzeit ist die Uhrlage am Umfang (12 = Auslauf).
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

    // SchachtPro-Export: Kopfzeile «SCHACHTPRO» oder die Tabelle «Ansc… Uhrzeit Tiefe …».
    private static readonly Regex SchachtProErkennungRegex = new(@"^\s*(?:SCHACHTPRO\b|Ansc\S*\s+Uhrzeit\s+Tiefe\b)", Zeilenweise);
    private static readonly Regex SchachtProKopfRegex = new(@"^\s*Ansc\S*\s+Uhrzeit\s+Tiefe\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SchachtProEndeRegex = new(@"^\s*(?:[A-ZÄÖÜ][A-ZÄÖÜ &/\-]{4,}\s*$|.*\bSeite\s+\d+\s+von\s+\d+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SchachtProZeileRegex = new(
        @"^\s*(?<k>[AE])(?<n>\d{1,2})\s+(?<uhr>\d{1,2}(?:[.:,]\d{1,2})?)\s+(?<tiefe>\d{1,3}(?:[.,]\d{1,3})?)\b(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SchachtProDnTypRegex = new(@"(?<dn>\d{2,4})\s*mm\s+(?:Auslauf|Einlauf|Ablauf|Zulauf)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SchachtProLegendeRegex = new(@"\b(?<k>[AE]\d{1,2})\s+DN\s*(?<dn>\d{2,4})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SchachtProMaterialRegex = new(@"^[ \t]*Material[ \t]{2,}(?<v>\S+(?: \S+)*)", Zeilenweise);
    private static readonly Regex SchachtProDeckelmaterialRegex = new(@"\bDeckelmaterial[ \t]+(?<v>\S+(?: \S+)*)", Zeilenweise);
    private static readonly Regex SchachtProDeckelDurchmesserRegex = new(@"\bDeckeldurchmesser[ \t]*\([ \t]*m[ \t]*\)[ \t:]*(?<v>\d+(?:[.,]\d+)?)", Zeilenweise);
    private static readonly Regex SchachtProSteighilfeRegex = new(@"^[ \t]*(?:Leiter/Steigeisen|Leiter|Steigeisen)[ \t]{2,}(?<v>vorhanden|fehlt|zu kurz|verrostet|defekt)\b", Zeilenweise);
    private static readonly Regex SchachtProTauchbogenRegex = new(@"^[ \t]*Tauchbogen[ \t]{2,}(?<v>vorhanden|fehlt|defekt|nicht notwendig)\b", Zeilenweise);

    internal static SchachtProtocolZusatz Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return SchachtProtocolZusatz.Leer;

        var normalized = SchachtProtocolParser.NormalizeCheckboxGlyphs(SchachtProtocolParser.NormalizePdfText(text));
        var zeilen = normalized.Split('\n');

        if (IstSchachtPro(normalized))
        {
            return new SchachtProtocolZusatz(
                AnschluesseSchachtPro(normalized, zeilen),
                Wert(MediumRegex, normalized),
                Wert(SchachtProMaterialRegex, normalized),
                Wert(SchachtProDeckelmaterialRegex, normalized),
                DeckelDurchmesserMm(SchachtProDeckelDurchmesserRegex, normalized),
                SchachtProWahl(SchachtProSteighilfeRegex, normalized),
                SchachtProWahl(SchachtProTauchbogenRegex, normalized));
        }

        return new SchachtProtocolZusatz(
            Anschluesse(zeilen),
            Wert(MediumRegex, normalized),
            Wert(MaterialSchachtRegex, normalized),
            Wert(MaterialDeckelRegex, normalized),
            DeckelDurchmesserMm(DeckelDnRegex, normalized),
            Kaestchenwahl(zeilen, @"Leiter/Steigeisen|Leiter|Steigeisen", SteighilfeWoerter),
            Kaestchenwahl(zeilen, @"Tauchbogen", TauchbogenWoerter));
    }

    /// <summary>SchachtPro-Export: Kopfzeile «SCHACHTPRO» oder die Tabelle «Ansc… Uhrzeit Tiefe».</summary>
    internal static bool IstSchachtPro(string normalized) => SchachtProErkennungRegex.IsMatch(normalized ?? string.Empty);

    /// <summary>
    /// Die SchachtPro-Tabelle: je Zeile Kennung, Uhrzeit und Tiefe; Durchmesser aus der
    /// Skizzenlegende («A1 DN150»), sonst aus der um eine Zeile verschobenen Spalte, wenn sie
    /// genau so viele Werte hat wie Zeilen; Art aus dem Buchstaben der Kennung; Material aus der
    /// Spalte nach dem Medium. Die laufende Nummer ist die Tabellenreihenfolge.
    /// </summary>
    private static List<SchachtAnschluss> AnschluesseSchachtPro(string normalized, string[] zeilen)
    {
        var legende = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in SchachtProLegendeRegex.Matches(normalized))
            legende.TryAdd(m.Groups["k"].Value.ToUpperInvariant(), int.Parse(m.Groups["dn"].Value, CultureInfo.InvariantCulture));

        var block = new List<string>();
        var inTabelle = false;
        foreach (var zeile in zeilen)
        {
            if (!inTabelle)
            {
                inTabelle = SchachtProKopfRegex.IsMatch(zeile);
                continue;
            }

            if (SchachtProEndeRegex.IsMatch(zeile))
                break;
            block.Add(zeile);
        }

        var reihen = new List<(string Kennung, string Uhr, decimal Tiefe, string Zeilenrest)>();
        foreach (var zeile in block)
        {
            var m = SchachtProZeileRegex.Match(zeile);
            if (!m.Success)
                continue;
            if (!decimal.TryParse(m.Groups["tiefe"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var tiefe))
                continue;
            reihen.Add((m.Groups["k"].Value.ToUpperInvariant() + m.Groups["n"].Value, m.Groups["uhr"].Value, tiefe, m.Groups["rest"].Value));
        }

        var verschobeneDn = SchachtProDnTypRegex.Matches(string.Join("\n", block))
            .Select(m => int.Parse(m.Groups["dn"].Value, CultureInfo.InvariantCulture))
            .ToList();

        var liste = new List<SchachtAnschluss>();
        for (var i = 0; i < reihen.Count; i++)
        {
            var (kennung, uhr, tiefe, rest) = reihen[i];
            int? dn = legende.TryGetValue(kennung, out var ausLegende)
                ? ausLegende
                : verschobeneDn.Count == reihen.Count ? verschobeneDn[i] : null;
            liste.Add(new SchachtAnschluss
            {
                Nr = i + 1,
                Art = kennung[0] == 'A' ? "Auslauf" : "Einlauf",
                DnMm = dn,
                TiefeM = tiefe,
                Material = MaterialAusSchachtProZeile(rest),
                Uhr = uhr,
                Quelle = "SchachtPro"
            });
        }

        return liste;
    }

    /// <summary>
    /// Der Rest einer SchachtPro-Zeile in Spalten (zwei oder mehr Leerzeichen): allenfalls
    /// «150 mm» und «Auslauf», dann Medium, Material, Zustand. Das Material ist die Spalte nach
    /// dem Medium; ohne erkennbares Medium die vorletzte Spalte.
    /// </summary>
    private static string? MaterialAusSchachtProZeile(string rest)
    {
        var spalten = Regex.Split(rest.Trim(), @"\s{2,}")
            .Select(s => Regex.Replace(s, @"^\d{2,4}\s*mm(?:\s+(?:Auslauf|Einlauf|Ablauf|Zulauf))?\s*|^(?:Auslauf|Einlauf|Ablauf|Zulauf)\s*$", "", RegexOptions.IgnoreCase).Trim())
            .Where(s => s.Length > 0)
            .ToList();
        var medium = spalten.FindIndex(s => s.EndsWith("wasser", StringComparison.OrdinalIgnoreCase));
        var material = medium >= 0 && medium + 1 < spalten.Count
            ? spalten[medium + 1]
            : spalten.Count >= 2 ? spalten[^2] : null;
        return string.IsNullOrWhiteSpace(material) || material == "-" ? null : material.Trim();
    }

    /// <summary>SchachtPro schreibt das Wort aus: «vorhanden», «fehlt», «nicht notwendig»; ein Schaden (defekt, zu kurz, verrostet) heisst vorhanden.</summary>
    private static string? SchachtProWahl(Regex regex, string text)
    {
        var wert = Wert(regex, text)?.ToLowerInvariant();
        if (wert is null)
            return null;
        return wert is "fehlt" or "nicht notwendig" ? wert : "vorhanden";
    }

    private static string? Wert(Regex regex, string text)
    {
        var m = regex.Match(text);
        if (!m.Success)
            return null;

        var wert = m.Groups["v"].Value.Trim();
        return wert.Length == 0 || wert == "-" ? null : wert;
    }

    /// <summary>«Deckel DN m 0.66» oder «Deckeldurchmesser (m) 0.50» → Millimeter. Ein Wert ab 100 gilt bereits als Millimeter.</summary>
    private static string? DeckelDurchmesserMm(Regex regex, string text)
    {
        var m = regex.Match(text);
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
