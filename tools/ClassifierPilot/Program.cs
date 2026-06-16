using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Evaluation;
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

// ── 4) Abgleich KI vs. Protokoll (gestuft, 1:1, vier Toepfe) ──
// Geteilte, ehrliche Logik aus der Application-Schicht: gruen<=0.20m / gelb<=0.50m,
// Eins-zu-Eins-Zuordnung (max. Treffer bei min. Abstand), BCD/BCE ausgenommen,
// KI-Detections ohne aufgeloesten Code zaehlen als Fehlalarm.
var gtFindings = gt.Select(e => new BefundMatchFinding(e.VsaCode, e.MeterStart, e.MeterEnd, e.Text)).ToList();
var kiFindings = result.Detections.Select(d => new BefundMatchFinding(d.Code, d.MeterStart, d.MeterEnd, d.FindingLabel)).ToList();
var matchResult = BefundMatcher.Match(gtFindings, kiFindings, BefundMatchOptions.Default);

var gtRelevantCount = gtFindings.Count(f => !BefundMatchOptions.Default.IsExcluded(f));
var kiRelevantCount = kiFindings.Count(f => !BefundMatchOptions.Default.IsExcluded(f));

Console.WriteLine($"\n=== ABGLEICH (gruen<=0.20m / gelb<=0.50m, 1:1; BCD/BCE ausgenommen) ===");
Console.WriteLine($"Protokoll-Befunde: {gtRelevantCount} | KI-Befunde: {kiRelevantCount}" +
    (matchResult.OhneCode > 0 ? $" (davon {matchResult.OhneCode} ohne aufgeloesten Code → Fehlalarm)" : ""));
Console.WriteLine($"Praezision {matchResult.Precision:P0} | Recall {matchResult.Recall:P0}");
Console.WriteLine($"TREFFER (TP): {matchResult.Treffer.Count} (gruen {matchResult.Treffer.Count(p => p.Tier == "gruen")}, gelb {matchResult.Treffer.Count(p => p.Tier == "gelb")})");
foreach (var p in matchResult.Treffer) Console.WriteLine($"  + {p.Tier,-5} {p.Gt.Code} @ {p.Gt.MeterStart:F2}m  <->  KI {p.Ki.Code} @ {p.Ki.MeterStart:F2}m  (Δ {p.Gap:F2}m)");
Console.WriteLine($"FALSCHER CODE (WC): {matchResult.FalscherCode.Count}");
foreach (var p in matchResult.FalscherCode) Console.WriteLine($"  ~ {p.Gt.Code} @ {p.Gt.MeterStart:F2}m  <->  KI {p.Ki.Code} @ {p.Ki.MeterStart:F2}m  (Δ {p.Gap:F2}m)");
Console.WriteLine($"VERPASST (FN): {matchResult.Verpasst.Count}");
foreach (var f in matchResult.Verpasst) Console.WriteLine($"  - {f.Code} @ {f.MeterStart:F2}m  ({Trim(f.Label, 40)})");
Console.WriteLine($"FEHLALARM (FP): {matchResult.Fehlalarm.Count}");
foreach (var f in matchResult.Fehlalarm) Console.WriteLine($"  ? {(f.Code.Length == 0 ? "(leer)" : f.Code)} @ {f.MeterStart:F2}m  ({Trim(f.Label, 40)})");

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
    match = new
    {
        praezision = Math.Round(matchResult.Precision, 4),
        recall = Math.Round(matchResult.Recall, 4),
        treffer = matchResult.Treffer.Select(p => new { p.Tier, gt_code = p.Gt.Code, gt_m = p.Gt.MeterStart, ki_code = p.Ki.Code, ki_m = p.Ki.MeterStart, gap_m = Math.Round(p.Gap, 2) }),
        falscher_code = matchResult.FalscherCode.Select(p => new { gt_code = p.Gt.Code, gt_m = p.Gt.MeterStart, ki_code = p.Ki.Code, ki_m = p.Ki.MeterStart, gap_m = Math.Round(p.Gap, 2) }),
        verpasst = matchResult.Verpasst.Select(f => new { f.Code, f.MeterStart, f.Label }),
        fehlalarm = matchResult.Fehlalarm.Select(f => new { f.Code, f.MeterStart, f.Label }),
        ohne_code = matchResult.OhneCode,
        ignoriert_anker = matchResult.IgnoriertAnker,
    },
};
File.WriteAllText(outPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"\nReport: {outPath}");
Console.WriteLine("Trace:  neueste pipeline_trace_*.jsonl unter %LOCALAPPDATA%\\SewerStudio\\Telemetry");
return 0;

static string Trim(string? s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "…");
