using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Regeln fuer das Kaestchenformular (Schachtprotokoll Abwasser Uri): Jede Zeile von
/// «Zustand der Bauteile» nennt den GANZEN Wortvorrat («gerissen ausgebrochen korrodiert …»),
/// angekreuzt ist nur, was eine Marke (●/✔) traegt — und die Marke steht VOR ihrem Wort.
///
/// Anlass (19.09.2026): Schacht 80409 stand mit 27 Schaeden im Projekt, das Protokoll kreuzt
/// vier an. Der Freitextweg des Zustandsabschnitts nahm jedes bekannte Wort der Zeile, und
/// die alte Nachbar-Regel «Wort gefolgt von Marke» band ausserdem das Wort VOR einer Marke,
/// also das falsche. Hier gilt nur die eine Richtung: Marke → folgendes Wort.
///
/// Die Zeilen «Verkalkung» und «Fremdwasser» sind umgekehrt aufgebaut: Das markierte Wort ist
/// das Bauteil (Konus, Schachtrohr, Bankett, Gerinne, Anschluss), der Schaden ist der
/// Zeilenname.
/// </summary>
internal static class SchachtProtocolKaestchenformular
{
    private const string MarkenMuster = @"(?:●|•|■|☒|☑|✓|✔|✗|✘|\[\s*[xX]\s*\]|\(\s*[xX]\s*\))";
    private static readonly Regex MarkeRegex = new(MarkenMuster, RegexOptions.Compiled);
    private static readonly Regex MaengelfreiRegex = new(@"M\S{0,2}ngelfrei", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BauteilzeileRegex = new(
        @"^\s*(?<art>Verkalkung(?:en)?|Fremdwasser)\b(?<tail>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Die Bauteilwoerter der Zeilen Verkalkung/Fremdwasser und ihr kanonischer Name.</summary>
    private static readonly (string Wort, string Bauteil)[] BauteilWoerter =
    [
        ("Durchlaufrinne", "Durchlaufrinne"),
        ("Schachtrohr", "Schachtrohr"),
        ("Schachthals", "Schachthals"),
        ("Anschluss", "Anschluss"),
        ("Bankett", "Bankett"),
        ("Gerinne", "Durchlaufrinne"),
        ("Konus", "Konus"),
    ];

    /// <summary>
    /// True fuer das Kaestchenformular: Die Kopfzeile «Zustand der Bauteile» traegt das Kaestchen
    /// «Maengelfrei», oder die Anschlusstabelle («Aus/Ein», «Tiefe m») ist vorhanden. Ein
    /// SchachtPro-Protokoll («ZUSTAND DER SCHACHTBAUTEILE», nur die vorhandenen Schaeden je
    /// Zeile, Punkte als Trennzeichen) erfuellt keines von beiden und behaelt den Freitextweg.
    /// </summary>
    internal static bool IstKaestchenformular(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var zeile in text.Split('\n'))
        {
            if (SchachtProtocolParser.IsConditionSectionStart(zeile) && MaengelfreiRegex.IsMatch(zeile))
                return true;
        }

        return Regex.IsMatch(text, @"\bAus/Ein\b") && Regex.IsMatch(text, @"\bTiefe\s*m\b");
    }

    /// <summary>
    /// Die markierten Schadenswoerter einer Bauteilzeile: je Marke das unmittelbar folgende
    /// bekannte Wort (laengster Kandidat zuerst, damit «Ablagerungen» nicht als «Ablagerung»
    /// endet). Text vor der ersten Marke ist unmarkiert und zaehlt nie.
    /// </summary>
    internal static IReadOnlyList<string> MarkierteSchaeden(string component, string tail)
    {
        var kandidaten = SchachtProtocolParser.GetDamageCandidatesForComponent(component);
        var ergebnis = new List<string>();
        foreach (var segment in SegmenteNachMarke(tail))
        {
            var treffer = WortAmAnfang(kandidaten, segment);
            if (treffer is not null && !ergebnis.Contains(treffer, StringComparer.OrdinalIgnoreCase))
                ergebnis.Add(treffer);
        }

        return ergebnis;
    }

    /// <summary>
    /// Liest eine Zeile «Verkalkung»/«Fremdwasser». True, wenn es eine solche Zeile ist —
    /// auch ohne Marke (dann bleibt <paramref name="eintraege"/> leer, die Zeile ist verbraucht).
    /// </summary>
    internal static bool TryLiesBauteilzeile(string line, out IReadOnlyList<(string Component, string Damage)> eintraege)
    {
        eintraege = [];
        var treffer = BauteilzeileRegex.Match(line ?? string.Empty);
        if (!treffer.Success)
            return false;

        var schaden = treffer.Groups["art"].Value.StartsWith("Verk", StringComparison.OrdinalIgnoreCase)
            ? "Verkalkung"
            : "Fremdwasser";
        var liste = new List<(string Component, string Damage)>();
        foreach (var segment in SegmenteNachMarke(treffer.Groups["tail"].Value))
        {
            foreach (var (wort, bauteil) in BauteilWoerter)
            {
                if (!Regex.IsMatch(segment, @"^\s*" + Regex.Escape(wort) + @"(?![\p{L}])", RegexOptions.IgnoreCase))
                    continue;

                if (!liste.Contains((bauteil, schaden)))
                    liste.Add((bauteil, schaden));
                break;
            }
        }

        eintraege = liste;
        return true;
    }

    /// <summary>
    /// Die Textstuecke NACH je einer Marke, in Zeilenreihenfolge. Das Stueck vor der ersten
    /// Marke faellt weg: Es ist unmarkiert.
    /// </summary>
    internal static IEnumerable<string> SegmenteNachMarke(string? tail)
        => MarkeRegex.Split(tail ?? string.Empty).Skip(1);

    /// <summary>Das laengste Wort aus <paramref name="woerter"/>, das das Segment beginnt; sonst null.</summary>
    internal static string? WortAmAnfang(IReadOnlyList<string> woerter, string segment)
    {
        foreach (var wort in woerter.OrderByDescending(w => w.Length))
        {
            var m = Regex.Match(
                segment,
                @"^\s*" + Regex.Escape(wort) + @"(?![\p{L}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (m.Success)
                return m.Value.Trim();
        }

        return null;
    }
}
