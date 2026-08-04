using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class EvalReviewedFullChainScorerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-eval-full-chain-" + Guid.NewGuid().ToString("N"));

    public EvalReviewedFullChainScorerTests()
        => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Evaluate_trennt_erkennung_von_gruenem_quality_gate()
    {
        var cases = new[]
        {
            ReviewedCase("a.png", "H1", "event-1", "BAJA", 4, expectedIsDamage: true),
            ReviewedCase("b.png", "H2", null, "LEER", null, expectedIsDamage: false),
        };
        var predictions = new[]
        {
            Prediction("a.png", "BAJA", 4, TrafficLight.Yellow),
            Prediction("b.png", "BAJA", 3, TrafficLight.Green),
        };

        var score = EvalReviewedFullChainScorer.Evaluate(cases, predictions);

        Assert.Equal(1, score.Summary.Damage.TruePositiveDamageFrames);
        Assert.Equal(1, score.Summary.Damage.ExactCodeCorrectFrames);
        Assert.Equal(1, score.Summary.Damage.FalsePositiveDamageFrames);
        Assert.Equal(0, score.Summary.DamageFramesPassingGate);
        Assert.Equal(1, score.Summary.FalsePositiveFramesPassingGate);
        Assert.Equal(1, score.Summary.PresenceEvents.AllEvents.DetectedEvents);
        Assert.Equal(0, score.Summary.PresenceEvents.AllEvents.GatePassedEvents);
        Assert.Equal(1, score.Summary.QualityGateGreenFrames);
        Assert.Equal(1, score.Summary.QualityGateYellowFrames);
    }

    [Fact]
    public void Evaluate_zaehlt_pipeline_stufen_und_technische_fehler()
    {
        var cases = new[]
        {
            ReviewedCase("a.png", "H1", "event-1", "BAJA", 4, expectedIsDamage: true),
        };
        var predictions = new[]
        {
            Prediction(
                "a.png",
                predictedCode: "",
                severity: 0,
                gate: null,
                error: "DINO degraded",
                dinoBoxes: 0,
                samCalled: false,
                qwenCalled: false,
                incomplete: true),
        };

        var score = EvalReviewedFullChainScorer.Evaluate(cases, predictions);

        Assert.Equal(1, score.Summary.DinoCalledFrames);
        Assert.Equal(0, score.Summary.SamCalledFrames);
        Assert.Equal(0, score.Summary.QwenVisionCalledFrames);
        Assert.Equal(1, score.Summary.IncompleteFrames);
        Assert.Equal(1, score.Summary.Damage.UnresolvedFrames);
        Assert.Equal(0, score.Summary.PresenceEvents.AllEvents.DetectedEvents);
    }

    [Fact]
    public void Writers_schreiben_stabile_csv_kopfzeile_und_json_form()
    {
        var cases = new[]
        {
            ReviewedCase("frame,\"eins\".png", "H1", "event-1", "BAJA", 4, expectedIsDamage: true),
        };
        var score = EvalReviewedFullChainScorer.Evaluate(
            cases,
            [Prediction("frame,\"eins\".png", "BAJA", 4, TrafficLight.Green)]);
        var csvPath = Path.Combine(_root, "full-chain.csv");
        var jsonPath = Path.Combine(_root, "full-chain.json");

        EvalReviewedFullChainScorer.WriteCsv(csvPath, score.Rows);
        EvalReviewedFullChainScorer.WriteSummaryJson(
            jsonPath,
            score.Summary,
            new { run = "test" });

        Assert.Equal(
            "frame,haltung,event_id,gt_original,gt_reviewed,gt_is_damage,gt_severity," +
            "pred,pred_severity,usable,pred_is_damage,presence_correct,exact_code,main_code," +
            "severity_evaluated,severity_exact,severity_within_one,time_ms,error," +
            "detector_bypassed,dino_called,dino_boxes,sam_called,sam_masks," +
            "qwen_vision_called,qwen_findings,code_mapping_called,code_mapping_count," +
            "quality_gate,quality_gate_composite,gate_passed,degraded,incomplete,drop_reason",
            File.ReadLines(csvPath).First());
        Assert.Contains("\"frame,\"\"eins\"\".png\"", File.ReadAllText(csvPath));

        using var json = JsonDocument.Parse(File.ReadAllText(jsonPath));
        Assert.Equal("test", json.RootElement.GetProperty("metadata").GetProperty("run").GetString());
        Assert.Equal(
            1,
            json.RootElement
                .GetProperty("summary")
                .GetProperty("Damage")
                .GetProperty("TotalFrames")
                .GetInt32());
    }

    [Fact]
    public void Full_chain_tool_aktiviert_code_mapping_unabhaengig_vom_app_schalter()
    {
        var program = File.ReadAllText(TestRepoPaths.RepoFile(
            "tools",
            "EvalSetBenchmark",
            "Program.cs"));

        Assert.Matches(
            @"var\s+fullChainSettings\s*=\s*config\s+with\s*\{[\s\S]{0,300}?Enabled\s*=\s*true",
            program);
    }

    [Fact]
    public void DescribeTechnicalError_meldet_dino_fehler_vor_allgemeinem_degraded_hinweis()
    {
        var error = EvalReviewedFullChainScorer.DescribeTechnicalError(
            primaryError: null,
            incomplete: true,
            degradedReason: "YOLO-Detektor nicht qualifiziert.",
            dropReason: "dino_error");

        Assert.Contains("DINO", error);
        Assert.Contains("dino_error", error);
        Assert.Contains("YOLO-Detektor nicht qualifiziert.", error);
    }

    [Fact]
    public void DescribeTechnicalError_behandelt_dino_ohne_box_als_gueltiges_negativergebnis()
    {
        var error = EvalReviewedFullChainScorer.DescribeTechnicalError(
            primaryError: null,
            incomplete: false,
            degradedReason: "YOLO-Detektor nicht qualifiziert.",
            dropReason: "dino_no_boxes");

        Assert.Null(error);
    }

    private static EvalReviewedDamageCase ReviewedCase(
        string frame,
        string holding,
        string? eventId,
        string code,
        int? severity,
        bool expectedIsDamage)
        => new(
            new EvalSetBenchmarkCase(
                Id: frame,
                FrameFileName: frame,
                ImagePath: frame,
                ExpectedFullCode: code,
                ExpectedMainCode: code.Length >= 3 ? code[..3] : code,
                Category: "review",
                Meter: 1,
                HoldingKey: holding,
                ExpectedSeverity: severity,
                EventId: eventId),
            OriginalExpectedCode: code,
            ReviewDecision: expectedIsDamage ? "matches" : "no_damage",
            ExpectedIsDamage: expectedIsDamage);

    private static EvalReviewedFullChainPrediction Prediction(
        string frame,
        string predictedCode,
        int severity,
        TrafficLight? gate,
        string? error = null,
        int dinoBoxes = 1,
        bool samCalled = true,
        bool qwenCalled = true,
        bool incomplete = false)
        => new(
            frame,
            predictedCode,
            severity,
            TimeMs: 100,
            Error: error,
            DetectorBypassed: true,
            DinoCalled: true,
            DinoBoxCount: dinoBoxes,
            SamCalled: samCalled,
            SamMaskCount: samCalled ? 1 : 0,
            QwenVisionCalled: qwenCalled,
            QwenVisionFindingCount: qwenCalled ? 1 : 0,
            CodeMappingCalled: qwenCalled,
            CodeMappingCount: qwenCalled ? 1 : 0,
            QualityGate: gate,
            QualityGateComposite: gate is null ? null : 0.8,
            Degraded: true,
            Incomplete: incomplete,
            DropReason: error);
}
