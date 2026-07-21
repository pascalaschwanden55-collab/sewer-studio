using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Evaluation;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class EvalSetBenchmarkWriterTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-eval-writers-" + Guid.NewGuid().ToString("N"));

    public EvalSetBenchmarkWriterTests()
        => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void BenchmarkWriters_preserve_headers_json_shape_and_csv_escaping()
    {
        var row = new EvalSetBenchmarkRow(
            FrameFileName: "frame,\"one\".png",
            ExpectedFullCode: "BAB,\"full\"",
            ExpectedMainCode: "BAB",
            Category: "damage,\"critical\"",
            PredictedCode: "BAB,\"pred\"",
            Exact: false,
            Main: false,
            Group: true,
            NullResponse: false,
            NegativCorrect: false,
            TimeMs: 42,
            Severity: 4,
            Error: "error,\"detail\"");
        var summary = EvalSetBenchmarkScorer.Summarize([row]);
        var byCode = new EvalSetCodeSummary(
            row.ExpectedFullCode,
            1,
            0,
            0,
            1,
            0,
            0,
            0,
            row.PredictedCode,
            1);
        var confusion = new EvalSetConfusionEntry(row.ExpectedFullCode, row.PredictedCode, 1);

        var rowsPath = Path.Combine(_root, "benchmark.csv");
        var byCodePath = Path.Combine(_root, "by-code.csv");
        var confusionPath = Path.Combine(_root, "confusion.csv");
        var summaryPath = Path.Combine(_root, "summary.json");
        EvalSetBenchmarkScorer.WriteCsv(rowsPath, [row]);
        EvalSetBenchmarkScorer.WriteByCodeCsv(byCodePath, [byCode]);
        EvalSetBenchmarkScorer.WriteConfusionCsv(confusionPath, [confusion]);
        EvalSetBenchmarkScorer.WriteSummaryJson(summaryPath, summary, new { run = "writer-test" });

        Assert.Equal(
            "frame,gt_full,gt_main,kategorie,pred,exact,main,group,null_resp,negativ_correct,time_ms,severity,error",
            File.ReadLines(rowsPath).First());
        Assert.Equal(
            "expected,total,exact_correct,main_correct,group_correct,null_responses,predicted_leer,exact_accuracy,top_prediction,top_prediction_count",
            File.ReadLines(byCodePath).First());
        Assert.Equal("expected,predicted,count", File.ReadLines(confusionPath).First());
        Assert.Contains("\"frame,\"\"one\"\".png\"", File.ReadAllText(rowsPath));
        Assert.Contains("\"BAB,\"\"full\"\"\"", File.ReadAllText(byCodePath));
        Assert.Contains("\"BAB,\"\"pred\"\"\"", File.ReadAllText(confusionPath));

        using var json = JsonDocument.Parse(File.ReadAllText(summaryPath));
        Assert.Equal("writer-test", json.RootElement.GetProperty("metadata").GetProperty("run").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("summary").GetProperty("Total").GetInt32());
    }

    [Fact]
    public void YoloWriters_preserve_headers_json_shape_and_csv_escaping()
    {
        var frameName = "frame,\"one\".png";
        var cases = new[]
        {
            new EvalSetBenchmarkCase(
                "case-1",
                frameName,
                frameName,
                "LEER",
                "LEER",
                "negative",
                null)
        };
        var predictions = new[]
        {
            new YoloDetectBaselinePrediction(
                frameName,
                IsRelevant: true,
                Detections: [new YoloDetectBaselineDetection("roots,\"fine\"", 0.91)],
                RoundtripMs: 42,
                InferenceTimeMs: 21,
                QueueWaitMs: 2,
                ModelName: "model,\"x\"",
                Device: "cuda",
                VramAllocatedGb: 2,
                VramTotalGb: 32,
                FrameClass: "relevant")
        };
        var rows = YoloDetectBaselineScorer.Evaluate(cases, predictions, confidenceThreshold: 0.5);
        var summary = YoloDetectBaselineScorer.Summarize(rows);
        var sweep = new[] { new YoloDetectThresholdSummary(0.5, summary) };

        var rowsPath = Path.Combine(_root, "yolo.csv");
        var sweepPath = Path.Combine(_root, "yolo-sweep.csv");
        var summaryPath = Path.Combine(_root, "yolo-summary.json");
        YoloDetectBaselineScorer.WriteCsv(rowsPath, rows);
        YoloDetectBaselineScorer.WriteSweepCsv(sweepPath, sweep);
        YoloDetectBaselineScorer.WriteSummaryJson(
            summaryPath,
            summary,
            new { run = "writer-test" },
            sweep);

        Assert.Equal(
            "frame,expected_code,expected_has_label,negative_kind,detected,detection_count,top_class,top_confidence,roundtrip_ms,inference_ms,queue_wait_ms,model_name,model_backend,device,vram_allocated_gb,vram_total_gb,gpu_utilization_percent,frame_class,error",
            File.ReadLines(rowsPath).First());
        Assert.Equal(
            "threshold,total,expected_positive,expected_negative,no_damage_negative,unlabeled_visible_or_other_code,detected_frames,true_positive,false_negative,false_positive,true_negative,total_detections,recall,precision,fp_per_frame,fp_rate,avg_roundtrip_ms,p50_roundtrip_ms,p95_roundtrip_ms,avg_inference_ms,p50_inference_ms,p95_inference_ms,avg_queue_wait_ms,max_vram_allocated_gb,max_vram_total_gb,max_gpu_utilization_percent",
            File.ReadLines(sweepPath).First());
        Assert.Contains("\"roots,\"\"fine\"\"\"", File.ReadAllText(rowsPath));
        Assert.Contains("\"model,\"\"x\"\"\"", File.ReadAllText(rowsPath));

        using var json = JsonDocument.Parse(File.ReadAllText(summaryPath));
        Assert.Equal("writer-test", json.RootElement.GetProperty("metadata").GetProperty("run").GetString());
        Assert.Equal("presence_health", json.RootElement.GetProperty("summary").GetProperty("MetricKind").GetString());
        Assert.Single(json.RootElement.GetProperty("threshold_sweep").EnumerateArray());
    }
}
