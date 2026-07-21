using AuswertungPro.Next.Application.Ai.Evaluation;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class EvalSetReleaseMetricsTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-release-eval-" + Guid.NewGuid().ToString("N"));
    private readonly string _imagePath;

    public EvalSetReleaseMetricsTests()
    {
        Directory.CreateDirectory(_root);
        _imagePath = Path.Combine(_root, "frame.png");
        File.WriteAllBytes(_imagePath, [1, 2, 3]);
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
            // Nur Testdaten aufraeumen.
        }
    }

    [Fact]
    public void ReleaseValidator_accepts_consistent_multiframe_event()
    {
        var cases = new[]
        {
            DamageCase("frame-1", "event-1", severity: 4, meter: 10.0),
            DamageCase("frame-2", "event-1", severity: 4, meter: 10.4)
        };

        EvalSetReleaseDatasetValidator.Validate(cases);
    }

    [Fact]
    public void ReleaseValidator_accepts_non_damage_without_event_id()
    {
        var benchmarkCase = new EvalSetBenchmarkCase(
            Id: "empty-frame",
            FrameFileName: "empty.png",
            ImagePath: _imagePath,
            ExpectedFullCode: "LEER",
            ExpectedMainCode: "LEER",
            Category: "empty",
            Meter: 0,
            HoldingKey: "H-2");

        EvalSetReleaseDatasetValidator.Validate([benchmarkCase]);
    }

    [Fact]
    public void ReleaseValidator_rejects_blank_optional_event_id()
    {
        var benchmarkCase = new EvalSetBenchmarkCase(
            Id: "empty-frame",
            FrameFileName: "empty.png",
            ImagePath: _imagePath,
            ExpectedFullCode: "LEER",
            ExpectedMainCode: "LEER",
            Category: "empty",
            Meter: 0,
            HoldingKey: "H-2",
            EventId: " ");

        var error = Assert.Throws<EvalSetReleaseDatasetValidationException>(
            () => EvalSetReleaseDatasetValidator.Validate([benchmarkCase]));

        Assert.Contains(error.Issues, issue => issue.Field == "event_id");
    }

    [Fact]
    public void ReleaseValidator_rejects_damage_without_event_id()
    {
        var benchmarkCase = DamageCase(
            "damage-without-event",
            eventId: null,
            severity: 4,
            meter: 10.0);

        var error = Assert.Throws<EvalSetReleaseDatasetValidationException>(
            () => EvalSetReleaseDatasetValidator.Validate([benchmarkCase]));

        Assert.Contains(error.Issues, issue => issue.Field == "event_id");
    }

    [Fact]
    public void ReleaseValidator_reports_missing_image_ids_severity_and_invalid_meter_range()
    {
        var invalid = DamageCase(
            "frame-1",
            eventId: " ",
            severity: null,
            meter: 8.0,
            holdingKey: " H-1 ",
            imagePath: Path.Combine(_root, "missing.png"),
            meterStart: 9.0,
            meterEnd: 8.0);

        var error = Assert.Throws<EvalSetReleaseDatasetValidationException>(
            () => EvalSetReleaseDatasetValidator.Validate([invalid]));

        Assert.Contains(error.Issues, issue => issue.Field == "image_path");
        Assert.Contains(error.Issues, issue => issue.Field == "holding_key");
        Assert.Contains(error.Issues, issue => issue.Field == "event_id");
        Assert.Contains(error.Issues, issue => issue.Field == "expected_severity");
        Assert.Contains(error.Issues, issue => issue.Field == "meter_range");
    }

    [Fact]
    public void ReleaseValidator_rejects_inconsistent_metadata_for_same_event()
    {
        var cases = new[]
        {
            DamageCase("frame-1", "event-1", severity: 4, meter: 10.0),
            DamageCase("frame-2", "event-1", severity: 5, meter: 10.4)
        };

        var error = Assert.Throws<EvalSetReleaseDatasetValidationException>(
            () => EvalSetReleaseDatasetValidator.Validate(cases));

        Assert.Contains(error.Issues, issue => issue.Field == "event_metadata");
    }

    [Fact]
    public void ReleaseValidator_accepts_same_event_id_in_different_holdings()
    {
        var cases = new[]
        {
            DamageCase("frame-1", "event-1", severity: 4, meter: 10.0, holdingKey: "H-1"),
            DamageCase("frame-2", "event-1", severity: 5, meter: 10.4, holdingKey: "H-2")
        };

        EvalSetReleaseDatasetValidator.Validate(cases);
    }

    [Fact]
    public void LoadAndValidate_reports_candidate_whose_image_is_missing()
    {
        var datasetRoot = Path.Combine(_root, "missing-image-set");
        Directory.CreateDirectory(Path.Combine(datasetRoot, "images"));
        File.WriteAllText(Path.Combine(datasetRoot, "_candidates.json"), """
            [
              {
                "id": "missing-frame",
                "frame_path": "missing.png",
                "haltung_key": "H-1",
                "code_full": "BABBA",
                "code_main": "BAB",
                "expected_severity": 4,
                "event_id": "event-1",
                "meter": 10.0,
                "meter_start": 9.5,
                "meter_end": 10.5
              }
            ]
            """);

        var error = Assert.Throws<EvalSetReleaseDatasetValidationException>(
            () => EvalSetReleaseDatasetValidator.LoadAndValidate(datasetRoot));

        Assert.Contains(
            error.Issues,
            issue => issue.CaseId == "missing-frame" && issue.Field == "image_path");
    }

    [Fact]
    public void EventScorer_counts_event_once_and_uses_one_hit_rule()
    {
        var score = EvalSetEventScorer.Score([
            Frame("frame-1", "event-1", 4, EvalSetEventFrameOutcome.NotCorrectlyDetected),
            Frame("frame-2", "event-1", 4, EvalSetEventFrameOutcome.CorrectlyDetectedGatePassed),
            Frame("frame-3", "event-2", 2, EvalSetEventFrameOutcome.CorrectlyDetectedGateBlocked)
        ]);

        Assert.Equal(2, score.AllEvents.EventCount);
        Assert.Equal(2, score.AllEvents.DetectedEvents);
        Assert.Equal(0, score.AllEvents.DetectionMisses.Misses);
        Assert.Equal(1, score.AllEvents.GatePassedEvents);
        Assert.Equal(1, score.AllEvents.GateMisses.Misses);
    }

    [Fact]
    public void EventScorer_counts_same_event_id_in_different_holdings_as_two_events()
    {
        var score = EvalSetEventScorer.Score([
            Frame(
                "frame-1",
                "event-1",
                4,
                EvalSetEventFrameOutcome.CorrectlyDetectedGatePassed,
                holdingKey: "H-1"),
            Frame(
                "frame-2",
                "event-1",
                4,
                EvalSetEventFrameOutcome.NotCorrectlyDetected,
                holdingKey: "H-2")
        ]);

        Assert.Equal(2, score.AllEvents.EventCount);
        Assert.Equal(1, score.AllEvents.DetectedEvents);
        Assert.Equal(1, score.AllEvents.DetectionMisses.Misses);
    }

    [Fact]
    public void EventScorer_rejects_conflicting_metadata_within_same_holding_and_event()
    {
        var frames = new[]
        {
            Frame("frame-1", "event-1", 4, EvalSetEventFrameOutcome.NotCorrectlyDetected),
            Frame("frame-2", "event-1", 5, EvalSetEventFrameOutcome.CorrectlyDetectedGatePassed)
        };

        Assert.Throws<ArgumentException>(() => EvalSetEventScorer.Score(frames));
    }

    [Fact]
    public void EventScorer_counts_severity_four_and_five_misses_separately()
    {
        var score = EvalSetEventScorer.Score([
            Frame("frame-1", "severe-missed", 4, EvalSetEventFrameOutcome.NotCorrectlyDetected),
            Frame("frame-2", "severe-blocked", 5, EvalSetEventFrameOutcome.CorrectlyDetectedGateBlocked),
            Frame("frame-3", "medium-missed", 3, EvalSetEventFrameOutcome.NotCorrectlyDetected)
        ]);

        Assert.Equal(2, score.SeverityFourOrFiveEvents.EventCount);
        Assert.Equal(1, score.SeverityFourOrFiveEvents.DetectedEvents);
        Assert.Equal(1, score.SeverityFourOrFiveEvents.DetectionMisses.Misses);
        Assert.Equal(0, score.SeverityFourOrFiveEvents.GatePassedEvents);
        Assert.Equal(2, score.SeverityFourOrFiveEvents.GateMisses.Misses);
        Assert.False(score.HasMinimumSeverityFourOrFiveEvents);
    }

    [Fact]
    public void EventScorer_reports_minimum_and_wilson_error_bounds_for_twenty_severe_events()
    {
        var frames = Enumerable.Range(1, 20)
            .Select(index => Frame(
                $"frame-{index}",
                $"event-{index}",
                4,
                EvalSetEventFrameOutcome.CorrectlyDetectedGatePassed))
            .ToList();

        var score = EvalSetEventScorer.Score(frames);
        var misses = score.SeverityFourOrFiveEvents.GateMisses;

        Assert.True(score.HasMinimumSeverityFourOrFiveEvents);
        Assert.Equal(20, score.RequiredSeverityFourOrFiveEvents);
        Assert.Equal(0, misses.Misses);
        Assert.Equal(0.0, misses.WilsonLower95);
        Assert.InRange(misses.WilsonUpper95, 0.1610, 0.1612);
        Assert.InRange(misses.ExactOneSidedUpper95, 0.1390, 0.1392);
    }

    private EvalSetBenchmarkCase DamageCase(
        string id,
        string? eventId,
        int? severity,
        double meter,
        string holdingKey = "H-1",
        string? imagePath = null,
        double meterStart = 9.5,
        double meterEnd = 10.5)
        => new(
            Id: id,
            FrameFileName: id + ".png",
            ImagePath: imagePath ?? _imagePath,
            ExpectedFullCode: "BABBA",
            ExpectedMainCode: "BABBA",
            Category: "damage",
            Meter: meter,
            HasYoloLabel: true,
            HoldingKey: holdingKey,
            ExpectedSeverity: severity,
            EventId: eventId,
            MeterStart: meterStart,
            MeterEnd: meterEnd);

    private static EvalSetDamageEventFrameResult Frame(
        string frameId,
        string eventId,
        int severity,
        EvalSetEventFrameOutcome outcome,
        string holdingKey = "H-1")
        => new(frameId, holdingKey, eventId, severity, outcome);
}
