using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Vsa.Classification;

// Reiner (dateiloser) Kern des Markdown-Regel-Parsers.
// Parse(string markdownPath) delegiert an ParseLines und liest lediglich die Datei ein.
internal static partial class MarkdownRuleParser
{
    private static readonly Regex NumberRegex = BuildNumberRegex();

    // Datei-basierter Einstiegspunkt — liest Zeilen und delegiert an ParseLines.
    public static ParseResult Parse(string markdownPath)
        => ParseLines(File.ReadLines(markdownPath));

    // Reiner Einstiegspunkt: akzeptiert beliebige Zeilenfolge ohne Datei-IO.
    public static ParseResult ParseLines(IEnumerable<string> lines)
    {
        var channelRules = new List<VsaClassificationRule>();
        var manholeRules = new List<VsaClassificationRule>();

        string assetKind = "";
        string sourceRef = "";
        var sequence = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("## Anhang C", StringComparison.OrdinalIgnoreCase))
            {
                assetKind = "channel";
                continue;
            }

            if (line.StartsWith("## Anhang D", StringComparison.OrdinalIgnoreCase))
            {
                assetKind = "manhole";
                continue;
            }

            if (line.StartsWith("Quelle:", StringComparison.OrdinalIgnoreCase))
            {
                sourceRef = NormalizeCell(line["Quelle:".Length..]);
                continue;
            }

            if (string.IsNullOrWhiteSpace(assetKind) || !LooksLikeRuleRow(line))
                continue;

            var cells = SplitRow(line);
            if (cells.Count != 11 || cells[0].Equals("Code", StringComparison.OrdinalIgnoreCase))
                continue;

            var rule = BuildRule(cells, assetKind, sourceRef, ++sequence);
            if (assetKind == "channel")
                channelRules.Add(rule);
            else
                manholeRules.Add(rule);
        }

        return new ParseResult(channelRules, manholeRules);
    }

    private static bool LooksLikeRuleRow(string line)
        => line.StartsWith('|') && !line.Contains("---", StringComparison.Ordinal);

    private static List<string> SplitRow(string line)
        => line.Trim('|')
            .Split('|')
            .Select(NormalizeCell)
            .ToList();

    private static VsaClassificationRule BuildRule(
        IReadOnlyList<string> cells,
        string assetKind,
        string sourceRef,
        int sequence)
    {
        var code = cells[0];
        var ch1 = SplitList(cells[1]);
        var ch2 = SplitList(cells[2]);
        var requirement = NormalizeMissing(cells[3]);
        var unit = NormalizeUnit(cells[4]);
        var ezCells = cells.Skip(5).Take(5).ToArray();
        var scopeRaw = cells[10];
        var notes = BuildNotes(scopeRaw, ezCells);

        var status = DetermineStatus(requirement, scopeRaw, ezCells);
        var classification = status == "ok"
            ? BuildClassification(ezCells, notes, out status)
            : Missing(status);

        return new VsaClassificationRule
        {
            Id = $"{assetKind[0]}-{sequence:000}-{code}-{requirement ?? "X"}",
            Code = code,
            CodeMatch = "exact",
            Ch1 = ch1,
            Ch2 = ch2,
            Requirement = requirement,
            Parameter = unit == "none" ? "none" : "q1",
            Unit = unit,
            Scope = BuildScope(scopeRaw),
            Classification = classification,
            Status = status,
            SourceRef = sourceRef,
            Notes = notes
        };
    }

    private static string DetermineStatus(string? requirement, string scopeRaw, IReadOnlyList<string> ezCells)
    {
        if (ezCells.Any(cell => cell.Contains("missing-vsa-source", StringComparison.OrdinalIgnoreCase)))
            return "missing-vsa-source";

        if (string.IsNullOrWhiteSpace(requirement))
            return "needs-review";

        if (scopeRaw.Contains("fachlich pruefen", StringComparison.OrdinalIgnoreCase))
            return "needs-review";

        return "ok";
    }

    private static VsaClassificationDefinition BuildClassification(
        IReadOnlyList<string> ezCells,
        List<string> notes,
        out string status)
    {
        var fixedCells = ezCells
            .Select((cell, index) => (cell, ez: index))
            .Where(item => item.cell.Contains("alle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (fixedCells.Count == 1)
        {
            status = "ok";
            if (fixedCells[0].cell.Contains('*'))
                notes.Add("EZ-Fussnote: alle*");
            return new VsaClassificationDefinition
            {
                Mode = "fixed",
                Ez = fixedCells[0].ez
            };
        }

        if (fixedCells.Count > 1)
        {
            status = "needs-review";
            notes.Add("Mehrere feste EZ-Spalten in einer Regel.");
            return Missing(status);
        }

        var ranges = new List<VsaClassificationRange>();
        for (var ez = 0; ez < ezCells.Count; ez++)
        {
            var range = ParseRange(ezCells[ez], ez);
            if (range is not null)
                ranges.Add(range);
        }

        if (ranges.Count == 0)
        {
            status = "needs-review";
            notes.Add("Keine auswertbare EZ-Zuordnung.");
            return Missing(status);
        }

        status = "ok";
        return new VsaClassificationDefinition
        {
            Mode = "range",
            Ranges = ranges
        };
    }

    private static VsaClassificationDefinition Missing(string reason)
        => new()
        {
            Mode = "missing",
            Reason = reason
        };

    private static VsaClassificationRange? ParseRange(string cell, int ez)
    {
        var cleaned = cell.Replace("*", "", StringComparison.Ordinal)
            .Replace("`", "", StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned) || cleaned == "-")
            return null;

        var numbers = NumberRegex.Matches(cleaned)
            .Select(match => double.Parse(match.Value.Replace(',', '.'), CultureInfo.InvariantCulture))
            .ToArray();

        if (cleaned.StartsWith(">=", StringComparison.Ordinal) && cleaned.Contains(" und < ", StringComparison.Ordinal))
        {
            return new VsaClassificationRange
            {
                Ez = ez,
                MinInclusive = numbers.ElementAtOrDefault(0),
                MaxExclusive = numbers.ElementAtOrDefault(1)
            };
        }

        if (cleaned.StartsWith(">=", StringComparison.Ordinal) && numbers.Length >= 1)
        {
            return new VsaClassificationRange
            {
                Ez = ez,
                MinInclusive = numbers[0]
            };
        }

        if (cleaned.StartsWith("<", StringComparison.Ordinal) && numbers.Length >= 1)
        {
            return new VsaClassificationRange
            {
                Ez = ez,
                MaxExclusive = numbers[0]
            };
        }

        return null;
    }

    private static VsaClassificationScope BuildScope(string scopeRaw)
    {
        var pipeFlexibility = scopeRaw.Contains("biegesteif", StringComparison.OrdinalIgnoreCase)
            ? "rigid"
            : scopeRaw.Contains("biegeweich", StringComparison.OrdinalIgnoreCase)
                ? "flexible"
                : "any";

        var areas = new List<string>();
        var marker = "Bereiche ";
        var markerIndex = scopeRaw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            var areaPart = scopeRaw[(markerIndex + marker.Length)..]
                .Split(';')[0]
                .Trim();
            if (!areaPart.Equals("alle", StringComparison.OrdinalIgnoreCase))
                areas.AddRange(SplitList(areaPart));
        }

        return new VsaClassificationScope
        {
            PipeFlexibility = pipeFlexibility,
            Areas = areas
        };
    }

    private static List<string> BuildNotes(string scopeRaw, IEnumerable<string> ezCells)
    {
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(scopeRaw) && !scopeRaw.Equals("alle", StringComparison.OrdinalIgnoreCase))
            notes.Add($"Geltung: {scopeRaw}");

        foreach (var cell in ezCells.Where(cell => cell.Contains('*')))
            notes.Add($"EZ-Fussnote: {cell}");

        return notes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizeCell(string value)
        => value.Trim().Replace(' ', ' ');

    private static string? NormalizeMissing(string value)
        => value == "-" || string.IsNullOrWhiteSpace(value) ? null : value;

    private static string NormalizeUnit(string value)
        => value.Equals("-", StringComparison.OrdinalIgnoreCase)
           || value.Equals("keine Quantifizierung", StringComparison.OrdinalIgnoreCase)
            ? "none"
            : value;

    private static List<string> SplitList(string value)
        => value.Equals("-", StringComparison.OrdinalIgnoreCase)
           || value.Equals("alle", StringComparison.OrdinalIgnoreCase)
           || string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',')
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToList();

    [GeneratedRegex(@"-?\d+(?:[\.,]\d+)?")]
    private static partial Regex BuildNumberRegex();
}
