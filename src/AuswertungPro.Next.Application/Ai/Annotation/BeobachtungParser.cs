using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Slice 1 (Operateur-Annotation): zieht eine Liste von (Code, Meterstand,
/// Beschreibung) aus dem Volltext eines Inspektions-PDFs. Pure-Text, ohne
/// Filesystem oder pdftotext-Dependency — testbar mit String-Inputs.
///
/// Regex-Muster ist identisch zur etablierten Logik in
/// <c>Infrastructure/Import/Pdf/PdfParser.TryParseDamageRow</c>: eine
/// Standard-Variante (zwei optionale Code-Tokens) und eine Fretz-Variante
/// (Foto-Nummer + HH:MM:SS vorgesetzt). Bewusst zentral hier in Application
/// gehalten, damit der OperateurSessionBuilder und auch reine Tests
/// dieselbe Quelle der Wahrheit nutzen.
/// </summary>
public static class BeobachtungParser
{
    /// <summary>Eine geparste Beobachtung — minimal, wir bauen daraus CodeTask.</summary>
    public sealed record Beobachtung(string Code, double Meter, string? Description);

    private static readonly Regex StandardRow = new(
        @"^\s*(?<dist>\d{1,4}\.\d{2})\s+(?<c1>[A-Z0-9]{1,6})(?:\s+(?<c2>[A-Z0-9]{1,6}))?\s+(?<desc>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex FretzRow = new(
        @"^\s*(?:\d{1,5}\s+)?(?:\d{2}:\d{2}:\d{2}\s+)?(?<dist>\d{1,4}[.,]\d{1,3})\s+(?<c1>[A-Z]{2,6}(?:\.[A-Z]{1,2}(?:\.[A-Z]{1,2})?)?)\s+(?<desc>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex TrailingTimestamp = new(
        @"\s+\d{2}:\d{2}:\d{2}\b.*$", RegexOptions.Compiled);

    /// <summary>
    /// Extrahiert alle erkennbaren Beobachtungen aus dem Volltext.
    /// Leere Eingabe -&gt; leere Liste. Kein Throw bei Parser-Fehlern auf
    /// einzelnen Zeilen — die Zeile wird einfach uebersprungen, damit der
    /// Operator nicht durch eine kaputte PDF-Zeile blockiert wird.
    /// </summary>
    public static IReadOnlyList<Beobachtung> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return System.Array.Empty<Beobachtung>();

        var result = new List<Beobachtung>();
        var lines = text.Replace("\r\n", "\n").Split('\n');

        foreach (var raw in lines)
        {
            if (TryParseLine(raw, out var beob) && beob is not null)
                result.Add(beob);
        }

        return result;
    }

    private static bool TryParseLine(string line, out Beobachtung? beob)
    {
        beob = null;
        if (string.IsNullOrWhiteSpace(line)) return false;

        // Erst Standard-Format (eine oder zwei Code-Tokens), dann Fretz-Format.
        var m = StandardRow.Match(line);
        var hasC2 = m.Success;
        if (!m.Success)
        {
            m = FretzRow.Match(line);
            hasC2 = false;
        }
        if (!m.Success) return false;

        var distStr = m.Groups["dist"].Value.Replace(',', '.');
        if (!double.TryParse(distStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var meter))
            return false;
        if (meter < 0 || meter > 10_000) return false;   // sanity

        var c1 = m.Groups["c1"].Value.Trim();
        var c2 = hasC2 ? m.Groups["c2"].Value.Trim() : "";
        var code = string.IsNullOrWhiteSpace(c2) ? c1 : $"{c1} {c2}";
        if (string.IsNullOrWhiteSpace(code)) return false;

        var desc = TrailingTimestamp.Replace(m.Groups["desc"].Value, "").Trim();
        // Beschreibung hat oft ein zweites Spalten-Element (10+ Spaces).
        var splitIdx = Regex.Match(desc, @"\s{2,}");
        if (splitIdx.Success) desc = desc[..splitIdx.Index].TrimEnd();

        beob = new Beobachtung(code, meter, string.IsNullOrWhiteSpace(desc) ? null : desc);
        return true;
    }
}
