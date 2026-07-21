namespace AuswertungPro.Next.Application.Ai.Evaluation;

public static class EvalSetRouterPlanner
{
    public static IReadOnlyList<EvalSetRouterClassSummary> BuildPlan(
        IReadOnlyList<EvalSetBenchmarkCase> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);

        return cases
            .GroupBy(c => MapExpectedCodeToRouterClass(c.ExpectedFullCode), StringComparer.OrdinalIgnoreCase)
            .Select(g => new EvalSetRouterClassSummary(
                RouterClass: g.Key,
                Count: g.Count(),
                ExpectedCodes: g
                    .Select(c => EvalSetBenchmarkDataset.NormalizeCode(c.ExpectedFullCode) ?? "")
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderByDescending(s => s.Count)
            .ThenBy(s => s.RouterClass, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string MapExpectedCodeToRouterClass(string? expectedCode)
    {
        var code = EvalSetBenchmarkDataset.NormalizeCode(expectedCode);
        if (string.IsNullOrWhiteSpace(code))
            return "sonstiges";

        if (string.Equals(code, "LEER", StringComparison.OrdinalIgnoreCase))
            return "leer";

        if (string.Equals(code, "BCD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "BCE", StringComparison.OrdinalIgnoreCase))
        {
            return "beginn_ende";
        }

        if (code.StartsWith("BD", StringComparison.OrdinalIgnoreCase))
            return "wasserstand";

        if (code.StartsWith("BCA", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("BCC", StringComparison.OrdinalIgnoreCase))
        {
            return "anschluss";
        }

        if (code.StartsWith("BAI", StringComparison.OrdinalIgnoreCase))
            return "dichtung";

        if (code.StartsWith("BAB", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("BAC", StringComparison.OrdinalIgnoreCase))
        {
            return "riss_bruch";
        }

        if (code.StartsWith("BAH", StringComparison.OrdinalIgnoreCase))
            return "versatz";

        if (code.StartsWith("BAA", StringComparison.OrdinalIgnoreCase))
            return "deformation";

        if (code.StartsWith("BAF", StringComparison.OrdinalIgnoreCase))
            return "oberflaeche";

        if (code.StartsWith("BAJ", StringComparison.OrdinalIgnoreCase))
            return "versatz";

        if (code.StartsWith("BBA", StringComparison.OrdinalIgnoreCase))
            return "wurzeln";

        if (code.StartsWith("BBB", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("BBC", StringComparison.OrdinalIgnoreCase))
        {
            return "ablagerung";
        }

        if (code.StartsWith("BBF", StringComparison.OrdinalIgnoreCase))
            return "infiltration";

        return "sonstiges";
    }
}
