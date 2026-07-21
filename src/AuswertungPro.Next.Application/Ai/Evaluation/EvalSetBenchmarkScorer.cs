using System.Globalization;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public static class EvalSetBenchmarkScorer
{
    public static IReadOnlyList<EvalSetBenchmarkRow> Evaluate(
        IReadOnlyList<EvalSetBenchmarkCase> cases,
        IReadOnlyList<EvalSetPrediction> predictions)
    {
        var byFrame = predictions.ToDictionary(
            p => p.FrameFileName,
            StringComparer.OrdinalIgnoreCase);

        return cases.Select(c =>
        {
            byFrame.TryGetValue(c.FrameFileName, out var prediction);
            var predicted = EvalSetBenchmarkDataset.NormalizeCode(prediction?.PredictedCode) ?? "";
            var nullResponse = string.IsNullOrWhiteSpace(predicted);
            var expectedIsNegative = IsNegative(c.ExpectedFullCode);
            var predictedIsNegative = nullResponse || IsNegative(predicted);

            var exact = !nullResponse &&
                        string.Equals(predicted, c.ExpectedFullCode, StringComparison.OrdinalIgnoreCase);
            var main = !nullResponse &&
                       string.Equals(predicted, c.ExpectedMainCode, StringComparison.OrdinalIgnoreCase);
            var group = !nullResponse &&
                        !expectedIsNegative &&
                        SameGroup(predicted, c.ExpectedFullCode);
            var negativeCorrect = expectedIsNegative && predictedIsNegative;

            return new EvalSetBenchmarkRow(
                FrameFileName: c.FrameFileName,
                ExpectedFullCode: c.ExpectedFullCode,
                ExpectedMainCode: c.ExpectedMainCode,
                Category: c.Category,
                PredictedCode: predicted,
                Exact: exact,
                Main: main,
                Group: group,
                NullResponse: nullResponse,
                NegativCorrect: negativeCorrect,
                TimeMs: prediction?.TimeMs ?? 0,
                Severity: prediction?.Severity ?? 0,
                Error: prediction?.Error);
        }).ToList();
    }

    public static EvalSetBenchmarkSummary Summarize(IReadOnlyList<EvalSetBenchmarkRow> rows)
    {
        var total = rows.Count;
        var negatives = rows.Count(r => IsNegative(r.ExpectedFullCode));

        return new EvalSetBenchmarkSummary(
            Total: total,
            ExactCorrect: rows.Count(r => r.Exact),
            MainCorrect: rows.Count(r => r.Main),
            GroupCorrect: rows.Count(r => r.Group || r.Exact || r.Main),
            NullResponses: rows.Count(r => r.NullResponse),
            NegativCorrect: rows.Count(r => r.NegativCorrect),
            ExactAccuracy: Ratio(rows.Count(r => r.Exact), total),
            MainAccuracy: Ratio(rows.Count(r => r.Main), total),
            GroupAccuracy: Ratio(rows.Count(r => r.Group || r.Exact || r.Main), total),
            NegativeAccuracy: Ratio(rows.Count(r => r.NegativCorrect), negatives),
            AverageTimeMs: total == 0 ? 0 : rows.Average(r => r.TimeMs));
    }

    public static IReadOnlyList<EvalSetCodeSummary> SummarizeByExpectedCode(
        IReadOnlyList<EvalSetBenchmarkRow> rows)
        => rows
            .GroupBy(r => r.ExpectedFullCode, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var total = g.Count();
                var top = g
                    .GroupBy(r => DisplayPrediction(r.PredictedCode), StringComparer.OrdinalIgnoreCase)
                    .Select(pg => new { Prediction = pg.Key, Count = pg.Count() })
                    .OrderByDescending(x => x.Count)
                    .ThenBy(x => x.Prediction, StringComparer.OrdinalIgnoreCase)
                    .First();

                return new EvalSetCodeSummary(
                    ExpectedCode: g.Key,
                    Total: total,
                    ExactCorrect: g.Count(r => r.Exact),
                    MainCorrect: g.Count(r => r.Main),
                    GroupCorrect: g.Count(r => r.Group || r.Exact || r.Main),
                    NullResponses: g.Count(r => r.NullResponse),
                    PredictedLeer: g.Count(r => string.Equals(r.PredictedCode, "LEER", StringComparison.OrdinalIgnoreCase)),
                    ExactAccuracy: Ratio(g.Count(r => r.Exact), total),
                    TopPrediction: top.Prediction,
                    TopPredictionCount: top.Count);
            })
            .OrderByDescending(s => s.Total)
            .ThenBy(s => s.ExpectedCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<EvalSetConfusionEntry> BuildConfusionMatrix(
        IReadOnlyList<EvalSetBenchmarkRow> rows)
        => rows
            .GroupBy(r => new
            {
                Expected = r.ExpectedFullCode,
                Predicted = DisplayPrediction(r.PredictedCode)
            })
            .Select(g => new EvalSetConfusionEntry(
                ExpectedCode: g.Key.Expected,
                PredictedCode: g.Key.Predicted,
                Count: g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.ExpectedCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.PredictedCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static void WriteCsv(string path, IReadOnlyList<EvalSetBenchmarkRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var sb = new StringBuilder();
        sb.AppendLine("frame,gt_full,gt_main,kategorie,pred,exact,main,group,null_resp,negativ_correct,time_ms,severity,error");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                Csv(r.FrameFileName),
                Csv(r.ExpectedFullCode),
                Csv(r.ExpectedMainCode),
                Csv(r.Category),
                Csv(r.PredictedCode),
                Bool(r.Exact),
                Bool(r.Main),
                Bool(r.Group),
                Bool(r.NullResponse),
                Bool(r.NegativCorrect),
                r.TimeMs.ToString(CultureInfo.InvariantCulture),
                r.Severity.ToString(CultureInfo.InvariantCulture),
                Csv(r.Error ?? "")));
        }
        AtomicTextFileWriter.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static void WriteSummaryJson(string path, EvalSetBenchmarkSummary summary, object metadata)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = JsonSerializer.Serialize(new { metadata, summary }, JsonDefaults.Indented);
        AtomicTextFileWriter.WriteAllText(path, json, new UTF8Encoding(false));
    }

    public static void WriteByCodeCsv(string path, IReadOnlyList<EvalSetCodeSummary> summaries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var sb = new StringBuilder();
        sb.AppendLine("expected,total,exact_correct,main_correct,group_correct,null_responses,predicted_leer,exact_accuracy,top_prediction,top_prediction_count");
        foreach (var s in summaries)
        {
            sb.AppendLine(string.Join(",",
                Csv(s.ExpectedCode),
                s.Total.ToString(CultureInfo.InvariantCulture),
                s.ExactCorrect.ToString(CultureInfo.InvariantCulture),
                s.MainCorrect.ToString(CultureInfo.InvariantCulture),
                s.GroupCorrect.ToString(CultureInfo.InvariantCulture),
                s.NullResponses.ToString(CultureInfo.InvariantCulture),
                s.PredictedLeer.ToString(CultureInfo.InvariantCulture),
                s.ExactAccuracy.ToString(CultureInfo.InvariantCulture),
                Csv(s.TopPrediction),
                s.TopPredictionCount.ToString(CultureInfo.InvariantCulture)));
        }
        AtomicTextFileWriter.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static void WriteConfusionCsv(string path, IReadOnlyList<EvalSetConfusionEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var sb = new StringBuilder();
        sb.AppendLine("expected,predicted,count");
        foreach (var c in entries)
        {
            sb.AppendLine(string.Join(",",
                Csv(c.ExpectedCode),
                Csv(c.PredictedCode),
                c.Count.ToString(CultureInfo.InvariantCulture)));
        }
        AtomicTextFileWriter.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static bool SameGroup(string predicted, string expected)
        => predicted.Length >= 3 &&
           expected.Length >= 3 &&
           string.Equals(predicted[..3], expected[..3], StringComparison.OrdinalIgnoreCase);

    private static bool IsNegative(string code)
        => string.Equals(code, "LEER", StringComparison.OrdinalIgnoreCase);

    private static double Ratio(int part, int total)
        => total == 0 ? 0 : (double)part / total;

    // Delegiert an gemeinsame Helferklasse EvalSetCsv
    private static string Bool(bool value) => EvalSetCsv.Bool(value);

    private static string DisplayPrediction(string? predicted)
        => string.IsNullOrWhiteSpace(predicted) ? "NULL" : predicted.Trim().ToUpperInvariant();

    // Delegiert an gemeinsame Helferklasse EvalSetCsv
    private static string Csv(string value) => EvalSetCsv.Csv(value);
}
