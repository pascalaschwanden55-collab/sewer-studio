using System.Security.Cryptography;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Evaluation;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class EvalReviewedDamageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-reviewed-damage-" + Guid.NewGuid().ToString("N"));
    private readonly string _evalRoot;
    private readonly string _reviewFile;

    public EvalReviewedDamageTests()
    {
        _evalRoot = Path.Combine(_root, "eval");
        _reviewFile = Path.Combine(_root, "review.json");
        Directory.CreateDirectory(Path.Combine(_evalRoot, "images"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Nur isolierte Testdaten aufraeumen.
        }
    }

    [Fact]
    public void Load_uebernimmt_Bestaetigung_Korrektur_und_Ausschluss_ohne_V1_zu_aendern()
    {
        WriteEvalSet();
        WriteReview();
        var candidatesPath = Path.Combine(_evalRoot, "_candidates.json");
        var bytesBefore = File.ReadAllBytes(candidatesPath);

        var result = EvalReviewedDamageDataset.Load(_evalRoot, _reviewFile);

        Assert.Equal(3, result.Cases.Count);
        Assert.Equal("BAJA", result.Cases[0].BenchmarkCase.ExpectedFullCode);
        Assert.Equal("BAF", result.Cases[1].BenchmarkCase.ExpectedFullCode);
        Assert.Equal(3, result.Cases[1].BenchmarkCase.ExpectedSeverity);
        Assert.Equal("LEER", result.Cases[2].BenchmarkCase.ExpectedFullCode);
        Assert.False(result.Cases[2].ExpectedIsDamage);
        Assert.Null(result.Cases[2].BenchmarkCase.ExpectedSeverity);
        Assert.Null(result.Cases[2].BenchmarkCase.EventId);
        Assert.Equal(bytesBefore, File.ReadAllBytes(candidatesPath));
    }

    [Fact]
    public void Load_sperrt_Review_mit_falschem_Quellhash()
    {
        WriteEvalSet();
        WriteReview(sourceHash: new string('0', 64));

        var error = Assert.Throws<InvalidDataException>(
            () => EvalReviewedDamageDataset.Load(_evalRoot, _reviewFile));

        Assert.Contains("gehoert nicht", error.Message);
    }

    [Fact]
    public void Load_sperrt_offene_Ereigniskonflikte()
    {
        WriteEvalSet();
        WriteReview(conflictingReviews: 1);

        var error = Assert.Throws<InvalidDataException>(
            () => EvalReviewedDamageDataset.Load(_evalRoot, _reviewFile));

        Assert.Contains("Ereigniskonflikte", error.Message);
    }

    [Fact]
    public void Scorer_misst_Schaden_Code_Stufe_und_Ereignis_getrennt()
    {
        var cases = new[]
        {
            DamageCase("frame-1.png", "event-1", "BAJA", severity: 2),
            DamageCase("frame-2.png", "event-1", "BAJA", severity: 2),
            DamageCase("frame-3.png", "event-2", "BAF", severity: 4),
            NoDamageCase("frame-4.png"),
            NoDamageCase("frame-5.png"),
            NoDamageCase("frame-6.png")
        };
        var predictions = new[]
        {
            Prediction("frame-1.png", "BAF", severity: 3),
            Prediction("frame-2.png", "BAJA", severity: 2),
            Prediction("frame-3.png", "LEER", severity: 0),
            Prediction("frame-4.png", "BCD", severity: 0),
            Prediction("frame-5.png", "BAIZ", severity: 2)
        };

        var result = EvalReviewedDamageScorer.Evaluate(cases, predictions);
        var summary = result.Summary;

        Assert.Equal(3, summary.DamageFrames);
        Assert.Equal(3, summary.NoDamageFrames);
        Assert.Equal(2, summary.TruePositiveDamageFrames);
        Assert.Equal(1, summary.FalseNegativeDamageFrames);
        Assert.Equal(1, summary.FalsePositiveDamageFrames);
        Assert.Equal(1, summary.TrueNegativeDamageFrames);
        Assert.Equal(1, summary.UnresolvedFrames);
        Assert.Equal(2.0 / 3.0, summary.DamageRecall, precision: 10);
        Assert.Equal(1, summary.ExactCodeCorrectFrames);
        Assert.Equal(2, summary.SeverityEvaluatedFrames);
        Assert.Equal(1, summary.SeverityExactFrames);
        Assert.Equal(2, summary.SeverityWithinOneFrames);
        Assert.Equal(2, summary.PresenceEvents.EventCount);
        Assert.Equal(1, summary.PresenceEvents.DetectedEvents);
        Assert.Equal(1, summary.ExactCodeEvents.DetectedEvents);
        Assert.Equal(1, summary.SeverePresenceEvents.EventCount);
        Assert.False(summary.HasMinimumSevereEvents);
    }

    private void WriteEvalSet()
    {
        var candidates = new[]
        {
            Candidate("case-1", "frame-1.png", "H-1", "BAJA"),
            Candidate("case-2", "frame-2.png", "H-1", "BAAA"),
            Candidate("case-3", "frame-3.png", "H-2", "BAIZ")
        };
        File.WriteAllText(
            Path.Combine(_evalRoot, "_candidates.json"),
            JsonSerializer.Serialize(candidates));

        File.WriteAllBytes(Path.Combine(_evalRoot, "images", "frame-1.png"), [1]);
        File.WriteAllBytes(Path.Combine(_evalRoot, "images", "frame-2.png"), [2]);
        File.WriteAllBytes(Path.Combine(_evalRoot, "images", "frame-3.png"), [3]);
    }

    private void WriteReview(string? sourceHash = null, int conflictingReviews = 0)
    {
        var candidatesPath = Path.Combine(_evalRoot, "_candidates.json");
        sourceHash ??= Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(candidatesPath)))
            .ToLowerInvariant();
        var reviewedAt = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        var reviews = new object[]
        {
            Review(
                "case-1",
                "frame-1.png",
                "H-1",
                "BAJA",
                "matches",
                severity: 2,
                eventId: "H-1-1",
                reviewedAt: reviewedAt),
            Review(
                "case-2",
                "frame-2.png",
                "H-1",
                "BAAA",
                "corrected",
                correctedCode: "BAF",
                severity: 3,
                eventId: "H-1-2",
                reviewedAt: reviewedAt),
            Review(
                "case-3",
                "frame-3.png",
                "H-2",
                "BAIZ",
                "no_damage",
                reviewedAt: reviewedAt)
        };
        var document = new
        {
            schema_version = 2,
            source_candidates_sha256 = sourceHash,
            damage_frames = reviews.Length,
            completed_reviews = reviews.Length,
            conflicting_reviews = conflictingReviews,
            reviews
        };

        File.WriteAllText(_reviewFile, JsonSerializer.Serialize(document));
    }

    private static object Candidate(
        string id,
        string frame,
        string holding,
        string code)
        => new
        {
            id,
            frame_path = frame,
            haltung_key = holding,
            meter = 1.0,
            code_main = code[..3],
            code_full = code,
            kategorie = "damage"
        };

    private static object Review(
        string id,
        string imageName,
        string holding,
        string expectedCode,
        string decision,
        string? correctedCode = null,
        int? severity = null,
        string? eventId = null,
        DateTimeOffset? reviewedAt = null)
        => new
        {
            id,
            image_name = imageName,
            image_exists = true,
            holding_key = holding,
            expected_code = expectedCode,
            code_decision = decision,
            corrected_code = correctedCode,
            expected_severity = severity,
            event_id = eventId,
            meter_start = (double?)null,
            meter_end = (double?)null,
            reviewed_by = "Pascal",
            reviewed_at_utc = reviewedAt
        };

    private static EvalReviewedDamageCase DamageCase(
        string frame,
        string eventId,
        string code,
        int severity)
        => new(
            new EvalSetBenchmarkCase(
                Id: frame,
                FrameFileName: frame,
                ImagePath: frame,
                ExpectedFullCode: code,
                ExpectedMainCode: code[..3],
                Category: "damage_review",
                Meter: 1,
                HoldingKey: "H-1",
                ExpectedSeverity: severity,
                EventId: eventId),
            OriginalExpectedCode: code,
            ReviewDecision: "matches",
            ExpectedIsDamage: true);

    private static EvalReviewedDamageCase NoDamageCase(string frame)
        => new(
            new EvalSetBenchmarkCase(
                Id: frame,
                FrameFileName: frame,
                ImagePath: frame,
                ExpectedFullCode: "LEER",
                ExpectedMainCode: "LEER",
                Category: "damage_review",
                Meter: 1,
                HoldingKey: "H-1"),
            OriginalExpectedCode: "BAIZ",
            ReviewDecision: "no_damage",
            ExpectedIsDamage: false);

    private static EvalSetPrediction Prediction(
        string frame,
        string code,
        int severity)
        => new(frame, code, severity, TimeMs: 10);
}
