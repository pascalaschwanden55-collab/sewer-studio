using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public sealed record EvalSetBenchmarkCase(
    string Id,
    string FrameFileName,
    string ImagePath,
    string ExpectedFullCode,
    string ExpectedMainCode,
    string Category,
    double? Meter,
    bool HasYoloLabel = false);

public sealed record EvalSetPrediction(
    string FrameFileName,
    string? PredictedCode,
    int Severity,
    long TimeMs,
    string? Error = null);

public sealed record EvalSetCandidatePrediction(
    string ClassName,
    double Confidence);

public sealed record EvalSetBenchmarkRow(
    string FrameFileName,
    string ExpectedFullCode,
    string ExpectedMainCode,
    string Category,
    string PredictedCode,
    bool Exact,
    bool Main,
    bool Group,
    bool NullResponse,
    bool NegativCorrect,
    long TimeMs,
    int Severity,
    string? Error);

public sealed record EvalSetBenchmarkSummary(
    int Total,
    int ExactCorrect,
    int MainCorrect,
    int GroupCorrect,
    int NullResponses,
    int NegativCorrect,
    double ExactAccuracy,
    double MainAccuracy,
    double GroupAccuracy,
    double NegativeAccuracy,
    double AverageTimeMs);

public sealed record EvalSetCodeSummary(
    string ExpectedCode,
    int Total,
    int ExactCorrect,
    int MainCorrect,
    int GroupCorrect,
    int NullResponses,
    int PredictedLeer,
    double ExactAccuracy,
    string TopPrediction,
    int TopPredictionCount);

public sealed record EvalSetConfusionEntry(
    string ExpectedCode,
    string PredictedCode,
    int Count);

public sealed record EvalSetClassifierCoverageSummary(
    int TotalEvalCases,
    int CoveredEvalCases,
    int MissingEvalCases,
    double CoverageRatio,
    IReadOnlyList<EvalSetClassifierCoverageCode> Codes);

public sealed record EvalSetClassifierCoverageCode(
    string ExpectedCode,
    int Count,
    bool Covered,
    string? CoveredBy);

public sealed record EvalSetRouterClassSummary(
    string RouterClass,
    int Count,
    IReadOnlyList<string> ExpectedCodes);

public sealed record YoloDetectBaselineDetection(
    string ClassName,
    double Confidence);

public sealed record YoloDetectBaselinePrediction(
    string FrameFileName,
    bool IsRelevant,
    IReadOnlyList<YoloDetectBaselineDetection> Detections,
    long RoundtripMs,
    double InferenceTimeMs,
    double QueueWaitMs,
    string? ModelName,
    string? Device,
    double? VramAllocatedGb,
    double? VramTotalGb,
    string? FrameClass,
    string? Error = null);

public sealed record YoloDetectBaselineRow(
    string FrameFileName,
    string ExpectedFullCode,
    bool ExpectedHasLabel,
    bool Detected,
    int DetectionCount,
    string TopClass,
    double TopConfidence,
    long RoundtripMs,
    double InferenceTimeMs,
    double QueueWaitMs,
    string? ModelName,
    string? Device,
    double? VramAllocatedGb,
    double? VramTotalGb,
    string? FrameClass,
    string? Error);

public sealed record YoloDetectBaselineSummary(
    int Total,
    int ExpectedPositiveFrames,
    int ExpectedNegativeFrames,
    int DetectedFrames,
    int TruePositiveFrames,
    int FalseNegativeFrames,
    int FalsePositiveFrames,
    int TrueNegativeFrames,
    int TotalDetections,
    double PositiveRecall,
    double FalsePositiveRate,
    double AverageRoundtripMs,
    double AverageInferenceMs,
    double AverageQueueWaitMs);

public static class YoloDetectBaselineScorer
{
    public static IReadOnlyList<YoloDetectBaselineRow> Evaluate(
        IReadOnlyList<EvalSetBenchmarkCase> cases,
        IReadOnlyList<YoloDetectBaselinePrediction> predictions)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(predictions);

        var byFrame = predictions.ToDictionary(p => p.FrameFileName, StringComparer.OrdinalIgnoreCase);

        return cases.Select(c =>
        {
            byFrame.TryGetValue(c.FrameFileName, out var prediction);
            var detections = prediction?.Detections ?? Array.Empty<YoloDetectBaselineDetection>();
            var top = detections
                .OrderByDescending(d => d.Confidence)
                .FirstOrDefault();
            var detected = detections.Count > 0;

            return new YoloDetectBaselineRow(
                FrameFileName: c.FrameFileName,
                ExpectedFullCode: c.ExpectedFullCode,
                ExpectedHasLabel: c.HasYoloLabel,
                Detected: detected,
                DetectionCount: detections.Count,
                TopClass: top?.ClassName ?? "",
                TopConfidence: top?.Confidence ?? 0,
                RoundtripMs: prediction?.RoundtripMs ?? 0,
                InferenceTimeMs: prediction?.InferenceTimeMs ?? 0,
                QueueWaitMs: prediction?.QueueWaitMs ?? 0,
                ModelName: prediction?.ModelName,
                Device: prediction?.Device,
                VramAllocatedGb: prediction?.VramAllocatedGb,
                VramTotalGb: prediction?.VramTotalGb,
                FrameClass: prediction?.FrameClass,
                Error: prediction?.Error);
        }).ToList();
    }

    public static YoloDetectBaselineSummary Summarize(IReadOnlyList<YoloDetectBaselineRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var total = rows.Count;
        var expectedPositive = rows.Count(r => r.ExpectedHasLabel);
        var expectedNegative = total - expectedPositive;
        var detected = rows.Count(r => r.Detected);
        var truePositive = rows.Count(r => r.ExpectedHasLabel && r.Detected);
        var falseNegative = rows.Count(r => r.ExpectedHasLabel && !r.Detected);
        var falsePositive = rows.Count(r => !r.ExpectedHasLabel && r.Detected);
        var trueNegative = rows.Count(r => !r.ExpectedHasLabel && !r.Detected);

        return new YoloDetectBaselineSummary(
            Total: total,
            ExpectedPositiveFrames: expectedPositive,
            ExpectedNegativeFrames: expectedNegative,
            DetectedFrames: detected,
            TruePositiveFrames: truePositive,
            FalseNegativeFrames: falseNegative,
            FalsePositiveFrames: falsePositive,
            TrueNegativeFrames: trueNegative,
            TotalDetections: rows.Sum(r => r.DetectionCount),
            PositiveRecall: Ratio(truePositive, expectedPositive),
            FalsePositiveRate: Ratio(falsePositive, expectedNegative),
            AverageRoundtripMs: Average(rows, r => r.RoundtripMs),
            AverageInferenceMs: Average(rows, r => r.InferenceTimeMs),
            AverageQueueWaitMs: Average(rows, r => r.QueueWaitMs));
    }

    public static void WriteCsv(string path, IReadOnlyList<YoloDetectBaselineRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("frame,expected_code,expected_has_label,detected,detection_count,top_class,top_confidence,roundtrip_ms,inference_ms,queue_wait_ms,model_name,device,vram_allocated_gb,vram_total_gb,frame_class,error");
        foreach (var r in rows)
        {
            writer.WriteLine(string.Join(",",
                Csv(r.FrameFileName),
                Csv(r.ExpectedFullCode),
                Bool(r.ExpectedHasLabel),
                Bool(r.Detected),
                r.DetectionCount.ToString(CultureInfo.InvariantCulture),
                Csv(r.TopClass),
                r.TopConfidence.ToString(CultureInfo.InvariantCulture),
                r.RoundtripMs.ToString(CultureInfo.InvariantCulture),
                r.InferenceTimeMs.ToString(CultureInfo.InvariantCulture),
                r.QueueWaitMs.ToString(CultureInfo.InvariantCulture),
                Csv(r.ModelName ?? ""),
                Csv(r.Device ?? ""),
                NullableDouble(r.VramAllocatedGb),
                NullableDouble(r.VramTotalGb),
                Csv(r.FrameClass ?? ""),
                Csv(r.Error ?? "")));
        }
    }

    public static void WriteSummaryJson(string path, YoloDetectBaselineSummary summary, object metadata)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = JsonSerializer.Serialize(new { metadata, summary }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    private static double Ratio(int part, int total)
        => total == 0 ? 0 : (double)part / total;

    private static double Average<T>(IReadOnlyList<T> rows, Func<T, double> selector)
        => rows.Count == 0 ? 0 : rows.Average(selector);

    private static double Average<T>(IReadOnlyList<T> rows, Func<T, long> selector)
        => rows.Count == 0 ? 0 : rows.Average(selector);

    private static string Bool(bool value) => value ? "True" : "False";

    private static string NullableDouble(double? value)
        => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "";

    private static string Csv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

internal static class EvalSetClassifierClassMapper
{
    private static readonly IReadOnlyDictionary<string, string> ClassToVsaCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["oberflaeche"] = "BAJ",
            ["versatz"] = "BAH",
            ["riss_bruch"] = "BAB",
            ["rissbruch"] = "BAB",
            ["bruch"] = "BAC",
            ["ablagerung"] = "BBC",
            ["anschluss"] = "BCA",
            ["infiltration"] = "BBF",
            ["deformation"] = "BAA",
            ["dichtung"] = "BAI",
        };

    private static readonly HashSet<string> NegativeClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "leer",
        "empty",
        "negative",
        "no_damage",
        "no_schaden",
        "meta",
        "start",
        "ende",
    };

    public static bool IsNegativeClass(string? className)
        => !string.IsNullOrWhiteSpace(className) &&
           NegativeClasses.Contains(NormalizeClassKey(className));

    public static string? TryMapToVsaCode(string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return null;

        var raw = className.Trim();
        var directCode = NormalizeDirectVsaCode(raw);
        if (directCode is not null)
            return directCode;

        var key = NormalizeClassKey(raw);
        if (NegativeClasses.Contains(key))
            return null;

        return ClassToVsaCode.TryGetValue(key, out var code)
            ? code
            : null;
    }

    public static string? TryMapToCoverageCode(string? className)
        => IsNegativeClass(className)
            ? "LEER"
            : TryMapToVsaCode(className);

    private static string NormalizeClassKey(string className)
        => className
            .Replace('-', '_')
            .Replace(' ', '_')
            .Trim()
            .ToLowerInvariant();

    private static string? NormalizeDirectVsaCode(string value)
    {
        var compact = new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (compact.Length < 3 || compact.Length > 6 || compact[0] != 'B')
            return null;

        return compact.All(char.IsLetterOrDigit)
            ? compact
            : null;
    }
}

public static class EvalSetRouterPlanner
{
    public static IReadOnlyList<EvalSetRouterClassSummary> BuildPlan(
        IReadOnlyList<EvalSetBenchmarkCase> cases)
    {
        ArgumentNullException.ThrowIfNull(cases);

        return cases
            .GroupBy(c => MapExpectedCodeToRouterClass(c.ExpectedFullCode), StringComparer.OrdinalIgnoreCase)
            .Select(g => new EvalSetRouterClassSummary(
                RouterClass: g.Key,
                Count: g.Count(),
                ExpectedCodes: g
                    .Select(c => EvalSetBenchmarkDataset.NormalizeCode(c.ExpectedFullCode) ?? "")
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderByDescending(s => s.Count)
            .ThenBy(s => s.RouterClass, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string MapExpectedCodeToRouterClass(string? expectedCode)
    {
        var code = EvalSetBenchmarkDataset.NormalizeCode(expectedCode);
        if (string.IsNullOrWhiteSpace(code))
            return "sonstiges";

        if (string.Equals(code, "LEER", StringComparison.OrdinalIgnoreCase))
            return "leer";

        if (string.Equals(code, "BCD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(code, "BCE", StringComparison.OrdinalIgnoreCase))
        {
            return "beginn_ende";
        }

        if (code.StartsWith("BD", StringComparison.OrdinalIgnoreCase))
            return "wasserstand";

        if (code.StartsWith("BCA", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("BCC", StringComparison.OrdinalIgnoreCase))
        {
            return "anschluss";
        }

        if (code.StartsWith("BAI", StringComparison.OrdinalIgnoreCase))
            return "dichtung";

        if (code.StartsWith("BAB", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("BAC", StringComparison.OrdinalIgnoreCase))
        {
            return "riss_bruch";
        }

        if (code.StartsWith("BAH", StringComparison.OrdinalIgnoreCase))
            return "versatz";

        if (code.StartsWith("BAA", StringComparison.OrdinalIgnoreCase))
            return "deformation";

        if (code.StartsWith("BAF", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("BAJ", StringComparison.OrdinalIgnoreCase))
        {
            return "oberflaeche";
        }

        if (code.StartsWith("BBA", StringComparison.OrdinalIgnoreCase))
            return "wurzeln";

        if (code.StartsWith("BBB", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("BBC", StringComparison.OrdinalIgnoreCase))
        {
            return "ablagerung";
        }

        if (code.StartsWith("BBF", StringComparison.OrdinalIgnoreCase))
            return "infiltration";

        return "sonstiges";
    }
}

public static class EvalSetBenchmarkDataset
{
    public static IReadOnlyList<EvalSetBenchmarkCase> Load(string evalSetRoot)
    {
        if (string.IsNullOrWhiteSpace(evalSetRoot))
            throw new ArgumentException("Eval-Set-Pfad fehlt.", nameof(evalSetRoot));
        if (!Directory.Exists(evalSetRoot))
            throw new DirectoryNotFoundException(evalSetRoot);

        var candidatesPath = Path.Combine(evalSetRoot, "_candidates.json");
        if (!File.Exists(candidatesPath))
            throw new FileNotFoundException("Eval-Set-Kandidaten nicht gefunden.", candidatesPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(candidatesPath, Encoding.UTF8));
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("_candidates.json muss ein JSON-Array sein.");

        var result = new List<EvalSetBenchmarkCase>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var framePath = GetString(item, "frame_path");
            if (string.IsNullOrWhiteSpace(framePath))
                continue;

            var frameFileName = Path.GetFileName(framePath);
            var imagePath = Path.Combine(evalSetRoot, "images", frameFileName);
            if (!File.Exists(imagePath))
                continue;

            var expectedFull = NormalizeCode(GetString(item, "korrektur"))
                ?? NormalizeCode(GetString(item, "code_full"))
                ?? "";
            var expectedMain = NormalizeCode(GetString(item, "code_main"))
                ?? expectedFull;

            result.Add(new EvalSetBenchmarkCase(
                Id: GetString(item, "id") ?? Path.GetFileNameWithoutExtension(frameFileName),
                FrameFileName: frameFileName,
                ImagePath: imagePath,
                ExpectedFullCode: expectedFull,
                ExpectedMainCode: expectedMain,
                Category: GetString(item, "kategorie") ?? "",
                Meter: GetDouble(item, "meter"),
                HasYoloLabel: HasNonEmptyYoloLabel(evalSetRoot, frameFileName)));
        }

        return result
            .OrderBy(c => c.FrameFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? GetString(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? GetDouble(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d)
            ? d
            : null;

    private static bool HasNonEmptyYoloLabel(string evalSetRoot, string frameFileName)
    {
        var labelFile = Path.ChangeExtension(frameFileName, ".txt");
        var labelPath = Path.Combine(evalSetRoot, "labels", labelFile);
        return File.Exists(labelPath) && !string.IsNullOrWhiteSpace(File.ReadAllText(labelPath));
    }

    internal static string? NormalizeCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        if (trimmed.Equals("leer", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("kein", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("kein_schaden", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("no_damage", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return "LEER";
        }

        var chars = trimmed
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray();

        return chars.Length == 0 ? null : new string(chars);
    }
}

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
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("frame,gt_full,gt_main,kategorie,pred,exact,main,group,null_resp,negativ_correct,time_ms,severity,error");
        foreach (var r in rows)
        {
            writer.WriteLine(string.Join(",",
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
    }

    public static void WriteSummaryJson(string path, EvalSetBenchmarkSummary summary, object metadata)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = JsonSerializer.Serialize(new { metadata, summary }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    public static void WriteByCodeCsv(string path, IReadOnlyList<EvalSetCodeSummary> summaries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("expected,total,exact_correct,main_correct,group_correct,null_responses,predicted_leer,exact_accuracy,top_prediction,top_prediction_count");
        foreach (var s in summaries)
        {
            writer.WriteLine(string.Join(",",
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
    }

    public static void WriteConfusionCsv(string path, IReadOnlyList<EvalSetConfusionEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("expected,predicted,count");
        foreach (var c in entries)
        {
            writer.WriteLine(string.Join(",",
                Csv(c.ExpectedCode),
                Csv(c.PredictedCode),
                c.Count.ToString(CultureInfo.InvariantCulture)));
        }
    }

    private static bool SameGroup(string predicted, string expected)
        => predicted.Length >= 3 &&
           expected.Length >= 3 &&
           string.Equals(predicted[..3], expected[..3], StringComparison.OrdinalIgnoreCase);

    private static bool IsNegative(string code)
        => string.Equals(code, "LEER", StringComparison.OrdinalIgnoreCase);

    private static double Ratio(int part, int total)
        => total == 0 ? 0 : (double)part / total;

    private static string Bool(bool value) => value ? "True" : "False";

    private static string DisplayPrediction(string? predicted)
        => string.IsNullOrWhiteSpace(predicted) ? "NULL" : predicted.Trim().ToUpperInvariant();

    private static string Csv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

public static class EvalSetClassifierCoverageAnalyzer
{
    public static IReadOnlyList<string> LoadClassifierClassesFromImageFolderDataset(string datasetRoot)
    {
        if (string.IsNullOrWhiteSpace(datasetRoot))
            throw new ArgumentException("Dataset-Pfad fehlt.", nameof(datasetRoot));
        if (!Directory.Exists(datasetRoot))
            throw new DirectoryNotFoundException(datasetRoot);

        var trainRoot = Path.Combine(datasetRoot, "train");
        var classRoot = Directory.Exists(trainRoot) ? trainRoot : datasetRoot;

        return Directory.EnumerateDirectories(classRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static EvalSetClassifierCoverageSummary Analyze(
        IReadOnlyList<EvalSetBenchmarkCase> cases,
        IEnumerable<string> classifierClasses)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(classifierClasses);

        var supported = classifierClasses
            .Select(raw => new
            {
                Raw = raw.Trim(),
                Code = EvalSetClassifierClassMapper.TryMapToCoverageCode(raw)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Raw) && !string.IsNullOrWhiteSpace(x.Code))
            .ToList();

        var codes = cases
            .GroupBy(c => c.ExpectedFullCode, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var expected = EvalSetBenchmarkDataset.NormalizeCode(g.Key) ?? "";
                var match = supported.FirstOrDefault(s => IsCovered(expected, s.Code!));
                return new EvalSetClassifierCoverageCode(
                    ExpectedCode: expected,
                    Count: g.Count(),
                    Covered: match is not null,
                    CoveredBy: match?.Raw);
            })
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.ExpectedCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = cases.Count;
        var covered = codes.Where(c => c.Covered).Sum(c => c.Count);
        return new EvalSetClassifierCoverageSummary(
            TotalEvalCases: total,
            CoveredEvalCases: covered,
            MissingEvalCases: total - covered,
            CoverageRatio: total == 0 ? 0 : (double)covered / total,
            Codes: codes);
    }

    private static bool IsCovered(string expectedCode, string supportedCode)
    {
        if (string.Equals(expectedCode, "LEER", StringComparison.OrdinalIgnoreCase))
            return string.Equals(supportedCode, "LEER", StringComparison.OrdinalIgnoreCase);

        return supportedCode.Length == 3
            ? expectedCode.StartsWith(supportedCode, StringComparison.OrdinalIgnoreCase)
            : string.Equals(expectedCode, supportedCode, StringComparison.OrdinalIgnoreCase);
    }
}

public static class EvalSetBenchmarkContext
{

    public static IReadOnlyList<(string Code, string Description, double Meter)> BuildOracleImportContext(
        EvalSetBenchmarkCase benchmarkCase)
    {
        if (string.Equals(benchmarkCase.ExpectedFullCode, "LEER", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(benchmarkCase.ExpectedFullCode))
        {
            return Array.Empty<(string Code, string Description, double Meter)>();
        }

        var meter = benchmarkCase.Meter ?? 0;
        return
        [
            (
                benchmarkCase.ExpectedFullCode,
                $"Eval-Set Erwartung {benchmarkCase.ExpectedFullCode}",
                meter)
        ];
    }

    public static IReadOnlyList<(string Code, string Description, double Meter)> BuildClassifierImportContext(
        IReadOnlyList<EvalSetCandidatePrediction> predictions,
        double meter = 0,
        double minConfidence = 0.05,
        int maxCandidates = 3)
    {
        ArgumentNullException.ThrowIfNull(predictions);
        if (maxCandidates <= 0)
            return Array.Empty<(string Code, string Description, double Meter)>();

        var result = new List<(string Code, string Description, double Meter)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in predictions
                     .Where(p => p.Confidence >= minConfidence)
                     .OrderByDescending(p => p.Confidence))
        {
            var code = TryMapClassifierClassToVsaCode(p.ClassName);
            if (string.IsNullOrWhiteSpace(code) || !seen.Add(code))
                continue;

            result.Add((
                code,
                $"YOLO-Kandidat {p.ClassName.Trim()} ({p.Confidence.ToString("P0", CultureInfo.InvariantCulture)})",
                meter));

            if (result.Count >= maxCandidates)
                break;
        }

        return result;
    }

    public static IReadOnlyList<string> BuildClassifierObservationHints(
        IReadOnlyList<EvalSetCandidatePrediction> predictions,
        double minConfidence = 0.05,
        int maxHints = 3)
    {
        ArgumentNullException.ThrowIfNull(predictions);
        if (maxHints <= 0)
            return Array.Empty<string>();

        return predictions
            .Where(p => p.Confidence >= minConfidence)
            .OrderByDescending(p => p.Confidence)
            .Select(p => new
            {
                Raw = p.ClassName.Trim(),
                p.Confidence
            })
            .Where(p => !string.IsNullOrWhiteSpace(p.Raw) && !EvalSetClassifierClassMapper.IsNegativeClass(p.Raw))
            .Take(maxHints)
            .Select(p => $"YOLO sieht eventuell {p.Raw} ({p.Confidence.ToString("P0", CultureInfo.InvariantCulture)})")
            .ToList();
    }

    private static string? TryMapClassifierClassToVsaCode(string? className)
        => EvalSetClassifierClassMapper.TryMapToVsaCode(className);
}
