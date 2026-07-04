using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

internal static class DichtheitShaftParser
{
    private const string SchachtIdPattern = @"[A-Za-z]{0,3}\s*[\-]?\s*(?:\d{1,2}\.)?\d{2,}(?:[.\-]\d{2,})?";

    private static readonly Regex UpperShaftRegex = new(
        @"oberer\s*Schacht\s*[:\-]?\s*(?<v>" + SchachtIdPattern + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LowerShaftRegex = new(
        @"unterer\s*Schacht\s*[:\-]?\s*(?<v>" + SchachtIdPattern + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // KINS-Dichtheitsprotokolle (kleine Leitungen): "von Schacht: X" / "nach Schacht: Y".
    // Ohne dieses Muster griff der Heuristik-Fallback die Norm-Referenz aus der
    // Kopfzeile ("Dichtheitspruefung nach SIA190:2017") als Schachtnummer ab.
    private static readonly Regex VonSchachtRegex = new(
        @"\bvon\s+Schacht\s*[:\-]?\s*(?<v>" + SchachtIdPattern + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NachSchachtRegex = new(
        @"\bnach\s+Schacht\s*[:\-]?\s*(?<v>" + SchachtIdPattern + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ShaftTopRegex = new(
        @"Schacht\s*oben\s*[:\-]?\s*(?<v>" + SchachtIdPattern + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ShaftBottomRegex = new(
        @"Schacht\s*unten\s*[:\-]?\s*(?<v>" + SchachtIdPattern + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PairLineRegex = new(
        "(?<a>(?:\\d{1,2}\\.)?\\d{4,6})\\s*(?<sep>[-^+><\\u2192]+[-^+><\\u2192.,\\s]*)\\s*(?<b>(?:\\d{1,2}\\.)?\\d{4,6})",
        RegexOptions.Compiled);

    internal static (string A, string B)? TryMatchPairLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var match = PairLineRegex.Match(line);
        if (!match.Success)
            return null;

        var a = match.Groups["a"].Value.Trim();
        var b = match.Groups["b"].Value.Trim();
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)
            || string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return null;

        return (a, b);
    }

    internal static (string? A, string? B) TryExtractShafts(string text)
    {
        var upper = UpperShaftRegex.Match(text);
        var lower = LowerShaftRegex.Match(text);
        if (upper.Success && lower.Success)
        {
            var a = NormalizeSchachtId(upper.Groups["v"].Value);
            var b = NormalizeSchachtId(lower.Groups["v"].Value);
            if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                return (a, b);
        }

        var top = ShaftTopRegex.Match(text);
        var bottom = ShaftBottomRegex.Match(text);
        if (top.Success && bottom.Success)
        {
            var a = NormalizeSchachtId(top.Groups["v"].Value);
            var b = NormalizeSchachtId(bottom.Groups["v"].Value);
            if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                return (a, b);
        }

        var von = VonSchachtRegex.Match(text);
        var nach = NachSchachtRegex.Match(text);
        if (von.Success && nach.Success)
        {
            var a = NormalizeSchachtId(von.Groups["v"].Value);
            var b = NormalizeSchachtId(nach.Groups["v"].Value);
            if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                return (a, b);
        }

        return (null, null);
    }

    private static string NormalizeSchachtId(string value)
    {
        var text = value.Trim();
        var ssPrefix = Regex.Match(
            text,
            @"^SS\s*[-]?\s*(?<id>(?:\d{1,2}\.)?\d{2,}(?:[.\-]\d{2,})?)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (ssPrefix.Success)
            return ssPrefix.Groups["id"].Value;

        return Regex.Replace(text, @"\s+", "");
    }
}
