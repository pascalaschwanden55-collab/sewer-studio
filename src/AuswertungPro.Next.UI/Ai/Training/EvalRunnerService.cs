// V4.2 Qualitaetshebel #1: Eval-Set automatisch durchlaufen.
// Liest C:\KI_BRAIN\eval_set\images/*.png + labels/*.txt,
// laesst jedes Frame durch Qwen analysieren und berechnet F1 pro VSA-Code.
// Schreibt CSV + Zusammenfassung — damit Regressionen nach Commits sofort sichtbar werden.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// V4.2: Laeuft das frozen 120-Frame Eval-Set durch Qwen und misst F1/Precision/Recall
/// pro VSA-Code. Schreibt CSV mit Datum + optional Git-Commit.
///
/// Usage:
///   var runner = new EvalRunnerService(qwen);
///   var result = await runner.RunAsync(@"C:\KI_BRAIN\eval_set", logger);
///   // result.CsvPath enthaelt den Report
/// </summary>
public sealed class EvalRunnerService
{
    private readonly EnhancedVisionAnalysisService _qwen;

    public EvalRunnerService(EnhancedVisionAnalysisService qwen)
    {
        _qwen = qwen ?? throw new ArgumentNullException(nameof(qwen));
    }

    /// <summary>
    /// Fuehrt den Eval-Lauf aus.
    /// </summary>
    /// <param name="evalSetDir">Pfad zum eval_set-Ordner mit images/ und labels/ Unterordnern.</param>
    /// <param name="progress">Optionaler Fortschritts-Callback (done, total, message).</param>
    /// <param name="ct">Cancellation Token.</param>
    public async Task<EvalRunResult> RunAsync(
        string evalSetDir,
        Action<int, int, string>? progress = null,
        CancellationToken ct = default)
    {
        var imagesDir = Path.Combine(evalSetDir, "images");
        var labelsDir = Path.Combine(evalSetDir, "labels");
        if (!Directory.Exists(imagesDir) || !Directory.Exists(labelsDir))
            throw new DirectoryNotFoundException(
                $"Eval-Set unvollstaendig: {evalSetDir} (benoetigt images/ + labels/)");

        var imageFiles = Directory.GetFiles(imagesDir, "*.png")
            .Concat(Directory.GetFiles(imagesDir, "*.jpg"))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (imageFiles.Count == 0)
            throw new InvalidOperationException($"Keine Eval-Frames in {imagesDir}");

        // Ground Truth laden (YOLO-Label → class_id → VSA-Hauptcode)
        var samples = new List<EvalSample>(imageFiles.Count);
        foreach (var imgPath in imageFiles)
        {
            var labelPath = Path.Combine(
                labelsDir,
                Path.ChangeExtension(Path.GetFileName(imgPath), ".txt"));
            var expected = LoadExpectedCode(labelPath);
            samples.Add(new EvalSample(imgPath, expected));
        }

        progress?.Invoke(0, samples.Count,
            $"Eval startet: {samples.Count} Frames aus {Path.GetFileName(evalSetDir)}");

        // Pro Frame: Qwen-Analyse, Top-Finding-Code extrahieren
        int done = 0;
        foreach (var s in samples)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var bytes = await File.ReadAllBytesAsync(s.ImagePath, ct).ConfigureAwait(false);
                var b64 = Convert.ToBase64String(bytes);
                var analysis = await _qwen.AnalyzeAsync(b64, ct).ConfigureAwait(false);
                var top = analysis.Findings.FirstOrDefault();
                var predictedRaw = top?.VsaCodeHint ?? top?.Label ?? "";
                s.PredictedCode = ExtractMainCode(predictedRaw);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                s.PredictedCode = "ERROR";
                s.Error = ex.Message;
            }
            done++;
            progress?.Invoke(done, samples.Count,
                $"{done}/{samples.Count}: {Path.GetFileName(s.ImagePath)} → {s.PredictedCode ?? "-"} (erwartet {s.ExpectedCode ?? "-"})");
        }

        // Metriken berechnen pro Code
        var perCode = ComputePerCodeMetrics(samples);

        // CSV + Summary schreiben
        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AuswertungPro", "eval_results");
        Directory.CreateDirectory(outputDir);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var commit = TryGetGitCommit();
        var commitSuffix = commit is null ? "" : $"_{commit[..Math.Min(7, commit.Length)]}";
        var csvPath = Path.Combine(outputDir, $"eval_{stamp}{commitSuffix}.csv");

        WriteCsv(csvPath, samples, perCode, commit);

        var overall = ComputeOverallMetrics(samples);

        return new EvalRunResult(
            CsvPath: csvPath,
            TotalFrames: samples.Count,
            CorrectPredictions: samples.Count(s => s.IsCorrect),
            OverallPrecision: overall.precision,
            OverallRecall: overall.recall,
            OverallF1: overall.f1,
            PerCode: perCode,
            GitCommit: commit);
    }

    // ── Hilfsfunktionen ──

    internal static string? LoadExpectedCode(string labelPath)
    {
        if (!File.Exists(labelPath)) return null;
        var line = File.ReadAllLines(labelPath).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(line)) return null; // Negative (leere Datei)
        var first = line.Split(' ')[0];
        if (!int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out var classId))
            return null;
        return VsaYoloClassMap.GetVsaCodeForClassId(classId);
    }

    internal static string? ExtractMainCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var code = raw.Trim().ToUpperInvariant();
        // VSA-Codes beginnen mit 2 Buchstaben + Zahlen/Buchstaben. Wir nehmen die ersten 3 als Hauptcode.
        if (code.Length < 3) return code;
        return code[..3];
    }

    internal static Dictionary<string, PerCodeMetrics> ComputePerCodeMetrics(List<EvalSample> samples)
    {
        var codes = samples
            .Select(s => s.ExpectedCode)
            .Where(c => !string.IsNullOrEmpty(c))
            .Concat(samples.Select(s => s.PredictedCode).Where(c => !string.IsNullOrEmpty(c)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new Dictionary<string, PerCodeMetrics>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in codes)
        {
            int tp = samples.Count(s =>
                string.Equals(s.ExpectedCode, c, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.PredictedCode, c, StringComparison.OrdinalIgnoreCase));
            int fp = samples.Count(s =>
                !string.Equals(s.ExpectedCode, c, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.PredictedCode, c, StringComparison.OrdinalIgnoreCase));
            int fn = samples.Count(s =>
                string.Equals(s.ExpectedCode, c, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(s.PredictedCode, c, StringComparison.OrdinalIgnoreCase));
            int support = samples.Count(s =>
                string.Equals(s.ExpectedCode, c, StringComparison.OrdinalIgnoreCase));

            double precision = tp + fp > 0 ? (double)tp / (tp + fp) : 0;
            double recall = tp + fn > 0 ? (double)tp / (tp + fn) : 0;
            double f1 = precision + recall > 0 ? 2 * precision * recall / (precision + recall) : 0;

            result[c!] = new PerCodeMetrics(support, tp, fp, fn, precision, recall, f1);
        }
        return result;
    }

    internal static (double precision, double recall, double f1) ComputeOverallMetrics(List<EvalSample> samples)
    {
        int tp = samples.Count(s => s.IsCorrect && !string.IsNullOrEmpty(s.ExpectedCode));
        int fp = samples.Count(s =>
            !string.IsNullOrEmpty(s.PredictedCode)
            && s.PredictedCode != "ERROR"
            && !s.IsCorrect);
        int fn = samples.Count(s =>
            !string.IsNullOrEmpty(s.ExpectedCode)
            && !s.IsCorrect);

        double p = tp + fp > 0 ? (double)tp / (tp + fp) : 0;
        double r = tp + fn > 0 ? (double)tp / (tp + fn) : 0;
        double f = p + r > 0 ? 2 * p * r / (p + r) : 0;
        return (p, r, f);
    }

    internal static void WriteCsv(string path, List<EvalSample> samples,
        Dictionary<string, PerCodeMetrics> perCode, string? commit)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Eval-Run {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (!string.IsNullOrEmpty(commit))
            sb.AppendLine($"# Git-Commit: {commit}");
        sb.AppendLine($"# Samples: {samples.Count}");
        sb.AppendLine();

        // Pro-Code Metriken
        sb.AppendLine("Code;Support;TP;FP;FN;Precision;Recall;F1");
        foreach (var kv in perCode.OrderByDescending(x => x.Value.Support))
        {
            var m = kv.Value;
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{kv.Key};{m.Support};{m.TP};{m.FP};{m.FN};{m.Precision:F3};{m.Recall:F3};{m.F1:F3}"));
        }
        sb.AppendLine();

        // Pro-Frame Detail
        sb.AppendLine("File;Expected;Predicted;Match;Error");
        foreach (var s in samples)
        {
            var err = s.Error?.Replace(";", ",").Replace("\n", " ").Replace("\r", "") ?? "";
            sb.AppendLine($"{Path.GetFileName(s.ImagePath)};{s.ExpectedCode ?? ""};{s.PredictedCode ?? ""};{(s.IsCorrect ? 1 : 0)};{err}");
        }

        File.WriteAllText(path, sb.ToString());
    }

    internal static string? TryGetGitCommit()
    {
        try
        {
            var repoRoot = FindRepoRoot();
            if (repoRoot is null) return null;
            var headPath = Path.Combine(repoRoot, ".git", "HEAD");
            if (!File.Exists(headPath)) return null;
            var head = File.ReadAllText(headPath).Trim();
            if (head.StartsWith("ref:", StringComparison.Ordinal))
            {
                var refName = head[4..].Trim();
                var refPath = Path.Combine(repoRoot, ".git", refName);
                if (File.Exists(refPath)) return File.ReadAllText(refPath).Trim();
            }
            return head;
        }
        catch { return null; }
    }

    private static string? FindRepoRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    // ── DTOs ──

    internal sealed class EvalSample
    {
        public string ImagePath { get; }
        public string? ExpectedCode { get; }
        public string? PredictedCode { get; set; }
        public string? Error { get; set; }
        public bool IsCorrect => !string.IsNullOrEmpty(ExpectedCode)
            && string.Equals(ExpectedCode, PredictedCode, StringComparison.OrdinalIgnoreCase);

        public EvalSample(string imagePath, string? expectedCode)
        {
            ImagePath = imagePath;
            ExpectedCode = expectedCode;
        }
    }
}

/// <summary>Per-Code-Metriken nach einem Eval-Run.</summary>
public sealed record PerCodeMetrics(
    int Support,
    int TP, int FP, int FN,
    double Precision, double Recall, double F1);

/// <summary>Gesamt-Ergebnis eines Eval-Laufs.</summary>
public sealed record EvalRunResult(
    string CsvPath,
    int TotalFrames,
    int CorrectPredictions,
    double OverallPrecision,
    double OverallRecall,
    double OverallF1,
    IReadOnlyDictionary<string, PerCodeMetrics> PerCode,
    string? GitCommit);
