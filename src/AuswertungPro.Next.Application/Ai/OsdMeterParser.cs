using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Parst die Meterzahl aus einem OSD-Text-Output (heutige Quelle:
/// Qwen-Vision-Antwort auf den OsdMeterPrompt). Reine deterministische
/// Mapping-Logik — keine I/O, kein VLC, kein Ollama.
///
/// Erwartet ist normalerweise eine einzelne Zahl wie "7.90" oder "0.00".
/// Toleriert Leerzeichen, Komma als Dezimaltrenner und etwas
/// umgebenden Text. Lehnt zu grosse Zahlen (Knotennummern) und nicht-
/// numerische Antworten ab.
///
/// Slice 8a.3 Step 1a: extrahiert aus PlayerWindow.CodingMode.cs
/// (CodingReadOsdMeterAsync) ohne Verhaltensaenderung.
/// </summary>
public interface IOsdMeterParser
{
    /// <summary>
    /// Liefert die Meterzahl wenn der Text plausibel parsbar ist,
    /// sonst <c>null</c>.
    /// </summary>
    double? TryParse(string? rawText);
}

/// <inheritdoc />
public sealed class OsdMeterParser : IOsdMeterParser
{
    // Regex matcht 1..3 Ziffern, optional ".dd" oder ".d" Dezimalstellen.
    // Bewusst eng: Knotennummern haben 5+ Stellen, der erste Match in
    // "Knoten 99999, Meter 7.90" landet bei "999" und faellt durch die
    // Plausibilitaets-Range raus — das ist beabsichtigt, weil wir aus
    // dem unklaren Text gar keine Meter-Garantie haben.
    private static readonly Regex _meterPattern =
        new(@"(\d{1,3}(?:\.\d{1,2})?)", RegexOptions.Compiled);

    // Plausible Sewer-Haltungslaenge: typisch 30-80m, max ~200m.
    // 500 als hartes Limit — alles darueber ist sehr wahrscheinlich
    // eine Knotennummer, die als Zahl falsch durchsickert.
    private const double MinMeter = 0.0;
    private const double MaxMeter = 500.0;

    public double? TryParse(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return null;

        // Dezimal-Komma → Punkt normalisieren (deutsche Locales)
        var normalized = rawText.Trim().Replace(",", ".");
        if (string.IsNullOrEmpty(normalized))
            return null;

        var match = _meterPattern.Match(normalized);
        if (!match.Success)
            return null;

        if (!double.TryParse(match.Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var meter))
            return null;

        if (meter < MinMeter || meter > MaxMeter)
            return null;

        return meter;
    }
}
