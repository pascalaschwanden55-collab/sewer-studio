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
        Assert.True(File.Exists(cases[0].ImagePath));

        Assert.Equal("case_b_kein_schaden.png", cases[1].FrameFileName);
        Assert.Equal("LEER", cases[1].ExpectedFullCode);
        Assert.Equal("LEER", cases[1].ExpectedMainCode);
        Assert.Equal("negativ", cases[1].Category);
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
}
