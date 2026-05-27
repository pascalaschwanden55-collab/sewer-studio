using System;
using System.IO;
using System.Text;
using AuswertungPro.Next.Application.Ai.Evaluation;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class EvalSetBenchmarkTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-eval-benchmark-" + Guid.NewGuid().ToString("N"));

    public EvalSetBenchmarkTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "images"));
        Directory.CreateDirectory(Path.Combine(_root, "labels"));
        File.WriteAllBytes(Path.Combine(_root, "images", "case_a_BDDC.png"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_root, "images", "case_b_kein_schaden.png"), [4, 5, 6]);
        File.WriteAllText(Path.Combine(_root, "labels", "case_a_BDDC.txt"), "0 0.1 0.1 0.2 0.2", Encoding.UTF8);
        File.WriteAllText(Path.Combine(_root, "labels", "case_b_kein_schaden.txt"), "", Encoding.UTF8);
        File.WriteAllText(Path.Combine(_root, "_candidates.json"), """
[
  {
    "id": "a",
    "frame_path": "C:\\KI_BRAIN\\training_frames\\case_a_BDDC.png",
    "haltung_key": "H1",
    "meter": 12.3,
    "code_main": "BDDC",
    "code_full": "BDDC",
    "kategorie": "top5_code",
    "status": "approved"
  },
  {
    "id": "b",
    "frame_path": "C:\\KI_BRAIN\\training_frames\\case_b_kein_schaden.png",
    "haltung_key": "H2",
    "meter": 0,
    "code_main": "leer",
    "code_full": "leer",
    "kategorie": "negativ",
    "status": "approved"
  }
]
""", Encoding.UTF8);
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
            // best effort cleanup
        }
    }

    [Fact]
    public void Dataset_loads_candidates_and_maps_images()
    {
        var cases = EvalSetBenchmarkDataset.Load(_root);

        Assert.Equal(2, cases.Count);
        Assert.Equal("case_a_BDDC.png", cases[0].FrameFileName);
        Assert.Equal("BDDC", cases[0].ExpectedFullCode);
        Assert.Equal("BDDC", cases[0].ExpectedMainCode);
        Assert.Equal("top5_code", cases[0].Category);
        Assert.Equal(12.3, cases[0].Meter);
        Assert.True(cases[0].HasYoloLabel);
        Assert.True(File.Exists(cases[0].ImagePath));

        Assert.Equal("case_b_kein_schaden.png", cases[1].FrameFileName);
        Assert.Equal("LEER", cases[1].ExpectedFullCode);
        Assert.Equal("LEER", cases[1].ExpectedMainCode);
        Assert.Equal("negativ", cases[1].Category);
        Assert.False(cases[1].HasYoloLabel);
    }

    [Fact]
    public void Scorer_computes_exact_main_group_and_negative_metrics()
    {
        var cases = EvalSetBenchmarkDataset.Load(_root);
        var rows = EvalSetBenchmarkScorer.Evaluate(
            cases,
            [
                new EvalSetPrediction("case_a_BDDC.png", "BDD", Severity: 3, TimeMs: 1200),
                new EvalSetPrediction("case_b_kein_schaden.png", "", Severity: 0, TimeMs: 800),
            ]);

        Assert.Equal(2, rows.Count);

        Assert.False(rows[0].Exact);
        Assert.False(rows[0].Main);
        Assert.True(rows[0].Group);
        Assert.False(rows[0].NullResponse);
        Assert.False(rows[0].NegativCorrect);

        Assert.False(rows[1].Exact);
        Assert.False(rows[1].Main);
        Assert.False(rows[1].Group);
        Assert.True(rows[1].NullResponse);
        Assert.True(rows[1].NegativCorrect);
    }

    [Fact]
    public void Summary_counts_accuracy_values()
    {
        var cases = EvalSetBenchmarkDataset.Load(_root);
        var rows = EvalSetBenchmarkScorer.Evaluate(
            cases,
            [
                new EvalSetPrediction("case_a_BDDC.png", "BDDC", Severity: 3, TimeMs: 1200),
                new EvalSetPrediction("case_b_kein_schaden.png", "LEER", Severity: 0, TimeMs: 800),
            ]);

        var summary = EvalSetBenchmarkScorer.Summarize(rows);

        Assert.Equal(2, summary.Total);
        Assert.Equal(2, summary.ExactCorrect);
        Assert.Equal(2, summary.MainCorrect);
        Assert.Equal(2, summary.GroupCorrect);
        Assert.Equal(1, summary.NegativCorrect);
        Assert.Equal(0, summary.NullResponses);
        Assert.Equal(1.0, summary.ExactAccuracy);
    }

    [Fact]
    public void SummarizeByExpectedCode_counts_top_prediction_and_nulls()
    {
        var rows = new[]
        {
            new EvalSetBenchmarkRow("a.png", "BDDC", "BDDC", "top5", "BDDC", true, true, true, false, false, 100, 3, null),
            new EvalSetBenchmarkRow("b.png", "BDDC", "BDDC", "top5", "LEER", false, false, false, false, false, 100, 0, null),
            new EvalSetBenchmarkRow("c.png", "BDDC", "BDDC", "top5", "LEER", false, false, false, false, false, 100, 0, null),
            new EvalSetBenchmarkRow("d.png", "BCE", "BCE", "top5", "", false, false, false, true, false, 100, 0, "timeout"),
        };

        var byCode = EvalSetBenchmarkScorer.SummarizeByExpectedCode(rows);

        Assert.Equal(2, byCode.Count);
        Assert.Equal("BDDC", byCode[0].ExpectedCode);
        Assert.Equal(3, byCode[0].Total);
        Assert.Equal(1, byCode[0].ExactCorrect);
        Assert.Equal(2, byCode[0].PredictedLeer);
        Assert.Equal(0, byCode[0].NullResponses);
        Assert.Equal("LEER", byCode[0].TopPrediction);
        Assert.Equal(2, byCode[0].TopPredictionCount);

        Assert.Equal("BCE", byCode[1].ExpectedCode);
        Assert.Equal(1, byCode[1].NullResponses);
        Assert.Equal("NULL", byCode[1].TopPrediction);
    }

    [Fact]
    public void BuildConfusionMatrix_groups_expected_and_predicted_codes()
    {
        var rows = new[]
        {
            new EvalSetBenchmarkRow("a.png", "BDDC", "BDDC", "top5", "BDDC", true, true, true, false, false, 100, 3, null),
            new EvalSetBenchmarkRow("b.png", "BDDC", "BDDC", "top5", "LEER", false, false, false, false, false, 100, 0, null),
            new EvalSetBenchmarkRow("c.png", "BDDC", "BDDC", "top5", "LEER", false, false, false, false, false, 100, 0, null),
        };

        var confusion = EvalSetBenchmarkScorer.BuildConfusionMatrix(rows);

        Assert.Equal(2, confusion.Count);
        Assert.Contains(confusion, c => c.ExpectedCode == "BDDC" && c.PredictedCode == "LEER" && c.Count == 2);
        Assert.Contains(confusion, c => c.ExpectedCode == "BDDC" && c.PredictedCode == "BDDC" && c.Count == 1);
    }

    [Fact]
    public void BuildOracleImportContext_returns_expected_code_for_positive_case()
    {
        var c = new EvalSetBenchmarkCase(
            Id: "x",
            FrameFileName: "frame.png",
            ImagePath: "frame.png",
            ExpectedFullCode: "BDDC",
            ExpectedMainCode: "BDDC",
            Category: "top5",
            Meter: 12.3);

        var context = EvalSetBenchmarkContext.BuildOracleImportContext(c);

        Assert.Single(context);
        Assert.Equal("BDDC", context[0].Code);
        Assert.Contains("Eval-Set", context[0].Description);
        Assert.Equal(12.3, context[0].Meter);
    }

    [Fact]
    public void BuildOracleImportContext_returns_empty_for_negative_case()
    {
        var c = new EvalSetBenchmarkCase(
            Id: "x",
            FrameFileName: "frame.png",
            ImagePath: "frame.png",
            ExpectedFullCode: "LEER",
            ExpectedMainCode: "LEER",
            Category: "negative",
            Meter: null);

        var context = EvalSetBenchmarkContext.BuildOracleImportContext(c);

        Assert.Empty(context);
    }

    [Fact]
    public void BuildClassifierImportContext_maps_manual_groups_to_vsa_candidates()
    {
        var predictions = new[]
        {
            new EvalSetCandidatePrediction("riss_bruch", 0.72),
            new EvalSetCandidatePrediction("ablagerung", 0.31),
            new EvalSetCandidatePrediction("leer", 0.95),
        };

        var context = EvalSetBenchmarkContext.BuildClassifierImportContext(
            predictions,
            meter: 8.4,
            minConfidence: 0.05,
            maxCandidates: 3);

        Assert.Equal(2, context.Count);
        Assert.Equal("BAB", context[0].Code);
        Assert.Equal("BBC", context[1].Code);
        Assert.All(context, c => Assert.Equal(8.4, c.Meter));
        Assert.All(context, c => Assert.Contains("YOLO", c.Description));
    }

    [Fact]
    public void BuildClassifierImportContext_accepts_direct_vsa_class_names()
    {
        var predictions = new[]
        {
            new EvalSetCandidatePrediction("BDDC", 0.61),
        };

        var context = EvalSetBenchmarkContext.BuildClassifierImportContext(predictions);

        Assert.Single(context);
        Assert.Equal("BDDC", context[0].Code);
    }

    [Fact]
    public void BuildClassifierImportContext_filters_confidence_duplicates_and_max_count()
    {
        var predictions = new[]
        {
            new EvalSetCandidatePrediction("riss_bruch", 0.80),
            new EvalSetCandidatePrediction("BAB", 0.70),
            new EvalSetCandidatePrediction("anschluss", 0.60),
            new EvalSetCandidatePrediction("infiltration", 0.01),
        };

        var context = EvalSetBenchmarkContext.BuildClassifierImportContext(
            predictions,
            minConfidence: 0.05,
            maxCandidates: 2);

        Assert.Equal(2, context.Count);
        Assert.Equal("BAB", context[0].Code);
        Assert.Equal("BCA", context[1].Code);
    }

    [Fact]
    public void BuildClassifierObservationHints_returns_non_binding_non_empty_hints()
    {
        var predictions = new[]
        {
            new EvalSetCandidatePrediction("leer", 0.95),
            new EvalSetCandidatePrediction("riss_bruch", 0.72),
            new EvalSetCandidatePrediction("ablagerung", 0.31),
            new EvalSetCandidatePrediction("anschluss", 0.01),
        };

        var hints = EvalSetBenchmarkContext.BuildClassifierObservationHints(
            predictions,
            minConfidence: 0.05,
            maxHints: 2);

        Assert.Equal(2, hints.Count);
        Assert.Contains("riss_bruch", hints[0]);
        Assert.Contains("ablagerung", hints[1]);
        Assert.DoesNotContain(hints, h => h.Contains("leer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnalyzeClassifierCoverage_marks_eval_codes_missing_when_classifier_has_no_matching_class()
    {
        var cases = new[]
        {
            new EvalSetBenchmarkCase("a", "a.png", "a.png", "LEER", "LEER", "negativ", null),
            new EvalSetBenchmarkCase("b", "b.png", "b.png", "BDDC", "BDDC", "top5", 1),
            new EvalSetBenchmarkCase("c", "c.png", "c.png", "BAIZ", "BAIZ", "top5", 2),
            new EvalSetBenchmarkCase("d", "d.png", "d.png", "BABBA", "BABBA", "top5", 3),
        };

        var coverage = EvalSetClassifierCoverageAnalyzer.Analyze(
            cases,
            ["leer", "dichtung", "riss_bruch"]);

        Assert.Equal(4, coverage.TotalEvalCases);
        Assert.Equal(3, coverage.CoveredEvalCases);
        Assert.Equal(1, coverage.MissingEvalCases);
        Assert.Equal(0.75, coverage.CoverageRatio);

        Assert.Contains(coverage.Codes, c => c.ExpectedCode == "LEER" && c.Covered && c.CoveredBy == "leer");
        Assert.Contains(coverage.Codes, c => c.ExpectedCode == "BAIZ" && c.Covered && c.CoveredBy == "dichtung");
        Assert.Contains(coverage.Codes, c => c.ExpectedCode == "BABBA" && c.Covered && c.CoveredBy == "riss_bruch");
        Assert.Contains(coverage.Codes, c => c.ExpectedCode == "BDDC" && !c.Covered);
    }

    [Fact]
    public void LoadClassifierClassesFromImageFolderDataset_reads_train_class_directories()
    {
        var datasetRoot = Path.Combine(_root, "classifier_dataset");
        Directory.CreateDirectory(Path.Combine(datasetRoot, "train", "riss_bruch"));
        Directory.CreateDirectory(Path.Combine(datasetRoot, "train", "leer"));
        Directory.CreateDirectory(Path.Combine(datasetRoot, "val", "darf_nicht_zaehlen"));

        var classes = EvalSetClassifierCoverageAnalyzer.LoadClassifierClassesFromImageFolderDataset(datasetRoot);

        Assert.Equal(["leer", "riss_bruch"], classes);
    }

    [Fact]
    public void BuildRouterPlan_groups_eval_codes_into_router_classes()
    {
        var cases = new[]
        {
            new EvalSetBenchmarkCase("a", "a.png", "a.png", "LEER", "LEER", "negativ", null),
            new EvalSetBenchmarkCase("b", "b.png", "b.png", "BCD", "BCD", "meta", 0),
            new EvalSetBenchmarkCase("c", "c.png", "c.png", "BCE", "BCE", "meta", 10),
            new EvalSetBenchmarkCase("d", "d.png", "d.png", "BDDC", "BDDC", "top5", 1),
            new EvalSetBenchmarkCase("e", "e.png", "e.png", "BAIZ", "BAIZ", "top5", 2),
            new EvalSetBenchmarkCase("f", "f.png", "f.png", "BABBA", "BABBA", "top5", 3),
            new EvalSetBenchmarkCase("g", "g.png", "g.png", "BCAEA", "BCAEA", "top5", 4),
            new EvalSetBenchmarkCase("h", "h.png", "h.png", "BBAA", "BBAA", "top5", 5),
            new EvalSetBenchmarkCase("i", "i.png", "i.png", "XYZ", "XYZ", "unknown", 6),
        };

        var plan = EvalSetRouterPlanner.BuildPlan(cases);

        Assert.Contains(plan, p => p.RouterClass == "leer" && p.Count == 1);
        Assert.Contains(plan, p => p.RouterClass == "beginn_ende" && p.Count == 2 && p.ExpectedCodes.Contains("BCD") && p.ExpectedCodes.Contains("BCE"));
        Assert.Contains(plan, p => p.RouterClass == "wasserstand" && p.Count == 1 && p.ExpectedCodes.Contains("BDDC"));
        Assert.Contains(plan, p => p.RouterClass == "dichtung" && p.Count == 1 && p.ExpectedCodes.Contains("BAIZ"));
        Assert.Contains(plan, p => p.RouterClass == "riss_bruch" && p.Count == 1 && p.ExpectedCodes.Contains("BABBA"));
        Assert.Contains(plan, p => p.RouterClass == "anschluss" && p.Count == 1 && p.ExpectedCodes.Contains("BCAEA"));
        Assert.Contains(plan, p => p.RouterClass == "wurzeln" && p.Count == 1 && p.ExpectedCodes.Contains("BBAA"));
        Assert.Contains(plan, p => p.RouterClass == "sonstiges" && p.Count == 1 && p.ExpectedCodes.Contains("XYZ"));
    }

    [Fact]
    public void YoloDetectBaselineScorer_counts_detection_presence_against_yolo_labels()
    {
        var cases = new[]
        {
            new EvalSetBenchmarkCase("a", "a.png", "a.png", "BABBA", "BABBA", "top5", 1, HasYoloLabel: true),
            new EvalSetBenchmarkCase("b", "b.png", "b.png", "LEER", "LEER", "negativ", null, HasYoloLabel: false),
            new EvalSetBenchmarkCase("c", "c.png", "c.png", "BBAA", "BBAA", "top5", 2, HasYoloLabel: true),
            new EvalSetBenchmarkCase("d", "d.png", "d.png", "LEER", "LEER", "negativ", null, HasYoloLabel: false),
        };
        var predictions = new[]
        {
            new YoloDetectBaselinePrediction(
                "a.png",
                IsRelevant: true,
                Detections: [new YoloDetectBaselineDetection("crack", 0.91), new YoloDetectBaselineDetection("roots", 0.72)],
                RoundtripMs: 120,
                InferenceTimeMs: 80,
                QueueWaitMs: 3,
                ModelName: "yolo26m.pt",
                Device: "cpu",
                VramAllocatedGb: 0,
                VramTotalGb: 31.5,
                FrameClass: "relevant"),
            new YoloDetectBaselinePrediction(
                "b.png",
                IsRelevant: false,
                Detections: [],
                RoundtripMs: 60,
                InferenceTimeMs: 0,
                QueueWaitMs: 0,
                ModelName: "yolo26m.pt",
                Device: "cpu",
                VramAllocatedGb: 0,
                VramTotalGb: 31.5,
                FrameClass: "too_uniform"),
            new YoloDetectBaselinePrediction(
                "c.png",
                IsRelevant: false,
                Detections: [],
                RoundtripMs: 70,
                InferenceTimeMs: 50,
                QueueWaitMs: 0,
                ModelName: "yolo26m.pt",
                Device: "cpu",
                VramAllocatedGb: 0,
                VramTotalGb: 31.5,
                FrameClass: "empty"),
            new YoloDetectBaselinePrediction(
                "d.png",
                IsRelevant: true,
                Detections: [new YoloDetectBaselineDetection("deposit", 0.62)],
                RoundtripMs: 90,
                InferenceTimeMs: 55,
                QueueWaitMs: 1,
                ModelName: "yolo26m.pt",
                Device: "cpu",
                VramAllocatedGb: 0,
                VramTotalGb: 31.5,
                FrameClass: "relevant"),
        };

        var rows = YoloDetectBaselineScorer.Evaluate(cases, predictions);
        var summary = YoloDetectBaselineScorer.Summarize(rows);

        Assert.Equal(4, summary.Total);
        Assert.Equal(2, summary.ExpectedPositiveFrames);
        Assert.Equal(2, summary.ExpectedNegativeFrames);
        Assert.Equal(2, summary.DetectedFrames);
        Assert.Equal(1, summary.TruePositiveFrames);
        Assert.Equal(1, summary.FalseNegativeFrames);
        Assert.Equal(1, summary.FalsePositiveFrames);
        Assert.Equal(1, summary.TrueNegativeFrames);
        Assert.Equal(0.5, summary.PositiveRecall);
        Assert.Equal(0.5, summary.FalsePositiveRate);
        Assert.Equal(3, summary.TotalDetections);
        Assert.Equal("crack", rows[0].TopClass);
        Assert.Equal(0.91, rows[0].TopConfidence);
        Assert.Equal(85, summary.AverageRoundtripMs);
        Assert.Equal(46.25, summary.AverageInferenceMs);
    }

    [Fact]
    public void YoloDetectBaselineScorer_marks_presence_metrics_as_health_not_quality()
    {
        var cases = new[]
        {
            new EvalSetBenchmarkCase("a", "a.png", "a.png", "BBAA", "BBAA", "top5", 1, HasYoloLabel: true),
            new EvalSetBenchmarkCase("b", "b.png", "b.png", "LEER", "LEER", "negativ", null, HasYoloLabel: false),
            new EvalSetBenchmarkCase("c", "c.png", "c.png", "BDCZC", "BDCZC", "top5", 2, HasYoloLabel: false),
        };
        var predictions = new[]
        {
            Prediction("a.png", [new YoloDetectBaselineDetection("roots", 0.91)]),
            Prediction("b.png", []),
            Prediction("c.png", [new YoloDetectBaselineDetection("deposit", 0.74)]),
        };

        var rows = YoloDetectBaselineScorer.Evaluate(cases, predictions, confidenceThreshold: 0.5);
        var summary = YoloDetectBaselineScorer.Summarize(rows);

        Assert.Equal("presence_health", summary.MetricKind);
        Assert.False(summary.IsQualityProof);
        Assert.Equal(1, summary.TruePositiveFrames);
        Assert.Equal(1, summary.FalsePositiveFrames);
        Assert.Equal(0.5, summary.Precision);
        Assert.Equal(1d / 3d, summary.FalsePositivesPerFrame);
        Assert.Equal(1, summary.NoDamageNegativeFrames);
        Assert.Equal(1, summary.UnlabeledVisibleOrOtherCodeFrames);
        Assert.Equal(YoloDetectNegativeKind.PositiveLabel, rows[0].NegativeKind);
        Assert.Equal(YoloDetectNegativeKind.NoDamage, rows[1].NegativeKind);
        Assert.Equal(YoloDetectNegativeKind.UnlabeledVisibleOrOtherCode, rows[2].NegativeKind);
    }

    [Fact]
    public void YoloDetectBaselineScorer_sweeps_fixed_confidence_thresholds()
    {
        var cases = new[]
        {
            new EvalSetBenchmarkCase("a", "a.png", "a.png", "BBAA", "BBAA", "top5", 1, HasYoloLabel: true),
            new EvalSetBenchmarkCase("b", "b.png", "b.png", "LEER", "LEER", "negativ", null, HasYoloLabel: false),
        };
        var predictions = new[]
        {
            Prediction("a.png", [new YoloDetectBaselineDetection("roots", 0.60)]),
            Prediction("b.png", [new YoloDetectBaselineDetection("roots", 0.80)]),
        };

        var sweep = YoloDetectBaselineScorer.SweepThresholds(cases, predictions, [0.25, 0.7, 0.9]);

        Assert.Equal(new[] { 0.25, 0.7, 0.9 }, sweep.Select(s => s.ConfidenceThreshold).ToArray());
        Assert.Equal(1, sweep[0].Summary.TruePositiveFrames);
        Assert.Equal(1, sweep[0].Summary.FalsePositiveFrames);
        Assert.Equal(0.5, sweep[0].Summary.Precision);
        Assert.Equal(0, sweep[1].Summary.TruePositiveFrames);
        Assert.Equal(1, sweep[1].Summary.FalsePositiveFrames);
        Assert.Equal(0, sweep[2].Summary.FalsePositiveFrames);
        Assert.Equal(1, sweep[2].Summary.TrueNegativeFrames);
    }

    [Fact]
    public void YoloDetectBaselineScorer_groups_false_positive_detections_by_class_and_confidence()
    {
        var cases = new[]
        {
            new EvalSetBenchmarkCase("a", "a.png", "a.png", "LEER", "LEER", "negativ", null, HasYoloLabel: false),
            new EvalSetBenchmarkCase("b", "b.png", "b.png", "BDCZC", "BDCZC", "top5", 2, HasYoloLabel: false),
        };
        var predictions = new[]
        {
            Prediction("a.png", [new YoloDetectBaselineDetection("roots", 0.92), new YoloDetectBaselineDetection("roots", 0.88)]),
            Prediction("b.png", [new YoloDetectBaselineDetection("crack", 0.52)]),
        };

        var rows = YoloDetectBaselineScorer.Evaluate(cases, predictions, confidenceThreshold: 0.25);
        var summary = YoloDetectBaselineScorer.Summarize(rows);

        Assert.Contains(summary.FalsePositiveBuckets, b =>
            b.ClassName == "roots" &&
            b.ConfidenceBucket == ">=0.90" &&
            b.Count == 1 &&
            b.MaxConfidence == 0.92);
        Assert.Contains(summary.FalsePositiveBuckets, b =>
            b.ClassName == "roots" &&
            b.ConfidenceBucket == "0.85-0.89" &&
            b.Count == 1);
        Assert.Contains(summary.FalsePositiveBuckets, b =>
            b.ClassName == "crack" &&
            b.ConfidenceBucket == "0.50-0.69" &&
            b.Count == 1);
    }

    private static YoloDetectBaselinePrediction Prediction(
        string frameFileName,
        IReadOnlyList<YoloDetectBaselineDetection> detections)
        => new(
            frameFileName,
            IsRelevant: true,
            Detections: detections,
            RoundtripMs: 100,
            InferenceTimeMs: 80,
            QueueWaitMs: 2,
            ModelName: "yolo26m.pt",
            Device: "cpu",
            VramAllocatedGb: 0,
            VramTotalGb: 31.5,
            FrameClass: "relevant");

    [Fact]
    public void RouterDatasetBuilder_copies_router_classes_and_skips_eval_set_images()
    {
        var sourceRoot = Path.Combine(_root, "router_source");
        var outputRoot = Path.Combine(_root, "router_output");

        WriteBytes(Path.Combine(sourceRoot, "train", "riss_bruch", "riss.png"), [10, 11, 12]);
        WriteBytes(Path.Combine(sourceRoot, "val", "BCE", "rohrende.jpg"), [20, 21, 22]);
        WriteBytes(Path.Combine(sourceRoot, "train", "BDDC", "eval_copy.png"), [4, 5, 6]);
        WriteBytes(Path.Combine(sourceRoot, "train", "irgendwas", "unknown.png"), [30, 31, 32]);

        var result = RouterDatasetBuilder.Build(new RouterDatasetBuilderOptions(
            SourceDatasetRoots: [sourceRoot],
            OutputRoot: outputRoot,
            EvalSetRoot: _root,
            DryRun: false));

        Assert.Equal(2, result.Copied);
        Assert.Equal(1, result.SkippedEvalSet);
        Assert.Equal(1, result.SkippedUnknownClass);

        Assert.True(File.Exists(Path.Combine(outputRoot, "train", "riss_bruch", "riss.png")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "val", "beginn_ende", "rohrende.jpg")));
        Assert.False(File.Exists(Path.Combine(outputRoot, "train", "wasserstand", "eval_copy.png")));
        Assert.False(File.Exists(Path.Combine(outputRoot, "train", "sonstiges", "unknown.png")));
    }

    [Fact]
    public void RouterDatasetBuilder_reads_source_file_list_and_maps_codes_from_file_names()
    {
        var listRoot = Path.Combine(_root, "list_source");
        var listPath = Path.Combine(_root, "router_file_list.txt");
        var outputRoot = Path.Combine(_root, "router_file_list_output");

        var bcd = Path.Combine(listRoot, "haltung_BCD_0.00_a.png");
        var bba = Path.Combine(listRoot, "haltung_BBAA_1.20_b.jpg");
        var evalCopy = Path.Combine(listRoot, "haltung_BDDC_1.20_eval.png");
        var unknown = Path.Combine(listRoot, "haltung_ABC_1.20_unknown.png");

        WriteBytes(bcd, [40, 41, 42]);
        WriteBytes(bba, [50, 51, 52]);
        WriteBytes(evalCopy, [4, 5, 6]);
        WriteBytes(unknown, [60, 61, 62]);

        File.WriteAllLines(listPath,
        [
            "FullName",
            "--------",
            bcd,
            bba,
            evalCopy,
            unknown,
            Path.Combine(listRoot, "missing_BCD.png"),
        ]);

        var result = RouterDatasetBuilder.Build(new RouterDatasetBuilderOptions(
            SourceDatasetRoots: [],
            OutputRoot: outputRoot,
            EvalSetRoot: _root,
            DryRun: false,
            SourceFileLists: [listPath]));

        Assert.Equal(2, result.Copied);
        Assert.Equal(1, result.SkippedEvalSet);
        Assert.Equal(1, result.SkippedUnknownClass);
        Assert.Equal(1, result.SkippedMissingFiles);

        Assert.True(File.Exists(Path.Combine(outputRoot, "train", "beginn_ende", "haltung_BCD_0.00_a.png")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "train", "wurzeln", "haltung_BBAA_1.20_b.jpg")));
        Assert.False(File.Exists(Path.Combine(outputRoot, "train", "wasserstand", "haltung_BDDC_1.20_eval.png")));
    }

    [Fact]
    public void RouterDatasetBuilder_can_put_file_list_entries_into_val_and_cap_per_class()
    {
        var listRoot = Path.Combine(_root, "list_source_cap");
        var listPath = Path.Combine(_root, "router_file_list_cap.txt");
        var outputRoot = Path.Combine(_root, "router_file_list_cap_output");
        var paths = new[]
        {
            Path.Combine(listRoot, "haltung_BCD_0.00_a.png"),
            Path.Combine(listRoot, "haltung_BCD_0.00_b.png"),
            Path.Combine(listRoot, "haltung_BCD_0.00_c.png"),
        };

        foreach (var p in paths)
            WriteBytes(p, [70, 71, 72, (byte)p[^5]]);
        File.WriteAllLines(listPath, paths);

        var result = RouterDatasetBuilder.Build(new RouterDatasetBuilderOptions(
            SourceDatasetRoots: [],
            OutputRoot: outputRoot,
            EvalSetRoot: _root,
            DryRun: false,
            SourceFileLists: [listPath],
            SourceFileListValidationRatio: 1.0,
            MaxPerClassPerSplit: 2));

        Assert.Equal(2, result.Copied);
        Assert.Equal(1, result.SkippedClassCap);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(outputRoot, "val", "beginn_ende")).Length);
        Assert.False(Directory.Exists(Path.Combine(outputRoot, "train", "beginn_ende")));
    }

    private static void WriteBytes(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }
}
