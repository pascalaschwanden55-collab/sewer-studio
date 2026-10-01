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

    /// <summary>
    /// Liest die Schadenszeilen strukturiert — mit Videozaehlerstand.
    ///
    /// Additiv neben <see cref="ParseFields"/>: Das Textfeld "Primaere_Schaeden"
    /// bleibt unveraendert, damit der Codierungs-Hash und seine Leser nicht
    /// beruehrt werden. Wer die Zeit braucht, nimmt diesen Weg.
    /// </summary>
    internal IReadOnlyList<PrimaryDamageRowParser.PrimaryDamageRow> ParseDamageRows(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return PrimaryDamageRowParser.ExtractRows(text.Replace("\r\n", "\n").Split('\n'));
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

            result.Remove("Haltungsname");
        }

        var inferred = TryExtractHaltungsname(text);
        if (!string.IsNullOrWhiteSpace(inferred) && IsLikelyHaltungId(inferred))
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

        // Frueh-Erkennung: Volles Paar nach "Oberer/Unterer Schacht" (Fretz-Stammdaten)
        var fullPairAfterSchacht = Regex.Match(text,
            @"(?im)\b(?:Oberer|Unterer)\s+Schacht[^\S\n]+(?<pair>\d[\d\.]*\s*-\s*\d[\d\.]*)\b");
        if (fullPairAfterSchacht.Success)
            return NormalizeHaltungId(fullPairAfterSchacht.Groups["pair"].Value);

        // Fallback from shaft pair
        var oben = Regex.Match(text, @"(?im)\bSchacht\s*oben\s*[:\-]?\s*(?<v>\d[\d\.]*)\b");
        var unten = Regex.Match(text, @"(?im)\bSchacht\s*unten\s*[:\-]?\s*(?<v>\d[\d\.]*)\b");
        if (oben.Success && unten.Success)
            return $"{oben.Groups["v"].Value.Trim()}-{unten.Groups["v"].Value.Trim()}";

        // Alternatives Bezeichnungsformat: "Oberer Punkt" / "Unterer Punkt"
        var oberPunkt = Regex.Match(text, @"(?im)\bOberer\s+(?:Punkt|Schacht)\s*[:\-]?\s*(?<v>\d[\d\.]*)\b");
        var unterPunkt = Regex.Match(text, @"(?im)\bUnterer\s+(?:Punkt|Schacht)\s*[:\-]?\s*(?<v>\d[\d\.]*)\b");
        if (oberPunkt.Success && unterPunkt.Success)
            return $"{oberPunkt.Groups["v"].Value.Trim()}-{unterPunkt.Groups["v"].Value.Trim()}";

        return null;
    }

    private static bool IsLikelyHaltungId(string? value)
        => HoldingIdPlausibility.IsLikelyHoldingId(value);

    private static string NormalizeHaltungId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return HoldingIdPlausibility.Normalize(value);
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

    // Delegation an PrimaryDamageRowParser (pure, zustandslos)
    private static string ExtractPrimaryDamages(string[] lines)
        => PrimaryDamageRowParser.ExtractPrimaryDamages(lines);

    private static bool TryParseDamageRow(string line, out string dist, out string code, out string desc)
        => PrimaryDamageRowParser.TryParseDamageRow(line, out dist, out code, out desc);

    private static string TakeFirstColumn(string line)
        => PrimaryDamageRowParser.TakeFirstColumn(line);

    private static string StripTrailingNoise(string line)
        => PrimaryDamageRowParser.StripTrailingNoise(line);

    private static bool IsNoiseLine(string line)
        => PrimaryDamageRowParser.IsNoiseLine(line);
}
