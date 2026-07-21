using System.Text;
using System.Text.Json;

namespace AuswertungPro.Next.Application.Ai.Evaluation;

public static class EvalSetBenchmarkDataset
{
    public static IReadOnlyList<EvalSetBenchmarkCase> Load(string evalSetRoot)
        => LoadCore(evalSetRoot, includeMissingImages: false);

    internal static IReadOnlyList<EvalSetBenchmarkCase> LoadForReleaseValidation(string evalSetRoot)
        => LoadCore(evalSetRoot, includeMissingImages: true);

    private static IReadOnlyList<EvalSetBenchmarkCase> LoadCore(
        string evalSetRoot,
        bool includeMissingImages)
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
            if (!includeMissingImages && string.IsNullOrWhiteSpace(framePath))
                continue;

            var frameFileName = string.IsNullOrWhiteSpace(framePath)
                ? ""
                : Path.GetFileName(framePath);
            var imagePath = Path.Combine(evalSetRoot, "images", frameFileName);
            if (!includeMissingImages && !File.Exists(imagePath))
                continue;

            var expectedFull = NormalizeCode(GetString(item, "korrektur"))
                ?? NormalizeCode(GetString(item, "code_full"))
                ?? "";
            var expectedMain = NormalizeCode(GetString(item, "code_main"))
                ?? expectedFull;
            var holdingKey = GetString(item, "haltung_key")
                             ?? GetString(item, "holding_key");
            var expectedSeverity = GetInt32(item, "expected_severity");
            var eventId = GetString(item, "event_id");
            var meterStart = GetDouble(item, "meter_start");
            var meterEnd = GetDouble(item, "meter_end");

            result.Add(new EvalSetBenchmarkCase(
                Id: GetString(item, "id") ?? Path.GetFileNameWithoutExtension(frameFileName),
                FrameFileName: frameFileName,
                ImagePath: imagePath,
                ExpectedFullCode: expectedFull,
                ExpectedMainCode: expectedMain,
                Category: GetString(item, "kategorie") ?? "",
                Meter: GetDouble(item, "meter"),
                HasYoloLabel: HasNonEmptyYoloLabel(evalSetRoot, frameFileName),
                HoldingKey: holdingKey,
                ExpectedSeverity: expectedSeverity,
                EventId: eventId,
                MeterStart: meterStart,
                MeterEnd: meterEnd));
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

    private static int? GetInt32(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
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
