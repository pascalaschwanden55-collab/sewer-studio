using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Ollama;
using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// V4.3 — Fairer Vergleich qwen3-vl:8b-q8 vs qwen3-vl:8b-thinking.
///
/// Korrigiert gegenueber erstem Versuch:
///   - Ground-Truth aus _candidates.json (YOLO-Labels im Eval-Set sind verfaelscht)
///   - Warmup-Call vor jeder Modell-Messung (Cold-Start eliminiert)
///   - Sequentielle Ausfuehrung (Base-Phase, dann Thinking-Phase)
///   - Eskalation AUS (referenceModel=null) — purer 8B-Modellvergleich
///   - Multi-Metrik-CSV + Markdown-Report
///   - Abbruch nach 30 Frames wenn Base <20% Hauptcode (Test-Pipeline-Bug)
///
/// AUSFUEHRUNG: ~100 Min (Base ~4 Min, Thinking ~96 Min).
/// GPU-Konflikt vermeiden — kein Nachtbatch, kein SewerStudio-Batch.
/// </summary>
public class QwenModelComparisonTest
{
    private readonly ITestOutputHelper _output;
    public QwenModelComparisonTest(ITestOutputHelper output) => _output = output;

    private const string EvalDir = @"C:\KI_BRAIN\eval_set";
    private const string BaseModel = "qwen3-vl:8b-q8";
    private const string ThinkModel = "qwen3-vl:8b-thinking";
    private const int PlausibilityCheckAt = 30;
    private const double PlausibilityThreshold = 0.20;

    private sealed class Candidate
    {
        public string frame_path { get; set; } = "";
        public string haltung_key { get; set; } = "";
        public string code_main { get; set; } = "";
        public string code_full { get; set; } = "";
        public string kategorie { get; set; } = "";
    }

    private sealed record FrameResult(
        string Frame,
        string GtCodeFull,
        string GtCodeMain,
        string Kategorie,
        string PredCode,
        bool ExactHit,
        bool MainHit,
        bool GroupHit,
        bool NullResponse,
        bool NegativCorrect,
        double TimeMs,
        int Severity);

    /// <summary>
    /// V4.3 Option B — Vergleich auf manuell approved Samples aus training_samples.json.
    /// Waehlt eine balancierte Stichprobe (10 Samples pro Top-Code) aus Frames die der User
    /// bereits als korrekt markiert hat. Code ↔ Bild ist hier belegt, anders als beim Eval-Set.
    /// </summary>
    [Fact]
    [Trait("Category", "GpuEval")]
    public async Task CompareBaseVsThinking_OnApprovedSamples()
    {
        const string samplesJson = @"C:\KI_BRAIN\training_samples.json";
        if (!File.Exists(samplesJson))
        {
            _output.WriteLine("training_samples.json fehlt — Test uebersprungen");
            return;
        }

        // Samples laden — Struktur ist dieselbe wie im Haupt-Store
        var raw = File.ReadAllText(samplesJson);
        using var doc = JsonDocument.Parse(raw);
        var samples = new List<(string Code, string Path, string Source)>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.TryGetProperty("Status", out var st) && st.GetInt32() != 1) continue;
            if (!el.TryGetProperty("Code", out var cp) || string.IsNullOrWhiteSpace(cp.GetString())) continue;
            if (!el.TryGetProperty("FramePath", out var fp)) continue;
            var p = fp.GetString() ?? "";
            if (!File.Exists(p)) continue;
            var src = el.TryGetProperty("SourceType", out var s) ? (s.GetString() ?? "") : "";
            samples.Add((cp.GetString()!.ToUpperInvariant(), p, src));
        }

        // Balancierte Stichprobe: 10 pro Top-Code, mit Mix Struktur + Schaden
        var targetCodes = new[]
        {
            "BCD", "BCE", "BCAAA",                    // 3× Struktur (Qualitaets-Floor)
            "BAFCE", "BAJC", "BBBA", "BAAA", "BDDC",  // 5× haeufige Schaeden
            "BAJB", "BAHC"                            // 2× schwierige Schaeden
        };
        const int perCode = 10;
        var rng = new Random(42);

        var picks = new List<(string Code, string Path, string Source)>();
        foreach (var code in targetCodes)
        {
            var avail = samples.Where(s => s.Code == code).ToList();
            if (avail.Count == 0)
            {
                _output.WriteLine($"  ⚠ Kein Sample fuer Code {code}");
                continue;
            }
            // Shuffle + Take
            for (int i = avail.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (avail[i], avail[j]) = (avail[j], avail[i]); }
            picks.AddRange(avail.Take(perCode));
        }

        _output.WriteLine($"Stichprobe: {picks.Count} approved Samples aus {targetCodes.Length} Codes");
        _output.WriteLine($"Modelle: {BaseModel}  vs  {ThinkModel}");
        _output.WriteLine($"Eskalation: AUS");
        _output.WriteLine("");

        var config = OllamaConfig.Load();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

        // Phase 1: Base
        _output.WriteLine("═══ Phase 1: BASE ═══");
        var baseClient = new OllamaClient(config.BaseUri, http, keepAlive: "24h", numCtx: 8192);
        var baseSvc = new EnhancedVisionAnalysisService(baseClient, BaseModel, referenceModel: null, numCtx: 8192);
        await WarmupAsync(baseSvc, picks[0].Path, BaseModel);
        var baseResults = await RunApprovedPhaseAsync(baseSvc, picks, BaseModel);
        WriteCsv(baseResults, Path.Combine(Path.GetDirectoryName(samplesJson)!, "metrics_approved_base.csv"));

        // Phase 2: Thinking
        _output.WriteLine("");
        _output.WriteLine("═══ Phase 2: THINKING ═══");
        var thinkClient = new OllamaClient(config.BaseUri, http, keepAlive: "24h", numCtx: 8192);
        var thinkSvc = new EnhancedVisionAnalysisService(thinkClient, ThinkModel, referenceModel: null, numCtx: 8192);
        await WarmupAsync(thinkSvc, picks[0].Path, ThinkModel);
        var thinkResults = await RunApprovedPhaseAsync(thinkSvc, picks, ThinkModel);
        WriteCsv(thinkResults, Path.Combine(Path.GetDirectoryName(samplesJson)!, "metrics_approved_think.csv"));

        _output.WriteLine("");
        _output.WriteLine("═══════════════════════════════════════════════");
        PrintSummary(baseResults, BaseModel);
        _output.WriteLine("");
        PrintSummary(thinkResults, ThinkModel);

        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var mdPath = Path.Combine(Path.GetDirectoryName(samplesJson)!, $"comparison_approved_{ts}.md");
        File.WriteAllText(mdPath, BuildComparisonReport(baseResults, thinkResults));
        _output.WriteLine($"\nReport: {mdPath}");

        Assert.True(baseResults.Count > 0);
    }

    private async Task<List<FrameResult>> RunApprovedPhaseAsync(
        EnhancedVisionAnalysisService svc,
        List<(string Code, string Path, string Source)> picks,
        string modelName)
    {
        var results = new List<FrameResult>();
        int idx = 0;
        foreach (var (code, path, source) in picks)
        {
            idx++;
            var fname = Path.GetFileName(path);
            var b64 = Convert.ToBase64String(File.ReadAllBytes(path));
            var t = DateTime.UtcNow;
            string pred = "";
            int severity = 0;
            try
            {
                var r = await svc.AnalyzeAsync(b64);
                pred = PickBestCode(r, code);
                severity = r?.Findings?.FirstOrDefault()?.Severity ?? 0;
            }
            catch (Exception ex)
            {
                pred = "ERR";
                _output.WriteLine($"  [{idx}] {fname}: {ex.GetType().Name}: {ex.Message[..Math.Min(80, ex.Message.Length)]}");
            }
            var timeMs = (DateTime.UtcNow - t).TotalMilliseconds;

            bool nullResp = string.IsNullOrEmpty(pred) || pred == "ERR";
            bool exact = !nullResp && pred == code;
            bool main = !nullResp && pred.Length >= 3 && code.Length >= 3 && pred[..3] == code[..3];
            bool group = !nullResp && pred.Length >= 2 && code.Length >= 2 && pred[..2] == code[..2];

            var kat = new[] { "BCD", "BCE", "BCAAA" }.Contains(code) ? "struktur" : "schaden";
            results.Add(new FrameResult(
                fname, code, code, kat, pred, exact, main, group, nullResp, false, timeMs, severity));

            if (idx % 10 == 0 || idx == picks.Count)
            {
                int hits = results.Count(r => r.MainHit);
                double avgMs = results.Average(r => r.TimeMs);
                _output.WriteLine($"  [{idx}/{picks.Count}] {modelName}: Hauptcode {hits}/{results.Count} ({100.0*hits/results.Count:F0}%)  avg {avgMs/1000:F1}s");
            }
        }
        return results;
    }

    [Fact]
    [Trait("Category", "GpuEval")]
    public async Task CompareBaseVsThinking_On120FrameEvalSet()
    {
        var imagesDir = Path.Combine(EvalDir, "images");
        var candidatesPath = Path.Combine(EvalDir, "_candidates.json");
        if (!Directory.Exists(imagesDir) || !File.Exists(candidatesPath))
        {
            _output.WriteLine($"Eval-Set unvollstaendig — Test uebersprungen. Erwartet: {imagesDir} + {candidatesPath}");
            return;
        }

        // Ground-Truth laden (via basename, nicht frame_path)
        var cands = JsonSerializer.Deserialize<List<Candidate>>(
            File.ReadAllText(candidatesPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var gtByName = cands.ToDictionary(
            c => Path.GetFileName(c.frame_path),
            c => c,
            StringComparer.OrdinalIgnoreCase);

        var images = Directory.GetFiles(imagesDir, "*.png").OrderBy(f => f).ToList();
        _output.WriteLine($"Eval-Set: {images.Count} Frames, {gtByName.Count} Ground-Truth-Eintraege");
        _output.WriteLine($"Modelle: {BaseModel}  vs  {ThinkModel}");
        _output.WriteLine($"Eskalation: AUS (referenceModel=null)");
        _output.WriteLine("");

        var config = OllamaConfig.Load();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

        // Phase 1: Base-Modell
        _output.WriteLine("═══ Phase 1: BASE ═══");
        var baseClient = new OllamaClient(config.BaseUri, http, keepAlive: "24h", numCtx: 8192);
        var baseSvc = new EnhancedVisionAnalysisService(baseClient, BaseModel, referenceModel: null, numCtx: 8192);
        await WarmupAsync(baseSvc, images[0], BaseModel);
        var baseResults = await RunPhaseAsync(baseSvc, images, gtByName, BaseModel, checkPlausibility: true);
        WriteCsv(baseResults, Path.Combine(EvalDir, "metrics_qwen3_vl_8b_q8.csv"));

        // Plausibility-Check
        if (baseResults.Count >= PlausibilityCheckAt)
        {
            var first30 = baseResults.Take(PlausibilityCheckAt).ToList();
            double mainRate = first30.Count(r => r.MainHit) / (double)first30.Count;
            if (mainRate < PlausibilityThreshold)
            {
                _output.WriteLine($"  ⚠ ABBRUCH: Base nach {PlausibilityCheckAt} Frames bei {mainRate:P0} Hauptcode — Test-Pipeline-Bug vermutet.");
                _output.WriteLine($"  → Thinking-Phase uebersprungen. Investigation erforderlich.");
                PrintSummary(baseResults, BaseModel);
                return;
            }
        }

        // Phase 2: Thinking-Modell
        _output.WriteLine("");
        _output.WriteLine("═══ Phase 2: THINKING ═══");
        var thinkClient = new OllamaClient(config.BaseUri, http, keepAlive: "24h", numCtx: 8192);
        var thinkSvc = new EnhancedVisionAnalysisService(thinkClient, ThinkModel, referenceModel: null, numCtx: 8192);
        await WarmupAsync(thinkSvc, images[0], ThinkModel);
        var thinkResults = await RunPhaseAsync(thinkSvc, images, gtByName, ThinkModel, checkPlausibility: false);
        WriteCsv(thinkResults, Path.Combine(EvalDir, "metrics_qwen3_vl_8b_thinking.csv"));

        _output.WriteLine("");
        _output.WriteLine("═══════════════════════════════════════════════");
        PrintSummary(baseResults, BaseModel);
        _output.WriteLine("");
        PrintSummary(thinkResults, ThinkModel);

        // Vergleichs-Markdown schreiben
        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var mdPath = Path.Combine(EvalDir, $"comparison_{ts}.md");
        File.WriteAllText(mdPath, BuildComparisonReport(baseResults, thinkResults));
        _output.WriteLine($"\nReport: {mdPath}");

        Assert.True(baseResults.Count > 0);
    }

    /// <summary>
    /// Waehlt aus Findings-Array das beste Code-Match fuer die Ground-Truth.
    /// Der Production-Prompt verlangt Pflicht-Meldungen (BCD/BCE/BCA/BCC) bei jedem
    /// Axial-Frame — diese duerfen echte Schaden-Findings nicht ueberdecken.
    ///
    /// Strategie:
    ///   1. Wenn GT in {BCD, BCE, BCA, BCC} (Strukturcode): nimm diesen Code falls vorhanden
    ///   2. Sonst: nimm das erste Finding das NICHT Strukturcode ist (echter Schaden)
    ///   3. Fallback: nimm Findings[0] wie bisher
    /// </summary>
    private static string PickBestCode(EnhancedFrameAnalysis? r, string gtMain)
    {
        if (r?.Findings == null || r.Findings.Count == 0) return "";
        var gtUpper = gtMain.ToUpperInvariant();
        bool gtIsStructure = gtUpper.StartsWith("BCD") || gtUpper.StartsWith("BCE")
                          || gtUpper.StartsWith("BCA") || gtUpper.StartsWith("BCC");
        var structCodes = new[] { "BCD", "BCE", "BCA", "BCC" };

        // Fall 1: GT ist Strukturcode → nimm diesen Code falls vorhanden
        if (gtIsStructure)
        {
            foreach (var f in r.Findings)
            {
                var hint = (f.VsaCodeHint ?? "").ToUpperInvariant();
                if (hint.Length >= 3 && hint[..3] == gtUpper[..3])
                    return hint;
            }
        }
        // Fall 2: GT ist Schaden → suche erstes Nicht-Struktur-Finding
        else
        {
            foreach (var f in r.Findings)
            {
                var hint = (f.VsaCodeHint ?? "").ToUpperInvariant();
                if (hint.Length < 2) continue;
                bool isStruct = structCodes.Any(s => hint.StartsWith(s));
                if (!isStruct) return hint;
            }
        }
        // Fallback
        return (r.Findings[0].VsaCodeHint ?? "").ToUpperInvariant();
    }

    private async Task WarmupAsync(EnhancedVisionAnalysisService svc, string imagePath, string modelName)
    {
        var b64 = Convert.ToBase64String(File.ReadAllBytes(imagePath));
        try
        {
            var t = DateTime.UtcNow;
            _ = await svc.AnalyzeAsync(b64);
            _output.WriteLine($"  Warmup {modelName}: {(DateTime.UtcNow - t).TotalSeconds:F1}s");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  Warmup {modelName}: FEHLER {ex.Message}");
        }
    }

    private async Task<List<FrameResult>> RunPhaseAsync(
        EnhancedVisionAnalysisService svc,
        List<string> images,
        Dictionary<string, Candidate> gtByName,
        string modelName,
        bool checkPlausibility)
    {
        var results = new List<FrameResult>();
        int idx = 0;
        foreach (var imgPath in images)
        {
            idx++;
            var fname = Path.GetFileName(imgPath);
            if (!gtByName.TryGetValue(fname, out var gt)) continue;

            var b64 = Convert.ToBase64String(File.ReadAllBytes(imgPath));
            var t = DateTime.UtcNow;
            string pred = "";
            int severity = 0;
            try
            {
                var r = await svc.AnalyzeAsync(b64);
                pred = PickBestCode(r, gt.code_main);
                severity = r?.Findings?.FirstOrDefault()?.Severity ?? 0;
            }
            catch (Exception ex)
            {
                pred = $"ERR";
                _output.WriteLine($"  [{idx}] {fname}: {ex.GetType().Name}: {ex.Message[..Math.Min(80, ex.Message.Length)]}");
            }
            var timeMs = (DateTime.UtcNow - t).TotalMilliseconds;

            bool isNegativ = gt.kategorie == "negativ";
            bool nullResp = string.IsNullOrEmpty(pred) || pred == "ERR";
            bool exact = !nullResp && pred == gt.code_full.ToUpperInvariant();
            bool main = !nullResp && pred.Length >= 3 && gt.code_main.Length >= 3
                        && pred[..3] == gt.code_main[..3].ToUpperInvariant();
            bool group = !nullResp && pred.Length >= 2 && gt.code_main.Length >= 2
                         && pred[..2] == gt.code_main[..2].ToUpperInvariant();
            bool negativCorrect = isNegativ && nullResp;

            results.Add(new FrameResult(
                fname, gt.code_full, gt.code_main, gt.kategorie,
                pred, exact, main, group, nullResp, negativCorrect, timeMs, severity));

            if (idx % 10 == 0 || idx == images.Count)
            {
                int hits = results.Count(r => r.MainHit);
                double avgMs = results.Average(r => r.TimeMs);
                _output.WriteLine($"  [{idx}/{images.Count}] {modelName}: Hauptcode {hits}/{results.Count} ({100.0*hits/results.Count:F0}%)  avg {avgMs/1000:F1}s");
            }
        }
        return results;
    }

    private static void WriteCsv(List<FrameResult> results, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("frame,gt_full,gt_main,kategorie,pred,exact,main,group,null_resp,negativ_correct,time_ms,severity");
        foreach (var r in results)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                r.Frame, r.GtCodeFull, r.GtCodeMain, r.Kategorie, r.PredCode,
                r.ExactHit.ToString(), r.MainHit.ToString(), r.GroupHit.ToString(),
                r.NullResponse.ToString(), r.NegativCorrect.ToString(),
                r.TimeMs.ToString("F0", CultureInfo.InvariantCulture),
                r.Severity.ToString(CultureInfo.InvariantCulture)
            }));
        }
        File.WriteAllText(path, sb.ToString());
    }

    private void PrintSummary(List<FrameResult> results, string modelName)
    {
        int n = results.Count;
        if (n == 0) { _output.WriteLine($"{modelName}: keine Ergebnisse"); return; }

        var nonNeg = results.Where(r => r.Kategorie != "negativ").ToList();
        var neg = results.Where(r => r.Kategorie == "negativ").ToList();

        _output.WriteLine($"Ergebnis — {modelName} ({n} Frames):");
        _output.WriteLine($"  Exakt (Code full):     {results.Count(r => r.ExactHit)}/{n}  = {100.0 * results.Count(r => r.ExactHit) / n:F1}%");
        _output.WriteLine($"  Hauptcode (3 Z.):      {results.Count(r => r.MainHit)}/{n}  = {100.0 * results.Count(r => r.MainHit) / n:F1}%");
        _output.WriteLine($"  Gruppe (2 Z.):         {results.Count(r => r.GroupHit)}/{n}  = {100.0 * results.Count(r => r.GroupHit) / n:F1}%");
        _output.WriteLine($"  Null-Antworten:        {results.Count(r => r.NullResponse)}/{n}  = {100.0 * results.Count(r => r.NullResponse) / n:F1}%");
        if (neg.Count > 0)
            _output.WriteLine($"  Negativ korrekt:       {neg.Count(r => r.NegativCorrect)}/{neg.Count}  = {100.0 * neg.Count(r => r.NegativCorrect) / neg.Count:F1}%");
        if (nonNeg.Count > 0)
        {
            _output.WriteLine($"  (Nicht-Negativ) Hauptcode: {nonNeg.Count(r => r.MainHit)}/{nonNeg.Count}  = {100.0 * nonNeg.Count(r => r.MainHit) / nonNeg.Count:F1}%");
        }

        var times = results.Select(r => r.TimeMs).OrderBy(t => t).ToList();
        double mean = times.Average();
        double p50 = times[times.Count / 2];
        double p95 = times[(int)(times.Count * 0.95)];
        _output.WriteLine($"  Zeit (ms):   mean={mean:F0}  p50={p50:F0}  p95={p95:F0}");
    }

    private static string BuildComparisonReport(List<FrameResult> baseR, List<FrameResult> thinkR)
    {
        double BaseHit(Func<FrameResult,bool> f) => baseR.Count > 0 ? 100.0 * baseR.Count(f) / baseR.Count : 0;
        double ThinkHit(Func<FrameResult,bool> f) => thinkR.Count > 0 ? 100.0 * thinkR.Count(f) / thinkR.Count : 0;

        var sb = new StringBuilder();
        sb.AppendLine($"# Qwen 8b-q8 vs 8b-thinking — Vergleich");
        sb.AppendLine();
        sb.AppendLine($"**Stand:** {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"**Eval-Set:** 120 Frames aus `C:\\KI_BRAIN\\eval_set\\images\\`");
        sb.AppendLine($"**GT-Quelle:** `_candidates.json` (Operateur-SDF)");
        sb.AppendLine($"**Eskalation:** aus");
        sb.AppendLine();
        sb.AppendLine("## Genauigkeit");
        sb.AppendLine();
        sb.AppendLine("| Metrik | Base (q8) | Thinking | Delta |");
        sb.AppendLine("|---|---:|---:|---:|");
        sb.AppendLine($"| Exakt | {BaseHit(r => r.ExactHit):F1}% | {ThinkHit(r => r.ExactHit):F1}% | {ThinkHit(r => r.ExactHit) - BaseHit(r => r.ExactHit):+0.0;-0.0;0.0} |");
        sb.AppendLine($"| Hauptcode (3 Z.) | {BaseHit(r => r.MainHit):F1}% | {ThinkHit(r => r.MainHit):F1}% | {ThinkHit(r => r.MainHit) - BaseHit(r => r.MainHit):+0.0;-0.0;0.0} |");
        sb.AppendLine($"| Gruppe (2 Z.) | {BaseHit(r => r.GroupHit):F1}% | {ThinkHit(r => r.GroupHit):F1}% | {ThinkHit(r => r.GroupHit) - BaseHit(r => r.GroupHit):+0.0;-0.0;0.0} |");
        sb.AppendLine($"| Null-Antwort | {BaseHit(r => r.NullResponse):F1}% | {ThinkHit(r => r.NullResponse):F1}% | {ThinkHit(r => r.NullResponse) - BaseHit(r => r.NullResponse):+0.0;-0.0;0.0} |");
        sb.AppendLine();
        sb.AppendLine("## Zeit");
        sb.AppendLine();
        double BaseMean = baseR.Count > 0 ? baseR.Average(r => r.TimeMs) : 0;
        double ThinkMean = thinkR.Count > 0 ? thinkR.Average(r => r.TimeMs) : 0;
        sb.AppendLine($"- Base: {BaseMean / 1000:F1} s / Frame");
        sb.AppendLine($"- Thinking: {ThinkMean / 1000:F1} s / Frame");
        sb.AppendLine($"- Faktor: {(BaseMean > 0 ? ThinkMean / BaseMean : 0):F1}×");
        sb.AppendLine();
        sb.AppendLine("## Nach Kategorie (Hauptcode-Treffer)");
        sb.AppendLine();
        foreach (var kat in new[] { "top5_code", "verwechslungspaar", "auffuellung", "negativ" })
        {
            var bk = baseR.Where(r => r.Kategorie == kat).ToList();
            var tk = thinkR.Where(r => r.Kategorie == kat).ToList();
            if (bk.Count == 0) continue;
            double bp = kat == "negativ"
                ? 100.0 * bk.Count(r => r.NegativCorrect) / bk.Count
                : 100.0 * bk.Count(r => r.MainHit) / bk.Count;
            double tp = tk.Count == 0 ? 0 : (kat == "negativ"
                ? 100.0 * tk.Count(r => r.NegativCorrect) / tk.Count
                : 100.0 * tk.Count(r => r.MainHit) / tk.Count);
            sb.AppendLine($"- **{kat}** ({bk.Count}): Base {bp:F1}% vs Thinking {tp:F1}%");
        }
        sb.AppendLine();
        sb.AppendLine("## Empfehlung");
        sb.AppendLine();
        double mainDelta = ThinkHit(r => r.MainHit) - BaseHit(r => r.MainHit);
        double nullDelta = BaseHit(r => r.NullResponse) - ThinkHit(r => r.NullResponse);
        if (mainDelta >= 10)
            sb.AppendLine($"- Thinking liefert {mainDelta:F1}% mehr Hauptcode-Treffer → **Einsatz als Eskalationsmodell erwaegen** trotz Zeit-Overhead.");
        else if (mainDelta >= 5 && nullDelta >= 15)
            sb.AppendLine($"- Thinking nur {mainDelta:F1}% besser bei Hauptcode, aber {nullDelta:F1}% weniger Null-Antworten → **Potenziell fuer Eskalation nuetzlich** (liefert ueberhaupt Output).");
        else if (mainDelta < -5)
            sb.AppendLine($"- Thinking ist {-mainDelta:F1}% schlechter → **NICHT einsetzen**.");
        else
            sb.AppendLine($"- Delta {mainDelta:+0.0;-0.0;0.0}% — **kein signifikanter Vorteil fuer Thinking**. Zeit-Overhead nicht gerechtfertigt. NICHT einsetzen.");
        return sb.ToString();
    }
}
