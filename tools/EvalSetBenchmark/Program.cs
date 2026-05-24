using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Pipeline;

var options = BenchmarkOptions.Parse(args);
if (options.ShowHelp)
{
    PrintHelp();
    return 0;
}

try
{
    var allCases = EvalSetBenchmarkDataset.Load(options.EvalSetRoot);
    var cases = options.MaxFrames > 0
        ? allCases.Take(options.MaxFrames).ToList()
        : allCases.ToList();

    if (cases.Count == 0)
        throw new InvalidOperationException("Keine Eval-Frames gefunden.");

    Directory.CreateDirectory(options.OutputDir);

    var config = AiPlatformConfig.Load();
    var baseUri = options.OllamaUrl ?? config.OllamaBaseUri;
    var model = options.Model ?? config.VisionModel;
    var timeout = options.TimeoutMinutes > 0
        ? TimeSpan.FromMinutes(options.TimeoutMinutes)
        : config.OllamaRequestTimeout;

    Console.WriteLine($"Eval-Set: {options.EvalSetRoot}");
    Console.WriteLine($"Frames:   {cases.Count}/{allCases.Count}");
    Console.WriteLine($"Ollama:   {baseUri}");
    Console.WriteLine($"Modell:   {model}");
    Console.WriteLine($"Kontext:  {DescribeContextMode(options)}");
    Console.WriteLine();

    using var client = new OllamaClient(
        baseUri,
        ownedTimeout: timeout,
        keepAlive: config.OllamaKeepAlive,
        numCtx: config.OllamaNumCtx);
    var service = new EnhancedVisionAnalysisService(client, model);

    var predictions = new List<EvalSetPrediction>(cases.Count);
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
        Console.WriteLine("Abbruch angefordert...");
    };

    using var yoloHttpClient = options.UseYoloContext
        ? new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(5, config.SidecarTimeoutSec)) }
        : null;
    VisionPipelineClient? yoloClient = null;
    if (options.UseYoloContext)
    {
        var sidecarUrl = options.SidecarUrl ?? config.SidecarUrl;
        yoloClient = new VisionPipelineClient(sidecarUrl, yoloHttpClient);
        var health = await yoloClient.HealthCheckAsync(cts.Token).ConfigureAwait(false);
        if (health is null)
            throw new InvalidOperationException($"Sidecar nicht erreichbar: {sidecarUrl}");

        Console.WriteLine($"Sidecar:  {sidecarUrl}");
        Console.WriteLine($"YOLO:     top_k={options.YoloTopK}, min_conf={options.YoloMinConfidence.ToString("P0", CultureInfo.InvariantCulture)}");
        Console.WriteLine();
    }

    for (var i = 0; i < cases.Count; i++)
    {
        cts.Token.ThrowIfCancellationRequested();

        var c = cases[i];
        var sw = Stopwatch.StartNew();
        EvalSetPrediction prediction;
        var contextInfo = "";

        try
        {
            var b64 = Convert.ToBase64String(File.ReadAllBytes(c.ImagePath));
            IReadOnlyList<(string Code, string Description, double Meter)>? context = null;
            if (options.UseOracleContext)
            {
                context = EvalSetBenchmarkContext.BuildOracleImportContext(c);
            }
            else if (options.UseYoloContext && yoloClient is not null)
            {
                var yolo = await yoloClient.ClassifyYoloAsync(
                    new YoloClassifyRequest(b64, options.YoloTopK),
                    cts.Token).ConfigureAwait(false);
                var candidates = yolo.Predictions
                    .Select(p => new EvalSetCandidatePrediction(p.ClassName, p.Confidence))
                    .ToList();
                context = EvalSetBenchmarkContext.BuildClassifierImportContext(
                    candidates,
                    meter: c.Meter ?? 0,
                    minConfidence: options.YoloMinConfidence,
                    maxCandidates: options.YoloTopK);
                contextInfo = context.Count > 0
                    ? "  CTX=" + string.Join("/", context.Select(x => x.Code))
                    : "  CTX=-";
            }

            var analysis = context is { Count: > 0 }
                ? await service.AnalyzeAsync(b64, context, cts.Token).ConfigureAwait(false)
                : await service.AnalyzeAsync(b64, cts.Token).ConfigureAwait(false);
            sw.Stop();

            var finding = analysis.Findings
                .Where(f => !string.IsNullOrWhiteSpace(f.VsaCodeHint))
                .OrderByDescending(f => f.Severity)
                .FirstOrDefault();

            var predicted = analysis.Error is not null
                ? ""
                : finding?.VsaCodeHint ?? (analysis.IsEmptyFrame ? "LEER" : "");

            prediction = new EvalSetPrediction(
                FrameFileName: c.FrameFileName,
                PredictedCode: predicted,
                Severity: finding?.Severity ?? 0,
                TimeMs: sw.ElapsedMilliseconds,
                Error: analysis.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            prediction = new EvalSetPrediction(
                c.FrameFileName,
                PredictedCode: "",
                Severity: 0,
                TimeMs: sw.ElapsedMilliseconds,
                Error: ex.Message);
        }

        predictions.Add(prediction);
        var predText = string.IsNullOrWhiteSpace(prediction.PredictedCode)
            ? "leer/fehler"
            : prediction.PredictedCode;
        Console.WriteLine($"[{i + 1,3}/{cases.Count}] {c.FrameFileName}  GT={c.ExpectedFullCode}  PRED={predText}{contextInfo}  {prediction.TimeMs} ms");
    }

    var rows = EvalSetBenchmarkScorer.Evaluate(cases, predictions);
    var summary = EvalSetBenchmarkScorer.Summarize(rows);
    var byCode = EvalSetBenchmarkScorer.SummarizeByExpectedCode(rows);
    var confusion = EvalSetBenchmarkScorer.BuildConfusionMatrix(rows);
    var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
    var modelSlug = Slug(model);
    var runSlug = options.UseOracleContext
        ? $"{modelSlug}_oracle_context"
        : options.UseYoloContext
            ? $"{modelSlug}_yolo_context"
            : modelSlug;
    var csvPath = Path.Combine(options.OutputDir, $"eval_{stamp}_{runSlug}.csv");
    var jsonPath = Path.Combine(options.OutputDir, $"eval_{stamp}_{runSlug}.json");
    var byCodePath = Path.Combine(options.OutputDir, $"eval_{stamp}_{runSlug}_by_code.csv");
    var confusionPath = Path.Combine(options.OutputDir, $"eval_{stamp}_{runSlug}_confusion.csv");

    EvalSetBenchmarkScorer.WriteCsv(csvPath, rows);
    EvalSetBenchmarkScorer.WriteByCodeCsv(byCodePath, byCode);
    EvalSetBenchmarkScorer.WriteConfusionCsv(confusionPath, confusion);
    EvalSetBenchmarkScorer.WriteSummaryJson(jsonPath, summary, new
    {
        created_at = DateTimeOffset.Now.ToString("O"),
        eval_set_root = options.EvalSetRoot,
        frames_total = allCases.Count,
        frames_run = cases.Count,
        model,
        ollama_url = baseUri.ToString(),
        oracle_context = options.UseOracleContext,
        yolo_context = options.UseYoloContext,
        sidecar_url = (options.SidecarUrl ?? config.SidecarUrl).ToString(),
        yolo_top_k = options.YoloTopK,
        yolo_min_confidence = options.YoloMinConfidence,
        manifest = TryReadManifestInfo(options.EvalSetRoot)
    });

    Console.WriteLine();
    Console.WriteLine("Ergebnis:");
    Console.WriteLine($"  Exact:   {summary.ExactCorrect}/{summary.Total} = {summary.ExactAccuracy:P1}");
    Console.WriteLine($"  Main:    {summary.MainCorrect}/{summary.Total} = {summary.MainAccuracy:P1}");
    Console.WriteLine($"  Group:   {summary.GroupCorrect}/{summary.Total} = {summary.GroupAccuracy:P1}");
    Console.WriteLine($"  Negativ: {summary.NegativCorrect} korrekt, Quote {summary.NegativeAccuracy:P1}");
    Console.WriteLine($"  Null:    {summary.NullResponses}");
    Console.WriteLine($"  CSV:     {csvPath}");
    Console.WriteLine($"  JSON:    {jsonPath}");
    Console.WriteLine($"  Codes:   {byCodePath}");
    Console.WriteLine($"  Matrix:  {confusionPath}");
    Console.WriteLine();
    Console.WriteLine("Schwaechste Codes:");
    foreach (var s in byCode
                 .Where(x => !string.Equals(x.ExpectedCode, "LEER", StringComparison.OrdinalIgnoreCase))
                 .OrderBy(x => x.ExactAccuracy)
                 .ThenByDescending(x => x.Total)
                 .Take(8))
    {
        Console.WriteLine($"  {s.ExpectedCode}: {s.ExactCorrect}/{s.Total} = {s.ExactAccuracy:P0}, haeufig: {s.TopPrediction} ({s.TopPredictionCount})");
    }
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Abgebrochen.");
    return 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FEHLER: {ex.Message}");
    return 2;
}

static string DescribeContextMode(BenchmarkOptions options)
{
    if (options.UseOracleContext)
        return "Oracle aus Eval-Set";
    if (options.UseYoloContext)
        return "YOLO-Kandidaten";
    return "kein Kontext";
}

static object? TryReadManifestInfo(string evalSetRoot)
{
    var path = Path.Combine(evalSetRoot, "_manifest.json");
    if (!File.Exists(path))
        return null;

    try
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        return new
        {
            frozen = TryGetBool(root, "frozen"),
            approved = TryGetInt(root, "approved"),
            hash_algorithm = TryGetString(root, "hash_algorithm"),
            hashes_count = TryGetInt(root, "hashes_count"),
            hashes_generated_utc = TryGetString(root, "hashes_generated_utc")
        };
    }
    catch
    {
        return null;
    }
}

static bool? TryGetBool(JsonElement root, string name)
    => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
        ? value.GetBoolean()
        : null;

static int? TryGetInt(JsonElement root, string name)
    => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i)
        ? i
        : null;

static string? TryGetString(JsonElement root, string name)
    => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString()
        : null;

static string Slug(string value)
{
    var chars = value
        .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
        .ToArray();
    return new string(chars).Trim('_');
}

static void PrintHelp()
{
    Console.WriteLine("""
EvalSetBenchmark

Misst ein eingefrorenes Eval-Set gegen das aktuelle Ollama-Vision-Modell.

Beispiele:
  dotnet run --project tools/EvalSetBenchmark -- --max 5
  dotnet run --project tools/EvalSetBenchmark -- --eval-set C:\KI_BRAIN\eval_set --model qwen3-vl:8b-q8

Optionen:
  --eval-set <pfad>     Standard: C:\KI_BRAIN\eval_set
  --out <ordner>        Standard: docs\benchmarks
  --model <name>        Standard: App-Konfiguration
  --ollama-url <url>    Standard: App-Konfiguration
  --timeout-min <zahl>  Standard: App-Konfiguration
  --max <zahl>          Nur erste N Frames laufen lassen
  --oracle-context      Testmodus: gibt Qwen den erwarteten Code als Kontext
  --yolo-context        Testmodus: YOLO-cls liefert 1-3 Kandidaten fuer Qwen
  --sidecar-url <url>   Standard: App-Konfiguration
  --yolo-top-k <zahl>   Standard: 3
  --yolo-min-conf <x>   Standard: 0.05
  --help                Hilfe anzeigen
""");
}

internal sealed record BenchmarkOptions(
    string EvalSetRoot,
    string OutputDir,
    string? Model,
    Uri? OllamaUrl,
    Uri? SidecarUrl,
    int TimeoutMinutes,
    int MaxFrames,
    bool UseOracleContext,
    bool UseYoloContext,
    int YoloTopK,
    double YoloMinConfidence,
    bool ShowHelp)
{
    public static BenchmarkOptions Parse(string[] args)
    {
        var root = @"C:\KI_BRAIN\eval_set";
        var output = Path.Combine("docs", "benchmarks");
        string? model = null;
        Uri? url = null;
        Uri? sidecarUrl = null;
        var timeoutMin = 0;
        var max = 0;
        var oracleContext = false;
        var yoloContext = false;
        var yoloTopK = 3;
        var yoloMinConf = 0.05;
        var help = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help" or "-h" or "/?":
                    help = true;
                    break;
                case "--eval-set":
                    root = RequireValue(args, ref i, arg);
                    break;
                case "--out":
                    output = RequireValue(args, ref i, arg);
                    break;
                case "--model":
                    model = RequireValue(args, ref i, arg);
                    break;
                case "--ollama-url":
                    url = new Uri(RequireValue(args, ref i, arg));
                    break;
                case "--sidecar-url":
                    sidecarUrl = new Uri(RequireValue(args, ref i, arg));
                    break;
                case "--timeout-min":
                    timeoutMin = int.Parse(RequireValue(args, ref i, arg));
                    break;
                case "--max":
                    max = int.Parse(RequireValue(args, ref i, arg));
                    break;
                case "--oracle-context":
                    oracleContext = true;
                    break;
                case "--yolo-context":
                    yoloContext = true;
                    break;
                case "--yolo-top-k":
                    yoloTopK = int.Parse(RequireValue(args, ref i, arg));
                    break;
                case "--yolo-min-conf":
                    yoloMinConf = double.Parse(RequireValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new ArgumentException($"Unbekannte Option: {arg}");
            }
        }

        if (oracleContext && yoloContext)
            throw new ArgumentException("--oracle-context und --yolo-context koennen nicht gleichzeitig genutzt werden.");
        if (yoloTopK <= 0)
            throw new ArgumentException("--yolo-top-k muss groesser als 0 sein.");
        if (yoloMinConf < 0 || yoloMinConf > 1)
            throw new ArgumentException("--yolo-min-conf muss zwischen 0 und 1 liegen.");

        return new BenchmarkOptions(
            root,
            output,
            model,
            url,
            sidecarUrl,
            timeoutMin,
            max,
            oracleContext,
            yoloContext,
            yoloTopK,
            yoloMinConf,
            help);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Wert fehlt fuer {option}");
        index++;
        return args[index];
    }
}
