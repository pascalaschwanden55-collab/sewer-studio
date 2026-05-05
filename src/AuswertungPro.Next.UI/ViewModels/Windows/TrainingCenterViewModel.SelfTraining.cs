using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training.Services;
using AuswertungPro.Next.UI.Services;
using AiTrack = AuswertungPro.Next.UI.Services.AiActivityTracker;

/// <summary>
/// Phase 6.2: Self-Training-Loop ausgelagert aus TrainingCenterViewModel.
/// Reduziert Hauptdatei um ~340 Zeilen.
/// </summary>
public partial class TrainingCenterViewModel
{
    [ObservableProperty] private bool _isSelfTrainingRunning;
    private CancellationTokenSource? _selfTrainingCts;
    private ISelfTrainingOrchestrator? _selfTrainingOrchestrator;
    private string _activeVisionModel = "Qwen2.5-VL";

    [RelayCommand]
    private async Task RunSelfTrainingAsync()
    {
        if (IsBusy || IsSelfTrainingRunning) return;

        // Auto-Scan: Wenn keine Faelle geladen, Ordner automatisch scannen
        if (Cases.Count == 0 && _rootFolders.Count > 0)
        {
            StatusText = "Scanne Ordner automatisch...";
            foreach (var folder in _rootFolders)
            {
                if (!Directory.Exists(folder)) continue;
                var found = await _import.ScanAsync(folder);
                foreach (var c in found)
                    Cases.Add(c);
            }
        }

        // Auto-Auswahl: Bereits verarbeitete Haltungen ueberspringen
        if (SelectedCase is null)
        {
            var existingSamples = await TrainingSamplesStore.LoadAsync();
            var processedIds = existingSamples.Select(s => s.CaseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var firstUnprocessed = Cases.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.ProtocolPath) && !processedIds.Contains(c.CaseId));

            if (firstUnprocessed is null)
            {
                // Fallback: Alle bereits verarbeitet oder keine mit Protokoll
                var withProtocol = Cases.Count(c => !string.IsNullOrEmpty(c.ProtocolPath));
                StatusText = withProtocol > 0
                    ? $"Alle {withProtocol} Faelle bereits verarbeitet. Waehle manuell fuer erneutes Training."
                    : "Keine Faelle mit Protokoll vorhanden. Bitte zuerst Ordner waehlen und scannen.";
                return;
            }
            SelectedCase = firstUnprocessed;
        }
        if (string.IsNullOrEmpty(SelectedCase.ProtocolPath))
        {
            StatusText = "Der ausgewaehlte Fall hat kein Protokoll (PDF).";
            return;
        }

        // Rotation analog zu RotateGenCts (Audit D2.2 / April-Race-Fix):
        // Atomarer Tausch + delayed Dispose. Verhindert die Race zwischen
        // Dispose und Token-Registrierungen in noch laufenden Self-Training-Tasks.
        var ct = RotateSelfTrainingCts();

        using var _aiToken = AiTrack.Begin("Selbsttraining");
        try
        {
            IsBusy = true;
            IsSelfTrainingRunning = true;
            ResetSelfTrainingVisuals(resetMatchRate: true);
            LogText = "";
            StatusText = $"Selbsttraining: {SelectedCase.CaseId}...";
            Log($"--- Selbsttraining starten: {SelectedCase.CaseId} ---");
            Log($"  Protokoll: {SelectedCase.ProtocolPath}");
            var settings = await TrainingCenterSettingsStore.LoadAsync();
            var gpuConcurrency = Math.Clamp(settings.GpuConcurrency, 1, 12);
            var preExtractCpuParallelism = Math.Clamp(settings.CpuPreExtractParallelism, 1, 48);
            var caseParallelism = Math.Clamp(settings.CaseParallelism, 1, 8);
            var maxInFlightRequests = gpuConcurrency * caseParallelism;
            settings.GpuConcurrency = gpuConcurrency;
            settings.CpuPreExtractParallelism = preExtractCpuParallelism;
            settings.CaseParallelism = caseParallelism;

            // Services instanziieren (gleicher Pattern wie BatchImport)
            var cfg = AiRuntimeConfig.Load();
            Log($"Ollama: {cfg.OllamaBaseUri}, Modell: {cfg.VisionModel}");
            Log($"Parallelitaet: GPU={gpuConcurrency}, Faelle={caseParallelism}, PDF-CPU={preExtractCpuParallelism} (max. Requests ~{maxInFlightRequests})");
            if (int.TryParse(Environment.GetEnvironmentVariable("OLLAMA_NUM_PARALLEL"), out var ollamaSlots)
                && ollamaSlots > 0
                && ollamaSlots < gpuConcurrency)
            {
                Log($"Hinweis: OLLAMA_NUM_PARALLEL={ollamaSlots} < GPU-Parallelitaet={gpuConcurrency}. Dadurch bleibt Kapazitaet ungenutzt.");
            }

            var visionModel = cfg.VisionModel ?? "Qwen2.5-VL";
            _activeVisionModel = visionModel;
            var ollamaClient = cfg.CreateOllamaClient();
            var vision = new EnhancedVisionAnalysisService(ollamaClient, visionModel);
            try
            {
                await vision.EnableFewShotAsync(new Ai.Training.FewShotExampleStore(), ct);
                Log("Few-Shot aktiviert: gespeicherte Beispiele werden fuer Self-Training genutzt.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Few-Shot konnte nicht aktiviert werden: {ex.Message}");
            }
            var comparison = new SelfTrainingComparisonService();
            var technique = new TechniqueAssessmentService(ollamaClient, visionModel);
            var pdfExtractor = new PdfProtocolExtractor();

            // Multi-Modell-Pipeline (YOLO/DINO/SAM) wenn Sidecar verfuegbar
            Ai.Pipeline.SingleFrameMultiModelService? multiModel = null;
            try
            {
                var pipeCfg = Ai.PipelineConfig.Load();
                if (pipeCfg.MultiModelEnabled)
                {
                    var sidecarHttp = new System.Net.Http.HttpClient
                    {
                        BaseAddress = pipeCfg.SidecarUrl,
                        Timeout = TimeSpan.FromSeconds(pipeCfg.SidecarTimeoutSec)
                    };
                    var pipelineClient = new Ai.Pipeline.VisionPipelineClient(pipeCfg.SidecarUrl, sidecarHttp);
                    multiModel = new Ai.Pipeline.SingleFrameMultiModelService(
                        pipelineClient, pipeCfg.YoloConfidence, pipeCfg.DinoBoxThreshold, pipeCfg.DinoTextThreshold);
                }
            }
            catch { /* Sidecar nicht konfiguriert — nur Qwen */ }

            _selfTrainingOrchestrator = new SelfTrainingOrchestrator(
                vision, comparison, technique, pdfExtractor, settings, multiModel, _sampleQualityGate,
                reviewQueue: ReviewQueueServiceRef);

            // Progress-Callback verbindet Orchestrator → ViewModel-Visualisierungen
            var progress = new Progress<SelfTrainingStep>(OnSelfTrainingStep);

            // Alle Faelle mit Protokoll durchlaufen (Batch-Selbsttraining)
            var existingSamples = await TrainingSamplesStore.LoadAsync();
            var processedIds = existingSamples.Select(s => s.CaseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var casesToTrain = Cases
                .Where(c => !string.IsNullOrEmpty(c.ProtocolPath))
                .Where(c => ForceRerunAll || !processedIds.Contains(c.CaseId))
                .ToList();

            if (casesToTrain.Count == 0)
            {
                // Fallback: Alle bereits verarbeitet → nur den ausgewaehlten Fall erneut
                casesToTrain = new List<TrainingCase> { SelectedCase };
            }

            Log($"Selbsttraining: {casesToTrain.Count} Faelle zu verarbeiten");
            ProgressMax = casesToTrain.Count;

            // PDF-Fotos fuer ALLE Faelle vorab extrahieren (CPU-parallel, blockiert GPU nicht)
            Log($"PDF-Fotos vorab extrahieren (CPU-Parallelitaet: {preExtractCpuParallelism})...");
            await Parallel.ForEachAsync(casesToTrain,
                new ParallelOptions { MaxDegreeOfParallelism = preExtractCpuParallelism, CancellationToken = ct },
                async (c, token) =>
                {
                    if (string.IsNullOrEmpty(c.ProtocolPath)) return;
                    var framesDir = Path.Combine(c.FolderPath, "self_training_frames");
                    if (Directory.Exists(framesDir) && Directory.GetFiles(framesDir, "*.png").Length > 0) return;
                    // PdfProtocolExtractor wird in RunAsync nochmal aufgerufen —
                    // aber die Frames sind dann schon auf Disk und muessen nicht nochmal extrahiert werden
                    try
                    {
                        var extractor = new PdfProtocolExtractor();
                        await extractor.ExtractAsync(c.ProtocolPath, framesDir, token);
                    }
                    catch { /* Fehler beim Vorextrahieren ignorieren — RunAsync versucht es nochmal */ }
                });
            Log("PDF-Fotos vorab extrahiert");

            int totalExact = 0, totalPartial = 0, totalMismatch = 0, totalNoFindings = 0, totalSamples = 0;
            int caseErrors = 0;
            int completedCases = 0;

            // Mehrere Faelle parallel: waehrend Fall A KB-Update macht (CPU+Disk),
            // analysieren andere Faelle bereits Frames (GPU).
            await Parallel.ForEachAsync(
                casesToTrain.Select((c, i) => (Case: c, Index: i)),
                new ParallelOptions { MaxDegreeOfParallelism = caseParallelism, CancellationToken = ct },
                async (item, token) =>
            {
                var (currentCase, ci) = item;
                var caseNum = Interlocked.Increment(ref completedCases);

                // UI-Updates via Dispatcher (Thread-Safety)
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    SelectedCase = currentCase;
                    ProgressValue = caseNum;
                    StatusText = $"[{caseNum}/{casesToTrain.Count}] {currentCase.CaseId}...";
                });
                Log($"--- [{ci + 1}/{casesToTrain.Count}] Selbsttraining: {currentCase.CaseId} ---");
                Log($"  Protokoll: {currentCase.ProtocolPath}");

                SelfTrainingResult result;
                try
                {
                    result = await _selfTrainingOrchestrator.RunAsync(currentCase, progress, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log($"  FEHLER: {ex.Message}");
                    Interlocked.Increment(ref caseErrors);
                    return;
                }

                // Ergebnis loggen
                Log($"  Eintraege: {result.TotalEntries} | CodeHit: {result.CodeHits} | "
                  + $"ExactMatch: {result.ExactMatches} | Partial: {result.PartialMatches} | "
                  + $"Mismatch: {result.Mismatches} | NoFindings: {result.NoFindings} | "
                  + $"Samples: {result.SamplesGenerated} | Dauer: {result.Duration:mm\\:ss}");
                if (result.OverallTechnique is { } tech)
                    Log($"  Technik: {tech.OverallGrade} (Licht={tech.LightingQuality}, Schaerfe={tech.SharpnessQuality})");

                Interlocked.Add(ref totalExact, result.ExactMatches);
                Interlocked.Add(ref totalPartial, result.PartialMatches);
                Interlocked.Add(ref totalMismatch, result.Mismatches);
                Interlocked.Add(ref totalNoFindings, result.NoFindings);
                Interlocked.Add(ref totalSamples, result.SamplesGenerated);

                // Fall als verarbeitet markieren
                currentCase.Status = TrainingCaseStatus.SelfTrained;

                // Match-Rate-Verlauf persistieren (HistoryStore hat eigenen Lock)
                var matchTotal = result.ExactMatches + result.PartialMatches + result.Mismatches + result.NoFindings;
                if (matchTotal > 0)
                {
                    await SelfTrainingHistoryStore.AppendRunAsync(new SelfTrainingRunSnapshot(
                        DateTime.UtcNow,
                        result.CaseId,
                        result.TotalEntries,
                        (double)result.ExactMatches / matchTotal,
                        (double)result.PartialMatches / matchTotal,
                        (double)result.Mismatches / matchTotal,
                        (double)result.NoFindings / matchTotal,
                        (double)result.CodeHits / matchTotal));
                }

                // Inkrementelles KB-Update — mit Lock serialisiert (SQLite)
                if (result.SamplesGenerated > 0)
                {
                    await _kbUpdateLock.WaitAsync(token);
                    try
                    {
                        var allSamples = await TrainingSamplesStore.LoadAsync();
                        var newApproved = allSamples
                            .Where(s => s.CaseId == result.CaseId
                                && s.Status == TrainingSampleStatus.Approved)
                            .ToList();

                        if (newApproved.Count > 0)
                        {
                            foreach (var s in newApproved.Where(s => s.KbIndexState is KbIndexState.None or KbIndexState.Error))
                                s.KbIndexState = KbIndexState.Pending;
                            await TrainingSamplesStore.MergeOrUpdateAsync(newApproved);

                            Log($"  {newApproved.Count} Samples → KB-Update...");
                            try
                            {
                                var indexed = await IncrementalKbUpdateAsync(newApproved, token);
                                var indexedSet = indexed.ToHashSet();
                                foreach (var s in newApproved)
                                    s.KbIndexState = indexedSet.Contains(s.SampleId)
                                        ? KbIndexState.Indexed
                                        : (s.KbIndexState == KbIndexState.Pending ? KbIndexState.Error : s.KbIndexState);
                                await TrainingSamplesStore.MergeOrUpdateAsync(newApproved);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                Log($"  KB-Update Fehler: {ex.Message}");
                            }
                        }
                    }
                    finally
                    {
                        _kbUpdateLock.Release();
                    }
                }
            });

            ForceRerunAll = false; // Nach Durchlauf zuruecksetzen
            var totalCodeHits = totalExact + totalPartial;
            Log($"=== Selbsttraining abgeschlossen ===");
            Log($"  Faelle: {casesToTrain.Count} ({caseErrors} Fehler)");
            Log($"  CodeHit: {totalCodeHits} (Code korrekt) | ExactMatch: {totalExact} (Code+Meter+Clock) | Partial: {totalPartial} | Mismatch: {totalMismatch} | NoFindings: {totalNoFindings}");
            Log($"  Samples erzeugt: {totalSamples}");

            StatusText = $"Fertig! {casesToTrain.Count} Faelle, {totalCodeHits} CodeHit, {totalExact} ExactMatch, {totalSamples} Samples";

            // Hinweis fuer Few-Shot-Export (B2)
            if (totalExact > 0)
            {
                Log($"{totalExact} ExactMatch-Samples erzeugt. Fuer Few-Shot-Export: Tab 'Samples' → 'Export Approved'");
            }

            // Review Queue befuellen mit PartialMatch/Mismatch (C1)
            if (ReviewQueueServiceRef is not null && (totalPartial > 0 || totalMismatch > 0))
            {
                var allSamplesForReview = await TrainingSamplesStore.LoadAsync();
                var reviewCandidates = allSamplesForReview
                    .Where(s => casesToTrain.Any(c => c.CaseId == s.CaseId)
                        && s.Status == TrainingSampleStatus.New
                        && (s.MatchLevel is MatchLevelNames.PartialMatch or MatchLevelNames.Mismatch))
                    .ToList();

                foreach (var s in reviewCandidates)
                {
                    ReviewQueueServiceRef.EnqueueFromSelfTraining(
                        s.CaseId, s.Code, s.KiCode ?? s.Code,
                        s.MeterStart, s.FramePath, s.MatchLevel!,
                        s.SampleId);
                }

                if (reviewCandidates.Count > 0)
                {
                    LoadReviewQueue(ReviewQueueServiceRef);
                    Log($"{reviewCandidates.Count} Samples in Review Queue eingereiht (PartialMatch/Mismatch)");
                }
            }

            // Samples-Liste aktualisieren
            await LoadSamplesInternalAsync();
            await RefreshKbStatusAsync();
        }
        catch (OperationCanceledException)
        {
            Log("Selbsttraining abgebrochen.");
            StatusText = "Selbsttraining abgebrochen.";
        }
        catch (Exception ex)
        {
            Log($"FEHLER: {ex.GetType().Name}: {ex.Message}");
            StatusText = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsSelfTrainingRunning = false;
            _selfTrainingOrchestrator = null;
        }
    }
}