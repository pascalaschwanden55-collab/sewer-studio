using System.Text.RegularExpressions;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed class PdfProjectMetadata
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
    public string? ProjectName { get; set; }
}

public static class PdfProjectMetadataParser
{
    public static PdfProjectMetadata Parse(PdfTextExtraction extraction)
    {
        var result = new PdfProjectMetadata();
        var text = (extraction.FullText ?? "").Replace("\r\n", "\n");
        var lines = text.Split('\n');

        var projektnummer = MatchValue(text, @"(?im)^\s*Projektnummer:\s*(?<value>.+)$");
        if (!string.IsNullOrWhiteSpace(projektnummer))
        {
            result.ProjectName = projektnummer.Trim();
            TryParseProjectNumber(projektnummer, result.Values);
        }

        var auftragsnummer = MatchValue(text, @"(?im)^\s*Auftragsnummer:\s*(?<value>.+)$");
        Set(result.Values, "AuftragNr", auftragsnummer);

        var titel = MatchValue(text, @"(?im)^\s*(?<value>.+\s+Kanton\s+\d{3,})\s*$");
        if (!string.IsNullOrWhiteSpace(titel) && string.IsNullOrWhiteSpace(result.ProjectName))
            result.ProjectName = titel.Trim();
        TryParseTitleLine(titel, result.Values);

        ParseAuftraggeberBlock(lines, result.Values);
        ParseZustaendigePersonBlock(lines, result.Values);
        ParseCompanyFromCover(extraction, result.Values);

        if (!result.Values.TryGetValue("InspektionsDatum", out var existing) || string.IsNullOrWhiteSpace(existing))
        {
            var range = Regex.Match(text, @"\b\d{2}\.\d{2}\.\d{4}\s*-\s*\d{2}\.\d{2}\.\d{4}\b");
            if (range.Success)
                result.Values["InspektionsDatum"] = range.Value.Trim();
        }

        if (!result.Values.ContainsKey("Gemeinde") || string.IsNullOrWhiteSpace(result.Values["Gemeinde"]))
            TryParseOrtLine(lines, result.Values);

        if ((!result.Values.ContainsKey("Strasse") || string.IsNullOrWhiteSpace(result.Values["Strasse"])))
        {
            var strasse = MatchValue(text, @"(?im)^\s*Stra(?:ß|ss)e\s*/\s*Standort\s+(?<value>.+)$");
            Set(result.Values, "Strasse", strasse);
        }

        return result;
    }

    private static void ParseAuftraggeberBlock(string[] lines, Dictionary<string, string> target)
    {
        var idx = FindLineIndex(lines, "Auftraggeber");
        if (idx < 0)
            return;

        var block = ParseKeyValueBlock(lines, idx + 1, "Bauleitung", "Zuständige Person", "Zustaendige Person", "Zusätzliche Informationen");
        if (block.TryGetValue("Name", out var name))
            Set(target, "Auftraggeber", name);

        if (block.TryGetValue("Straße", out var strasse) || block.TryGetValue("Strasse", out strasse))
            Set(target, "Strasse", strasse);

        if (block.TryGetValue("Ort", out var ort))
            TryParseOrtValue(ort, target);
    }

    private static void ParseZustaendigePersonBlock(string[] lines, Dictionary<string, string> target)
    {
        var idx = FindLineIndex(lines, "Zuständige Person");
        if (idx < 0)
            idx = FindLineIndex(lines, "Zustaendige Person");
        if (idx < 0)
            return;

        var block = ParseKeyValueBlock(lines, idx + 1, "Zusätzliche Informationen");
        if (block.TryGetValue("Auftraggeber", out var ap))
            Set(target, "Bearbeiter", ap);
    }

    private static void ParseCompanyFromCover(PdfTextExtraction extraction, Dictionary<string, string> target)
    {
        if (extraction.Pages is null || extraction.Pages.Count == 0)
            return;

        var coverLines = extraction.Pages[0].Split('\n');
        for (int i = 0; i < coverLines.Length - 2; i++)
        {
            if (!coverLines[i].Contains("www.", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = SplitLeft(coverLines[i], out _);
            var addr = SplitLeft(coverLines[i + 1], out var email);
            var city = SplitLeft(coverLines[i + 2], out var phone);

            Set(target, "FirmaName", name);
            Set(target, "FirmaEmail", email);
            Set(target, "FirmaTelefon", phone);

            var address = string.Join(", ", new[] { addr, city }.Where(x => !string.IsNullOrWhiteSpace(x)));
            Set(target, "FirmaAdresse", address);
            break;
        }
    }

    private static Dictionary<string, string> ParseKeyValueBlock(string[] lines, int startIndex, params string[] stopLabels)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i] ?? "";
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (stopLabels.Any(l => string.Equals(trimmed, l, StringComparison.OrdinalIgnoreCase)))
                break;

            var m = Regex.Match(line, @"^\s*(?<key>[^:]+?)\s*:\s*(?<value>.*)$");
            if (!m.Success)
                continue;

            var key = m.Groups["key"].Value.Trim();
            var value = m.Groups["value"].Value.Trim();
            if (!dict.ContainsKey(key))
                dict[key] = value;
        }

        return dict;
    }

    private static void TryParseOrtLine(string[] lines, Dictionary<string, string> target)
    {
        foreach (var line in lines)
        {
            var m = Regex.Match(line ?? "", @"^\s*Ort\s+(?<zip>\d{4})\s+(?<value>.+)$");
            if (!m.Success)
                continue;

            var ort = m.Groups["value"].Value.Trim();
            TryParseOrtValue(ort, target);
            return;
        }
    }

    private static void TryParseOrtValue(string ort, Dictionary<string, string> target)
    {
        if (string.IsNullOrWhiteSpace(ort))
            return;

        if (!target.ContainsKey("Gemeinde") || string.IsNullOrWhiteSpace(target["Gemeinde"]))
            target["Gemeinde"] = ort.Trim();

        var zone = Regex.Match(ort, @"\b(?<zone>[A-Z]{2})\b");
        if (zone.Success)
            Set(target, "Zone", zone.Groups["zone"].Value);
    }

    private static void TryParseProjectNumber(string projektnummer, Dictionary<string, string> target)
    {
        var parts = projektnummer.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 1)
            Set(target, "Gemeinde", parts[0]);
        if (parts.Length >= 2)
            Set(target, "Zone", parts[1]);
        if (parts.Length >= 3)
            Set(target, "Strasse", parts[2]);
        if (parts.Length >= 5 && Regex.IsMatch(parts[4], @"^\d+$"))
            Set(target, "AuftragNr", parts[4]);
    }

    private static void TryParseTitleLine(string? titel, Dictionary<string, string> target)
    {
        if (string.IsNullOrWhiteSpace(titel))
            return;

        var m = Regex.Match(titel, @"^(?<gemeinde>\S+)\s+(?<strasse>.+?)\s+Kanton\s+(?<nr>\d+)");
        if (!m.Success)
            return;

        Set(target, "Gemeinde", m.Groups["gemeinde"].Value);
        Set(target, "Strasse", m.Groups["strasse"].Value);
        Set(target, "AuftragNr", m.Groups["nr"].Value);
    }

    private static string? MatchValue(string text, string pattern)
    {
        var m = Regex.Match(text ?? "", pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return m.Success ? m.Groups["value"].Value.Trim() : null;
    }

    private static int FindLineIndex(string[] lines, string label)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.Equals(lines[i]?.Trim(), label, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string SplitLeft(string line, out string right)
    {
        right = "";
        if (string.IsNullOrWhiteSpace(line))
            return "";

        var m = Regex.Match(line, @"^\s*(?<left>.+?)\s{2,}(?<right>.+)$");
        if (m.Success)
        {
            right = m.Groups["right"].Value.Trim();
            return m.Groups["left"].Value.Trim();
        }

        return line.Trim();
    }

    private static void Set(Dictionary<string, string> target, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        target[key] = value.Trim();
    }
}
