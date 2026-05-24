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
    double? Meter);

public sealed record EvalSetPrediction(
    string FrameFileName,
    string? PredictedCode,
    int Severity,
    long TimeMs,
    string? Error = null);

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
                Meter: GetDouble(item, "meter")));
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

    private static bool SameGroup(string predicted, string expected)
        => predicted.Length >= 3 &&
           expected.Length >= 3 &&
           string.Equals(predicted[..3], expected[..3], StringComparison.OrdinalIgnoreCase);

    private static bool IsNegative(string code)
        => string.Equals(code, "LEER", StringComparison.OrdinalIgnoreCase);

    private static double Ratio(int part, int total)
        => total == 0 ? 0 : (double)part / total;

    private static string Bool(bool value) => value ? "True" : "False";

    private static string Csv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
