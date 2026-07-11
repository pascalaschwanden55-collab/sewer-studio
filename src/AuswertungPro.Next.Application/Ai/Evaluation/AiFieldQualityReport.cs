using System.Globalization;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Schatten;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public sealed record FieldQualityCaseMetadata(
    string CaseId,
    int? DnMm,
    string PipeMaterial,
    string ImageQuality);

public sealed record ShadowQualityInput(
    string CaseId,
    string HumanConditionClass,
    string HumanMeasure,
    string HumanCost,
    string? ShadowConditionClass,
    string? ShadowMeasure,
    decimal? ShadowCost,
    bool IsStale,
    string? Model);

public sealed record DetectionQualitySummary(
    int Holdings,
    int RawAiSamples,
    int DeduplicatedAiFindings,
    int ReviewedAiFindings,
    int ExactCodeCorrect,
    int SubtypeMismatch,
    int WrongCodeFamily,
    int RejectedFalsePositive,
    int QuantificationCorrections,
    int ManualDamageFindings,
    int ManualFindingsMatched,
    int PossibleMisses,
    int PossibleMeterMismatches,
    double ExactCodeAccuracy,
    double DetectionRecall);

public sealed record GreenReleaseSummary(
    int DeduplicatedGreenFindings,
    int ReviewedGreenFindings,
    int CorrectGreenFindings,
    int GreenErrors,
    int PendingReview,
    int Holdings,
    double ErrorRate,
    double ErrorRateUpper95,
    bool ReleaseCriterionMet,
    string Criterion);

public sealed record QualityCoverageSummary(
    int Holdings,
    IReadOnlyDictionary<string, int> DnBands,
    IReadOnlyDictionary<string, int> Materials,
    IReadOnlyDictionary<string, int> ImageQualities);

public sealed record ShadowQualitySummary(
    int Holdings,
    int Comparable,
    int Equal,
    int LightDifference,
    int StrongDifference,
    int NoComparison,
    int Stale,
    int ConditionClassDifferences,
    int MeasureDifferences,
    int CostDifferences);

public sealed record QualityFindingIssue(
    string Category,
    string CaseId,
    string? SampleId,
    double MeterStart,
    double MeterEnd,
    string ExpectedCode,
    string PredictedCode,
    string Detail,
    bool MeterNeedsReview);

public sealed record AiFieldQualityReport(
    DateTimeOffset GeneratedAtUtc,
    DetectionQualitySummary Detection,
    GreenReleaseSummary GreenRelease,
    QualityCoverageSummary Coverage,
    ShadowQualitySummary Shadow,
    IReadOnlyList<QualityFindingIssue> Issues);

public sealed record AiFieldQualityReportOptions(
    double MatchGreenMeters = 0.20,
    double MatchYellowMeters = 0.50,
    double MeterMismatchReviewMeters = 2.0,
    int RequiredGreenFindings = 300,
    int RequiredHoldings = 20,
    int AllowedGreenErrors = 1,
    double MaximumUpperErrorRate = 0.02);

/// <summary>
/// Erstellt einen Feldqualitaetsbericht aus menschlich geprueften Trainings-Samples.
/// Gezahlt wird pro dedupliziertem Befund, nicht pro Frame.
/// </summary>
public static class AiFieldQualityReportAnalyzer
{
    public static AiFieldQualityReport Analyze(
        IReadOnlyList<TrainingSample> samples,
        IReadOnlyList<FieldQualityCaseMetadata>? metadata = null,
        IReadOnlyList<ShadowQualityInput>? shadowInputs = null,
        AiFieldQualityReportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var opts = options ?? new AiFieldQualityReportOptions();

        var active = samples
            .Where(sample => sample.Status != TrainingSampleStatus.Removed)
            .ToList();
        var aiRaw = active
            .Where(sample => !string.IsNullOrWhiteSpace(sample.KiCode)
                             && IsDamageCode(sample.KiCode))
            .ToList();
        var manualRaw = active
            .Where(sample => string.Equals(
                                 sample.SourceType,
                                 SourceTypeNames.ManualCoding,
                                 StringComparison.OrdinalIgnoreCase)
                             && sample.HumanConfirmed == true
                             && sample.Status == TrainingSampleStatus.Approved
                             && IsDamageCode(sample.Code))
            .ToList();

        var aiFindings = Deduplicate(aiRaw, sample => sample.KiCode ?? sample.Code, opts.MatchYellowMeters);
        var manualFindings = Deduplicate(manualRaw, sample => sample.Code, opts.MatchYellowMeters);
        var issues = new List<QualityFindingIssue>();

        var exact = 0;
        var subtype = 0;
        var wrongFamily = 0;
        var rejected = 0;
        var quantification = 0;
        var reviewed = 0;

        foreach (var finding in aiFindings)
        {
            var reviewedSample = SelectWorstReviewed(finding.Samples);
            if (reviewedSample is null)
                continue;

            reviewed++;
            var predicted = NormalizeCode(reviewedSample.KiCode);
            var expected = NormalizeCode(reviewedSample.Code);
            if (reviewedSample.HumanConfirmed == false)
            {
                rejected++;
                issues.Add(ToIssue(
                    "false_positive",
                    reviewedSample,
                    expected,
                    predicted,
                    "KI-Befund wurde vom Menschen abgelehnt."));
                continue;
            }

            if (predicted == expected)
            {
                exact++;
                if (reviewedSample.Corrected == true)
                {
                    quantification++;
                    issues.Add(ToIssue(
                        "quantification_or_detail_correction",
                        reviewedSample,
                        expected,
                        predicted,
                        "Code blieb gleich, aber der Befund wurde bearbeitet."));
                }
                continue;
            }

            if (BefundMatcher.MainCode(predicted) == BefundMatcher.MainCode(expected))
            {
                subtype++;
                issues.Add(ToIssue(
                    "subtype_mismatch",
                    reviewedSample,
                    expected,
                    predicted,
                    "Code-Familie stimmt, genauer Untertyp nicht."));
            }
            else
            {
                wrongFamily++;
                issues.Add(ToIssue(
                    "wrong_code",
                    reviewedSample,
                    expected,
                    predicted,
                    "Falsche VSA-Code-Familie."));
            }
        }

        var manualMatch = MatchManualAgainstAi(manualFindings, aiFindings, opts);
        foreach (var missed in manualMatch.Result.Verpasst)
        {
            var manual = manualMatch.ManualByRef[missed.RefId!];
            var nearbySameFamily = FindNearbySameFamily(manual, aiFindings, opts);
            var meterReview = nearbySameFamily is not null || MeterNeedsReview(manual);
            issues.Add(ToIssue(
                "possible_miss",
                manual,
                NormalizeCode(manual.Code),
                nearbySameFamily is null ? "" : NormalizeCode(nearbySameFamily.Representative.KiCode),
                nearbySameFamily is null
                    ? "Manueller Schaden ohne KI-Partner innerhalb der Match-Toleranz."
                    : "Kein Treffer innerhalb 0.5 m; gleichartige KI-Erkennung liegt weiter entfernt.",
                meterReview));

            if (nearbySameFamily is not null)
            {
                issues.Add(ToIssue(
                    "possible_meter_mismatch",
                    nearbySameFamily.Representative,
                    NormalizeCode(manual.Code),
                    NormalizeCode(nearbySameFamily.Representative.KiCode),
                    "Gleiche Code-Familie, aber Meterabstand liegt zwischen 0.5 m und 2.0 m.",
                    meterNeedsReview: true));
            }
        }

        var possibleMeterMismatches = issues.Count(issue => issue.Category == "possible_meter_mismatch");
        var manualMatched = manualFindings.Count - manualMatch.Result.Verpasst.Count;
        var exactAccuracyDenominator = exact + subtype + wrongFamily + rejected;
        var detection = new DetectionQualitySummary(
            Holdings: aiFindings.Concat(manualFindings)
                .Select(finding => NormalizeCaseId(finding.Representative.CaseId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            RawAiSamples: aiRaw.Count,
            DeduplicatedAiFindings: aiFindings.Count,
            ReviewedAiFindings: reviewed,
            ExactCodeCorrect: exact,
            SubtypeMismatch: subtype,
            WrongCodeFamily: wrongFamily,
            RejectedFalsePositive: rejected,
            QuantificationCorrections: quantification,
            ManualDamageFindings: manualFindings.Count,
            ManualFindingsMatched: manualMatched,
            PossibleMisses: manualMatch.Result.Verpasst.Count,
            PossibleMeterMismatches: possibleMeterMismatches,
            ExactCodeAccuracy: exactAccuracyDenominator == 0
                ? 0
                : (double)exact / exactAccuracyDenominator,
            DetectionRecall: manualFindings.Count == 0
                ? 0
                : (double)manualMatched / manualFindings.Count);

        var greenRelease = AnalyzeGreenRelease(aiFindings, opts, issues);
        var coverage = AnalyzeCoverage(
            aiFindings.Concat(manualFindings).ToList(),
            metadata ?? Array.Empty<FieldQualityCaseMetadata>());
        var shadow = AnalyzeShadow(shadowInputs ?? Array.Empty<ShadowQualityInput>());

        return new AiFieldQualityReport(
            DateTimeOffset.UtcNow,
            detection,
            greenRelease,
            coverage,
            shadow,
            issues
                .OrderBy(issue => issue.CaseId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(issue => issue.MeterStart)
                .ThenBy(issue => issue.Category, StringComparer.Ordinal)
                .ToList());
    }

    private static GreenReleaseSummary AnalyzeGreenRelease(
        IReadOnlyList<DeduplicatedFinding> aiFindings,
        AiFieldQualityReportOptions options,
        ICollection<QualityFindingIssue> issues)
    {
        var green = aiFindings
            .Where(finding => finding.Samples.Any(IsGreenDecision))
            .ToList();
        var reviewed = 0;
        var correct = 0;
        var errors = 0;
        var holdings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in green)
        {
            var greenSamples = finding.Samples.Where(IsGreenDecision).ToList();
            var reviewedSample = SelectWorstReviewed(greenSamples);
            if (reviewedSample is null)
                continue;

            reviewed++;
            holdings.Add(NormalizeCaseId(reviewedSample.CaseId));
            var predicted = NormalizeCode(reviewedSample.KiCode);
            var expected = NormalizeCode(reviewedSample.Code);
            var isCorrect = reviewedSample.HumanConfirmed == true
                            && predicted == expected
                            && reviewedSample.Corrected != true;
            if (isCorrect)
            {
                correct++;
                continue;
            }

            errors++;
            issues.Add(ToIssue(
                "green_decision_error",
                reviewedSample,
                expected,
                predicted,
                "KI-Kriterien waren erfuellt, menschliche Pruefung bestaetigte den Befund aber nicht unveraendert."));
        }

        var upper = BinomialUpper95(reviewed, errors);
        var releaseReady = reviewed >= options.RequiredGreenFindings
                           && holdings.Count >= options.RequiredHoldings
                           && errors <= options.AllowedGreenErrors
                           && upper < options.MaximumUpperErrorRate;

        return new GreenReleaseSummary(
            DeduplicatedGreenFindings: green.Count,
            ReviewedGreenFindings: reviewed,
            CorrectGreenFindings: correct,
            GreenErrors: errors,
            PendingReview: green.Count - reviewed,
            Holdings: holdings.Count,
            ErrorRate: reviewed == 0 ? 0 : (double)errors / reviewed,
            ErrorRateUpper95: upper,
            ReleaseCriterionMet: releaseReady,
            Criterion: $"Mindestens {options.RequiredGreenFindings} gepruefte, deduplizierte gruene Befunde "
                       + $"aus {options.RequiredHoldings} Haltungen; hoechstens {options.AllowedGreenErrors} Fehler; "
                       + $"obere 95%-Fehlergrenze unter {options.MaximumUpperErrorRate:P0}.");
    }

    private static ManualMatchResult MatchManualAgainstAi(
        IReadOnlyList<DeduplicatedFinding> manual,
        IReadOnlyList<DeduplicatedFinding> ai,
        AiFieldQualityReportOptions options)
    {
        var aggregate = new BefundMatchResult();
        var manualByRef = new Dictionary<string, TrainingSample>(StringComparer.Ordinal);

        foreach (var caseId in manual.Select(f => NormalizeCaseId(f.Representative.CaseId))
                     .Union(ai.Select(f => NormalizeCaseId(f.Representative.CaseId)), StringComparer.OrdinalIgnoreCase))
        {
            var gt = manual
                .Where(f => NormalizeCaseId(f.Representative.CaseId).Equals(caseId, StringComparison.OrdinalIgnoreCase))
                .Select(f =>
                {
                    var sample = f.Representative;
                    var refId = "manual:" + StableId(sample);
                    manualByRef[refId] = sample;
                    return ToMatchFinding(sample, sample.Code, refId);
                })
                .ToList();
            var detections = ai
                .Where(f => NormalizeCaseId(f.Representative.CaseId).Equals(caseId, StringComparison.OrdinalIgnoreCase))
                .Select(f => ToMatchFinding(
                    f.Representative,
                    f.Representative.KiCode ?? "",
                    "ai:" + StableId(f.Representative)))
                .ToList();

            aggregate.Add(BefundMatcher.Match(
                gt,
                detections,
                new BefundMatchOptions
                {
                    TolGruen = options.MatchGreenMeters,
                    TolGelb = options.MatchYellowMeters,
                    ExcludedFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "BCC", "BCD", "BCE"
                    }
                }));
        }

        return new ManualMatchResult(aggregate, manualByRef);
    }

    private static DeduplicatedFinding? FindNearbySameFamily(
        TrainingSample manual,
        IReadOnlyList<DeduplicatedFinding> ai,
        AiFieldQualityReportOptions options)
    {
        var manualFinding = ToMatchFinding(manual, manual.Code, null);
        return ai
            .Where(candidate => NormalizeCaseId(candidate.Representative.CaseId)
                .Equals(NormalizeCaseId(manual.CaseId), StringComparison.OrdinalIgnoreCase))
            .Where(candidate => BefundMatcher.MainCode(candidate.Representative.KiCode)
                == BefundMatcher.MainCode(manual.Code))
            .Select(candidate => new
            {
                Candidate = candidate,
                Gap = BefundMatcher.Gap(
                    manualFinding,
                    ToMatchFinding(candidate.Representative, candidate.Representative.KiCode ?? "", null))
            })
            .Where(item => item.Gap > options.MatchYellowMeters
                           && item.Gap <= options.MeterMismatchReviewMeters)
            .OrderBy(item => item.Gap)
            .Select(item => item.Candidate)
            .FirstOrDefault();
    }

    private static IReadOnlyList<DeduplicatedFinding> Deduplicate(
        IEnumerable<TrainingSample> samples,
        Func<TrainingSample, string> codeSelector,
        double tolerance)
    {
        var result = new List<DeduplicatedFinding>();
        foreach (var byCase in samples
                     .GroupBy(sample => NormalizeCaseId(sample.CaseId), StringComparer.OrdinalIgnoreCase))
        {
            var caseFindings = new List<DeduplicatedFinding>();
            foreach (var sample in byCase
                         .OrderBy(item => Math.Min(item.MeterStart, item.MeterEnd))
                         .ThenBy(item => item.SampleId, StringComparer.Ordinal))
            {
                var code = codeSelector(sample);
                var match = caseFindings.FirstOrDefault(existing =>
                    BefundMatcher.MainCode(codeSelector(existing.Representative))
                    == BefundMatcher.MainCode(code)
                    && BefundMatcher.Gap(
                        ToMatchFinding(existing.Representative, codeSelector(existing.Representative), null),
                        ToMatchFinding(sample, code, null)) <= tolerance);
                if (match is null)
                    caseFindings.Add(new DeduplicatedFinding(new List<TrainingSample> { sample }));
                else
                    match.Samples.Add(sample);
            }

            result.AddRange(caseFindings);
        }

        return result;
    }

    private static TrainingSample? SelectWorstReviewed(IEnumerable<TrainingSample> samples)
        => samples
            .Where(sample => sample.HumanConfirmed.HasValue)
            .OrderBy(ReviewRisk)
            .ThenByDescending(sample => sample.ConfirmedAtUtc)
            .FirstOrDefault();

    private static int ReviewRisk(TrainingSample sample)
    {
        if (sample.HumanConfirmed == false)
            return 0;
        if (NormalizeCode(sample.KiCode) != NormalizeCode(sample.Code))
            return 1;
        if (sample.Corrected == true)
            return 2;
        return 3;
    }

    private static bool IsGreenDecision(TrainingSample sample)
        => string.Equals(
            sample.CentralDecision?.Outcome,
            AiDecisionOutcome.AutoAccept.ToString(),
            StringComparison.OrdinalIgnoreCase);

    private static QualityCoverageSummary AnalyzeCoverage(
        IReadOnlyList<DeduplicatedFinding> findings,
        IReadOnlyList<FieldQualityCaseMetadata> metadata)
    {
        var metadataByCase = metadata
            .GroupBy(item => NormalizeCaseId(item.CaseId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var cases = findings
            .Select(finding => NormalizeCaseId(finding.Representative.CaseId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dn = new List<string>();
        var materials = new List<string>();
        var qualities = new List<string>();
        foreach (var caseId in cases)
        {
            metadataByCase.TryGetValue(caseId, out var item);
            dn.Add(item?.DnMm is { } value ? ToDnBand(value) : "unbekannt");
            materials.Add(string.IsNullOrWhiteSpace(item?.PipeMaterial) ? "unbekannt" : item.PipeMaterial.Trim());
            var sampleQuality = findings
                .Where(finding => NormalizeCaseId(finding.Representative.CaseId)
                    .Equals(caseId, StringComparison.OrdinalIgnoreCase))
                .Select(finding => finding.Representative.TechniqueGrade)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            qualities.Add(!string.IsNullOrWhiteSpace(item?.ImageQuality)
                ? item.ImageQuality.Trim()
                : !string.IsNullOrWhiteSpace(sampleQuality) ? sampleQuality! : "unbekannt");
        }

        return new QualityCoverageSummary(
            cases.Count,
            CountValues(dn),
            CountValues(materials),
            CountValues(qualities));
    }

    private static ShadowQualitySummary AnalyzeShadow(IReadOnlyList<ShadowQualityInput> inputs)
    {
        var equal = 0;
        var light = 0;
        var strong = 0;
        var noComparison = 0;
        var classDifferences = 0;
        var measureDifferences = 0;
        var costDifferences = 0;

        foreach (var item in inputs)
        {
            if (item.IsStale)
            {
                noComparison++;
                continue;
            }

            var result = SchattenVergleich.Bewerte(
                item.HumanConditionClass,
                item.HumanMeasure,
                item.HumanCost,
                item.ShadowConditionClass,
                item.ShadowMeasure,
                item.ShadowCost);
            switch (result)
            {
                case SchattenAbweichung.Gleich: equal++; break;
                case SchattenAbweichung.LeichtAbweichend: light++; break;
                case SchattenAbweichung.StarkAbweichend: strong++; break;
                default: noComparison++; break;
            }

            if (!string.IsNullOrWhiteSpace(item.HumanConditionClass)
                && !string.IsNullOrWhiteSpace(item.ShadowConditionClass)
                && !item.HumanConditionClass.Trim().Equals(
                    item.ShadowConditionClass.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                classDifferences++;

            if (!string.IsNullOrWhiteSpace(item.HumanMeasure)
                && !string.IsNullOrWhiteSpace(item.ShadowMeasure)
                && !SchattenVergleich.MassnahmeStimmtUeberein(item.HumanMeasure, item.ShadowMeasure))
                measureDifferences++;

            var humanCost = SchattenVergleich.TryParseKosten(item.HumanCost);
            if (humanCost is > 0 && item.ShadowCost is > 0
                && Math.Abs(humanCost.Value - item.ShadowCost.Value) / humanCost.Value
                > SchattenVergleich.KostenToleranz)
                costDifferences++;
        }

        return new ShadowQualitySummary(
            Holdings: inputs.Select(input => NormalizeCaseId(input.CaseId))
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Comparable: equal + light + strong,
            Equal: equal,
            LightDifference: light,
            StrongDifference: strong,
            NoComparison: noComparison,
            Stale: inputs.Count(input => input.IsStale),
            ConditionClassDifferences: classDifferences,
            MeasureDifferences: measureDifferences,
            CostDifferences: costDifferences);
    }

    internal static double BinomialUpper95(int trials, int errors)
    {
        if (trials <= 0)
            return 1.0;
        errors = Math.Clamp(errors, 0, trials);
        if (errors == trials)
            return 1.0;

        const double alpha = 0.05;
        var low = (double)errors / trials;
        var high = 1.0;
        for (var i = 0; i < 80; i++)
        {
            var mid = (low + high) / 2.0;
            var cdf = BinomialCdf(errors, trials, mid);
            if (cdf > alpha)
                low = mid;
            else
                high = mid;
        }

        return (low + high) / 2.0;
    }

    private static double BinomialCdf(int maxErrors, int trials, double probability)
    {
        if (probability <= 0)
            return 1.0;
        if (probability >= 1)
            return maxErrors >= trials ? 1.0 : 0.0;

        var term = Math.Pow(1.0 - probability, trials);
        var sum = term;
        for (var k = 0; k < maxErrors; k++)
        {
            term *= (trials - k) / (double)(k + 1) * probability / (1.0 - probability);
            sum += term;
        }
        return Math.Clamp(sum, 0.0, 1.0);
    }

    private static QualityFindingIssue ToIssue(
        string category,
        TrainingSample sample,
        string expected,
        string predicted,
        string detail,
        bool? meterNeedsReview = null)
        => new(
            category,
            NormalizeCaseId(sample.CaseId),
            string.IsNullOrWhiteSpace(sample.SampleId) ? null : sample.SampleId,
            Math.Min(sample.MeterStart, sample.MeterEnd),
            Math.Max(sample.MeterStart, sample.MeterEnd),
            expected,
            predicted,
            detail,
            meterNeedsReview ?? MeterNeedsReview(sample));

    private static BefundMatchFinding ToMatchFinding(
        TrainingSample sample,
        string code,
        string? refId)
        => new(
            code,
            Math.Min(sample.MeterStart, sample.MeterEnd),
            Math.Max(sample.MeterStart, sample.MeterEnd),
            sample.Beschreibung,
            refId);

    private static bool MeterNeedsReview(TrainingSample sample)
    {
        if (sample.HasOsdMismatch)
            return true;
        var source = sample.MeterSource ?? "";
        return source.Contains("linear", StringComparison.OrdinalIgnoreCase)
               || source.Contains("estimate", StringComparison.OrdinalIgnoreCase)
               || source.Contains("geschaetzt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDamageCode(string? code)
    {
        var normalized = NormalizeCode(code);
        return normalized.StartsWith("BA", StringComparison.Ordinal)
               || normalized.StartsWith("BB", StringComparison.Ordinal);
    }

    private static string NormalizeCode(string? code)
        => string.IsNullOrWhiteSpace(code)
            ? ""
            : new string(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string NormalizeCaseId(string? caseId)
        => EvalContaminationGuard.NormalizeHaltungKey(caseId) ?? (caseId ?? "").Trim();

    private static string StableId(TrainingSample sample)
        => !string.IsNullOrWhiteSpace(sample.SampleId)
            ? sample.SampleId
            : TrainingSample.BuildCanonicalSignature(
                NormalizeCaseId(sample.CaseId),
                sample.Code,
                sample.MeterStart,
                sample.MeterEnd);

    private static string ToDnBand(int dn) => dn switch
    {
        <= 200 => "DN <= 200",
        <= 400 => "DN 201-400",
        <= 800 => "DN 401-800",
        _ => "DN > 800"
    };

    private static IReadOnlyDictionary<string, int> CountValues(IEnumerable<string> values)
        => values
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

    private sealed class DeduplicatedFinding(List<TrainingSample> samples)
    {
        public List<TrainingSample> Samples { get; } = samples;
        public TrainingSample Representative => Samples
            .OrderBy(ReviewRisk)
            .ThenByDescending(sample => IsGreenDecision(sample))
            .ThenBy(sample => sample.SampleId, StringComparer.Ordinal)
            .First();
    }

    private sealed record ManualMatchResult(
        BefundMatchResult Result,
        IReadOnlyDictionary<string, TrainingSample> ManualByRef);
}

public sealed record AiFieldQualityReportFiles(string JsonPath, string MarkdownPath, string IssuesCsvPath);

public static class AiFieldQualityReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static AiFieldQualityReportFiles Write(
        string outputDirectory,
        AiFieldQualityReport report,
        string? fileStem = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Ausgabeordner fehlt.", nameof(outputDirectory));

        Directory.CreateDirectory(outputDirectory);
        var stem = string.IsNullOrWhiteSpace(fileStem)
            ? $"ai_quality_{report.GeneratedAtUtc:yyyyMMdd_HHmmss}"
            : fileStem.Trim();
        var jsonPath = Path.Combine(outputDirectory, stem + ".json");
        var markdownPath = Path.Combine(outputDirectory, stem + ".md");
        var csvPath = Path.Combine(outputDirectory, stem + "_issues.csv");

        AtomicTextFileWriter.WriteAllText(jsonPath, JsonSerializer.Serialize(report, JsonOptions));
        AtomicTextFileWriter.WriteAllText(markdownPath, BuildMarkdown(report));
        AtomicTextFileWriter.WriteAllText(csvPath, BuildIssuesCsv(report.Issues));
        return new AiFieldQualityReportFiles(jsonPath, markdownPath, csvPath);
    }

    private static string BuildMarkdown(AiFieldQualityReport report)
    {
        var d = report.Detection;
        var g = report.GreenRelease;
        var s = report.Shadow;
        var sb = new StringBuilder();
        sb.AppendLine("# KI-Qualitaetsbericht");
        sb.AppendLine();
        sb.AppendLine($"Erstellt: {report.GeneratedAtUtc:O}");
        sb.AppendLine();
        sb.AppendLine("## Erkennungsebene");
        sb.AppendLine();
        sb.AppendLine("| Kennzahl | Wert |");
        sb.AppendLine("|---|---:|");
        sb.AppendLine($"| Haltungen | {d.Holdings} |");
        sb.AppendLine($"| KI-Rohsamples | {d.RawAiSamples} |");
        sb.AppendLine($"| Deduplizierte KI-Befunde | {d.DeduplicatedAiFindings} |");
        sb.AppendLine($"| Exakter Code korrekt | {d.ExactCodeCorrect} |");
        sb.AppendLine($"| Falscher Untertyp | {d.SubtypeMismatch} |");
        sb.AppendLine($"| Falsche Code-Familie | {d.WrongCodeFamily} |");
        sb.AppendLine($"| Abgelehnte Fehlalarme | {d.RejectedFalsePositive} |");
        sb.AppendLine($"| Quantifizierung/Detail korrigiert | {d.QuantificationCorrections} |");
        sb.AppendLine($"| Moegliche verpasste Schaeden | {d.PossibleMisses} |");
        sb.AppendLine($"| Moegliche Meterfehler | {d.PossibleMeterMismatches} |");
        sb.AppendLine($"| Exakte Code-Genauigkeit | {d.ExactCodeAccuracy:P1} |");
        sb.AppendLine($"| Erkennungs-Recall gegen manuelle Befunde | {d.DetectionRecall:P1} |");
        sb.AppendLine();
        sb.AppendLine("## Gruene Entscheidungen");
        sb.AppendLine();
        sb.AppendLine($"- Geprueft: {g.ReviewedGreenFindings} von {g.DeduplicatedGreenFindings}");
        sb.AppendLine($"- Fehler: {g.GreenErrors} ({g.ErrorRate:P2})");
        sb.AppendLine($"- Obere 95%-Fehlergrenze: {g.ErrorRateUpper95:P2}");
        sb.AppendLine($"- Haltungen: {g.Holdings}");
        sb.AppendLine($"- Freigabekriterium: {(g.ReleaseCriterionMet ? "ERFUELLT" : "NICHT ERFUELLT")}");
        sb.AppendLine($"- Regel: {g.Criterion}");
        sb.AppendLine();
        sb.AppendLine("## Auswertungsebene (Schatten)");
        sb.AppendLine();
        sb.AppendLine($"- Vergleichbare Haltungen: {s.Comparable}");
        sb.AppendLine($"- Gleich: {s.Equal}");
        sb.AppendLine($"- Leicht abweichend: {s.LightDifference}");
        sb.AppendLine($"- Stark abweichend: {s.StrongDifference}");
        sb.AppendLine($"- Veraltet: {s.Stale}");
        sb.AppendLine($"- Zustandsklasse abweichend: {s.ConditionClassDifferences}");
        sb.AppendLine($"- Massnahme abweichend: {s.MeasureDifferences}");
        sb.AppendLine($"- Kosten ueber 25 % abweichend: {s.CostDifferences}");
        sb.AppendLine();
        sb.AppendLine("## Fehlergruppen");
        sb.AppendLine();
        foreach (var group in report.Issues.GroupBy(issue => issue.Category).OrderBy(group => group.Key))
            sb.AppendLine($"- {group.Key}: {group.Count()}");
        return sb.ToString();
    }

    private static string BuildIssuesCsv(IReadOnlyList<QualityFindingIssue> issues)
    {
        var sb = new StringBuilder();
        sb.AppendLine("category;case_id;sample_id;meter_start;meter_end;expected_code;predicted_code;meter_needs_review;detail");
        foreach (var issue in issues)
        {
            sb.Append(Csv(issue.Category)).Append(';')
                .Append(Csv(issue.CaseId)).Append(';')
                .Append(Csv(issue.SampleId ?? "")).Append(';')
                .Append(issue.MeterStart.ToString("0.###", CultureInfo.InvariantCulture)).Append(';')
                .Append(issue.MeterEnd.ToString("0.###", CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(issue.ExpectedCode)).Append(';')
                .Append(Csv(issue.PredictedCode)).Append(';')
                .Append(issue.MeterNeedsReview ? "true" : "false").Append(';')
                .Append(Csv(issue.Detail)).AppendLine();
        }
        return sb.ToString();
    }

    private static string Csv(string value)
        => '"' + value.Replace("\"", "\"\"") + '"';
}
