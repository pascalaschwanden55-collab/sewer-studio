using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public sealed record EvalReviewedDamageCase(
    EvalSetBenchmarkCase BenchmarkCase,
    string OriginalExpectedCode,
    string ReviewDecision,
    bool ExpectedIsDamage);

public sealed record EvalReviewedDamageDatasetResult(
    IReadOnlyList<EvalReviewedDamageCase> Cases,
    int SchemaVersion,
    string SourceCandidatesSha256,
    int CompletedReviews);

/// <summary>
/// Verbindet die getrennte menschliche Schadensreview mit dem unveraenderten V1-Eval-Set.
/// Das eingefrorene Eval-Set wird dabei ausschliesslich gelesen.
/// </summary>
public static class EvalReviewedDamageDataset
{
    private const int SupportedSchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static EvalReviewedDamageDatasetResult Load(string evalSetRoot, string reviewFile)
    {
        if (string.IsNullOrWhiteSpace(evalSetRoot))
            throw new ArgumentException("Eval-Set-Pfad fehlt.", nameof(evalSetRoot));
        if (string.IsNullOrWhiteSpace(reviewFile))
            throw new ArgumentException("Review-Datei fehlt.", nameof(reviewFile));

        var fullEvalRoot = Path.GetFullPath(evalSetRoot);
        var fullReviewFile = Path.GetFullPath(reviewFile);
        var candidatesPath = Path.Combine(fullEvalRoot, "_candidates.json");

        if (!File.Exists(candidatesPath))
            throw new FileNotFoundException("Eval-Set-Kandidaten nicht gefunden.", candidatesPath);
        if (!File.Exists(fullReviewFile))
            throw new FileNotFoundException("Schadensreview nicht gefunden.", fullReviewFile);

        var review = LoadReview(fullReviewFile);
        ValidateHeader(review, candidatesPath);
        var reviewEntries = review.Reviews
                            ?? throw new InvalidDataException(
                                "Die Schadensreview enthaelt keine Review-Liste.");

        var originalDamageCases = EvalSetBenchmarkDataset
            .LoadForReleaseValidation(fullEvalRoot)
            .Where(item => IsDamageCode(item.ExpectedFullCode))
            .ToList();

        if (review.DamageFrames != originalDamageCases.Count)
        {
            throw new InvalidDataException(
                $"Review und Eval-Set haben unterschiedlich viele Schadensbilder " +
                $"({review.DamageFrames} statt {originalDamageCases.Count}).");
        }

        if (review.CompletedReviews != reviewEntries.Count
            || review.CompletedReviews != originalDamageCases.Count)
        {
            throw new InvalidDataException(
                $"Die Schadensreview ist nicht vollstaendig " +
                $"({review.CompletedReviews}/{originalDamageCases.Count}).");
        }

        if (review.ConflictingReviews != 0)
            throw new InvalidDataException("Die Schadensreview enthaelt noch Ereigniskonflikte.");

        var reviewById = new Dictionary<string, ReviewEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in reviewEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Id) || !reviewById.TryAdd(entry.Id, entry))
                throw new InvalidDataException($"Doppelte oder leere Review-ID: '{entry.Id}'.");
        }

        var result = new List<EvalReviewedDamageCase>(originalDamageCases.Count);
        foreach (var original in originalDamageCases)
        {
            if (!reviewById.Remove(original.Id, out var entry))
                throw new InvalidDataException($"Review fuer Eval-Fall '{original.Id}' fehlt.");

            result.Add(BuildCase(original, entry));
        }

        if (reviewById.Count > 0)
        {
            throw new InvalidDataException(
                "Die Review enthaelt unbekannte Eval-IDs: "
                + string.Join(", ", reviewById.Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)));
        }

        EvalSetReleaseDatasetValidator.Validate(result.Select(item => item.BenchmarkCase).ToList());

        return new EvalReviewedDamageDatasetResult(
            result,
            review.SchemaVersion,
            review.SourceCandidatesSha256,
            review.CompletedReviews);
    }

    private static ReviewDocument LoadReview(string reviewFile)
    {
        try
        {
            return JsonSerializer.Deserialize<ReviewDocument>(
                       File.ReadAllText(reviewFile, Encoding.UTF8),
                       JsonOptions)
                   ?? throw new InvalidDataException("Die Schadensreview ist leer.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Die Schadensreview ist kein gueltiges JSON.", ex);
        }
    }

    private static void ValidateHeader(ReviewDocument review, string candidatesPath)
    {
        if (review.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidDataException(
                $"Nicht unterstuetzte Review-Schemaversion: {review.SchemaVersion}.");
        }

        var actualHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(candidatesPath)))
            .ToLowerInvariant();
        if (!string.Equals(
                actualHash,
                review.SourceCandidatesSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Die Schadensreview gehoert nicht zum aktuellen eingefrorenen Eval-Set.");
        }
    }

    private static EvalReviewedDamageCase BuildCase(
        EvalSetBenchmarkCase original,
        ReviewEntry review)
    {
        RequireSame(
            original.FrameFileName,
            review.ImageName,
            original.Id,
            "Bildname");
        RequireSame(
            original.HoldingKey,
            review.HoldingKey,
            original.Id,
            "Haltung");
        RequireSame(
            original.ExpectedFullCode,
            review.ExpectedCode,
            original.Id,
            "Vorgabe-Code");

        if (!review.ImageExists || !File.Exists(original.ImagePath))
            throw new InvalidDataException($"Review-Fall {original.Id}: Bilddatei fehlt.");
        if (string.IsNullOrWhiteSpace(review.ReviewedBy) || review.ReviewedAtUtc is null)
            throw new InvalidDataException($"Review-Fall {original.Id}: Pruefbeleg fehlt.");

        var decision = review.CodeDecision?.Trim().ToLowerInvariant() ?? "";
        var expectedCode = decision switch
        {
            "matches" => NormalizeRequiredCode(review.ExpectedCode, original.Id),
            "corrected" => NormalizeRequiredCode(review.CorrectedCode, original.Id),
            "no_damage" => "LEER",
            _ => throw new InvalidDataException(
                $"Review-Fall {original.Id}: unbekannte Code-Entscheidung '{review.CodeDecision}'.")
        };
        var expectedIsDamage = decision != "no_damage";

        if (expectedIsDamage && !IsDamageCode(expectedCode))
        {
            throw new InvalidDataException(
                $"Review-Fall {original.Id}: '{expectedCode}' ist kein BA-/BB-Schadencode.");
        }

        if (decision == "matches" && !string.IsNullOrWhiteSpace(review.CorrectedCode))
            throw new InvalidDataException($"Review-Fall {original.Id}: unnoetiger Korrekturcode.");

        if (!expectedIsDamage)
        {
            if (!string.IsNullOrWhiteSpace(review.CorrectedCode)
                || review.ExpectedSeverity is not null
                || !string.IsNullOrWhiteSpace(review.EventId)
                || review.MeterStart is not null
                || review.MeterEnd is not null)
            {
                throw new InvalidDataException(
                    $"Review-Fall {original.Id}: Ausschluss enthaelt Schadensmetadaten.");
            }
        }

        var benchmarkCase = original with
        {
            ExpectedFullCode = expectedCode,
            ExpectedMainCode = expectedIsDamage ? MainCode(expectedCode) : "LEER",
            ExpectedSeverity = expectedIsDamage ? review.ExpectedSeverity : null,
            EventId = expectedIsDamage ? review.EventId : null,
            MeterStart = expectedIsDamage ? review.MeterStart : null,
            MeterEnd = expectedIsDamage ? review.MeterEnd : null
        };

        return new EvalReviewedDamageCase(
            benchmarkCase,
            NormalizeRequiredCode(review.ExpectedCode, original.Id),
            decision,
            expectedIsDamage);
    }

    private static void RequireSame(
        string? expected,
        string? actual,
        string caseId,
        string field)
    {
        if (!string.Equals(expected?.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Review-Fall {caseId}: {field} passt nicht zum eingefrorenen Eval-Set.");
        }
    }

    private static string NormalizeRequiredCode(string? code, string caseId)
        => EvalSetBenchmarkDataset.NormalizeCode(code)
           ?? throw new InvalidDataException($"Review-Fall {caseId}: Schadencode fehlt.");

    private static string MainCode(string code)
        => code.Length <= 3 ? code : code[..3];

    private static bool IsDamageCode(string? code)
    {
        var normalized = EvalSetBenchmarkDataset.NormalizeCode(code);
        return normalized is not null
               && (normalized.StartsWith("BA", StringComparison.Ordinal)
                   || normalized.StartsWith("BB", StringComparison.Ordinal));
    }

    private sealed class ReviewDocument
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("source_candidates_sha256")]
        public string SourceCandidatesSha256 { get; set; } = "";

        [JsonPropertyName("damage_frames")]
        public int DamageFrames { get; set; }

        [JsonPropertyName("completed_reviews")]
        public int CompletedReviews { get; set; }

        [JsonPropertyName("conflicting_reviews")]
        public int ConflictingReviews { get; set; }

        [JsonPropertyName("reviews")]
        public List<ReviewEntry>? Reviews { get; set; } = [];
    }

    private sealed class ReviewEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("image_name")]
        public string ImageName { get; set; } = "";

        [JsonPropertyName("image_exists")]
        public bool ImageExists { get; set; }

        [JsonPropertyName("holding_key")]
        public string HoldingKey { get; set; } = "";

        [JsonPropertyName("expected_code")]
        public string ExpectedCode { get; set; } = "";

        [JsonPropertyName("code_decision")]
        public string CodeDecision { get; set; } = "";

        [JsonPropertyName("corrected_code")]
        public string? CorrectedCode { get; set; }

        [JsonPropertyName("expected_severity")]
        public int? ExpectedSeverity { get; set; }

        [JsonPropertyName("event_id")]
        public string? EventId { get; set; }

        [JsonPropertyName("meter_start")]
        public double? MeterStart { get; set; }

        [JsonPropertyName("meter_end")]
        public double? MeterEnd { get; set; }

        [JsonPropertyName("reviewed_by")]
        public string ReviewedBy { get; set; } = "";

        [JsonPropertyName("reviewed_at_utc")]
        public DateTimeOffset? ReviewedAtUtc { get; set; }
    }
}
