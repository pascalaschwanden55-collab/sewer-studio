using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Media;

/// <summary>
/// Robustes Matching fuer das typische Kanal-TV-Export-Namensmuster
/// "{Haltungsname}_{Sektionsnummer}", z.B. Haltung "175.1-408" -> Datei "175_1-408_0001.mp4".
///
/// Loest zwei Probleme der generischen Suche:
/// 1. Beim Export werden Punkte (und teils andere Trennzeichen) zu Unterstrichen ("175.1" -> "175_1").
///    Hier werden BEIDE Seiten gleich normalisiert (Trennzeichen entfernt), statt nur den Suchbegriff.
/// 2. Einstellige Haltungsnamen ("1", "2") werden sicher per Trennzeichen-Anker verglichen
///    (z.B. "1_0001"), ohne laengere Namen wie "175_1-408_0001" faelschlich zu treffen.
/// </summary>
public static class ExportNameMatcher
{
    private static readonly Regex SectionSuffix = new(@"[_-]\d+$", RegexOptions.Compiled);

    /// <summary>
    /// Sucht unter <paramref name="files"/> die Datei(en), deren Name zur <paramref name="haltungsname"/>
    /// passt. Mehrere Abschnitte derselben Haltung (nur Sektionsnummer unterschiedlich) ergeben einen
    /// eindeutigen Treffer (erster Abschnitt). Echte Mehrdeutigkeit ergibt <see cref="MediaMatchStatus.Ambiguous"/>.
    /// </summary>
    public static (MediaMatchStatus status, string? path, List<string>? candidates) Match(
        IReadOnlyList<string> files, string haltungsname)
    {
        if (files is null || files.Count == 0 || string.IsNullOrWhiteSpace(haltungsname))
            return (MediaMatchStatus.NotFound, null, null);

        var matches = files
            .Where(f => NameMatches(Path.GetFileNameWithoutExtension(f), haltungsname))
            .ToList();

        if (matches.Count == 0) return (MediaMatchStatus.NotFound, null, null);
        if (matches.Count == 1) return (MediaMatchStatus.Found, matches[0], null);

        // Mehrere Treffer: Wenn sie sich nur durch die Sektionsnummer unterscheiden (gleiche Basis),
        // ist das EINE Haltung mit mehreren Abschnitten -> ersten Abschnitt als eindeutigen Treffer.
        var bases = matches
            .Select(m => StripSectionSuffix(Path.GetFileNameWithoutExtension(m)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (bases.Count == 1)
        {
            var first = matches
                .OrderBy(m => Path.GetFileNameWithoutExtension(m), StringComparer.OrdinalIgnoreCase)
                .First();
            return (MediaMatchStatus.Found, first, null);
        }

        return (MediaMatchStatus.Ambiguous, matches[0], matches);
    }

    /// <summary>
    /// True, wenn der Dateiname (ohne Endung) zum Haltungsnamen passt: gleich (trennzeichen-tolerant)
    /// oder Haltungsname gefolgt von einer reinen Sektionsnummer.
    /// </summary>
    public static bool NameMatches(string fileNameNoExt, string haltungsname)
    {
        if (string.IsNullOrWhiteSpace(fileNameNoExt) || string.IsNullOrWhiteSpace(haltungsname))
            return false;

        string nt = NormalizeLoose(haltungsname);
        if (nt.Length == 0) return false;
        string nf = NormalizeLoose(fileNameNoExt);

        if (nf == nt) return true;
        if (nf.Length <= nt.Length || !nf.StartsWith(nt, StringComparison.Ordinal)) return false;

        // Der Rest hinter dem Haltungs-Teil muss eine Sektionsnummer sein (nur Ziffern).
        for (int k = nt.Length; k < nf.Length; k++)
            if (!char.IsDigit(nf[k]))
                return false;

        if (nt.Length >= 2) return true;

        // Einstellige Haltung: nur akzeptieren, wenn im Original direkt ein Trennzeichen folgt
        // ("1_0001" ja; "175_1-408_0001" nein, da dort '7' auf '1' folgt).
        return fileNameNoExt.StartsWith(haltungsname, StringComparison.OrdinalIgnoreCase)
            && fileNameNoExt.Length > haltungsname.Length
            && IsSeparator(fileNameNoExt[haltungsname.Length]);
    }

    private static string NormalizeLoose(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (!IsSeparator(ch))
                sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    private static bool IsSeparator(char c) => c is '.' or '-' or '_' or ' ';

    private static string StripSectionSuffix(string nameNoExt)
        => SectionSuffix.Replace(nameNoExt, "");
}
