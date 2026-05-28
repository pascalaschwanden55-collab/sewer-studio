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
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

public sealed class SelfTrainingOrchestrator : ISelfTrainingOrchestrator
{
    private readonly EnhancedVisionAnalysisService _vision;
    private readonly ISelfTrainingComparisonService _comparison;
    private readonly ITechniqueAssessmentService _technique;
    private readonly PdfProtocolExtractor _pdfExtractor;
    private readonly TrainingCenterSettings _settings;
    private readonly string _ffmpegPath;

    private readonly ManualResetEventSlim _pauseGate = new(true);

    public bool IsPaused => !_pauseGate.IsSet;

    public SelfTrainingOrchestrator(
        EnhancedVisionAnalysisService vision,
        ISelfTrainingComparisonService comparison,
        ITechniqueAssessmentService technique,
        PdfProtocolExtractor pdfExtractor,
        TrainingCenterSettings? settings = null,
        string? ffmpegPath = null)
    {
        _vision = vision;
        _comparison = comparison;
        _technique = technique;
        _pdfExtractor = pdfExtractor;
        _settings = settings ?? new TrainingCenterSettings();
        _ffmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? "ffmpeg" : ffmpegPath;
    }

    public void Pause() => _pauseGate.Reset();
    public void Resume() => _pauseGate.Set();

    public async Task<SelfTrainingResult> RunAsync(
        TrainingCaseInput tc,
        IProgress<SelfTrainingStep> progress,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // 1. Protokoll-Eintraege extrahieren
        string framesDir = Path.Combine(tc.FolderPath, "self_training_frames");
        Directory.CreateDirectory(framesDir);

        progress.Report(new SelfTrainingStep(
            0, 1, "", 0, SelfTrainingStage.BuildingTimeline, null, null, null,
            "PDF-Protokoll wird gelesen..."));

        var allEntries = await _pdfExtractor.ExtractAsync(tc.ProtocolPath, framesDir, ct);

        progress.Report(new SelfTrainingStep(
            0, allEntries.Count, "", 0, SelfTrainingStage.BuildingTimeline, null, null, null,
            $"{allEntries.Count} Protokoll-Eintraege gefunden"));

        // Eintraege mit Foto behalten
        var entries = allEntries
            .Where(e => !string.IsNullOrEmpty(e.ExtractedFramePath)
                        && File.Exists(e.ExtractedFramePath))
            .ToList();
        bool usedVideoFallback = false;

        // Fallback: Wenn keine Fotos im PDF, Frames aus Video extrahieren
        if (entries.Count == 0 && !string.IsNullOrEmpty(tc.VideoPath) && File.Exists(tc.VideoPath))
        {
            progress.Report(new SelfTrainingStep(
                0, allEntries.Count, "", 0, SelfTrainingStage.ExtractingFrame, null, null, null,
                "Keine Fotos im PDF — extrahiere Frames aus Video..."));

            var ffmpeg = _ffmpegPath;

            // Videodauer ermitteln fuer Meter→Zeit-Mapping
            var probe = new VideoProbeService(ffmpegPath: ffmpeg);
            var probeResult = await probe.ProbeAsync(tc.VideoPath, ct);
            double videoDuration = probeResult.Success ? probeResult.DurationSeconds : 300.0;

            // Max-Meter aus Protokoll fuer lineare Interpolation
            double maxMeter = allEntries.Max(e => Math.Max(e.MeterStart, e.MeterEnd));
            if (maxMeter <= 0) maxMeter = 100.0;

            progress.Report(new SelfTrainingStep(
                0, allEntries.Count, "", 0, SelfTrainingStage.ExtractingFrame, null, null, null,
                $"Video: {videoDuration:F0}s, Max-Meter: {maxMeter:F1}m — {allEntries.Count} Frames extrahieren..."));

            var videoEntries = new List<GroundTruthEntry>();

            for (int i = 0; i < allEntries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var entry = allEntries[i];

                // Zeitstempel: 1. Protokoll-Zeit, 2. Meter→Zeit linear interpoliert
                double timeSec;
                if (entry.Zeit.HasValue && entry.Zeit.Value.TotalSeconds > 0)
                {
                    timeSec = entry.Zeit.Value.TotalSeconds;
                }
                else
                {
                    // Meter-Position linear auf Videodauer mappen
                    // +10s Offset um Logo/Anfang zu ueberspringen
                    double meterCenter = (entry.MeterStart + entry.MeterEnd) / 2.0;
                    timeSec = 10.0 + (meterCenter / maxMeter) * (videoDuration - 20.0);
                    timeSec = Math.Clamp(timeSec, 5.0, videoDuration - 2.0);
                }

                var sampleId = $"st_{tc.CaseId}_{i:D3}";
                var framePath = await FrameStore.ExtractAndStoreAsync(
                    ffmpeg, tc.VideoPath, timeSec, sampleId, framesDir, ct);

                if (framePath is not null)
                {
                    videoEntries.Add(entry with { ExtractedFramePath = framePath });

                    progress.Report(new SelfTrainingStep(
                        i, allEntries.Count, entry.VsaCode, entry.MeterStart,
                        SelfTrainingStage.ExtractingFrame, null, null, framePath,
                        $"Frame @ {timeSec:F1}s fuer {entry.VsaCode} @ {entry.MeterStart:F1}m"));
                }
            }

            entries = videoEntries;
            usedVideoFallback = true;

            progress.Report(new SelfTrainingStep(
                0, entries.Count, "", 0, SelfTrainingStage.ExtractingFrame, null, null, null,
                $"{entries.Count} Frames aus Video extrahiert"));
        }

        if (entries.Count == 0)
        {
            progress.Report(new SelfTrainingStep(
                0, allEntries.Count, "", 0, SelfTrainingStage.Completed, null, null, null,
                "Keine Bilder verfuegbar (weder PDF-Fotos noch Video-Frames)."));
            return new SelfTrainingResult(tc.CaseId, allEntries.Count, 0, 0, 0, 0, null, sw.Elapsed, 0);
        }

        // 2. Jeden Eintrag mit Foto durchlaufen
        int exactMatches = 0, partialMatches = 0, mismatches = 0, noFindings = 0;
        var generatedSamples = new List<TrainingSample>();
        TechniqueAssessment? overallTechnique = null;
        bool techniqueAssessed = false;

        for (int i = 0; i < entries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            _pauseGate.Wait(ct);

            var entry = entries[i];
            string framePath = entry.ExtractedFramePath!;

            // ── Foto laden ──
            progress.Report(new SelfTrainingStep(
                i, entries.Count, entry.VsaCode, entry.MeterStart,
                SelfTrainingStage.ExtractingFrame, null, null, framePath));

            byte[] pngBytes;
            try
            {
                pngBytes = await File.ReadAllBytesAsync(framePath, ct);
            }
            catch
            {
                continue;
            }

            // ── Blinde KI-Analyse (weiss NICHTS vom Protokoll) ──
            progress.Report(new SelfTrainingStep(
                i, entries.Count, entry.VsaCode, entry.MeterStart,
                SelfTrainingStage.Analyzing, null, null, framePath));

            string b64 = Convert.ToBase64String(pngBytes);

            // KI-Fehler in Logdatei schreiben (fuer Debugging auf anderen PCs)
            EnhancedFrameAnalysis analysis;
            try
            {
                analysis = await _vision.AnalyzeAsync(b64, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var errMsg = $"[SelfTraining] EXCEPTION bei {entry.VsaCode}@{entry.MeterStart:F1}m: {ex.GetType().Name}: {ex.Message}";
                try { File.AppendAllText(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "SewerStudio", "logs", "selftraining_errors.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {errMsg}\n"); } catch { }
                progress.Report(new SelfTrainingStep(
                    i, entries.Count, entry.VsaCode, entry.MeterStart,
                    SelfTrainingStage.Analyzing, null, null, framePath,
                    ErrorMessage: errMsg));
                analysis = EnhancedFrameAnalysis.Empty(ex.Message);
            }

            if (analysis.Error is not null)
            {
                var errMsg = $"KI-Fehler: {analysis.Error}";
                try { File.AppendAllText(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "SewerStudio", "logs", "selftraining_errors.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [SelfTraining] {entry.VsaCode}@{entry.MeterStart:F1}m: {errMsg}\n"); } catch { }
                progress.Report(new SelfTrainingStep(
                    i, entries.Count, entry.VsaCode, entry.MeterStart,
                    SelfTrainingStage.Analyzing, null, null, framePath,
                    ErrorMessage: errMsg));
            }

            // ── Deterministischer Vergleich mit Protokoll ──
            progress.Report(new SelfTrainingStep(
                i, entries.Count, entry.VsaCode, entry.MeterStart,
                SelfTrainingStage.Comparing, null, null, framePath));

            var comparison = _comparison.Compare(entry, analysis);

            // ── Aufnahmetechnik (1x mit Qwen, danach deterministisch) ──
            TechniqueAssessment? technique = null;
            if (!techniqueAssessed)
            {
                progress.Report(new SelfTrainingStep(
                    i, entries.Count, entry.VsaCode, entry.MeterStart,
                    SelfTrainingStage.AssessingTechnique, comparison, null, framePath));

                try
                {
                    technique = await _technique.AssessFrameWithVisionAsync(
                        pngBytes, analysis.Meter, entry.MeterStart, ct);
                    overallTechnique = technique;
                    techniqueAssessed = true;
                }
                catch
                {
                    technique = _technique.AssessFrame(pngBytes, analysis.Meter, entry.MeterStart);
                    overallTechnique = technique;
                    techniqueAssessed = true;
                }
            }
            else
            {
                technique = _technique.AssessFrame(pngBytes, analysis.Meter, entry.MeterStart);
            }

            // ── Zaehler aktualisieren ──
            switch (comparison.Level)
            {
                case MatchLevel.ExactMatch: exactMatches++; break;
                case MatchLevel.PartialMatch: partialMatches++; break;
                case MatchLevel.Mismatch: mismatches++; break;
                case MatchLevel.NoFindings: noFindings++; break;
            }

            // ── TrainingSample erzeugen ──
            var meterCenter = (entry.MeterStart + entry.MeterEnd) / 2.0;
            var eligibility = TrainingSampleEligibility.Evaluate(tc.InspectionDate);
            var sample = new TrainingSample
            {
                SampleId = $"{tc.CaseId}_st_{i:D3}_{DateTime.UtcNow:HHmmss}",
                CaseId = tc.CaseId,
                Code = entry.VsaCode,
                Beschreibung = entry.Text,
                MeterStart = Math.Round(entry.MeterStart, 1),
                MeterEnd = Math.Round(entry.MeterEnd, 1),
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
                HasOsdMismatch = technique?.OsdDeltaMeters > _settings.OsdMismatchThresholdMeters,
                Signature = TrainingSample.BuildCanonicalSignature(tc.CaseId, entry.VsaCode, meterCenter, entry.MeterEnd),
                MatchLevel = comparison.Level.ToString(),
                KiCode = comparison.BestMatchCode,
                SourceType = usedVideoFallback
                    ? (entry.Zeit.HasValue ? SourceTypeNames.VideoTimestamp : SourceTypeNames.VideoLinear)
                    : SourceTypeNames.PdfPhoto,
                TechniqueGrade = technique?.OverallGrade,
                InspectionDate = tc.InspectionDate,
                TrainingEligible = eligibility.IsEligible,
                TrainingEligibilityReason = eligibility.Reason
            };
            generatedSamples.Add(sample);

            // ── Abschluss melden ──
            progress.Report(new SelfTrainingStep(
                i, entries.Count, entry.VsaCode, entry.MeterStart,
                SelfTrainingStage.Completed, comparison, technique, framePath));
        }

        // Samples mergen (bestehende laden + neue hinzufuegen + Dedup via Signature)
        if (generatedSamples.Count > 0)
        {
            await TrainingSamplesStore.MergeAndSaveAsync(generatedSamples);
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
            SamplesGenerated: generatedSamples.Count);
    }

}
