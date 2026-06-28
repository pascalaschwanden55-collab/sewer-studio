using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Reiner, zustandsloser Parser fuer Schadenszeilen aus PDF-Protokollen.
/// Kapselt die fuenf zuvor privaten Hilfsmethoden von <see cref="PdfParser"/>:
/// ExtractPrimaryDamages, TryParseDamageRow, TakeFirstColumn, StripTrailingNoise, IsNoiseLine.
/// </summary>
internal static class PrimaryDamageRowParser
{
    /// <summary>
    /// Extrahiert alle primaeren Schaeden aus den uebergebenen Zeilen und gibt
    /// sie als zeilengetrennten Text zurueck. Leerer String wenn keine Schaeden.
    /// </summary>
    internal static string ExtractPrimaryDamages(string[] lines)
    {
        var entries = new List<string>();
        string? currentCode = null;
        string? currentDist = null;
        string? currentDesc = null;

        void Flush()
        {
            if (string.IsNullOrWhiteSpace(currentCode))
                return;

            var detail = currentCode!.Trim();
            if (!string.IsNullOrWhiteSpace(currentDist))
                detail += $" @{currentDist}m";
            if (!string.IsNullOrWhiteSpace(currentDesc))
                detail += $" ({currentDesc!.Trim()})";

            entries.Add(detail);
            currentCode = null;
            currentDist = null;
            currentDesc = null;
        }

        foreach (var raw in lines)
        {
            var line = raw ?? "";
            if (string.IsNullOrWhiteSpace(line))
            {
                Flush();
                continue;
            }

            if (TryParseDamageRow(line, out var dist, out var code, out var desc))
            {
                Flush();
                currentCode = code;
                currentDist = dist;
                currentDesc = desc;
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

        if (entries.Count == 0)
            return "";

        return string.Join("\n", entries);
    }

    /// <summary>
    /// Versucht eine einzelne Schadenszeile zu parsen (Meter, Code, Beschreibung).
    /// Unterstuetzt Standard- und Fretz-Format.
    /// </summary>
    internal static bool TryParseDamageRow(string line, out string dist, out string code, out string desc)
    {
        dist = "";
        code = "";
        desc = "";

        // Standard-Format: "[meter] [code] [beschreibung]"
        var m = Regex.Match(line, @"^\s*(?<dist>\d{1,4}\.\d{2})\s+(?<c1>[A-Z0-9]{1,6})(?:\s+(?<c2>[A-Z0-9]{1,6}))?\s+(?<desc>.+)$");
        bool hasC2Group = m.Success;

        if (!m.Success)
        {
            // Fretz-Format: "[Foto?] [HH:MM:SS] [meter] [code] [beschreibung]"
            // Foto-Nummer und Timestamp kommen VOR dem Meterwert
            m = Regex.Match(line, @"^\s*(?:\d{1,5}\s+)?(?:\d{2}:\d{2}:\d{2}\s+)?(?<dist>\d{1,4}[.,]\d{1,3})\s+(?<c1>[A-Z]{2,6}(?:\.[A-Z]{1,2}(?:\.[A-Z]{1,2})?)?)\s+(?<desc>.+)$");
            hasC2Group = false;
        }
        if (!m.Success)
            return false;

        dist = m.Groups["dist"].Value.Trim().Replace(',', '.');
        var c1 = m.Groups["c1"].Value.Trim();
        var c2 = hasC2Group ? m.Groups["c2"].Value.Trim() : "";
        code = string.IsNullOrWhiteSpace(c2) ? c1 : $"{c1} {c2}";

        desc = TakeFirstColumn(m.Groups["desc"].Value);
        desc = StripTrailingNoise(desc);
        return !string.IsNullOrWhiteSpace(code);
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
