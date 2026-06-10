using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

// ClassifierPilot — Video-Pilot fuer den ClassifierDecision-Pfad (Paket 2).
// Faehrt die ECHTE Multi-Model-Pipeline (YOLO->DINO->SAM->Klassifikator->Qwen)
// headless ueber ein Video mit bekanntem PDF-Protokoll und vergleicht:
//   KI-Detections (Klassifikator fuehrend) vs. Protokoll-Ground-Truth.
// Das Flag wird NUR als Property gesetzt — keine persistente Env-Aenderung.
//
// Aufruf: dotnet run --project tools/ClassifierPilot -- <video> <pdf> [dnMm]

if (args.Length < 2)
{
    Console.WriteLine("Aufruf: ClassifierPilot <video> <pdf> [dnMm]");
    return 1;
}

// --gt-only <pdf>: nur Protokoll-Codes anzeigen (zur Pilot-Fall-Auswahl)
if (args[0] == "--gt-only")
{
    var gtOnly = await new PdfProtocolExtractor().ExtractAsync(args[1]);
    Console.WriteLine($"{Path.GetFileName(args[1])}: " + string.Join(", ",
        gtOnly.Select(e => $"{e.VsaCode}@{e.MeterStart:F1}")));
    return 0;
}

var videoPath = args[0];
var pdfPath = args[1];
var dnMm = args.Length > 2 && int.TryParse(args[2], out var dn) ? dn : 300;

if (!File.Exists(videoPath) || !File.Exists(pdfPath))
{
    Console.WriteLine($"FEHLER: Video oder PDF nicht gefunden.\n  {videoPath}\n  {pdfPath}");
    return 1;
}

Console.WriteLine("=== Klassifikator-Pilot (SEWERSTUDIO_CLASSIFIER_DECISION nur prozesslokal) ===");
Console.WriteLine($"Video: {videoPath}");
Console.WriteLine($"PDF:   {pdfPath}");

// ── 1) Ground-Truth aus dem PDF-Protokoll ──
var extractor = new PdfProtocolExtractor();
var gt = await extractor.ExtractAsync(pdfPath);
Console.WriteLine($"Ground-Truth: {gt.Count} Eintraege");
foreach (var e in gt)
    Console.WriteLine($"  GT {e.VsaCode,-6} @ {e.MeterStart,6:F2}m{(e.IsStreckenschaden ? $"-{e.MeterEnd:F2}m" : "")}  {Trim(e.Text, 60)}");
if (gt.Count == 0)
{
    Console.WriteLine("FEHLER: Protokoll lieferte keine Eintraege — Fall ungeeignet.");
    return 1;
}

var reachLength = Math.Max(gt.Max(e => Math.Max(e.MeterStart, e.MeterEnd)), 5.0);
Console.WriteLine($"Haltungslaenge (aus GT): ~{reachLength:F1} m | DN {dnMm}");

// ── 2) Pipeline aufbauen (gleiche Konfiguration wie die App) ──
// VSA-Katalog wie in der App konfigurieren — OHNE ihn liefert NormalizeFindingCode
// null und die Dedup-Schluessel degradieren auf Labels (Pilot 3c: BAJ-Duplikate
// am selben Meter trotz funktionierender Voting-Hysterese).
var manifestPath = Path.Combine("src", "AuswertungPro.Next.UI", "Data", "vsa_kek_2020_catalog_manifest.json");
if (File.Exists(manifestPath))
{
    VsaCodeResolver.ConfigureCatalog(
        new AuswertungPro.Next.Application.Protocol.ManifestCodeCatalogProvider(manifestPath));
    Console.WriteLine("VSA-Katalog konfiguriert (Dedup-Schluessel nutzen Codes).");
}
else
{
    Console.WriteLine($"WARNUNG: Katalog-Manifest fehlt ({manifestPath}) — Dedup-Schluessel degradieren auf Labels!");
}

var settings = AiSettingsFactory.Load();
var pipelineCfg = settings.ToPipelineConfig();
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
var client = new VisionPipelineClient(pipelineCfg.SidecarUrl, http, pipelineCfg.SidecarToken);

var health = await client.HealthCheckAsync(CancellationToken.None);
if (health is null || health.Status != "ok")
{
    Console.WriteLine("FEHLER: Sidecar nicht erreichbar — Pilot braucht die Multi-Model-Pipeline.");
    return 1;
}

using var ollama = new OllamaClient(settings.OllamaBaseUri, ownedTimeout: TimeSpan.FromMinutes(5),
    keepAlive: settings.OllamaKeepAlive, numCtx: settings.OllamaNumCtx);
var qwen = new EnhancedVisionAnalysisService(ollama, settings.VisionModel, null);

var pipeline = new MultiModelAnalysisService(client, pipelineCfg, settings.FfmpegPath ?? "ffmpeg", qwen)
{
    ClassifierDecisionEnabled = true,           // << der Pilot-Schalter (nur dieser Prozess)
    EstimatedReachLengthM = reachLength,
};

Console.WriteLine($"VLM: {settings.VisionModel} | DINO {pipelineCfg.DinoBoxThreshold}/{pipelineCfg.DinoTextThreshold} | ClassifierDecision: AN");
Console.WriteLine();

// ── 3) Lauf ──
var lastReport = DateTime.MinValue;
var progress = new Progress<VideoAnalysisProgress>(p =>
{
    if ((DateTime.UtcNow - lastReport).TotalSeconds >= 10)
    {
        lastReport = DateTime.UtcNow;
        Console.WriteLine($"  [{p.FramesDone}/{p.FramesTotal}] {p.Status}");
    }
});

var started = DateTime.UtcNow;
var result = await pipeline.AnalyzeAsync(videoPath, progress, CancellationToken.None);
var elapsed = DateTime.UtcNow - started;

if (!result.IsSuccess)
{
    Console.WriteLine($"FEHLER: Pipeline fehlgeschlagen: {result.Error}");
    return 1;
}

Console.WriteLine($"\nPipeline fertig in {elapsed.TotalMinutes:F1} min — {result.Detections.Count} Detections aus {result.FramesAnalyzed} Frames");
foreach (var d in result.Detections)
    Console.WriteLine($"  KI {d.Code,-6} @ {d.MeterStart,6:F2}m{(Math.Abs(d.MeterEnd - d.MeterStart) > 0.05 ? $"-{d.MeterEnd:F2}m" : "")}  {Trim(d.FindingLabel, 50)}  [{d.Severity}]");

// ── 4) Vergleich KI vs. Protokoll (Hauptcode, ±2.0 m Toleranz) ──
const double meterTol = 2.0;
string MainCode(string? c) => string.IsNullOrWhiteSpace(c) ? "" : c.Trim().ToUpperInvariant()[..Math.Min(3, c.Trim().Length)];
bool Overlaps(double aS, double aE, double bS, double bE) =>
    Math.Max(aS, bS) - meterTol <= Math.Min(aE, bE) + meterTol;

var gtRelevant = gt.Where(e => MainCode(e.VsaCode) is not ("BCD" or "BCE")).ToList();
var kiRelevant = result.Detections.Where(d => MainCode(d.Code) is not ("" or "BCD" or "BCE")).ToList();

var matched = new HashSet<int>();
var tp = new List<string>();
var fn = new List<string>();
foreach (var e in gtRelevant)
{
    var hit = kiRelevant
        .Select((d, i) => (d, i))
        .Where(x => !matched.Contains(x.i)
            && MainCode(x.d.Code) == MainCode(e.VsaCode)
            && Overlaps(e.MeterStart, Math.Max(e.MeterStart, e.MeterEnd), x.d.MeterStart, x.d.MeterEnd))
        .OrderBy(x => Math.Abs(x.d.MeterStart - e.MeterStart))
        .Cast<(RawVideoDetection d, int i)?>()
        .FirstOrDefault();
    if (hit is { } h)
    {
        matched.Add(h.i);
        tp.Add($"{e.VsaCode} @ {e.MeterStart:F2}m  <->  KI {h.d.Code} @ {h.d.MeterStart:F2}m");
    }
    else
    {
        fn.Add($"{e.VsaCode} @ {e.MeterStart:F2}m  ({Trim(e.Text, 40)})");
    }
}
var fp = kiRelevant.Where((d, i) => !matched.Contains(i))
    .Select(d => $"{d.Code} @ {d.MeterStart:F2}m  ({Trim(d.FindingLabel, 40)})").ToList();

Console.WriteLine($"\n=== VERGLEICH (Hauptcode, ±{meterTol:F1} m; BCD/BCE ausgenommen) ===");
Console.WriteLine($"Protokoll-Befunde: {gtRelevant.Count} | KI-Befunde: {kiRelevant.Count}");
Console.WriteLine($"TREFFER (TP): {tp.Count}");   foreach (var s in tp) Console.WriteLine($"  + {s}");
Console.WriteLine($"VERPASST (FN): {fn.Count}");  foreach (var s in fn) Console.WriteLine($"  - {s}");
Console.WriteLine($"ZUSATZ (FP): {fp.Count}");    foreach (var s in fp) Console.WriteLine($"  ? {s}");

// ── 5) Report-JSON neben die Benchmarks legen ──
var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
var caseId = Path.GetFileNameWithoutExtension(videoPath);
var outPath = Path.Combine("docs", "benchmarks", $"classifier_pilot_{caseId}_{stamp}.json");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
var report = new
{
    video = videoPath,
    pdf = pdfPath,
    elapsed_min = Math.Round(elapsed.TotalMinutes, 1),
    frames = result.FramesAnalyzed,
    classifier_decision = true,
    reach_length_m = reachLength,
    ground_truth = gt.Select(e => new { e.VsaCode, e.MeterStart, e.MeterEnd, e.Text }),
    detections = result.Detections.Select(d => new { d.Code, d.MeterStart, d.MeterEnd, d.FindingLabel, d.Severity }),
    vergleich = new { tp, fn, fp },
};
File.WriteAllText(outPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"\nReport: {outPath}");
Console.WriteLine("Trace:  neueste pipeline_trace_*.jsonl unter %LOCALAPPDATA%\\SewerStudio\\Telemetry");
return 0;

static string Trim(string? s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "…");
