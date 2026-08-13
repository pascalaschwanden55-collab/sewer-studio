using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Reiner, zustandsloser Parser fuer Schadenszeilen aus PDF-Protokollen.
/// Kapselt die fuenf zuvor privaten Hilfsmethoden von <see cref="PdfParser"/>:
/// ExtractPrimaryDamages, TryParseDamageRow, TakeFirstColumn, StripTrailingNoise, IsNoiseLine.
/// </summary>
internal static class PrimaryDamageRowParser
{
    /// <summary>
    /// Eine gelesene Schadenszeile mit allem, was in ihr stand.
    /// </summary>
    /// <param name="VideoTime">
    /// Videozaehlerstand der Zeile, also die Sekunde ab Dateianfang
    /// (SN EN 13508-2, Kapitel 3.1.10). null = im Protokoll nicht vorhanden.
    /// </param>
    internal sealed record PrimaryDamageRow(
        string Code,
        string? Meter,
        string? Description,
        TimeSpan? VideoTime);

    /// <summary>
    /// Liest alle primaeren Schaeden strukturiert.
    ///
    /// Bis 2026-08-13 gab es nur den Textweg: Die Zeilen wurden zu einem String
    /// zusammengesetzt und spaeter wieder zerlegt. Der Videozaehlerstand ging
    /// dabei verloren — er wurde vom Fretz-Ausdruck sogar erkannt und dann
    /// verworfen. Diese Fassung parst einmal und behaelt alles.
    /// </summary>
    internal static IReadOnlyList<PrimaryDamageRow> ExtractRows(string[] lines)
    {
        var rows = new List<PrimaryDamageRow>();
        string? currentCode = null;
        string? currentDist = null;
        string? currentDesc = null;
        TimeSpan? currentTime = null;

        void Flush()
        {
            if (string.IsNullOrWhiteSpace(currentCode))
                return;

            rows.Add(new PrimaryDamageRow(
                currentCode!.Trim(),
                string.IsNullOrWhiteSpace(currentDist) ? null : currentDist,
                string.IsNullOrWhiteSpace(currentDesc) ? null : currentDesc!.Trim(),
                currentTime));
            currentCode = null;
            currentDist = null;
            currentDesc = null;
            currentTime = null;
        }

        foreach (var raw in lines)
        {
            var line = raw ?? "";
            if (string.IsNullOrWhiteSpace(line))
            {
                Flush();
                continue;
            }

            if (TryParseDamageRow(line, out var dist, out var code, out var desc, out var time))
            {
                Flush();
                currentCode = code;
                currentDist = dist;
                currentDesc = desc;
                currentTime = time;
                continue;
            }

            if (currentCode is null)
                continue;

            var continuation = StripTrailingNoise(TakeFirstColumn(line));
            if (IsNoiseLine(continuation))
                continue;

            if (!string.IsNullOrWhiteSpace(currentDesc))
                currentDesc += " " + continuation.Trim();
            else
                currentDesc = continuation.Trim();
        }

        Flush();
        return rows;
    }

    /// <summary>
    /// Extrahiert alle primaeren Schaeden aus den uebergebenen Zeilen und gibt
    /// sie als zeilengetrennten Text zurueck. Leerer String wenn keine Schaeden.
    ///
    /// Die Ausgabe geht in das Feld "Primaere_Schaeden" und damit in
    /// <c>SchattenCodierungsHash</c>. Sie muss byteidentisch bleiben; dafuer
    /// stehen die Kennzeichnungstests in
    /// <c>PrimaryDamageRowParserCharacterizationTests</c>.
    /// </summary>
    internal static string ExtractPrimaryDamages(string[] lines)
        => string.Join("\n", ExtractRows(lines).Select(Format));

    private static string Format(PrimaryDamageRow row)
    {
        var detail = row.Code;
        if (!string.IsNullOrWhiteSpace(row.Meter))
            detail += $" @{row.Meter}m";
        if (!string.IsNullOrWhiteSpace(row.Description))
            detail += $" ({row.Description})";
        return detail;
    }

    /// <summary>
    /// Versucht eine einzelne Schadenszeile zu parsen (Meter, Code, Beschreibung).
    /// Unterstuetzt Standard- und Fretz-Format.
    /// </summary>
    internal static bool TryParseDamageRow(string line, out string dist, out string code, out string desc)
        => TryParseDamageRow(line, out dist, out code, out desc, out _);

    /// <summary>
    /// Wie oben, liefert zusaetzlich den Videozaehlerstand der Zeile.
    ///
    /// Er steht je nach Protokollformat vor dem Meterwert (Fretz) oder hinter
    /// der Beschreibung. Beide Stellen werden gelesen; ohne Fund bleibt der Wert
    /// null statt geraten.
    /// </summary>
    internal static bool TryParseDamageRow(
        string line, out string dist, out string code, out string desc, out TimeSpan? videoTime)
    {
        dist = "";
        code = "";
        desc = "";
        videoTime = null;

        // Standard-Format: "[meter] [code] [beschreibung]"
        var m = Regex.Match(line, @"^\s*(?<dist>\d{1,4}\.\d{2})\s+(?<c1>[A-Z0-9]{1,6})(?:\s+(?<c2>[A-Z0-9]{1,6}))?\s+(?<desc>.+)$");
        bool hasC2Group = m.Success;

        if (!m.Success)
        {
            // Fretz-Format: "[Foto?] [HH:MM:SS] [meter] [code] [beschreibung]"
            // Foto-Nummer und Zeitstempel kommen VOR dem Meterwert. Der
            // Zeitstempel wurde hier frueher als nicht-erfassende Gruppe
            // erkannt und weggeworfen.
            m = Regex.Match(line, @"^\s*(?:\d{1,5}\s+)?(?:(?<time>\d{2}:\d{2}:\d{2})\s+)?(?<dist>\d{1,4}[.,]\d{1,3})\s+(?<c1>[A-Z]{2,6}(?:\.[A-Z]{1,2}(?:\.[A-Z]{1,2})?)?)\s+(?<desc>.+)$");
            hasC2Group = false;
        }
        if (!m.Success)
            return false;

        dist = m.Groups["dist"].Value.Trim().Replace(',', '.');
        var c1 = m.Groups["c1"].Value.Trim();
        var c2 = hasC2Group ? m.Groups["c2"].Value.Trim() : "";
        code = string.IsNullOrWhiteSpace(c2) ? c1 : $"{c1} {c2}";

        var rohBeschreibung = TakeFirstColumn(m.Groups["desc"].Value);
        // Der Zeitstempel hinter der Beschreibung wird ohnehin abgeschnitten —
        // hier wird er vorher noch gelesen statt nur entfernt.
        videoTime = m.Groups["time"].Success
            ? ProtocolTimeParser.ParseMpegTime(m.Groups["time"].Value)
            : ReadTrailingTime(rohBeschreibung);

        desc = StripTrailingNoise(rohBeschreibung);
        return !string.IsNullOrWhiteSpace(code);
    }

    private static TimeSpan? ReadTrailingTime(string line)
    {
        var m = Regex.Match(line ?? "", @"\s+(?<time>\d{2}:\d{2}:\d{2})\b");
        return m.Success ? ProtocolTimeParser.ParseMpegTime(m.Groups["time"].Value) : null;
    }

    /// <summary>
    /// Gibt den Text vor dem ersten Doppel-Leerzeichen zurueck (erste Spalte).
    /// </summary>
    internal static string TakeFirstColumn(string line)
    {
        var m = Regex.Match(line ?? "", @"^\s*(?<t>.+?)(\s{2,}|$)");
        return m.Success ? m.Groups["t"].Value.TrimEnd() : (line ?? "").TrimEnd();
    }

    /// <summary>
    /// Entfernt trailing Timestamps (HH:MM:SS und alles danach) aus einer Zeile.
    /// </summary>
    internal static string StripTrailingNoise(string line)
    {
        var cleaned = Regex.Replace(line ?? "", @"\s+\d{2}:\d{2}:\d{2}\b.*$", "");
        return cleaned.Trim();
    }

    /// <summary>
    /// Prueft ob eine Zeile als Noise gilt (Seitenangabe, Dateiname, GUID, reiner Timestamp etc.).
    /// </summary>
    internal static bool IsNoiseLine(string line)
    {
        var t = (line ?? "").Trim();
        if (string.IsNullOrWhiteSpace(t)) return true;
        if (Regex.IsMatch(t, @"^(Seite|Page)\s+\d+", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(t, @"^\d{4,}$")) return true;
        if (Regex.IsMatch(t, @"\.(jpg|jpeg|png|mpg|mpeg)\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(t, @"^[a-f0-9]{8}-[a-f0-9]{4}-", RegexOptions.IgnoreCase)) return true;
        // Timestamp-only-Zeilen sind Noise, aber Zeilen mit Timestamp + Meterwert + Code
        // sind echte Beobachtungen (Fretz-Format: "00:01:31  4.60  BCC.Y.B  Beschreibung")
        if (Regex.IsMatch(t, @"^\d{2}:\d{2}:\d{2}\b"))
        {
            // Pruefe ob nach dem Timestamp noch ein Meterwert + VSA-Code folgt
            if (Regex.IsMatch(t, @"\d{2}:\d{2}:\d{2}\s+\d{1,4}[.,]\d{1,3}\s+[A-Z]{2,6}"))
                return false; // Echte Beobachtung, kein Noise
            return true;
        }
        return false;
    }
}
