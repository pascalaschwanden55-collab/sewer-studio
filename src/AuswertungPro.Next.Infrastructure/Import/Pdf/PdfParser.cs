using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed class PdfParser
{
    private readonly IReadOnlyDictionary<string, PdfFieldRule> _mapping;
    private readonly List<string> _allRegexes;

    public PdfParser(IReadOnlyDictionary<string, PdfFieldRule>? mapping = null)
    {
        _mapping = mapping ?? PdfFieldMapping.Rules;
        _allRegexes = _mapping.Values.SelectMany(v => v.Regexes).Distinct().ToList();
    }

    public Dictionary<string, string> ParseFields(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text))
            return result;

        text = text.Replace("\r\n", "\n");
        var lines = text.Split('\n');

        foreach (var fieldName in _mapping.Keys)
        {
            var rule = _mapping[fieldName];
            foreach (var rx in rule.Regexes)
            {
                if (rule.Multiline)
                {
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (!Regex.IsMatch(lines[i], rx))
                            continue;

                        var value = lines[i];
                        var lineCount = 1;

                        for (int j = i + 1; j < lines.Length && lineCount < rule.MaxLines; j++)
                        {
                            var next = lines[j];
                            if (string.IsNullOrWhiteSpace(next))
                                break;

                            // Nächstes Label?
                            bool isLabel = false;
                            foreach (var check in _allRegexes)
                            {
                                if (Regex.IsMatch(next, check))
                                {
                                    isLabel = true;
                                    break;
                                }
                            }

                            if (isLabel)
                                break;

                            value += "\n" + next;
                            lineCount++;
                        }

                        var matches = Regex.Matches(value, rx, RegexOptions.Multiline);
                        if (matches.Count > 0)
                        {
                            var m = matches[0];
                            var extracted = ExtractLastGroup(m);
                            extracted = PdfPostProcessors.Apply(fieldName, extracted);
                            if (!string.IsNullOrWhiteSpace(extracted))
                            {
                                result[fieldName] = extracted;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    foreach (var line in lines)
                    {
                        var m = Regex.Match(line, rx);
                        if (!m.Success) continue;

                        var extracted = ExtractLastGroup(m);
                        extracted = PdfPostProcessors.Apply(fieldName, extracted);

                        if (!string.IsNullOrWhiteSpace(extracted))
                        {
                            result[fieldName] = extracted;
                            break;
                        }
                    }
                }

                if (result.ContainsKey(fieldName))
                    break;
            }
        }

        if (!result.ContainsKey("Primaere_Schaeden"))
        {
            var damages = ExtractPrimaryDamages(lines);
            if (!string.IsNullOrWhiteSpace(damages))
                result["Primaere_Schaeden"] = damages;
        }

        EnsureValidHaltungsname(result, text);

        return result;
    }

    private static void EnsureValidHaltungsname(Dictionary<string, string> result, string text)
    {
        if (result.TryGetValue("Haltungsname", out var existing))
        {
            var normalizedExisting = NormalizeHaltungId(existing);
            if (IsLikelyHaltungId(normalizedExisting))
            {
                result["Haltungsname"] = normalizedExisting;
                return;
            }
        }

        var inferred = TryExtractHaltungsname(text);
        if (!string.IsNullOrWhiteSpace(inferred))
            result["Haltungsname"] = inferred!;
    }

    private static string? TryExtractHaltungsname(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Same-line layout: "Haltungsname: 23021-22369 ..."
        var sameLine = Regex.Match(
            text,
            @"(?im)\bHaltungsname\s*:\s*(?<id>\d[\d\.]*\s*-\s*\d[\d\.]*)\b");
        if (sameLine.Success)
            return NormalizeHaltungId(sameLine.Groups["id"].Value);

        // Two-row table layout:
        // Haltungsname:   Datum: ...
        // 23021-22369     22.04.2014 ...
        var valueRow = Regex.Match(
            text,
            @"(?im)^\s*(?<id>\d[\d\.]*\s*-\s*\d[\d\.]*)\s+\d{2}\.\d{2}\.\d{4}\b");
        if (valueRow.Success)
            return NormalizeHaltungId(valueRow.Groups["id"].Value);

        // Fallback from shaft pair
        var oben = Regex.Match(text, @"(?im)\bSchacht\s*oben\s*:\s*(?<v>\d[\d\.]*)\b");
        var unten = Regex.Match(text, @"(?im)\bSchacht\s*unten\s*:\s*(?<v>\d[\d\.]*)\b");
        if (oben.Success && unten.Success)
            return $"{oben.Groups["v"].Value.Trim()}-{unten.Groups["v"].Value.Trim()}";

        return null;
    }

    private static bool IsLikelyHaltungId(string? value)
        => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, @"^\d[\d\.]*-\d[\d\.]*$");

    private static string NormalizeHaltungId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Trim();
        normalized = Regex.Replace(normalized, @"\s+", "");
        normalized = normalized.Replace("/", "-");
        return normalized;
    }

    private static string ExtractLastGroup(Match m)
    {
        for (int g = m.Groups.Count - 1; g > 0; g--)
        {
            var v = m.Groups[g].Value;
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return "";
    }

    private static string ExtractPrimaryDamages(string[] lines)
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

    private static bool TryParseDamageRow(string line, out string dist, out string code, out string desc)
    {
        dist = "";
        code = "";
        desc = "";

        var m = Regex.Match(line, @"^\s*(?<dist>\d{1,4}\.\d{2})\s+(?<c1>[A-Z0-9]{1,6})(?:\s+(?<c2>[A-Z0-9]{1,6}))?\s+(?<desc>.+)$");
        if (!m.Success)
            return false;

        dist = m.Groups["dist"].Value.Trim();
        var c1 = m.Groups["c1"].Value.Trim();
        var c2 = m.Groups["c2"].Value.Trim();
        code = string.IsNullOrWhiteSpace(c2) ? c1 : $"{c1} {c2}";

        desc = TakeFirstColumn(m.Groups["desc"].Value);
        desc = StripTrailingNoise(desc);
        return !string.IsNullOrWhiteSpace(code);
    }

    private static string TakeFirstColumn(string line)
    {
        var m = Regex.Match(line ?? "", @"^\s*(?<t>.+?)(\s{2,}|$)");
        return m.Success ? m.Groups["t"].Value.TrimEnd() : (line ?? "").TrimEnd();
    }

    private static string StripTrailingNoise(string line)
    {
        var cleaned = Regex.Replace(line ?? "", @"\s+\d{2}:\d{2}:\d{2}\b.*$", "");
        return cleaned.Trim();
    }

    private static bool IsNoiseLine(string line)
    {
        var t = (line ?? "").Trim();
        if (string.IsNullOrWhiteSpace(t)) return true;
        if (Regex.IsMatch(t, @"^(Seite|Page)\s+\d+", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(t, @"^\d{4,}$")) return true;
        if (Regex.IsMatch(t, @"\.(jpg|jpeg|png|mpg|mpeg)\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(t, @"^[a-f0-9]{8}-[a-f0-9]{4}-", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(t, @"^\d{2}:\d{2}:\d{2}\b")) return true;
        return false;
    }
}
