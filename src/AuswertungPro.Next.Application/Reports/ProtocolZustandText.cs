using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Textlogik fuer Zustandsbeschreibungen in Protokoll-Ausgaben.
/// Aus <see cref="ProtocolPdfExporter"/> extrahiert (verhaltensneutral), damit unit-testbar.
/// </summary>
public static class ProtocolZustandText
{
    /// <summary>
    /// Bereinigt eine Zustandsbeschreibung: entfernt Meter-Prefixe, Code-Token und Import-Artefakte.
    /// </summary>
    public static string NormalizeZustandDescription(string? raw, string? code)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var text = raw.Trim();
        var codeToken = code?.Trim();

        // Muster "CODE @0.00m (desc)" -> Inhalt der Klammer extrahieren
        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            var prefix = text.Substring(0, open);
            if ((!string.IsNullOrWhiteSpace(codeToken) && prefix.Contains(codeToken, StringComparison.OrdinalIgnoreCase))
                || Regex.IsMatch(prefix, @"@\s*\d"))
            {
                text = text.Substring(open + 1, close - open - 1);
            }
        }

        if (!string.IsNullOrWhiteSpace(codeToken))
            text = Regex.Replace(text, @"^\s*" + Regex.Escape(codeToken) + @"\b\s*", "", RegexOptions.IgnoreCase);

        text = Regex.Replace(text, @"^\s*@?\s*\d+(?:[.,]\d+)?\s*m\b\s*", "", RegexOptions.IgnoreCase);
        // Nur isolierte Kuerzel (z.B. "BCD", "BBCC") am Anfang entfernen, keine normalen Woerter
        text = Regex.Replace(text, @"^\s*[A-Z0-9]{1,6}(?:\s+[A-Z0-9]{1,6})?(?=\s|$)", "", RegexOptions.None);

        // Import-Artefakte: Trailing Hash/ID-Fragmente entfernen
        // Beispiele: "-80631_6e c06c5c-c9", "137124-fc", "80fd46-", "f5fa69-828"
        text = Regex.Replace(text, @"\s+-?\d+_[0-9a-fA-F]+(?:\s+[0-9a-fA-F-]+)*\s*$", "");
        text = Regex.Replace(text, @"\s+[0-9a-fA-F]{5,}-[0-9a-fA-F]*\s*$", "");

        // Klartext: Redundante Phrasen kuerzen
        text = Regex.Replace(text, @"\s*Richtungs[aä]nderung\b", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"^Anderer Grund f[uü]r Abbruch der Inspektion,?\s*", "", RegexOptions.IgnoreCase);

        text = text.Trim(' ', '-', '–', ':', ',', '/');

        return text;
    }

    /// <summary>
    /// Baut den Zustandstext fuer einen Eintrag in der Haltungsgrafik (max. 120 Zeichen).
    /// </summary>
    public static string BuildHaltungsgrafikZustandText(ProtocolEntry entry)
    {
        var desc = NormalizeZustandDescription(entry.Beschreibung, entry.Code);
        if (string.IsNullOrWhiteSpace(desc))
            desc = ProtocolPdfObservationText.BuildParameterShortText(entry);
        if (string.IsNullOrWhiteSpace(desc))
            desc = entry.CodeMeta?.Notes?.Trim();

        if (string.IsNullOrWhiteSpace(desc))
            return "-";

        return Shorten(desc, 120);
    }

    /// <summary>
    /// Baut den vollstaendigen Zustandstext fuer tabellarische Ausgaben (ungekuerzt).
    /// </summary>
    public static string BuildObservationZustandTextLong(ProtocolEntry entry)
    {
        var desc = NormalizeZustandDescription(entry.Beschreibung, entry.Code);
        if (string.IsNullOrWhiteSpace(desc))
            desc = ProtocolPdfObservationText.BuildParameterShortText(entry);
        if (string.IsNullOrWhiteSpace(desc))
            desc = entry.CodeMeta?.Notes?.Trim();

        return string.IsNullOrWhiteSpace(desc) ? "-" : desc;
    }

    /// <summary>
    /// Kuerzt einen Text auf <paramref name="max"/> Zeichen und haengt "…" an.
    /// </summary>
    public static string Shorten(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        if (text.Length <= max)
            return text;
        return text.Substring(0, Math.Max(0, max - 1)).TrimEnd() + "…";
    }
}
