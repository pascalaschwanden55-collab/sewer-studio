// AuswertungPro – Selbststaendiges KI-Training Orchestrator (v3: PDF-Foto-basiert)
// Das PDF-Protokoll ist massgebend. Nur Eintraege MIT eingebettetem Foto werden trainiert.
// KI analysiert das Foto blind → deterministischer Vergleich mit Protokoll.
using System;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Training;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.CodeCatalog;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

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
    private readonly FewShotExampleStore _fewShotStore;
    private readonly SingleFrameMultiModelService? _multiModel;
    private readonly ReviewQueueService? _reviewQueue;
    private readonly int _gpuConcurrency;
    private readonly int _pipeDiameterMm;
    private readonly bool _autoApproveHighConfidenceCodeHits;
    private readonly double _partialAutoApproveMinScore;
    private readonly bool _enableGuidedVerification;
    private readonly int _guidedVerificationBudgetPerCase;
    private readonly ILogger<SelfTrainingOrchestrator>? _logger;

    private readonly ManualResetEventSlim _pauseGate = new(true);
    private bool _sidecarAvailable;

    // Codes die KEINE nuetzlichen Few-Shot-Beispiele sind (Admin/Steuercodes)
    // BCD/BCE/BCC NICHT mehr skippen — Few-Shot-Beispiele helfen Qwen
    // bei der Unterscheidung Rohranfang (BCD) vs Loch (BACB)
    private static readonly HashSet<string> _fewShotSkipCodes = new(StringComparer.OrdinalIgnoreCase)
        { "BDB", "BDA", "BDC", "BDD", "BDE", "BDF", "BDG",
          "AEC", "AED", "AEF" };

    public bool IsPaused => !_pauseGate.IsSet;

    public SelfTrainingOrchestrator(
        EnhancedVisionAnalysisService vision,
        ISelfTrainingComparisonService comparison,
        ITechniqueAssessmentService technique,
        PdfProtocolExtractor pdfExtractor,
        TrainingCenterSettings? settings = null,
        SingleFrameMultiModelService? multiModel = null,
        SampleQualityGateService? qualityGate = null,
        FewShotExampleStore? fewShotStore = null,
        ReviewQueueService? reviewQueue = null,
        ILogger<SelfTrainingOrchestrator>? logger = null)
    {
        _vision = vision;
        _comparison = comparison;
        _technique = technique;
        _pdfExtractor = pdfExtractor;
        _multiModel = multiModel;
        _reviewQueue = reviewQueue;
        var effectiveSettings = settings ?? new TrainingCenterSettings();
        _gpuConcurrency = Math.Max(1, effectiveSettings.GpuConcurrency);
        _autoApproveHighConfidenceCodeHits = effectiveSettings.AutoApproveHighConfidenceCodeHits;
        _partialAutoApproveMinScore = Math.Clamp(effectiveSettings.PartialAutoApproveMinScore, 0.0, 1.0);
        _enableGuidedVerification = effectiveSettings.EnableGuidedVerification;
        _guidedVerificationBudgetPerCase = Math.Max(0, effectiveSettings.GuidedVerificationBudgetPerCase);
        _pipeDiameterMm = 300;
        _qualityGate = qualityGate ?? new SampleQualityGateService();
        _fewShotStore = fewShotStore ?? new FewShotExampleStore();
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

        // 0. Sidecar-Verfuegbarkeit pruefen (YOLO/DINO/SAM)
        _sidecarAvailable = false;
        if (_multiModel is not null)
        {
            try
            {
                var cfg = AuswertungPro.Next.Application.Ai.PipelineConfigProvider.Load();
                using var sidecarHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var resp = await sidecarHttp.GetAsync(new Uri(cfg.SidecarUrl, "/health"), ct);
                _sidecarAvailable = resp.IsSuccessStatusCode;
            }
            catch { /* Sidecar nicht erreichbar */ }

            progress.Report(new SelfTrainingStep(
                0, 1, "", 0, SelfTrainingStage.BuildingTimeline, null, null, null,
                _sidecarAvailable
                    ? "Multi-Modell aktiv: YOLO + DINO + SAM + Qwen"
                    : "Sidecar nicht erreichbar — Fallback: nur Qwen"));
        }

        // 0b. Qwen-Modell vorladen (verhindert Timeout beim ersten Frame)
        try
        {
            progress.Report(new SelfTrainingStep(
                0, 1, "", 0, SelfTrainingStage.BuildingTimeline, null, null, null,
                "Qwen Vision-Modell wird geladen..."));

            var modelLoaded = await _vision.Client.EnsureModelLoadedAsync(
                _vision.ModelName, 0, ct: ct);

            if (!modelLoaded)
            {
                _logger?.LogWarning("Qwen-Modell konnte nicht geladen werden — Batch wird trotzdem versucht");
                progress.Report(new SelfTrainingStep(
                    0, 1, "", 0, SelfTrainingStage.BuildingTimeline, null, null, null,
                    "WARNUNG: Qwen konnte nicht geladen werden. Analyse kann langsam sein."));
            }
            else
            {
                progress.Report(new SelfTrainingStep(
                    0, 1, "", 0, SelfTrainingStage.BuildingTimeline, null, null, null,
                    "Qwen Vision-Modell bereit"));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Modell-Vorladen fehlgeschlagen");
        }

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
            return new SelfTrainingResult(tc.CaseId, allEntries.Count, 0, 0, 0, 0, 0, null, sw.Elapsed, 0);
        }

        // 2. Aufnahmetechnik EINMAL mit dem ersten Frame bewerten (Qwen-basiert)
        TechniqueAssessment? overallTechnique = null;
        if (entries.Count > 0)
        {
            var firstEntry = entries[0];
            var firstPath = firstEntry.ExtractedFramePath!;
            try
            {
                // Timeout fuer initiale Technik-Bewertung (kann haengen wenn Qwen langsam)
                using var techCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                techCts.CancelAfter(TimeSpan.FromSeconds(30));
                var firstBytes = await File.ReadAllBytesAsync(firstPath, techCts.Token);
                var firstB64 = Convert.ToBase64String(firstBytes);
                var firstAnalysis = await _vision.AnalyzeAsync(firstB64, techCts.Token);
                overallTechnique = await _technique.AssessFrameWithVisionAsync(
                    firstBytes, firstAnalysis.Meter, firstEntry.MeterStart, techCts.Token);
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
        int guidedBudgetRemaining = _guidedVerificationBudgetPerCase;
        GuidedVerificationService? guidedVerification = _enableGuidedVerification
            ? new GuidedVerificationService(_vision.Client, _vision.ModelName)
            : null;

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
                // Timeout pro Frame: 120s fuer Multi-Modell, 90s fuer Qwen-only
                // Erhoeht gegenueber 60/45s weil Ollama bei parallelen Requests
                // intern serialisiert und Wartezeiten von 30-60s normal sind
                using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                frameCts.CancelAfter(TimeSpan.FromSeconds(_sidecarAvailable ? 120 : 90));
                analysis = await AnalyzeFrameAsync(pngBytes, b64, frameCts.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                // Frame-Timeout (nicht Batch-Abbruch) → ueberspringen und weitermachen
                var errMsg = $"[SelfTraining] TIMEOUT bei {entry.VsaCode}@{entry.MeterStart:F1}m — Frame uebersprungen";
                _logger?.LogWarning("Selbsttraining Frame-Timeout: {Code}@{Meter:F1}m",
                    entry.VsaCode, entry.MeterStart);
                LogToFile(errMsg);
                progress.Report(new SelfTrainingStep(
                    i, entries.Count, entry.VsaCode, entry.MeterStart,
                    SelfTrainingStage.Analyzing, null, null, framePath,
                    ErrorMessage: errMsg));
                analysis = EnhancedFrameAnalysis.Empty("Frame-Timeout nach 120s");
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

            progress.Report(new SelfTrainingStep(
                i, entries.Count, entry.VsaCode, entry.MeterStart,
                SelfTrainingStage.Comparing, comparison, null, framePath));

            // ── Aufnahmetechnik (deterministisch, Qwen wurde oben einmalig gemacht) ──
            var technique = _technique.AssessFrame(pngBytes, analysis.Meter, entry.MeterStart);

            progress.Report(new SelfTrainingStep(
                i, entries.Count, entry.VsaCode, entry.MeterStart,
                SelfTrainingStage.AssessingTechnique, comparison, technique, framePath));

            var autoApproveCodeHit = _autoApproveHighConfidenceCodeHits
                && isPdfPhoto
                && comparison.Level == MatchLevel.PartialMatch
                && comparison.CodeMatched
                && comparison.ConfidenceScore >= _partialAutoApproveMinScore;

            var guidedApproved = false;
            if (!autoApproveCodeHit
                && guidedVerification is not null
                && ShouldAttemptGuidedVerification(comparison, isPdfPhoto)
                && TryConsumeBudget(ref guidedBudgetRemaining))
            {
                try
                {
                    var guided = await guidedVerification.VerifyAsync(pngBytes, entry, token);
                    var guidedCode = ResolveGuidedCode(guided);
                    if (GuidedVerificationSupportsApproval(entry, guided, guidedCode))
                        guidedApproved = true;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Guided Verification ist optional.
                }
            }

            var approvedByPolicy = comparison.Level == MatchLevel.ExactMatch
                || autoApproveCodeHit
                || guidedApproved;

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
                // Nur ExactMatch → Approved (strenger: PartialMatch koennte falscher Code sein,
                // z.B. durch zu lockeren Praefix-Match oder fehlende Clock/Meter-Validierung)
                Status = approvedByPolicy
                    ? TrainingSampleStatus.Approved
                    : TrainingSampleStatus.New,
                KbIndexState = approvedByPolicy
                    ? KbIndexState.Pending
                    : KbIndexState.None,
                TruthMeterCenter = meterCenter,
                OdsDeltaMeters = technique?.OsdDeltaMeters,
                HasOsdMismatch = technique?.OsdDeltaMeters > 5.0,
                Signature = TrainingSample.BuildCanonicalSignature(tc.CaseId, entry.VsaCode, meterCenter, entry.MeterEnd, entry.ClockPosition),
                MatchLevel = comparison.Level.ToString(),
                KiCode = comparison.BestMatchCode,
                SourceType = SourceTypeNames.PdfPhoto,
                TechniqueGrade = technique?.OverallGrade,
                Notes = BuildApprovalNotes(comparison, autoApproveCodeHit, guidedApproved)
            };
            generatedSamples.Add(sample);

            // ── Few-Shot: ExactMatch-Samples als Trainingsbeispiele speichern ──
            // Nur echte Schaeden (nicht BCD/BCE/BDB etc.), nur mit gutem Foto
            if ((comparison.Level == MatchLevel.ExactMatch || guidedApproved)
                && pngBytes.Length > 10_000
                && !_fewShotSkipCodes.Contains(entry.VsaCode.Replace(".", "").ToUpperInvariant()[..Math.Min(3, entry.VsaCode.Length)]))
            {
                try
                {
                    var clock = entry.ClockPosition;
                    await _fewShotStore.AddExampleAsync(
                        pngBytes, ".png", entry.VsaCode, entry.Text,
                        clock, entry.MeterStart, tc.Rohrmaterial, tc.Profil,
                        $"selftraining:{tc.CaseId}", 0.85, token);
                }
                catch { /* Few-Shot ist optional */ }
            }

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

            // PartialMatch- und Mismatch-Samples in die Review Queue einspeisen
            if (_reviewQueue is not null)
            {
                foreach (var s in accepted.Where(s =>
                    s.Status == TrainingSampleStatus.New
                    && (s.MatchLevel is MatchLevelNames.PartialMatch or MatchLevelNames.Mismatch)))
                {
                    _reviewQueue.EnqueueFromSelfTraining(
                        s.CaseId, s.Code, s.KiCode ?? "",
                        s.MeterStart, s.FramePath, s.MatchLevel!,
                        s.SampleId);
                }
            }
        }

        sw.Stop();
        return new SelfTrainingResult(
            CaseId: tc.CaseId,
            TotalEntries: allEntries.Count,
            ExactMatches: exactMatches,
            PartialMatches: partialMatches,
            Mismatches: mismatches,
            NoFindings: noFindings,
            CodeHits: exactMatches + partialMatches,
            OverallTechnique: overallTechnique,
            Duration: sw.Elapsed,
            SamplesGenerated: samplesAccepted);
    }

    /// <summary>
    /// Analysiert einen Frame mit der vollen Multi-Modell-Pipeline (YOLO→DINO→SAM→Qwen)
    /// oder nur mit Qwen wenn der Sidecar nicht verfuegbar ist.
    /// </summary>
    private async Task<EnhancedFrameAnalysis> AnalyzeFrameAsync(
        byte[] pngBytes, string b64, CancellationToken ct)
    {
        // Multi-Modell: YOLO -> DINO -> SAM -> Qwen (mit Kontext)
        if (_sidecarAvailable && _multiModel is not null)
        {
            try
            {
                var result = await _multiModel.AnalyzeFrameAsync(pngBytes, _pipeDiameterMm, null, ct);
                if (result.Error is null && result.IsRelevant)
                {
                    // Konvertiere SingleFrameResult -> MultiModelFrameResult fuer Qwen
                    var context = new MultiModelFrameResult(
                        TimestampSec: 0,
                        Meter: null,
                        IsRelevant: true,
                        DinoDetections: result.DinoDetections ?? Array.Empty<DinoDetectionDto>(),
                        SamMasks: result.SamResponse?.Masks ?? Array.Empty<SamMaskResult>(),
                        ImageWidth: result.SamResponse?.ImageWidth ?? 0,
                        ImageHeight: result.SamResponse?.ImageHeight ?? 0,
                        YoloTimeMs: result.YoloTimeMs,
                        DinoTimeMs: result.DinoTimeMs,
                        SamTimeMs: result.SamTimeMs);

                    var qwen = await _vision.AnalyzeWithContextAsync(b64, context, _pipeDiameterMm, ct);
                    return MergeWithSingleFrameFallback(qwen, result);
                }

                // YOLO sagt "nicht relevant" -> Qwen-only Fallback
                if (result.Error is null && !result.IsRelevant)
                    return await _vision.AnalyzeAsync(b64, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "Multi-Modell-Pipeline fehlgeschlagen, Fallback auf Qwen-only");
            }
        }

        // Fallback: nur Qwen
        return await _vision.AnalyzeAsync(b64, ct);
    }

    private static EnhancedFrameAnalysis MergeWithSingleFrameFallback(
        EnhancedFrameAnalysis qwen,
        SingleFrameResult multiModel)
    {
        var fallback = BuildFallbackFindings(multiModel);
        if (fallback.Count == 0)
            return qwen;

        if (!qwen.HasFindings)
            return qwen with { Findings = fallback, IsEmptyFrame = false };

        var merged = qwen.Findings.ToList();
        var signatures = new HashSet<string>(
            merged.Select(BuildFindingSignature),
            StringComparer.OrdinalIgnoreCase);

        foreach (var finding in fallback)
        {
            if (signatures.Add(BuildFindingSignature(finding)))
                merged.Add(finding);
        }

        return qwen with { Findings = merged, IsEmptyFrame = false };
    }

    private static IReadOnlyList<EnhancedFinding> BuildFallbackFindings(SingleFrameResult multiModel)
    {
        if (multiModel.QuantifiedMasks.Count == 0)
            return Array.Empty<EnhancedFinding>();

        var findings = new List<EnhancedFinding>(multiModel.QuantifiedMasks.Count);
        for (var i = 0; i < multiModel.QuantifiedMasks.Count; i++)
        {
            var q = multiModel.QuantifiedMasks[i];
            if (string.IsNullOrWhiteSpace(q.Label))
                continue;

            findings.Add(new EnhancedFinding(
                Label: q.Label,
                VsaCodeHint: VsaCodeResolver.InferCodeFromLabel(q.Label),
                Severity: EstimateSeverity(q),
                PositionClock: VsaCodeResolver.NormalizeClock(q.ClockPosition),
                ExtentPercent: q.ExtentPercent,
                HeightMm: q.HeightMm,
                WidthMm: q.WidthMm,
                IntrusionPercent: q.IntrusionPercent,
                CrossSectionReductionPercent: q.CrossSectionReductionPercent,
                DiameterReductionMm: null,
                Notes: "Fallback aus DINO/SAM"));
        }

        return findings;
    }

    private static string BuildFindingSignature(EnhancedFinding finding)
    {
        var code = VsaCodeResolver.NormalizeFindingCode(finding.VsaCodeHint)
            ?? VsaCodeResolver.InferCodeFromLabel(finding.Label)
            ?? finding.Label.Trim().ToUpperInvariant();

        var clock = VsaCodeResolver.NormalizeClock(finding.PositionClock);
        return string.IsNullOrWhiteSpace(clock) ? code : $"{code}|{clock}";
    }

    private static int EstimateSeverity(MaskQuantificationService.QuantifiedMask q)
    {
        if (q.CrossSectionReductionPercent is > 50) return 5;
        if (q.CrossSectionReductionPercent is > 25) return 4;
        if (q.ExtentPercent is > 50) return 4;
        if (q.HeightMm is > 50) return 3;
        if (q.ExtentPercent is > 25) return 3;
        if (q.HeightMm is > 10) return 2;
        return 1;
    }

    private static bool ShouldAttemptGuidedVerification(ComparisonResult comparison, bool isPdfPhoto)
    {
        if (!isPdfPhoto) return false;
        return comparison.Level switch
        {
            MatchLevel.ExactMatch => false,
            MatchLevel.PartialMatch => !comparison.CodeMatched || comparison.ConfidenceScore < 0.60,
            _ => true
        };
    }

    private static bool TryConsumeBudget(ref int remaining)
    {
        while (true)
        {
            var current = Volatile.Read(ref remaining);
            if (current <= 0)
                return false;

            if (Interlocked.CompareExchange(ref remaining, current - 1, current) == current)
                return true;
        }
    }

    private static bool GuidedVerificationSupportsApproval(
        GroundTruthEntry entry,
        GuidedVerificationResult guided,
        string? guidedCode)
    {
        var confirmation = (guided.ConfirmationLevel ?? string.Empty).Trim().ToLowerInvariant();
        var confirmsDamage = guided.ProtocolDamageVisible
            || confirmation is "bestaetigt" or "teilweise";

        if (!confirmsDamage)
            return false;

        if (string.IsNullOrWhiteSpace(guidedCode))
            return true;

        return CodesLikelyMatch(entry.VsaCode, guidedCode);
    }

    private static string? ResolveGuidedCode(GuidedVerificationResult guided)
    {
        var fromCode = VsaCodeResolver.NormalizeFindingCode(guided.ActualVsaCode)
            ?? VsaCodeResolver.InferCodeFromLabel(guided.ActualVsaCode ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(fromCode))
            return fromCode;

        return VsaCodeResolver.InferCodeFromLabel(guided.ActualLabel ?? string.Empty)
            ?? VsaCodeTree.ReverseLookup(guided.ActualLabel ?? string.Empty);
    }

    private static bool CodesLikelyMatch(string truthCode, string candidateCode)
    {
        if (string.IsNullOrWhiteSpace(truthCode) || string.IsNullOrWhiteSpace(candidateCode))
            return false;

        var t = truthCode.ToUpperInvariant().Trim().Split('.')[0];
        var k = candidateCode.ToUpperInvariant().Trim().Split('.')[0];

        if (t == k) return true;
        if (k.Length > t.Length && t.Length >= 3 && k.StartsWith(t, StringComparison.Ordinal)) return true;
        if (t.Length > k.Length && k.Length >= 3 && t.StartsWith(k, StringComparison.Ordinal)) return true;
        if (t.Length >= 4 && k.Length >= 4 && t[..3] == k[..3]) return true;
        return false;
    }

    private static string BuildApprovalNotes(
        ComparisonResult comparison,
        bool autoApproveCodeHit,
        bool guidedApproved)
    {
        if (guidedApproved)
            return comparison.Explanation + " · GuidedVerify bestaetigt Protokollschaden.";

        if (autoApproveCodeHit)
            return comparison.Explanation + $" · AutoApprove: codekorrekter Partial-Match (Score {comparison.ConfidenceScore:F2}).";

        return comparison.Explanation;
    }

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

