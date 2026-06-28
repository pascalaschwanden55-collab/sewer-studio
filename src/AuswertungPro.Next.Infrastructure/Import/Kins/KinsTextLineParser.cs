using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>
/// Rein statische Zeilen-Parsing-Logik fuer KINS kiDVDaten.txt.
/// Kein Datei-IO, kein Datenbankzugriff — alle Methoden arbeiten
/// ausschliesslich auf Strings und einfachen Werttypen.
/// </summary>
internal static class KinsTextLineParser
{
    /// <summary>
    /// Regex fuer eine Beobachtungszeile im KINS-TXT-Format:
    ///   &lt;meter&gt;m &lt;text&gt; @Pos=&lt;position&gt;
    /// </summary>
    internal static readonly Regex ObservationLineRegex = new(
        @"^\s*(?<meter>\d+(?:[.,]\d+)?)m\s+(?<text>.*?)(?:\s+@Pos=(?<pos>.*))?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Versucht, eine Haltungs-Kopfzeile (Header) aus einer KINS-TXT-Zeile zu lesen.
    /// Format: &lt;Nutzungsart&gt; &lt;Von&gt; -&gt; &lt;Nach&gt; [Material] [DN] @Datei=&lt;Videodatei&gt;
    /// </summary>
    internal static bool TryParseHeaderLine(string line, out KinsHoldingHeader header)
    {
        header = default;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var marker = line.IndexOf("@Datei=", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
            return false;

        var prefix = line[..marker].Trim();
        var videoFile = line[(marker + "@Datei=".Length)..].Trim();
        if (string.IsNullOrWhiteSpace(videoFile))
            return false;

        var arrowIndex = prefix.IndexOf("->", StringComparison.Ordinal);
        if (arrowIndex < 0)
            return false;

        var left = prefix[..arrowIndex].Trim();
        var right = prefix[(arrowIndex + 2)..].Trim();

        var leftTokens = Tokenize(left);
        if (leftTokens.Length < 2)
            return false;

        var rightTokens = Tokenize(right);
        if (rightTokens.Length < 1)
            return false;

        var usage = leftTokens[0];
        var from = leftTokens[1];
        var to = rightTokens[0];

        string material = string.Empty;
        string? diameter = null;

        if (rightTokens.Length > 1)
        {
            var tail = rightTokens.Skip(1).ToList();
            if (tail.Count > 0 && int.TryParse(tail[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                diameter = tail[^1];
                tail.RemoveAt(tail.Count - 1);
            }

            material = string.Join(" ", tail);
        }

        header = new KinsHoldingHeader(usage, from, to, material, diameter, videoFile);
        return true;
    }

    /// <summary>
    /// Versucht, eine Beobachtungszeile aus einer KINS-TXT-Zeile zu lesen.
    /// Format: &lt;meter&gt;m &lt;Beschreibung&gt; @Pos=&lt;Zeitstempel&gt;
    /// </summary>
    internal static bool TryParseObservationLine(string line, out ProtocolEntry entry)
    {
        entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Imported
        };

        var match = ObservationLineRegex.Match(line ?? string.Empty);
        if (!match.Success)
            return false;

        var meterText = match.Groups["meter"].Value.Trim().Replace(',', '.');
        if (!double.TryParse(meterText, NumberStyles.Float, CultureInfo.InvariantCulture, out var meter))
            return false;

        var description = match.Groups["text"].Value.Trim();
        var pos = match.Groups["pos"].Success ? match.Groups["pos"].Value.Trim() : string.Empty;

        entry.Code = string.Empty;
        entry.Beschreibung = description;
        entry.MeterStart = meter;
        entry.MeterEnd = meter;
        entry.IsStreckenschaden = false;
        entry.Mpeg = string.IsNullOrWhiteSpace(pos) ? null : pos;
        entry.Zeit = ParseKinsTime(pos);

        return true;
    }

    /// <summary>
    /// Zerlegt einen String anhand von Whitespace in ein Token-Array.
    /// Leere Tokens werden herausgefiltert.
    /// </summary>
    internal static string[] Tokenize(string value)
        => Regex.Split(value?.Trim() ?? string.Empty, @"\s+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

    /// <summary>
    /// Parst einen KINS-Zeitstempel (z.B. "0:02:23" oder "1:23:45") als TimeSpan.
    /// Gibt null zurueck wenn der Text leer oder nicht parsebar ist.
    /// </summary>
    internal static TimeSpan? ParseKinsTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var value = text.Trim();
        var formats = new[] { @"h\:mm\:ss", @"hh\:mm\:ss", @"m\:ss", @"mm\:ss" };
        if (TimeSpan.TryParseExact(value, formats, CultureInfo.InvariantCulture, out var ts))
            return ts;

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out ts))
            return ts;

        return null;
    }
}
