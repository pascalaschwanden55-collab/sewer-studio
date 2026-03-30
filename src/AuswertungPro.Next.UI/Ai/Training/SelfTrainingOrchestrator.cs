// AuswertungPro – Selbststaendiges KI-Training Orchestrator (v3: PDF-Foto-basiert)
// Das PDF-Protokoll ist massgebend. Nur Eintraege MIT eingebettetem Foto werden trainiert.
// KI analysiert das Foto blind → deterministischer Vergleich mit Protokoll.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.UI.Ai.Training.Services;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Durchlaeuft das PDF-Protokoll, nimmt die eingebetteten Fotos als Ground Truth,
/// laesst die KI blind analysieren und vergleicht deterministisch.
/// </summary>
public interface ISelfTrainingOrchestrator
{
    /// <summary>Startet autonomes Training fuer einen Fall.</summary>
    Task<SelfTrainingResult> RunAsync(
        TrainingCase tc,
        IProgress<SelfTrainingStep> progress,
        CancellationToken ct);

    /// <summary>Pausiert den laufenden Trainingslauf.</summary>
    void Pause();

    /// <summary>Setzt nach Pause fort.</summary>
    void Resume();

    /// <summary>True wenn gerade pausiert.</summary>
    bool IsPaused { get; }
}

public sealed class SelfTrainingOrchestrator : ISelfTrainingOrchestrator
{
    private readonly EnhancedVisionAnalysisService _vision;
    private readonly ISelfTrainingComparisonService _comparison;
    private readonly ITechniqueAssessmentService _technique;
    private readonly PdfProtocolExtractor _pdfExtractor;
    private readonly SampleQualityGateService _qualityGate;
    private readonly int _gpuConcurrency;
    private readonly ILogger<SelfTrainingOrchestrator>? _logger;

    private readonly ManualResetEventSlim _pauseGate = new(true);

    public bool IsPaused => !_pauseGate.IsSet;

    public SelfTrainingOrchestrator(
        EnhancedVisionAnalysisService vision,
        ISelfTrainingComparisonService comparison,
        ITechniqueAssessmentService technique,
        PdfProtocolExtractor pdfExtractor,
        TrainingCenterSettings? settings = null,
        SampleQualityGateService? qualityGate = null,
        ILogger<SelfTrainingOrchestrator>? logger = null)
    {
        _vision = vision;
        _comparison = comparison;
        _technique = technique;
        _pdfExtractor = pdfExtractor;
        _gpuConcurrency = Math.Max(1, (settings ?? new TrainingCenterSettings()).GpuConcurrency);
        _qualityGate = qualityGate ?? new SampleQualityGateService();
        _logger = logger;
    }

    public void Pause() => _pauseGate.Reset();
    public void Resume() => _pauseGate.Set();

    public async Task<SelfTrainingResult> RunAsync(
        TrainingCase tc,
        IProgress<SelfTrainingStep> progress,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // 1. Protokoll-Eintraege MIT Fotos extrahieren
        string framesDir = Path.Combine(tc.FolderPath, "self_training_frames");
        Directory.CreateDirectory(framesDir);

        progress.Report(new SelfTrainingStep(
            0, 1, "", 0, SelfTrainingStage.BuildingTimeline, null, null, null,
            "PDF-Protokoll wird gelesen..."));

        var allEntries = await _pdfExtractor.ExtractAsync(tc.ProtocolPath, framesDir, ct);

        progress.Report(new SelfTrainingStep(
            0, allEntries.Count, "", 0, SelfTrainingStage.BuildingTimeline, null, null, null,
            $"{allEntries.Count} Protokoll-Eintraege gefunden"));

        // Nur Eintraege mit Foto behalten — das sind unsere Trainingsbilder
        var entries = allEntries
            .Where(e => !string.IsNullOrEmpty(e.ExtractedFramePath)
                        && File.Exists(e.ExtractedFramePath))
            .ToList();

        if (entries.Count == 0)
        {
            return new SelfTrainingResult(tc.CaseId, allEntries.Count, 0, 0, 0, 0, null, sw.Elapsed, 0);
        }

        // 2. Aufnahmetechnik EINMAL mit dem ersten Frame bewerten (Qwen-basiert)
        TechniqueAssessment? overallTechnique = null;
        if (entries.Count > 0)
        {
            var firstEntry = entries[0];
            var firstPath = firstEntry.ExtractedFramePath!;
            try
            {
                var firstBytes = await File.ReadAllBytesAsync(firstPath, ct);
                var firstB64 = Convert.ToBase64String(firstBytes);
                var firstAnalysis = await _vision.AnalyzeAsync(firstB64, ct);
                overallTechnique = await _technique.AssessFrameWithVisionAsync(
                    firstBytes, firstAnalysis.Meter, firstEntry.MeterStart, ct);
            }
            catch
            {
                // Fallback: deterministisch ohne Qwen
                try
                {
                    var firstBytes = await File.ReadAllBytesAsync(firstPath, ct);
                    overallTechnique = _technique.AssessFrame(firstBytes, 0, firstEntry.MeterStart);
                }
                catch { /* Technik-Bewertung nicht moeglich */ }
            }
        }

        // 3. Alle Entries PARALLEL verarbeiten (GPU-Concurrency konfigurierbar)
        int exactMatches = 0, partialMatches = 0, mismatches = 0, noFindings = 0;
        var generatedSamples = new System.Collections.Concurrent.ConcurrentBag<TrainingSample>();
        int gpuConcurrency = _gpuConcurrency;
        int completedCount = 0;

        progress.Report(new SelfTrainingStep(
            0, entries.Count, "", 0, SelfTrainingStage.Analyzing, null, null, null,
            $"Parallele KI-Analyse: {gpuConcurrency} gleichzeitige Requests..."));

        await Parallel.ForEachAsync(
            entries.Select((e, i) => (Entry: e, Index: i)),
            new ParallelOptions { MaxDegreeOfParallelism = gpuConcurrency, CancellationToken = ct },
            async (item, token) =>
        {
            _pauseGate.Wait(token);
            var (entry, i) = item;
            string framePath = entry.ExtractedFramePath!;

            // ── Foto laden ──
            progress.Report(new SelfTrainingStep(
                i, entries.Count, entry.VsaCode, entry.MeterStart,
                SelfTrainingStage.ExtractingFrame, null, null, framePath));

            byte[] pngBytes;
            try
            {
                pngBytes = await File.ReadAllBytesAsync(framePath, token);
            }
            catch
            {
                return; // Skip
            }

            // ── Blinde KI-Analyse (weiss NICHTS vom Protokoll) ──
            progress.Report(new SelfTrainingStep(
                i, entries.Count, entry.VsaCode, entry.MeterStart,
                SelfTrainingStage.Analyzing, null, null, framePath));

            string b64 = Convert.ToBase64String(pngBytes);
            bool isPdfPhoto = framePath.Contains("self_training_frames", StringComparison.OrdinalIgnoreCase);

            EnhancedFrameAnalysis analysis;
            try
            {
                analysis = await _vision.AnalyzeAsync(b64, token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var errMsg = $"[SelfTraining] EXCEPTION bei {entry.VsaCode}@{entry.MeterStart:F1}m: {ex.GetType().Name}: {ex.Message}";
                _logger?.LogWarning(ex, "Selbsttraining KI-Analyse fehlgeschlagen: {Code}@{Meter:F1}m",
                    entry.VsaCode, entry.MeterStart);
                LogToFile(errMsg);
                progress.Report(new SelfTrainingStep(
                    i, entries.Count, entry.VsaCode, entry.MeterStart,
                    SelfTrainingStage.Analyzing, null, null, framePath,
                    ErrorMessage: errMsg));
                analysis = EnhancedFrameAnalysis.Empty(ex.Message);
            }

            if (analysis.Error is not null)
            {
                var errMsg = $"KI-Fehler: {analysis.Error}";
                _logger?.LogWarning("Selbsttraining KI-Fehler: {Code}@{Meter:F1}m: {Error}",
                    entry.VsaCode, entry.MeterStart, analysis.Error);
                LogToFile($"[SelfTraining] {entry.VsaCode}@{entry.MeterStart:F1}m: {errMsg}");
            }

            // ── Deterministischer Vergleich ──
            var comparison = _comparison.Compare(entry, analysis, isPdfPhoto: isPdfPhoto);

            // ── Aufnahmetechnik (deterministisch, Qwen wurde oben einmalig gemacht) ──
            var technique = _technique.AssessFrame(pngBytes, analysis.Meter, entry.MeterStart);

            // ── Thread-safe Zaehler ──
            switch (comparison.Level)
            {
                case MatchLevel.ExactMatch: Interlocked.Increment(ref exactMatches); break;
                case MatchLevel.PartialMatch: Interlocked.Increment(ref partialMatches); break;
                case MatchLevel.Mismatch: Interlocked.Increment(ref mismatches); break;
                case MatchLevel.NoFindings: Interlocked.Increment(ref noFindings); break;
            }

            // ── TrainingSample erzeugen ──
            var meterCenter = (entry.MeterStart + entry.MeterEnd) / 2.0;
            var sample = new TrainingSample
            {
                SampleId = $"{tc.CaseId}_st_{i:D3}",
                CaseId = tc.CaseId,
                Code = entry.VsaCode,
                Beschreibung = entry.Text,
                MeterStart = entry.MeterStart,
                MeterEnd = entry.MeterEnd,
                IsStreckenschaden = entry.IsStreckenschaden,
                TimeSeconds = 0,
                DetectedMeter = analysis.Meter,
                MeterSource = "Protokoll",
                FramePath = framePath,
                Status = comparison.Level == MatchLevel.ExactMatch
                    ? TrainingSampleStatus.Approved
                    : TrainingSampleStatus.New,
                KbIndexState = comparison.Level == MatchLevel.ExactMatch
                    ? KbIndexState.Pending
                    : KbIndexState.None,
                TruthMeterCenter = meterCenter,
                OdsDeltaMeters = technique?.OsdDeltaMeters,
                HasOsdMismatch = technique?.OsdDeltaMeters > 5.0,
                Signature = TrainingSample.BuildCanonicalSignature(tc.CaseId, entry.VsaCode, meterCenter, entry.MeterEnd),
                MatchLevel = comparison.Level.ToString(),
                KiCode = comparison.BestMatchCode,
                SourceType = SourceTypeNames.PdfPhoto,
                TechniqueGrade = technique?.OverallGrade
            };
            generatedSamples.Add(sample);

            // ── Fortschritt melden ──
            var done = Interlocked.Increment(ref completedCount);
            progress.Report(new SelfTrainingStep(
                done - 1, entries.Count, entry.VsaCode, entry.MeterStart,
                SelfTrainingStage.Completed, comparison, technique, framePath));
        });

        // QualityGate: nur akzeptierte Samples speichern
        var samplesList = generatedSamples.ToList();
        var samplesAccepted = 0;
        if (samplesList.Count > 0)
        {
            var qgBatch = _qualityGate.EvaluateBatch(samplesList);
            if (qgBatch.Red > 0)
            {
                _logger?.LogWarning(
                    "QualityGate: {Count} Samples abgelehnt (Red) fuer {CaseId}",
                    qgBatch.Red, tc.CaseId);
            }
            var accepted = qgBatch.Accepted.ToList();
            samplesAccepted = accepted.Count;
            if (accepted.Count > 0)
                await TrainingSamplesStore.MergeAndSaveAsync(accepted);
        }

        sw.Stop();
        return new SelfTrainingResult(
            CaseId: tc.CaseId,
            TotalEntries: allEntries.Count,
            ExactMatches: exactMatches,
            PartialMatches: partialMatches,
            Mismatches: mismatches,
            NoFindings: noFindings,
            OverallTechnique: overallTechnique,
            Duration: sw.Elapsed,
            SamplesGenerated: samplesAccepted);
    }

    /// <summary>
    private static void LogToFile(string message)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SewerStudio", "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(
                Path.Combine(logDir, "selftraining_errors.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}\n");
        }
        catch { /* Logging darf nie crashen */ }
    }
}
