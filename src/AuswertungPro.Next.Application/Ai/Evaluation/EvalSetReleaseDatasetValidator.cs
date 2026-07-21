using System.Text;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public sealed record EvalSetReleaseDatasetValidationIssue(
    string CaseId,
    string Field,
    string Message);

public sealed class EvalSetReleaseDatasetValidationException : Exception
{
    public EvalSetReleaseDatasetValidationException()
        : this(Array.Empty<EvalSetReleaseDatasetValidationIssue>())
    {
    }

    public EvalSetReleaseDatasetValidationException(string message)
        : base(message)
    {
        Issues = Array.Empty<EvalSetReleaseDatasetValidationIssue>();
    }

    public EvalSetReleaseDatasetValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Issues = Array.Empty<EvalSetReleaseDatasetValidationIssue>();
    }

    public EvalSetReleaseDatasetValidationException(IReadOnlyList<EvalSetReleaseDatasetValidationIssue> issues)
        : base(BuildMessage(issues))
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = issues.ToArray();
    }

    public IReadOnlyList<EvalSetReleaseDatasetValidationIssue> Issues { get; }

    private static string BuildMessage(IReadOnlyList<EvalSetReleaseDatasetValidationIssue> issues)
        => issues.Count == 0
            ? "Das Release-Eval-Set ist ungueltig."
            : "Das Release-Eval-Set ist ungueltig: "
              + string.Join(" | ", issues.Select(issue => $"{issue.CaseId}/{issue.Field}: {issue.Message}"));
}

public static class EvalSetReleaseDatasetValidator
{
    public static IReadOnlyList<EvalSetBenchmarkCase> LoadAndValidate(string evalSetRoot)
    {
        var cases = EvalSetBenchmarkDataset.LoadForReleaseValidation(evalSetRoot);
        Validate(cases);
        return cases;
    }

    public static void Validate(IReadOnlyList<EvalSetBenchmarkCase> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);

        var issues = new List<EvalSetReleaseDatasetValidationIssue>();
        var events = new Dictionary<EventKey, EventMetadata>();

        if (cases.Count == 0)
        {
            issues.Add(new EvalSetReleaseDatasetValidationIssue(
                "<dataset>",
                "cases",
                "Mindestens ein Fall ist erforderlich."));
        }

        foreach (var benchmarkCase in cases)
        {
            var caseId = string.IsNullOrWhiteSpace(benchmarkCase.Id)
                ? "<ohne-id>"
                : benchmarkCase.Id;

            if (string.IsNullOrWhiteSpace(benchmarkCase.ImagePath) || !File.Exists(benchmarkCase.ImagePath))
            {
                Add(issues, caseId, "image_path", "Die Bilddatei fehlt.");
            }

            var holdingKey = NormalizeIdentifier(
                benchmarkCase.HoldingKey,
                caseId,
                "holding_key",
                required: true,
                issues);
            var expectedCode = EvalSetBenchmarkDataset.NormalizeCode(benchmarkCase.ExpectedFullCode);
            var isDamage = IsDamageCode(expectedCode);
            var eventId = NormalizeIdentifier(
                benchmarkCase.EventId,
                caseId,
                "event_id",
                required: isDamage,
                issues);

            if (isDamage)
            {
                if (benchmarkCase.ExpectedSeverity is not (>= 1 and <= 5))
                {
                    Add(
                        issues,
                        caseId,
                        "expected_severity",
                        "Ein Schadensereignis braucht einen Wert von 1 bis 5.");
                }
            }
            else if (benchmarkCase.ExpectedSeverity is < 1 or > 5)
            {
                Add(issues, caseId, "expected_severity", "Ein vorhandener Wert muss zwischen 1 und 5 liegen.");
            }

            ValidateMeterRange(benchmarkCase, caseId, issues);

            if (eventId is null || holdingKey is null)
                continue;

            var metadata = new EventMetadata(
                expectedCode ?? "",
                EvalSetBenchmarkDataset.NormalizeCode(benchmarkCase.ExpectedMainCode) ?? "",
                benchmarkCase.ExpectedSeverity,
                benchmarkCase.MeterStart,
                benchmarkCase.MeterEnd);
            var eventKey = EventKey.Create(holdingKey, eventId);

            if (!events.TryAdd(eventKey, metadata) && events[eventKey] != metadata)
            {
                Add(
                    issues,
                    caseId,
                    "event_metadata",
                    $"Die Angaben fuer Haltung '{holdingKey}', Ereignis '{eventId}' widersprechen einem anderen Frame.");
            }
        }

        if (issues.Count > 0)
            throw new EvalSetReleaseDatasetValidationException(issues);
    }

    private static void ValidateMeterRange(
        EvalSetBenchmarkCase benchmarkCase,
        string caseId,
        ICollection<EvalSetReleaseDatasetValidationIssue> issues)
    {
        if (benchmarkCase.Meter is { } meter && (!double.IsFinite(meter) || meter < 0))
            Add(issues, caseId, "meter", "Der Frame-Meterwert muss endlich und mindestens 0 sein.");

        var hasStart = benchmarkCase.MeterStart.HasValue;
        var hasEnd = benchmarkCase.MeterEnd.HasValue;
        if (hasStart != hasEnd)
        {
            Add(issues, caseId, "meter_range", "MeterStart und MeterEnd muessen gemeinsam gesetzt sein.");
            return;
        }

        if (!hasStart)
            return;

        var start = benchmarkCase.MeterStart!.Value;
        var end = benchmarkCase.MeterEnd!.Value;
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end < 0 || start > end)
        {
            Add(
                issues,
                caseId,
                "meter_range",
                "Der Meterbereich muss endlich, nicht negativ und aufsteigend sein.");
            return;
        }

        if (benchmarkCase.Meter is { } frameMeter && (frameMeter < start || frameMeter > end))
        {
            Add(issues, caseId, "meter", "Der Frame-Meterwert liegt ausserhalb des Ereignisbereichs.");
        }
    }

    private static string? NormalizeIdentifier(
        string? raw,
        string caseId,
        string field,
        bool required,
        ICollection<EvalSetReleaseDatasetValidationIssue> issues)
    {
        if (raw is null)
        {
            if (required)
                Add(issues, caseId, field, "Die Kennung fehlt.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            Add(issues, caseId, field, "Eine vorhandene Kennung darf nicht leer sein.");
            return null;
        }

        var trimmed = raw.Trim();
        if (!raw.Equals(trimmed, StringComparison.Ordinal))
            Add(issues, caseId, field, "Die Kennung darf keine Rand-Leerzeichen enthalten.");
        if (trimmed.Any(char.IsControl))
            Add(issues, caseId, field, "Die Kennung darf keine Steuerzeichen enthalten.");
        if (!trimmed.IsNormalized(NormalizationForm.FormC))
            Add(issues, caseId, field, "Die Kennung muss Unicode-normalisiert sein.");

        return trimmed.Normalize(NormalizationForm.FormC);
    }

    private static void Add(
        ICollection<EvalSetReleaseDatasetValidationIssue> issues,
        string caseId,
        string field,
        string message)
        => issues.Add(new EvalSetReleaseDatasetValidationIssue(caseId, field, message));

    private static bool IsDamageCode(string? code)
        => code is not null
           && (code.StartsWith("BA", StringComparison.Ordinal)
               || code.StartsWith("BB", StringComparison.Ordinal));

    private readonly record struct EventKey(string HoldingKey, string EventId)
    {
        public static EventKey Create(string holdingKey, string eventId)
            => new(
                (EvalContaminationGuard.NormalizeHaltungKey(holdingKey) ?? holdingKey).ToUpperInvariant(),
                eventId.ToUpperInvariant());
    }

    private sealed record EventMetadata(
        string ExpectedFullCode,
        string ExpectedMainCode,
        int? ExpectedSeverity,
        double? MeterStart,
        double? MeterEnd);
}
