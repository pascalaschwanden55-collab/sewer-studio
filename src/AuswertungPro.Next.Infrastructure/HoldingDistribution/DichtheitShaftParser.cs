using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

internal static class DichtheitShaftParser
{
    private const string SchachtIdPattern = @"[A-Za-z]{0,3}[\-]?\d{2,}(?:[.\-]\d{2,})?";

    private static readonly Regex UpperShaftRegex = new(
        @"oberer\s*Schacht\s*[:\-]?\s*(?<v>" + SchachtIdPattern + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LowerShaftRegex = new(
        @"unterer\s*Schacht\s*[:\-]?\s*(?<v>" + SchachtIdPattern + ")",
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
            var a = upper.Groups["v"].Value;
            var b = lower.Groups["v"].Value;
            if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                return (a, b);
        }

        var top = ShaftTopRegex.Match(text);
        var bottom = ShaftBottomRegex.Match(text);
        if (top.Success && bottom.Success)
        {
            var a = top.Groups["v"].Value;
            var b = bottom.Groups["v"].Value;
            if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                return (a, b);
        }

        return (null, null);
    }
}
