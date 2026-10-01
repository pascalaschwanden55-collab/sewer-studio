using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Application.Dossiers.Preview;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Liest die eigenen benannten Ziele aus der erzeugten PDF.
///
/// Die Datei stammt immer aus dem eigenen Wandler, ist also kein fremdes
/// Format. Trotzdem gilt durchgehend fail-closed: Jedes Ziel, das nicht sicher
/// gelesen werden kann - unbekanntes Seitenobjekt, fehlende Koordinaten,
/// fremder Name - wird weggelassen. Bleibt am Ende nichts uebrig, arbeitet die
/// Vorschau unveraendert mit dem bisherigen Weg ueber den Text weiter.
/// </summary>
public static class DossierPdfFieldAnchorReader
{
    // Der Ausdruck erfasst JEDES benannte Ziel. Ob es eines von uns ist,
    // entscheidet allein DossierPdfFieldMarker.IsMarker - eine Stelle, eine
    // Regel. Stuende die Vorsilbe schon hier, gaebe es zwei Wahrheiten.
    private static readonly Regex ZielPattern = new(
        @"/(?<name>[A-Za-z0-9]{1,120})\s*\[\s*(?<obj>\d+)\s+\d+\s+R\s*/(?<art>[A-Za-z]+)(?<rest>[^\]]*)\]",
        RegexOptions.Compiled);

    private static readonly Regex SeitenbaumPattern = new(
        @"/Type\s*/Pages.*?/Kids\s*\[(?<kids>[^\]]*)\]",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ZahlPattern = new(
        @"-?\d+(?:\.\d+)?",
        RegexOptions.Compiled);

    private static readonly Regex VerweisPattern = new(
        @"(?<obj>\d+)\s+\d+\s+R",
        RegexOptions.Compiled);

    public static IReadOnlyList<DossierPdfFieldAnchor> Read(byte[]? pdfBytes)
    {
        if (pdfBytes is null || pdfBytes.Length == 0)
            return Array.Empty<DossierPdfFieldAnchor>();

        try
        {
            var roh = Encoding.Latin1.GetString(pdfBytes);
            var seiten = Seitennummern(roh);
            if (seiten.Count == 0)
                return Array.Empty<DossierPdfFieldAnchor>();

            var ergebnis = new List<DossierPdfFieldAnchor>();
            foreach (Match treffer in ZielPattern.Matches(roh))
            {
                var name = treffer.Groups["name"].Value;
                if (!DossierPdfFieldMarker.IsMarker(name))
                    continue;

                if (!int.TryParse(treffer.Groups["obj"].Value, out var objektNummer)
                    || !seiten.TryGetValue(objektNummer, out var seitenNummer))
                {
                    // Ein Ziel auf einer unbekannten Seite waere schlimmer als keines.
                    continue;
                }

                // Nur /XYZ traegt eine Position. /Fit und Verwandte sagen nur
                // „diese Seite" - daraus laesst sich keine Zelle bestimmen.
                if (!string.Equals(treffer.Groups["art"].Value, "XYZ", StringComparison.Ordinal))
                    continue;

                var zahlen = ZahlPattern.Matches(treffer.Groups["rest"].Value);
                if (zahlen.Count < 2
                    || !TryZahl(zahlen[0].Value, out var x)
                    || !TryZahl(zahlen[1].Value, out var y))
                {
                    continue;
                }

                ergebnis.Add(new DossierPdfFieldAnchor(name, seitenNummer, x, y));
            }

            return ergebnis;
        }
        catch
        {
            // Eine unlesbare Datei darf die Vorschau nie stoppen - sie faellt
            // dann auf den bisherigen Weg ueber den Text zurueck.
            return Array.Empty<DossierPdfFieldAnchor>();
        }
    }

    /// <summary>
    /// Objektnummer je Seite, in der Reihenfolge des Seitenbaums. Nur so wird
    /// aus dem Verweis „15 0 R" die sichtbare Seitennummer.
    /// </summary>
    private static IReadOnlyDictionary<int, int> Seitennummern(string roh)
    {
        var ergebnis = new Dictionary<int, int>();
        var baum = SeitenbaumPattern.Match(roh);
        if (!baum.Success)
            return ergebnis;

        var nummer = 1;
        foreach (Match verweis in VerweisPattern.Matches(baum.Groups["kids"].Value))
        {
            if (int.TryParse(verweis.Groups["obj"].Value, out var objektNummer)
                && !ergebnis.ContainsKey(objektNummer))
            {
                ergebnis[objektNummer] = nummer++;
            }
        }

        return ergebnis;
    }

    private static bool TryZahl(string wert, out double zahl)
        => double.TryParse(wert, NumberStyles.Float, CultureInfo.InvariantCulture, out zahl);
}
