using System.Globalization;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public static class EvalSetBenchmarkContext
{

    public static IReadOnlyList<(string Code, string Description, double Meter)> BuildOracleImportContext(
        EvalSetBenchmarkCase benchmarkCase)
    {
        if (string.Equals(benchmarkCase.ExpectedFullCode, "LEER", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(benchmarkCase.ExpectedFullCode))
        {
            return Array.Empty<(string Code, string Description, double Meter)>();
        }

        var meter = benchmarkCase.Meter ?? 0;
        return
        [
            (
                benchmarkCase.ExpectedFullCode,
                $"Eval-Set Erwartung {benchmarkCase.ExpectedFullCode}",
                meter)
        ];
    }

    public static IReadOnlyList<(string Code, string Description, double Meter)> BuildClassifierImportContext(
        IReadOnlyList<EvalSetCandidatePrediction> predictions,
        double meter = 0,
        double minConfidence = 0.05,
        int maxCandidates = 3)
    {
        ArgumentNullException.ThrowIfNull(predictions);
        if (maxCandidates <= 0)
            return Array.Empty<(string Code, string Description, double Meter)>();

        var result = new List<(string Code, string Description, double Meter)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in predictions
                     .Where(p => p.Confidence >= minConfidence)
                     .OrderByDescending(p => p.Confidence))
        {
            var code = TryMapClassifierClassToVsaCode(p.ClassName);
            if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
                continue;

            result.Add((
                code,
                $"YOLO-Kandidat {p.ClassName.Trim()} ({p.Confidence.ToString("P0", CultureInfo.InvariantCulture)})",
                meter));

            if (result.Count >= maxCandidates)
                break;
        }

        return result;
    }

    public static IReadOnlyList<string> BuildClassifierObservationHints(
        IReadOnlyList<EvalSetCandidatePrediction> predictions,
        double minConfidence = 0.05,
        int maxHints = 3)
    {
        ArgumentNullException.ThrowIfNull(predictions);
        if (maxHints <= 0)
            return Array.Empty<string>();

        return predictions
            .Where(p => p.Confidence >= minConfidence)
            .OrderByDescending(p => p.Confidence)
            .Select(p => new
            {
                Raw = p.ClassName.Trim(),
                p.Confidence
            })
            .Where(p => !string.IsNullOrWhiteSpace(p.Raw) && !EvalSetClassifierClassMapper.IsNegativeClass(p.Raw))
            .Take(maxHints)
            .Select(p => $"YOLO sieht eventuell {p.Raw} ({p.Confidence.ToString("P0", CultureInfo.InvariantCulture)})")
            .ToList();
    }

    private static string? TryMapClassifierClassToVsaCode(string? className)
        => EvalSetClassifierClassMapper.TryMapToVsaCode(className);
}
