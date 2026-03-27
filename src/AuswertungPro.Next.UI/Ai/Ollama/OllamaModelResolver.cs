using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.UI.Ai.Ollama;

public static class OllamaModelResolver
{
    private static readonly Regex SizeRegex = new(@":(?<size>\d+(?:\.\d+)?)b\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string? ResolveBestInstalledModel(string preferredModel, IReadOnlyList<string> installedModels)
    {
        if (string.IsNullOrWhiteSpace(preferredModel) || installedModels.Count == 0)
            return null;

        var exact = installedModels.FirstOrDefault(m =>
            string.Equals(m, preferredModel, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(exact))
            return exact;

        var family = GetFamily(preferredModel);
        var sameFamily = installedModels
            .Where(m => string.Equals(GetFamily(m), family, StringComparison.OrdinalIgnoreCase))
            .OrderBy(GetModelSizeRank)
            .ThenBy(m => m, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return sameFamily.Count > 0 ? sameFamily[0] : null;
    }

    public static int ClampNumCtxForVideoAnalysis(int requestedNumCtx)
    {
        const int safeNumCtx = 2048;
        if (requestedNumCtx <= 0)
            return safeNumCtx;

        return Math.Min(requestedNumCtx, safeNumCtx);
    }

    private static string GetFamily(string model)
    {
        var trimmed = model.Trim();
        var separator = trimmed.IndexOf(':');
        return separator >= 0 ? trimmed[..separator] : trimmed;
    }

    private static decimal GetModelSizeRank(string model)
    {
        var match = SizeRegex.Match(model);
        if (!match.Success)
            return decimal.MaxValue;

        return decimal.TryParse(match.Groups["size"].Value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var size)
            ? size
            : decimal.MaxValue;
    }
}
