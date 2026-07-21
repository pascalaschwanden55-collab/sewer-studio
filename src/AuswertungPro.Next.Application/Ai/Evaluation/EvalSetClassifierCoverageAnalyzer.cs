namespace AuswertungPro.Next.Application.Ai.Evaluation;

public static class EvalSetClassifierCoverageAnalyzer
{
    public static IReadOnlyList<string> LoadClassifierClassesFromImageFolderDataset(string datasetRoot)
    {
        if (string.IsNullOrWhiteSpace(datasetRoot))
            throw new ArgumentException("Dataset-Pfad fehlt.", nameof(datasetRoot));
        if (!Directory.Exists(datasetRoot))
            throw new DirectoryNotFoundException(datasetRoot);

        var trainRoot = Path.Combine(datasetRoot, "train");
        var classRoot = Directory.Exists(trainRoot) ? trainRoot : datasetRoot;

        return Directory.EnumerateDirectories(classRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static EvalSetClassifierCoverageSummary Analyze(
        IReadOnlyList<EvalSetBenchmarkCase> cases,
        IEnumerable<string> classifierClasses)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(classifierClasses);

        var supported = classifierClasses
            .Select(raw => new
            {
                Raw = raw.Trim(),
                Code = EvalSetClassifierClassMapper.TryMapToCoverageCode(raw)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Raw) && !string.IsNullOrWhiteSpace(x.Code))
            .ToList();

        var codes = cases
            .GroupBy(c => c.ExpectedFullCode, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var expected = EvalSetBenchmarkDataset.NormalizeCode(g.Key) ?? "";
                var match = supported.FirstOrDefault(s => IsCovered(expected, s.Code!));
                return new EvalSetClassifierCoverageCode(
                    ExpectedCode: expected,
                    Count: g.Count(),
                    Covered: match is not null,
                    CoveredBy: match?.Raw);
            })
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.ExpectedCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = cases.Count;
        var covered = codes.Where(c => c.Covered).Sum(c => c.Count);
        return new EvalSetClassifierCoverageSummary(
            TotalEvalCases: total,
            CoveredEvalCases: covered,
            MissingEvalCases: total - covered,
            CoverageRatio: total == 0 ? 0 : (double)covered / total,
            Codes: codes);
    }

    private static bool IsCovered(string expectedCode, string supportedCode)
    {
        if (string.Equals(expectedCode, "LEER", StringComparison.OrdinalIgnoreCase))
            return string.Equals(supportedCode, "LEER", StringComparison.OrdinalIgnoreCase);

        return supportedCode.Length == 3
            ? expectedCode.StartsWith(supportedCode, StringComparison.OrdinalIgnoreCase)
            : string.Equals(expectedCode, supportedCode, StringComparison.OrdinalIgnoreCase);
    }
}
