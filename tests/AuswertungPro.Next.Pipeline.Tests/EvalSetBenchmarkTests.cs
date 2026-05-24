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
}
