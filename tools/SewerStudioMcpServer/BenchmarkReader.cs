using System.Text;
using System.Text.Json;

namespace AuswertungPro.Tools.SewerStudioMcpServer;

// Liest den neuesten Eval-Set-Benchmark aus docs/benchmarks (read-only).
// Erkennt zwei Formate:
//   - Klassifikator-Lauf (Autopilot/Verify): frames/exact_acc/per_class[code]=[korrekt, gesamt]
//   - Qwen-VL-Lauf (EvalSetBenchmark): metadata + summary{ExactAccuracy,...}
// Unbekannte Formate werden roh in "raw" durchgereicht.
public static class BenchmarkReader
{
    private const int MaxRunList = 25;

    public static LatestBenchmarkResult ReadLatest(string benchmarksDir, string? nameContains, int maxCodes)
    {
        if (maxCodes <= 0)
            maxCodes = 12;

        if (string.IsNullOrWhiteSpace(benchmarksDir) || !Directory.Exists(benchmarksDir))
            return Empty(benchmarksDir, "Benchmark-Ordner nicht gefunden.", Array.Empty<BenchmarkRunInfo>());

        var allJson = Directory.EnumerateFiles(benchmarksDir, "*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        if (allJson.Count == 0)
            return Empty(benchmarksDir, "Keine Benchmark-JSON-Dateien gefunden.", Array.Empty<BenchmarkRunInfo>());

        var runList = BuildRunList(allJson);

        var candidates = string.IsNullOrWhiteSpace(nameContains)
            ? allJson
            : allJson
                .Where(p => Path.GetFileName(p).Contains(nameContains!, StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (candidates.Count == 0)
            return Empty(benchmarksDir, $"Kein Benchmark passt zum Filter '{nameContains}'.", runList);

        return Parse(benchmarksDir, candidates[0], maxCodes, runList);
    }

    private static LatestBenchmarkResult Parse(
        string dir,
        string path,
        int maxCodes,
        IReadOnlyList<BenchmarkRunInfo> runs)
    {
        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            root = doc.RootElement.Clone();
        }
        catch (Exception ex)
        {
            return Empty(dir, $"JSON nicht lesbar: {ex.Message}", runs);
        }

        var name = Path.GetFileName(path);
        var modified = File.GetLastWriteTimeUtc(path).ToString("O");

        // Format A: Klassifikator-Lauf (Autopilot/Verify) mit per_class.
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("per_class", out var perClass)
            && perClass.ValueKind == JsonValueKind.Object)
        {
            var weak = perClass.EnumerateObject()
                .Select(p => ReadClassPair(p.Name, p.Value))
                .Where(c => c is not null)
                .Select(c => c!)
                .OrderBy(c => c.Accuracy)
                .ThenByDescending(c => c.Total)
                .Take(maxCodes)
                .ToList();

            return new LatestBenchmarkResult(
                BenchmarksDir: dir,
                Found: true,
                FileName: name,
                FilePath: path,
                ModifiedUtc: modified,
                Format: "classifier",
                Frames: GetInt(root, "frames"),
                ExactAccuracy: GetDouble(root, "exact_acc"),
                FindingsAccuracy: GetDouble(root, "findings_acc"),
                LeerAccuracy: GetDouble(root, "leer_acc"),
                ModelOrWeights: GetString(root, "weights"),
                EvalRoot: GetString(root, "eval_root"),
                WeakestCodes: weak,
                Raw: root,
                AvailableRuns: runs,
                Note: "Klassifikator-Lauf. per_class = [korrekt, gesamt] je Code; weakest_codes sind aufsteigend nach Quote.");
        }

        // Format B: Qwen-VL-Lauf (EvalSetBenchmark) mit summary.
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("summary", out var summary)
            && summary.ValueKind == JsonValueKind.Object)
        {
            var weak = ReadByCodeCsv(path, maxCodes);

            return new LatestBenchmarkResult(
                BenchmarksDir: dir,
                Found: true,
                FileName: name,
                FilePath: path,
                ModifiedUtc: modified,
                Format: "qwen_vl",
                Frames: GetInt(summary, "Total"),
                ExactAccuracy: GetDouble(summary, "ExactAccuracy"),
                FindingsAccuracy: GetDouble(summary, "MainAccuracy"),
                LeerAccuracy: GetDouble(summary, "NegativeAccuracy"),
                ModelOrWeights: GetMetaString(root, "model"),
                EvalRoot: GetMetaString(root, "eval_set_root"),
                WeakestCodes: weak,
                Raw: root,
                AvailableRuns: runs,
                Note: "Qwen-VL-Lauf. findings_accuracy=MainAccuracy, leer_accuracy=NegativeAccuracy; schwaechste Codes aus *_by_code.csv (falls vorhanden).");
        }

        return new LatestBenchmarkResult(
            BenchmarksDir: dir,
            Found: true,
            FileName: name,
            FilePath: path,
            ModifiedUtc: modified,
            Format: "unknown",
            Frames: null,
            ExactAccuracy: null,
            FindingsAccuracy: null,
            LeerAccuracy: null,
            ModelOrWeights: null,
            EvalRoot: null,
            WeakestCodes: Array.Empty<BenchmarkWeakCode>(),
            Raw: root,
            AvailableRuns: runs,
            Note: "Unbekanntes Format. Das vollstaendige JSON steht in raw.");
    }

    private static BenchmarkWeakCode? ReadClassPair(string code, JsonElement value)
        => BenchmarkParsers.ParseClassifierPair(code, value);

    // Liest die zum Qwen-JSON gehoerende *_by_code.csv (gleiches Praefix) und liefert die schwaechsten Codes.
    private static IReadOnlyList<BenchmarkWeakCode> ReadByCodeCsv(string jsonPath, int maxCodes)
    {
        var basePath = jsonPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? jsonPath[..^5]
            : jsonPath;
        var csvPath = basePath + "_by_code.csv";
        if (!File.Exists(csvPath))
            return Array.Empty<BenchmarkWeakCode>();

        var lines = File.ReadAllLines(csvPath, Encoding.UTF8);
        if (lines.Length < 2)
            return Array.Empty<BenchmarkWeakCode>();

        // Header: expected,total,exact_correct,main_correct,group_correct,null_responses,predicted_leer,exact_accuracy,...
        var result = new List<BenchmarkWeakCode>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var cols = lines[i].Split(',');
            // Originalverhalten: Zeilen mit weniger als 8 Spalten ueberspringen
            // (Header: expected,total,exact_correct,main_correct,group_correct,null_responses,predicted_leer,exact_accuracy,...)
            if (cols.Length < 8)
                continue;

            var entry = BenchmarkParsers.ParseByCodeRow(cols);
            if (entry is not null)
                result.Add(entry);
        }

        return result
            .OrderBy(c => c.Accuracy)
            .ThenByDescending(c => c.Total)
            .Take(maxCodes)
            .ToList();
    }

    private static IReadOnlyList<BenchmarkRunInfo> BuildRunList(IReadOnlyList<string> jsonPaths)
        => jsonPaths
            .Take(MaxRunList)
            .Select(p => new BenchmarkRunInfo(
                Path.GetFileName(p),
                File.GetLastWriteTimeUtc(p).ToString("O"),
                TryReadHeadlineAccuracy(p)))
            .ToList();

    private static double? TryReadHeadlineAccuracy(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var direct = GetDouble(root, "exact_acc");
            if (direct is not null)
                return direct;

            return root.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.Object
                ? GetDouble(summary, "ExactAccuracy")
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static LatestBenchmarkResult Empty(string dir, string note, IReadOnlyList<BenchmarkRunInfo> runs)
        => new(
            BenchmarksDir: dir ?? "",
            Found: false,
            FileName: null,
            FilePath: null,
            ModifiedUtc: null,
            Format: "none",
            Frames: null,
            ExactAccuracy: null,
            FindingsAccuracy: null,
            LeerAccuracy: null,
            ModelOrWeights: null,
            EvalRoot: null,
            WeakestCodes: Array.Empty<BenchmarkWeakCode>(),
            Raw: null,
            AvailableRuns: runs,
            Note: note);

    private static int? GetInt(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object
           && obj.TryGetProperty(name, out var v)
           && v.ValueKind == JsonValueKind.Number
           && v.TryGetInt32(out var i)
            ? i
            : null;

    private static double? GetDouble(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object
           && obj.TryGetProperty(name, out var v)
           && v.ValueKind == JsonValueKind.Number
           && v.TryGetDouble(out var d)
            ? d
            : null;

    private static string? GetString(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object
           && obj.TryGetProperty(name, out var v)
           && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static string? GetMetaString(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty("metadata", out var meta)
            ? GetString(meta, name)
            : null;
}
