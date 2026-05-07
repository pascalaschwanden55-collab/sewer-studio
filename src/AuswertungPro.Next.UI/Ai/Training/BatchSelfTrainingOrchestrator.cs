// AuswertungPro – Video-Selbsttraining: Voll-automatischer Batch-Betrieb
// Verarbeitet Ordner mit WinCan/IBAK-Exports automatisch ueber Nacht.
// Protokoll = Ground-Truth. Treffer und Korrekturen fliessen automatisch in die KB.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.UI.Ai.Training.Services;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Voll-automatischer Batch-Selbsttraining-Orchestrator.
///
/// Ablauf pro Haltung:
/// 1. Protokoll importieren (WinCan DB3 oder IBAK Daten.txt)
/// 2. Video blind durch die KI-Pipeline
/// 3. Vergleich KI vs. Protokoll → DifferenceReport
/// 4. Auto-Approve: Treffer + Korrekturen direkt in KB (Protokoll = Wahrheit)
/// 5. False Positives verwerfen
/// 6. Naechste Haltung
///
/// Fehlertoleranz: Wenn eine Haltung scheitert → ueberspringen, naechste.
/// </summary>
public sealed class BatchSelfTrainingOrchestrator : AuswertungPro.Next.Application.Ai.Training.IBatchSelfTrainingOrchestrator
{
    private readonly VideoSelfTrainingOrchestrator _videoOrchestrator;
    private readonly Func<VideoSelfTrainingOrchestrator>? _orchestratorFactory;
    private readonly ProtocolLoaderFactory _protocolLoader;
    private readonly KbEnrichmentService _enrichment;
    private readonly AuswertungPro.Next.Application.Ai.SelfImproving.UncertaintySamplingService? _uncertaintySampler;
    private readonly ILogger? _log;

    // YOLO-Retraining nach Batch-Durchlauf (optional)
    private readonly YoloRetrainOrchestrator? _yoloRetrain;

    // Zustandsverwaltung
    private volatile bool _isPaused;
    private volatile bool _isCancelled;
    private readonly SemaphoreSlim _pauseGate = new(1, 1);
    // Pause/Resume/Cancel muessen gegen Race-Conditions (Doppelklick) geschuetzt
    // werden — sonst Wait() zweimal auf Semaphor(1) = UI-Deadlock.
    private readonly object _pauseSync = new();

    // Sidecar Auto-Restart: Pfad zum Sidecar-Verzeichnis
    private readonly string? _sidecarDir;

    // V4.2 Fix: Sidecar-URL aus Config statt hardcoded.
    private readonly string _sidecarUrl;

    public bool IsPaused => _isPaused;
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public string Name => "BatchSelfTraining (Ordner-Tree)";

    public BatchSelfTrainingOrchestrator(
        VideoSelfTrainingOrchestrator videoOrchestrator,
        ProtocolLoaderFactory protocolLoader,
        KbEnrichmentService enrichment,
        ILogger? log = null,
        string? sidecarDir = null,
        YoloRetrainOrchestrator? yoloRetrain = null,
        Func<VideoSelfTrainingOrchestrator>? orchestratorFactory = null,
        AuswertungPro.Next.Application.Ai.SelfImproving.UncertaintySamplingService? uncertaintySampler = null,
        string? sidecarUrl = null)
    {
        _videoOrchestrator = videoOrchestrator ?? throw new ArgumentNullException(nameof(videoOrchestrator));
        _protocolLoader = protocolLoader ?? throw new ArgumentNullException(nameof(protocolLoader));
        _enrichment = enrichment ?? throw new ArgumentNullException(nameof(enrichment));
        _log = log;
        _sidecarDir = sidecarDir;
        _yoloRetrain = yoloRetrain;
        _orchestratorFactory = orchestratorFactory;
        _uncertaintySampler = uncertaintySampler;
        // V4.2 Fix: URL aus Config > Env > Default.
        _sidecarUrl = sidecarUrl
            ?? Environment.GetEnvironmentVariable("SEWERSTUDIO_SIDECAR_URL")
            ?? "http://localhost:8100";
    }

    /// <summary>
    /// Startet den Batch-Durchlauf.
    /// Scannt den Export-Ordner, findet alle Haltungen mit Video + Protokoll,
    /// und verarbeitet sie nacheinander.
    /// </summary>
    public async Task<BatchSelfTrainingResult> RunAsync(
        BatchSelfTrainingRequest request,
        IProgress<BatchSelfTrainingProgress>? progress = null,
        CancellationToken ct = default)
    {
        IsRunning = true;
        _isCancelled = false;
        var sw = Stopwatch.StartNew();
        var startedUtc = DateTime.UtcNow;

        var stats = new BatchKbStats();
        var haltungResults = new List<BatchHaltungResult>();
        int processed = 0, skipped = 0, failed = 0;

        try
        {
            // 1. Alle Haltungen mit Video + Protokoll finden
            var haltungen = DiscoverHaltungen(request);
            _log?.LogInformation("Batch: {Count} Haltungen gefunden in {Path}",
                haltungen.Count, request.ExportRootPath);

            if (haltungen.Count == 0)
            {
                progress?.Report(new BatchSelfTrainingProgress
                {
                    TotalHaltungen = 0,
                    Status = "Keine Haltungen mit Video + Protokoll gefunden."
                });
                return BuildResult(startedUtc, sw.Elapsed, 0, 0, 0, 0, stats, haltungResults);
            }

            // 1b. Bereits verarbeitete Haltungen ueberspringen (sofern aktiviert)
            if (request.SkipAlreadyProcessed)
            {
                var processedKeys = await LoadBatchHistoryAsync().ConfigureAwait(false);

                var beforeCount = haltungen.Count;
                haltungen = haltungen
                    .Where(h => !processedKeys.Contains(BuildHistoryKey(h)))
                    .ToList();
                skipped = beforeCount - haltungen.Count;

                _log?.LogInformation(
                    "Batch: {Skipped} bereits verarbeitete Haltungen uebersprungen, {Remaining} verbleibend",
                    skipped, haltungen.Count);

                if (haltungen.Count == 0)
                {
                    progress?.Report(new BatchSelfTrainingProgress
                    {
                        TotalHaltungen = 0,
                        Status = $"Alle {beforeCount} Haltungen bereits verarbeitet. Checkbox 'Alle erneut' fuer erneuten Durchlauf."
                    });
                    return BuildResult(startedUtc, sw.Elapsed, beforeCount, 0, skipped, 0, stats, haltungResults);
                }
            }

            var total = request.MaxHaltungen > 0
                ? Math.Min(haltungen.Count, request.MaxHaltungen)
                : haltungen.Count;

            var policy = new BatchAutoApprovePolicy
            {
                ApproveMatches = request.AutoApproveMatches,
                ApproveCorrections = request.AutoApproveCorrections,
                LearnFromMissed = request.LearnFromMissed,
                MinDetectionConfidence = request.MinDetectionConfidence
            };

            // ── PDF-Vorlade-Phase: Alle Protokolle parallel auf CPU parsen ──
            // So wartet die GPU nie auf PDF-Parsing.
            progress?.Report(new BatchSelfTrainingProgress
            {
                TotalHaltungen = total,
                Phase = "Vorbereitung",
                Status = $"Protokolle vorladen ({total} Haltungen)...",
                RunningStats = stats
            });

            var preloadedProtocols = new System.Collections.Concurrent.ConcurrentDictionary<string, PreloadedProtocol>();
            var preloadSw = Stopwatch.StartNew();

            var cpuParallelism = new TrainingCenterSettings().CpuPreExtractParallelism;
            await Task.Run(() =>
            {
                Parallel.For(0, total, new ParallelOptions { MaxDegreeOfParallelism = cpuParallelism },
                    i =>
                    {
                        var h = haltungen[i];
                        try
                        {
                            var (protocol, record) = _protocolLoader.LoadProtocolWithRecord(
                                h.ProtocolSource, h.SourceType, h.HaltungId);
                            preloadedProtocols[h.HaltungId] = new PreloadedProtocol(protocol, record);
                        }
                        catch (Exception ex)
                        {
                            _log?.LogWarning(ex, "Batch: Protokoll-Vorladen fehlgeschlagen fuer {Id}", h.HaltungId);
                            preloadedProtocols[h.HaltungId] = new PreloadedProtocol(null, null, ex.Message);
                        }
                    });
            }, ct).ConfigureAwait(false);

            preloadSw.Stop();
            _log?.LogInformation("Batch: {Count} Protokolle in {Sec:F1}s vorgeladen",
                preloadedProtocols.Count, preloadSw.Elapsed.TotalSeconds);

            progress?.Report(new BatchSelfTrainingProgress
            {
                TotalHaltungen = total,
                Phase = "Vorbereitung",
                Status = $"{preloadedProtocols.Count} Protokolle in {preloadSw.Elapsed.TotalSeconds:F0}s vorgeladen",
                RunningStats = stats
            });

            // Zeitmessung fuer Restzeit-Schaetzung
            var haltungTimes = new List<double>();
            var parallelism = Math.Max(1, request.MaxParallelHaltungen);

            // Lock fuer thread-sichere Statistik-Updates bei paralleler Verarbeitung
            var statsLock = new object();

            // 2. Haltungen verarbeiten (parallel wenn MaxParallelHaltungen > 1)
            if (parallelism <= 1)
            {
                // Sequentieller Pfad (unveraendert, bewaehrt)
                for (int i = 0; i < total; i++)
                {
                    if (_isCancelled || ct.IsCancellationRequested) break;
                    await CheckPauseAsync(ct).ConfigureAwait(false);

                    var h = haltungen[i];
                    var hSw = Stopwatch.StartNew();

                    TimeSpan? estimatedRemaining = null;
                    if (haltungTimes.Count >= 2)
                    {
                        var avg = haltungTimes.Average();
                        estimatedRemaining = TimeSpan.FromSeconds(avg * (total - i));
                    }

                    progress?.Report(new BatchSelfTrainingProgress
                    {
                        CurrentIndex = i + 1,
                        TotalHaltungen = total,
                        HaltungId = h.HaltungId,
                        Phase = "Start",
                        Status = $"Haltung {i + 1}/{total}: {h.HaltungId}",
                        EstimatedRemaining = estimatedRemaining,
                        RunningStats = stats
                    });

                    await EnsureSidecarRunningAsync(progress, i + 1, total, h.HaltungId, stats, ct)
                        .ConfigureAwait(false);

                    try
                    {
                        var hSw2 = Stopwatch.StartNew();
                        var result = await ProcessSingleHaltungAsync(
                            h, request, policy, stats, progress, i + 1, total, ct,
                            preloaded: preloadedProtocols).ConfigureAwait(false);
                        hSw2.Stop();

                        LogToFile($"[{i+1}/{total}] {h.HaltungId}: Success={result.Success}, " +
                            $"TP={result.TruePositives}, FN={result.FalseNegatives}, FP={result.FalsePositives}, " +
                            $"KB=+{result.KbIndexed}, Error={result.Error}, Dauer={hSw2.Elapsed.TotalSeconds:F1}s");

                        haltungResults.Add(result);

                        if (result.Success)
                        {
                            await AppendBatchHistoryAsync(BuildHistoryKey(h)).ConfigureAwait(false);
                            processed++;
                            progress?.Report(new BatchSelfTrainingProgress
                            {
                                CurrentIndex = i + 1,
                                TotalHaltungen = total,
                                HaltungId = h.HaltungId,
                                Phase = "Ergebnis",
                                Status = $"✓ {h.HaltungId}: TP:{result.TruePositives} FN:{result.FalseNegatives} FP:{result.FalsePositives} MM:{result.CodeMismatches} | KB:+{result.KbIndexed} | {result.Duration.TotalSeconds:F0}s",
                                RunningStats = stats
                            });
                        }
                        else
                        {
                            failed++;
                            progress?.Report(new BatchSelfTrainingProgress
                            {
                                CurrentIndex = i + 1,
                                TotalHaltungen = total,
                                HaltungId = h.HaltungId,
                                Phase = "Ergebnis",
                                Status = $"✗ {h.HaltungId}: {result.Error ?? "unbekannter Fehler"} | {result.Duration.TotalSeconds:F0}s",
                                RunningStats = stats
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        var errMsg = $"{ex.GetType().Name}: {ex.Message}";
                        LogToFile($"[{i+1}/{total}] {h.HaltungId}: EXCEPTION {errMsg}");
                        progress?.Report(new BatchSelfTrainingProgress
                        {
                            CurrentIndex = i + 1, TotalHaltungen = total,
                            HaltungId = h.HaltungId, Phase = "Ergebnis",
                            Status = $"✗ {h.HaltungId}: EXCEPTION {errMsg}",
                            RunningStats = stats
                        });
                        haltungResults.Add(new BatchHaltungResult
                        {
                            HaltungId = h.HaltungId,
                            VideoPath = h.VideoPath,
                            Success = false,
                            Error = errMsg,
                            Duration = hSw.Elapsed
                        });
                    }

                    hSw.Stop();
                    haltungTimes.Add(hSw.Elapsed.TotalSeconds);
                }
            }
            else
            {
                // Paralleler Pfad: N Haltungen gleichzeitig, jede mit eigener Pipeline-Instanz.
                // Die Ollama-Slots (OLLAMA_NUM_PARALLEL) werden so tatsaechlich ausgelastet.
                _log?.LogInformation("Batch: Paralleler Modus mit {N} gleichzeitigen Haltungen", parallelism);

                // Index-Zaehler fuer Fortschritts-Tracking (thread-safe)
                int completedCount = 0;

                await Parallel.ForEachAsync(
                    Enumerable.Range(0, total),
                    new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
                    async (i, token) =>
                    {
                        if (_isCancelled) return;
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        var h = haltungen[i];
                        var hSw = Stopwatch.StartNew();

                        // Jede parallele Haltung braucht ihren eigenen Orchestrator
                        // (VideoSelfTrainingOrchestrator hat internen State pro Video)
                        var localOrch = _orchestratorFactory?.Invoke() ?? _videoOrchestrator;

                        // V4.2 Phase 2: Protokoll-First-Modus auf lokalen Orchestrator uebertragen.
                        localOrch.UseProtocolFirst = request.UseProtocolFirst;
                        localOrch.ProtocolFirstMeterTolerance = request.ProtocolFirstMeterTolerance;
                        localOrch.EnableSurpriseGapsPass = request.EnableSurpriseGapsPass;
                        localOrch.SurpriseGapsFrameStep = request.SurpriseGapsFrameStep;

                        progress?.Report(new BatchSelfTrainingProgress
                        {
                            CurrentIndex = Interlocked.Increment(ref completedCount),
                            TotalHaltungen = total,
                            HaltungId = h.HaltungId,
                            Phase = "Start",
                            Status = $"[P{parallelism}] Haltung {i + 1}/{total}: {h.HaltungId}",
                            RunningStats = stats
                        });

                        try
                        {
                            var result = await ProcessSingleHaltungParallelAsync(
                                h, request, policy, stats, statsLock, localOrch,
                                progress, i + 1, total, parallelism, token,
                                preloaded: preloadedProtocols).ConfigureAwait(false);

                            LogToFile($"[{i+1}/{total}] {h.HaltungId}: Success={result.Success}, " +
                                $"TP={result.TruePositives}, FN={result.FalseNegatives}, FP={result.FalsePositives}, " +
                                $"KB=+{result.KbIndexed}, Error={result.Error}, Dauer={result.Duration.TotalSeconds:F1}s");

                            lock (statsLock)
                            {
                                haltungResults.Add(result);
                                haltungTimes.Add(hSw.Elapsed.TotalSeconds);

                                if (result.Success)
                                    processed++;
                                else
                                    failed++;
                            }

                            if (result.Success)
                                await AppendBatchHistoryAsync(BuildHistoryKey(h)).ConfigureAwait(false);

                            var icon = result.Success ? "✓" : "✗";
                            var detail = result.Success
                                ? $"TP:{result.TruePositives} FN:{result.FalseNegatives} FP:{result.FalsePositives} MM:{result.CodeMismatches} | KB:+{result.KbIndexed} | {result.Duration.TotalSeconds:F0}s"
                                : $"{result.Error ?? "unbekannter Fehler"} | {result.Duration.TotalSeconds:F0}s";
                            progress?.Report(new BatchSelfTrainingProgress
                            {
                                CurrentIndex = i + 1,
                                TotalHaltungen = total,
                                HaltungId = h.HaltungId,
                                Phase = "Ergebnis",
                                Status = $"{icon} {h.HaltungId}: {detail}",
                                RunningStats = stats
                            });
                        }
                        catch (OperationCanceledException) { /* Abbruch */ }
                        catch (Exception ex)
                        {
                            // V4.2 Nachbesserung: Exception-Details auch ins Live-Log,
                            // damit crashes nicht stumm verschluckt werden.
                            var errMsg = $"{ex.GetType().Name}: {ex.Message}";
                            var stackHead = ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim() ?? "";
                            LogToFile($"[{i+1}/{total}] {h.HaltungId}: EXCEPTION {errMsg}\n{ex.StackTrace}");
                            progress?.Report(new BatchSelfTrainingProgress
                            {
                                CurrentIndex = i + 1, TotalHaltungen = total,
                                HaltungId = h.HaltungId, Phase = "EXCEPTION",
                                Status = $"✗ {h.HaltungId}: {errMsg} | {stackHead}",
                                RunningStats = stats
                            });
                            lock (statsLock)
                            {
                                failed++;
                                haltungResults.Add(new BatchHaltungResult
                                {
                                    HaltungId = h.HaltungId,
                                    VideoPath = h.VideoPath,
                                    Success = false,
                                    Error = errMsg,
                                    Duration = hSw.Elapsed
                                });
                            }
                        }
                    }).ConfigureAwait(false);
            }

            sw.Stop();

            // Abschluss-Meldung
            progress?.Report(new BatchSelfTrainingProgress
            {
                CurrentIndex = total,
                TotalHaltungen = total,
                Phase = "Fertig",
                Status = $"Batch abgeschlossen: {processed} verarbeitet, {failed} fehlgeschlagen, KB: {stats.KbIndexed} neu, F1={stats.F1:P0}",
                RunningStats = stats
            });

            _log?.LogInformation(
                "Batch abgeschlossen: {Processed}/{Total} OK, {Failed} Fehler, KB +{Indexed} (Dedup {Dedup}), F1={F1:P1}",
                processed, total, failed, stats.KbIndexed, stats.KbDeduplicated, stats.F1);

            // ── YOLO-Retraining PAUSIERT bis Eval-Set steht ──
            // Grund: 98.9% der Labels sind nicht menschlich verifiziert (Confirmation Bias Risiko).
            // Auto-Retrain ohne eingefrorenes Eval-Set ist ein Confirmation-Bias-Generator.
            // Reaktivieren wenn: 120+ manuell gelabelte Eval-Frames, Baseline-mAP gemessen, Gates definiert.
            bool autoRetrainEnabled = false; // ← HIER AKTIVIEREN wenn Eval-Set bereit
            if (autoRetrainEnabled && _yoloRetrain != null && processed > 0)
            {
                try
                {
                    progress?.Report(new BatchSelfTrainingProgress
                    {
                        CurrentIndex = total, TotalHaltungen = total,
                        Phase = "Retraining",
                        Status = "Pruefe YOLO-Retraining-Berechtigung...",
                        RunningStats = stats
                    });

                    _log?.LogInformation("Pruefe YOLO-Retraining nach Batch-Durchlauf...");
                    var retrainResult = await _yoloRetrain.RunIfEligibleAsync(ct: ct)
                        .ConfigureAwait(false);
                    _log?.LogInformation("YOLO-Retraining: {Status}", retrainResult.StatusText);

                    progress?.Report(new BatchSelfTrainingProgress
                    {
                        CurrentIndex = total, TotalHaltungen = total,
                        Phase = "Retraining",
                        Status = $"YOLO-Retraining: {retrainResult.StatusText}",
                        RunningStats = stats
                    });
                }
                catch (Exception ex)
                {
                    _log?.LogWarning(ex, "YOLO-Retraining nach Batch fehlgeschlagen");
                }
            }

            return BuildResult(startedUtc, sw.Elapsed, total, processed, skipped, failed, stats, haltungResults);
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>Verarbeitet eine einzelne Haltung.</summary>
    private async Task<BatchHaltungResult> ProcessSingleHaltungAsync(
        DiscoveredHaltung h,
        BatchSelfTrainingRequest request,
        BatchAutoApprovePolicy policy,
        BatchKbStats stats,
        IProgress<BatchSelfTrainingProgress>? progress,
        int currentIndex,
        int total,
        CancellationToken ct,
        System.Collections.Concurrent.ConcurrentDictionary<string, PreloadedProtocol>? preloaded = null)
    {
        var hSw = Stopwatch.StartNew();

        // Phase 1: Protokoll aus Vorladen-Cache oder frisch laden
        Domain.Protocol.ProtocolDocument? protocol;
        Domain.Models.HaltungRecord? record;

        if (preloaded is not null && preloaded.TryGetValue(h.HaltungId, out var cached))
        {
            if (cached.Error is not null)
                return new BatchHaltungResult
                {
                    HaltungId = h.HaltungId, VideoPath = h.VideoPath,
                    Success = false, Error = cached.Error,
                    Duration = hSw.Elapsed
                };
            protocol = cached.Protocol;
            record = cached.Record;
        }
        else
        {
            progress?.Report(new BatchSelfTrainingProgress
            {
                CurrentIndex = currentIndex, TotalHaltungen = total,
                HaltungId = h.HaltungId, Phase = "Import",
                Status = $"{h.HaltungId}: Protokoll importieren...",
                RunningStats = stats
            });
            (protocol, record) = _protocolLoader.LoadProtocolWithRecord(
                h.ProtocolSource, h.SourceType, h.HaltungId);
        }

        if (protocol is null || protocol.Original.Entries.Count == 0)
        {
            var baseReason = protocol is null
                ? "PDF konnte nicht gelesen werden"
                : "PDF gelesen aber keine Protokoll-Eintraege gefunden";

            // V4.2 Nachbesserung: Diagnose anhaengen damit User im UI sieht warum.
            string diagnostic;
            try { diagnostic = Services.ProtocolLoaderFactory.DiagnosePdf(h.ProtocolSource); }
            catch (Exception ex) { diagnostic = $"Diag-Exception: {ex.Message}"; }

            var reason = $"{baseReason} — {diagnostic}";
            _log?.LogWarning("Batch: {Id}: {Reason} (PDF: {Pdf})",
                h.HaltungId, reason, Path.GetFileName(h.ProtocolSource));
            return new BatchHaltungResult
            {
                HaltungId = h.HaltungId, VideoPath = h.VideoPath,
                Success = false, Error = reason,
                Duration = hSw.Elapsed
            };
        }

        var rohrmaterial = record?.GetFieldValue("Rohrmaterial");
        var dnText = record?.GetFieldValue("DN_mm");
        int? dn = int.TryParse(dnText, out var d) ? d : null;
        var hlText = record?.GetFieldValue("Haltungslaenge_m");
        double? inspLength = double.TryParse(
            hlText?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var hl) ? hl : null;

        // Phase 2: Video-Blinddurchlauf
        progress?.Report(new BatchSelfTrainingProgress
        {
            CurrentIndex = currentIndex, TotalHaltungen = total,
            HaltungId = h.HaltungId, Phase = "KI-Analyse",
            Status = $"{h.HaltungId}: Video wird analysiert...",
            RunningStats = stats
        });

        var trainingRequest = new VideoTrainingRequest
        {
            VideoPath = h.VideoPath,
            ProtocolSource = h.ProtocolSource,
            ProtocolSourceType = h.SourceType,
            Rohrmaterial = rohrmaterial,
            NennweiteMm = dn,
            InspektionslaengeMeter = inspLength,
            FrameStepSeconds = request.FrameStepSeconds,
            MeterTolerance = request.MeterTolerance
        };

        // Video-Progress an Batch-Progress weiterleiten
        var videoProgress = progress is not null
            ? new Progress<VideoTrainingProgress>(vp =>
            {
                progress.Report(new BatchSelfTrainingProgress
                {
                    CurrentIndex = currentIndex,
                    TotalHaltungen = total,
                    HaltungId = h.HaltungId,
                    Phase = "KI-Analyse",
                    Status = $"{h.HaltungId}: {vp.Status} ({vp.Current}/{vp.Total})",
                    RunningStats = stats
                });
            })
            : null;

        // V4.2 Phase 2: Protokoll-First-Modus auch fuer den sequentiellen Pfad durchreichen.
        _videoOrchestrator.UseProtocolFirst = request.UseProtocolFirst;
        _videoOrchestrator.ProtocolFirstMeterTolerance = request.ProtocolFirstMeterTolerance;
        _videoOrchestrator.EnableSurpriseGapsPass = request.EnableSurpriseGapsPass;
        _videoOrchestrator.SurpriseGapsFrameStep = request.SurpriseGapsFrameStep;

        var trainingResult = await _videoOrchestrator.RunAsync(
            trainingRequest, protocol, videoProgress, ct).ConfigureAwait(false);

        var report = trainingResult.Report;

        // Statistik aktualisieren
        stats.TruePositives += report.TruePositiveCount;
        stats.FalseNegatives += report.FalseNegativeCount;
        stats.FalsePositives += report.FalsePositiveCount;
        stats.CodeMismatches += report.CodeMismatchCount;

        // Phase 3: Auto-Approve in KB
        progress?.Report(new BatchSelfTrainingProgress
        {
            CurrentIndex = currentIndex, TotalHaltungen = total,
            HaltungId = h.HaltungId, Phase = "KB-Update",
            Status = $"{h.HaltungId}: KB-Anreicherung (TP:{report.TruePositiveCount} FN:{report.FalseNegativeCount} MM:{report.CodeMismatchCount})...",
            RunningStats = stats
        });

        var enrichResult = await _enrichment.AutoEnrichFromReportAsync(
            report, rohrmaterial, dn, policy, ct, haltungId: h.HaltungId).ConfigureAwait(false);

        stats.KbIndexed += enrichResult.Indexed;
        stats.KbDeduplicated += enrichResult.Deduplicated;
        stats.KbSkipped += enrichResult.Skipped;
        stats.Errors += enrichResult.Errors;

        // V4.2 Phase 1.4: Unsicherste Items in Review-Queue fuer manuelles Labeln.
        var samplingResult = _uncertaintySampler?.EnqueueTopUncertain(h.HaltungId, report);

        // V4.2 Nachbesserung A: Strukturiertes Haltungs-Telemetrie-Log.
        _log?.LogInformation(
            "Haltung {Id} Telemetry | auto_approved={Auto} queued={Q} rejected_by_gate={Gate} " +
            "dedup={Dd} errors={Err} tp={TP} fn={FN} fp={FP} mm={MM} mean_prio={Mean:F3} max_prio={Max:F3}",
            h.HaltungId,
            enrichResult.Indexed,
            samplingResult?.Enqueued ?? 0,
            enrichResult.Skipped,
            enrichResult.Deduplicated,
            enrichResult.Errors,
            report.TruePositiveCount, report.FalseNegativeCount,
            report.FalsePositiveCount, report.CodeMismatchCount,
            samplingResult?.MeanPriority ?? 0.0,
            samplingResult?.MaxPriority ?? 0.0);

        // Phase 4: YOLO-Trainingskandidaten generieren
        try
        {
            var yoloOutputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "KI_BRAIN", "yolo_training_candidates", h.HaltungId.Replace("/", "_"));

            var candidates = new List<Infrastructure.Import.WinCan.YoloTrainingDataGenerator.TrainingCandidate>();
            foreach (var entry in report.Entries)
            {
                if (string.IsNullOrEmpty(entry.FramePath) || !File.Exists(entry.FramePath))
                    continue;

                var opCode = entry.ProtocolEntry?.VsaCode ?? entry.KiDetection?.VsaCode ?? "";
                if (string.IsNullOrEmpty(opCode)) continue;

                var yoloClassId = Infrastructure.Import.WinCan.YoloTrainingDataGenerator.GetYoloClassId(opCode);
                var kiLabel = entry.KiDetection?.VsaCode ?? entry.KiDetection?.Label ?? "";
                var kiConf = entry.KiDetection?.Confidence ?? 0;

                // Nachbar-Frame-Konsistenz: vereinfacht — wenn TP dann stabil
                bool neighborMatch = entry.Category == AuswertungPro.Next.Application.Ai.Training.Models.DifferenceCategory.TruePositive;

                var quality = Infrastructure.Import.WinCan.YoloTrainingDataGenerator.ClassifyQuality(
                    opCode, kiLabel, kiConf, boxAreaNorm: 0.3, neighborMatch);

                // BBox aus KI-Detection (wenn vorhanden)
                double bx1 = entry.KiDetection?.BboxX1 ?? 0.1;
                double by1 = entry.KiDetection?.BboxY1 ?? 0.1;
                double bx2 = entry.KiDetection?.BboxX2 ?? 0.9;
                double by2 = entry.KiDetection?.BboxY2 ?? 0.9;

                // FP → Negativ-Beispiel (leeres Label)
                if (entry.Category == AuswertungPro.Next.Application.Ai.Training.Models.DifferenceCategory.FalsePositive)
                {
                    yoloClassId = -1;
                    quality = "green"; // FP-Frames sind wertvolle Negativ-Beispiele
                }

                candidates.Add(new Infrastructure.Import.WinCan.YoloTrainingDataGenerator.TrainingCandidate(
                    FramePath: entry.FramePath,
                    Quality: quality,
                    OperatorCode: opCode,
                    YoloClassId: yoloClassId,
                    YoloLabel: kiLabel,
                    YoloConfidence: kiConf,
                    BoxX1: bx1 * 640, BoxY1: by1 * 480,
                    BoxX2: bx2 * 640, BoxY2: by2 * 480,
                    ImgWidth: 640, ImgHeight: 480,
                    Reason: $"{entry.Category}: op={opCode} ki={kiLabel} conf={kiConf:F2}"));
            }

            if (candidates.Count > 0)
            {
                var genStats = Infrastructure.Import.WinCan.YoloTrainingDataGenerator.SaveCandidates(
                    candidates, yoloOutputDir);
                _log?.LogInformation(
                    "Batch [{I}/{T}] {Id}: YOLO-Training G={G} Y={Y} R={R} Neg={N}",
                    currentIndex, total, h.HaltungId,
                    genStats.Green, genStats.Yellow, genStats.Red, genStats.Negatives);
            }
        }
        catch (Exception yoloGenEx)
        {
            _log?.LogWarning(yoloGenEx, "Batch [{I}/{T}] {Id}: YOLO-Kandidaten Fehler",
                currentIndex, total, h.HaltungId);
        }

        hSw.Stop();

        _log?.LogInformation(
            "Batch [{I}/{T}] {Id}: TP={TP} FN={FN} FP={FP} MM={MM}, KB +{KB} (Dedup {DD}) in {Dur:F0}s",
            currentIndex, total, h.HaltungId,
            report.TruePositiveCount, report.FalseNegativeCount,
            report.FalsePositiveCount, report.CodeMismatchCount,
            enrichResult.Indexed, enrichResult.Deduplicated,
            hSw.Elapsed.TotalSeconds);

        return new BatchHaltungResult
        {
            HaltungId = h.HaltungId,
            VideoPath = h.VideoPath,
            Success = true,
            ProtocolEntries = protocol.Original.Entries.Count,
            KiDetections = report.TruePositiveCount + report.FalsePositiveCount + report.CodeMismatchCount,
            TruePositives = report.TruePositiveCount,
            FalseNegatives = report.FalseNegativeCount,
            FalsePositives = report.FalsePositiveCount,
            CodeMismatches = report.CodeMismatchCount,
            KbIndexed = enrichResult.Indexed,
            KbDeduplicated = enrichResult.Deduplicated,
            Duration = hSw.Elapsed
        };
    }

    /// <summary>
    /// Parallele Variante: Verarbeitet eine Haltung mit eigenem Orchestrator.
    /// Stats werden thread-safe via Lock aktualisiert.
    /// </summary>
    private async Task<BatchHaltungResult> ProcessSingleHaltungParallelAsync(
        DiscoveredHaltung h,
        BatchSelfTrainingRequest request,
        BatchAutoApprovePolicy policy,
        BatchKbStats stats,
        object statsLock,
        VideoSelfTrainingOrchestrator localOrch,
        IProgress<BatchSelfTrainingProgress>? progress,
        int currentIndex,
        int total,
        int parallelism,
        CancellationToken ct,
        System.Collections.Concurrent.ConcurrentDictionary<string, PreloadedProtocol>? preloaded = null)
    {
        var hSw = Stopwatch.StartNew();

        // Phase 1: Protokoll aus Vorladen-Cache oder frisch laden
        Domain.Protocol.ProtocolDocument? protocol;
        Domain.Models.HaltungRecord? record;

        if (preloaded is not null && preloaded.TryGetValue(h.HaltungId, out var cached))
        {
            if (cached.Error is not null)
                return new BatchHaltungResult
                {
                    HaltungId = h.HaltungId, VideoPath = h.VideoPath,
                    Success = false, Error = cached.Error,
                    Duration = hSw.Elapsed
                };
            protocol = cached.Protocol;
            record = cached.Record;
        }
        else
        {
            (protocol, record) = _protocolLoader.LoadProtocolWithRecord(
                h.ProtocolSource, h.SourceType, h.HaltungId);
        }

        if (protocol is null || protocol.Original.Entries.Count == 0)
        {
            var baseReason = protocol is null
                ? "PDF konnte nicht gelesen werden"
                : "PDF gelesen aber keine Protokoll-Eintraege gefunden";

            // V4.2 Nachbesserung: Diagnose anhaengen damit User im UI sieht warum.
            string diagnostic;
            try { diagnostic = Services.ProtocolLoaderFactory.DiagnosePdf(h.ProtocolSource); }
            catch (Exception ex) { diagnostic = $"Diag-Exception: {ex.Message}"; }

            var reason = $"{baseReason} — {diagnostic}";
            _log?.LogWarning("Batch: {Id}: {Reason} (PDF: {Pdf})",
                h.HaltungId, reason, Path.GetFileName(h.ProtocolSource));
            return new BatchHaltungResult
            {
                HaltungId = h.HaltungId, VideoPath = h.VideoPath,
                Success = false, Error = reason,
                Duration = hSw.Elapsed
            };
        }

        var rohrmaterial = record?.GetFieldValue("Rohrmaterial");
        var dnText = record?.GetFieldValue("DN_mm");
        int? dn = int.TryParse(dnText, out var d) ? d : null;
        var hlText = record?.GetFieldValue("Haltungslaenge_m");
        double? inspLength = double.TryParse(
            hlText?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var hl) ? hl : null;

        // Phase 2: Video-Blinddurchlauf (mit eigenem Orchestrator)
        progress?.Report(new BatchSelfTrainingProgress
        {
            CurrentIndex = currentIndex, TotalHaltungen = total,
            HaltungId = h.HaltungId, Phase = "KI-Analyse",
            Status = $"[P{parallelism}] {h.HaltungId}: Video wird analysiert...",
            RunningStats = stats
        });

        var trainingRequest = new VideoTrainingRequest
        {
            VideoPath = h.VideoPath,
            ProtocolSource = h.ProtocolSource,
            ProtocolSourceType = h.SourceType,
            Rohrmaterial = rohrmaterial,
            NennweiteMm = dn,
            InspektionslaengeMeter = inspLength,
            FrameStepSeconds = request.FrameStepSeconds,
            MeterTolerance = request.MeterTolerance
        };

        // Frame-Fortschritt an Batch-Progress weiterleiten
        var frameProgress = progress is not null
            ? new Progress<VideoTrainingProgress>(fp =>
                progress.Report(new BatchSelfTrainingProgress
                {
                    CurrentIndex = currentIndex, TotalHaltungen = total,
                    HaltungId = h.HaltungId, Phase = fp.Phase,
                    Status = $"[P{parallelism}] {h.HaltungId}: {fp.Status} ({fp.Current}/{fp.Total})",
                    RunningStats = stats
                }))
            : null;

        var trainingResult = await localOrch.RunAsync(
            trainingRequest, protocol, frameProgress, ct).ConfigureAwait(false);

        var report = trainingResult.Report;

        // Thread-sichere Statistik-Updates
        lock (statsLock)
        {
            stats.TruePositives += report.TruePositiveCount;
            stats.FalseNegatives += report.FalseNegativeCount;
            stats.FalsePositives += report.FalsePositiveCount;
            stats.CodeMismatches += report.CodeMismatchCount;
        }

        // Phase 3: KB-Anreicherung (KbEnrichmentService ist thread-safe dank SQLite-Transaktion)
        var enrichResult = await _enrichment.AutoEnrichFromReportAsync(
            report, rohrmaterial, dn, policy, ct, haltungId: h.HaltungId).ConfigureAwait(false);

        lock (statsLock)
        {
            stats.KbIndexed += enrichResult.Indexed;
            stats.KbDeduplicated += enrichResult.Deduplicated;
            stats.KbSkipped += enrichResult.Skipped;
            stats.Errors += enrichResult.Errors;
        }

        // V4.2 Phase 1.4: Unsicherste Items in Review-Queue fuer manuelles Labeln.
        var samplingResult = _uncertaintySampler?.EnqueueTopUncertain(h.HaltungId, report);

        // V4.2 Nachbesserung A: Strukturiertes Haltungs-Telemetrie-Log.
        _log?.LogInformation(
            "Haltung {Id} Telemetry | auto_approved={Auto} queued={Q} rejected_by_gate={Gate} " +
            "dedup={Dd} errors={Err} tp={TP} fn={FN} fp={FP} mm={MM} mean_prio={Mean:F3} max_prio={Max:F3}",
            h.HaltungId,
            enrichResult.Indexed,
            samplingResult?.Enqueued ?? 0,
            enrichResult.Skipped,
            enrichResult.Deduplicated,
            enrichResult.Errors,
            report.TruePositiveCount, report.FalseNegativeCount,
            report.FalsePositiveCount, report.CodeMismatchCount,
            samplingResult?.MeanPriority ?? 0.0,
            samplingResult?.MaxPriority ?? 0.0);

        hSw.Stop();

        _log?.LogInformation(
            "Batch [{I}/{T}] {Id}: TP={TP} FN={FN} FP={FP} MM={MM}, KB +{KB} (Dedup {DD}) in {Dur:F0}s",
            currentIndex, total, h.HaltungId,
            report.TruePositiveCount, report.FalseNegativeCount,
            report.FalsePositiveCount, report.CodeMismatchCount,
            enrichResult.Indexed, enrichResult.Deduplicated,
            hSw.Elapsed.TotalSeconds);

        return new BatchHaltungResult
        {
            HaltungId = h.HaltungId,
            VideoPath = h.VideoPath,
            Success = true,
            ProtocolEntries = protocol.Original.Entries.Count,
            KiDetections = report.TruePositiveCount + report.FalsePositiveCount + report.CodeMismatchCount,
            TruePositives = report.TruePositiveCount,
            FalseNegatives = report.FalseNegativeCount,
            FalsePositives = report.FalsePositiveCount,
            CodeMismatches = report.CodeMismatchCount,
            KbIndexed = enrichResult.Indexed,
            KbDeduplicated = enrichResult.Deduplicated,
            Duration = hSw.Elapsed
        };
    }

    // ═══ Haltungen im Ordner entdecken ═══════════════════════════════════

    // Video-Erweiterungen die wir suchen
    private static readonly string[] VideoExtensions = [".mp4", ".mpg", ".mpeg", ".avi", ".mkv", ".wmv"];

    /// <summary>
    /// Scannt einen Export-Ordner und findet alle Haltungen die ein Video + Protokoll haben.
    ///
    /// Primaere Struktur (D:\Haltungen):
    ///   Jeder Unterordner = eine Haltung
    ///   Darin: *.pdf (Protokoll) + *.mp4/*.mpg (Video)
    ///
    /// Sekundaer: WinCan-Ordner (DB3) und IBAK-Ordner (Daten.txt).
    /// </summary>
    /// <summary>
    /// Windows-System-/Hidden-Ordner die beim Laufwerk-Scan Berechtigungsfehler werfen.
    /// Case-insensitive Match auf den Ordnernamen (nicht auf den ganzen Pfad).
    /// </summary>
    private static readonly HashSet<string> SystemFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System Volume Information",
        "$RECYCLE.BIN",
        "Recovery",
        "Config.Msi",
        "MSOCache",
        ".git",
        "node_modules",
        "__pycache__",
        ".venv",
        "venv"
    };

    /// <summary>
    /// Rekursiver Verzeichnis-Scan der System-Ordner ueberspringt und UnauthorizedAccessException pro Ordner
    /// einzeln abfaengt. Vermeidet Crash beim Scan von Laufwerk-Roots (C:, E:, etc.).
    /// </summary>
    private void SafeRecursiveDirectoryScan(string root, List<string> accumulator)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (SystemFolderNames.Contains(name)) continue;

                // Hidden/System-Attribut ueberspringen
                try
                {
                    var attr = File.GetAttributes(dir);
                    if ((attr & (FileAttributes.System | FileAttributes.Hidden)) == (FileAttributes.System | FileAttributes.Hidden))
                        continue;
                }
                catch { /* Attribut nicht lesbar → trotzdem versuchen */ }

                accumulator.Add(dir);
                SafeRecursiveDirectoryScan(dir, accumulator);
            }
        }
        catch (UnauthorizedAccessException) { /* Ordner ueberspringen */ }
        catch (System.IO.DirectoryNotFoundException) { /* geloescht waehrend Scan */ }
        catch (Exception ex)
        {
            _log?.LogDebug(ex, "Discovery: {Root} uebersprungen", root);
        }
    }

    private List<DiscoveredHaltung> DiscoverHaltungen(BatchSelfTrainingRequest request)
    {
        var root = request.ExportRootPath;
        if (!Directory.Exists(root)) return [];

        var result = new List<DiscoveredHaltung>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var scanDirs = new List<string> { root };
        if (request.RecurseSubdirectories)
            SafeRecursiveDirectoryScan(root, scanDirs);
        else
        {
            try
            {
                scanDirs.AddRange(Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly));
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "Discovery: Top-Level-Unterordner nicht vollstaendig gelesen");
            }
        }

        // Hauptstrategie: Ordner-pro-Haltung mit PDF + Video (D:\Haltungen-Struktur)
        foreach (var dir in scanDirs)
        {
            var haltungId = Path.GetFileName(dir);

            // Video finden (erstes passendes)
            string? videoPath = null;
            foreach (var ext in VideoExtensions)
            {
                var videos = Directory.GetFiles(dir, $"*{ext}");
                if (videos.Length > 0)
                {
                    videoPath = videos[0];
                    break;
                }
            }

            if (videoPath is null)
            {
                _log?.LogDebug("Discovery: {Dir} uebersprungen — kein Video gefunden", haltungId);
                continue;
            }

            // PDF finden (Inspektionsprotokoll, keine Nebenakten)
            var pdfs = Directory.GetFiles(dir, "*.pdf")
                .Where(p =>
                {
                    var fn = Path.GetFileName(p);
                    // Bekannte Nicht-Protokoll-PDFs ausfiltern
                    return !fn.Contains("Plan", StringComparison.OrdinalIgnoreCase)
                        && !fn.Contains("compressed", StringComparison.OrdinalIgnoreCase)
                        && !fn.Contains("Rechnung", StringComparison.OrdinalIgnoreCase)
                        && !fn.Contains("Offerte", StringComparison.OrdinalIgnoreCase)
                        && !fn.Contains("Angebot", StringComparison.OrdinalIgnoreCase)
                        && !fn.Contains("Liner", StringComparison.OrdinalIgnoreCase)
                        && !fn.Contains("Sanierung", StringComparison.OrdinalIgnoreCase)
                        && !fn.Contains("Kosten", StringComparison.OrdinalIgnoreCase)
                        && !fn.Contains("Devis", StringComparison.OrdinalIgnoreCase)
                        && !fn.Contains("Rapport", StringComparison.OrdinalIgnoreCase)
                        && !fn.Contains("Foto", StringComparison.OrdinalIgnoreCase)
                        && !fn.Contains("Photo", StringComparison.OrdinalIgnoreCase);
                })
                .OrderByDescending(p => new FileInfo(p).Length) // Groesstes PDF zuerst
                .ToArray();

            if (pdfs.Length == 0)
            {
                _log?.LogDebug("Discovery: {Dir} uebersprungen — kein Inspektions-PDF gefunden", haltungId);
                continue;
            }

            // Inspektions-PDF waehlen: bevorzugt eines das den Haltungsnamen enthaelt
            var pdfPath = pdfs.FirstOrDefault(p =>
                Path.GetFileNameWithoutExtension(p).Contains(haltungId.Replace(".", ""), StringComparison.OrdinalIgnoreCase))
                ?? pdfs[0];

            result.Add(new DiscoveredHaltung
            {
                HaltungId = haltungId,
                VideoPath = videoPath,
                ProtocolSource = pdfPath,
                SourceType = ProtocolSourceTypes.InspektionsPdf,
                ExportDir = dir
            });

            var key = BuildHistoryKey(result[^1]);
            if (!seen.Add(key))
                result.RemoveAt(result.Count - 1);
        }

        _log?.LogInformation("Discovery: {Count} Haltungen mit Video+PDF in {Root}",
            result.Count, root);

        return result;
    }

    // ═══ Pause / Cancel ══════════════════════════════════════════════════

    public void Pause()
    {
        lock (_pauseSync)
        {
            if (_isPaused) return;
            _isPaused = true;
            _pauseGate.Wait();
        }
    }

    public void Resume()
    {
        lock (_pauseSync)
        {
            if (!_isPaused) return;
            _isPaused = false;
            _pauseGate.Release();
        }
    }

    public void Cancel()
    {
        _isCancelled = true;
        // Wenn gerade pausiert: Gate freigeben, damit die Worker aus CheckPauseAsync
        // herauskommen und dank _isCancelled den Batch sauber beenden koennen.
        lock (_pauseSync)
        {
            if (_isPaused)
            {
                _isPaused = false;
                _pauseGate.Release();
            }
        }
    }

    private async Task CheckPauseAsync(CancellationToken ct)
    {
        if (!_isPaused) return;
        await _pauseGate.WaitAsync(ct).ConfigureAwait(false);
        _pauseGate.Release();
    }

    // ═══ Sidecar Auto-Restart ═════════════════════════════════════════════

    /// <summary>
    /// Prueft ob der Sidecar (YOLO/DINO/SAM) erreichbar ist.
    /// Falls nicht → automatisch neu starten und warten bis bereit.
    /// Verhindert dass der Batch nach einem Sidecar-Crash nur noch
    /// im schwachen Ollama-Only-Modus laeuft.
    /// </summary>
    private async Task EnsureSidecarRunningAsync(
        IProgress<BatchSelfTrainingProgress>? progress,
        int currentIndex, int total, string haltungId,
        BatchKbStats stats, CancellationToken ct)
    {
        // Schnell-Check: Sidecar erreichbar?
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var healthUrlCheck = _sidecarUrl.TrimEnd('/') + "/health";
            var resp = await http.GetAsync(healthUrlCheck, ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode) return; // Sidecar laeuft → weiter
        }
        catch { /* Sidecar nicht erreichbar */ }

        // Sidecar ist tot → neu starten
        _log?.LogWarning("Batch: Sidecar nicht erreichbar — Neustart...");
        progress?.Report(new BatchSelfTrainingProgress
        {
            CurrentIndex = currentIndex, TotalHaltungen = total,
            HaltungId = haltungId, Phase = "Sidecar-Restart",
            Status = "Sidecar (YOLO/DINO/SAM) nicht erreichbar — wird neu gestartet...",
            RunningStats = stats
        });

        // Sidecar-Verzeichnis bestimmen
        var sidecarDir = _sidecarDir
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "sidecar");
        var venvPython = Path.Combine(sidecarDir, ".venv", "Scripts", "python.exe");

        if (!File.Exists(venvPython))
        {
            _log?.LogWarning("Batch: Python venv nicht gefunden: {Path} — weiter ohne Sidecar", venvPython);
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = venvPython,
                Arguments = "-m uvicorn sidecar.main:app --host 127.0.0.1 --port 8100",
                WorkingDirectory = sidecarDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // V4.2 Fix: Process-Object disposen (Subprozess laeuft weiter, nur Management-Handles werden freigegeben).
            using var proc = Process.Start(psi);
            if (proc is null)
            {
                _log?.LogWarning("Batch: Sidecar-Prozess konnte nicht gestartet werden");
                return;
            }

            // stdout/stderr im Hintergrund lesen (verhindert Deadlock)
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            _log?.LogInformation("Batch: Sidecar-Prozess gestartet (PID={Pid})", proc.Id);

            // V4.2 Fix: Sidecar-URL aus Config statt hardcoded (_sidecarUrl wird im Ctor gesetzt).
            var healthUrl = _sidecarUrl.TrimEnd('/') + "/health";

            // Warten bis Health-Check antwortet (max 45s)
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            for (int attempt = 0; attempt < 15; attempt++)
            {
                await Task.Delay(3000, ct).ConfigureAwait(false);
                try
                {
                    var resp = await http.GetAsync(healthUrl, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        _log?.LogInformation("Batch: Sidecar neu gestartet und bereit");
                        progress?.Report(new BatchSelfTrainingProgress
                        {
                            CurrentIndex = currentIndex, TotalHaltungen = total,
                            HaltungId = haltungId, Phase = "Sidecar-Restart",
                            Status = "Sidecar neu gestartet — bereit",
                            RunningStats = stats
                        });
                        return;
                    }
                }
                catch { /* Noch nicht bereit */ }
            }

            _log?.LogWarning("Batch: Sidecar nicht bereit nach 45s — weiter ohne Sidecar");
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Batch: Sidecar-Neustart fehlgeschlagen");
        }
    }

    // ═══ Logging ════════════════════════════════════════════════════════

    private static void LogToFile(string message)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SewerStudio", "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(
                Path.Combine(logDir, "batch_nachtbetrieb.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}\n");
        }
        catch { /* Logging darf nie crashen */ }
    }

    // ═══ Hilfsmethoden ═══════════════════════════════════════════════════

    private static BatchSelfTrainingResult BuildResult(
        DateTime startedUtc, TimeSpan duration,
        int total, int processed, int skipped, int failed,
        BatchKbStats stats, List<BatchHaltungResult> results)
    {
        return new BatchSelfTrainingResult
        {
            StartedUtc = startedUtc,
            TotalDuration = duration,
            TotalHaltungen = total,
            Processed = processed,
            Skipped = skipped,
            Failed = failed,
            FinalStats = stats,
            HaltungResults = results
        };
    }

    /// <summary>Intern: Eine entdeckte Haltung mit Video + Protokoll-Quelle.</summary>
    /// <summary>Vorgeladenes Protokoll aus der CPU-Vorlade-Phase.</summary>
    private sealed record PreloadedProtocol(
        AuswertungPro.Next.Domain.Protocol.ProtocolDocument? Protocol,
        AuswertungPro.Next.Domain.Models.HaltungRecord? Record,
        string? Error = null);

    private sealed class DiscoveredHaltung
    {
        public required string HaltungId { get; init; }
        public required string VideoPath { get; init; }
        public required string ProtocolSource { get; init; }
        public required string SourceType { get; init; }
        public required string ExportDir { get; init; }
    }

    // ═══ Batch-History: Merkt sich welche Ordner bereits verarbeitet wurden ═══

    private static string GetBatchHistoryPath()
        => Path.Combine(KnowledgeRoot.GetRoot(), "batch_processed.txt");

    /// <summary>
    /// Laedt die Liste bereits verarbeiteter Batch-Keys.
    /// Nur v2-Eintraege werden ausgewertet (Legacy-IDs ohne Pfadkontext werden ignoriert).
    /// </summary>
    private static async Task<HashSet<string>> LoadBatchHistoryAsync()
    {
        var path = GetBatchHistoryPath();
        if (!File.Exists(path))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var lines = await File.ReadAllLinesAsync(path).ConfigureAwait(false);
        return new HashSet<string>(
            lines.Where(l =>
                    !string.IsNullOrWhiteSpace(l)
                    && l.StartsWith("v2|", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Trim()),
            StringComparer.OrdinalIgnoreCase);
    }

    // Schuetzt Parallel-Append an batch_processed.txt. Bei MaxParallelHaltungen>1
    // wuerde ohne Lock `File.AppendAllTextAsync` verschraenkte Zeilen schreiben
    // koennen — mit Folge: Haltungen doppelt verarbeitet oder faelschlich als
    // "erledigt" markiert.
    private static readonly SemaphoreSlim _batchHistoryLock = new(1, 1);

    /// <summary>Fuegt einen Batch-History-Key hinzu (append, kein volles Neuschreiben).</summary>
    private static async Task AppendBatchHistoryAsync(string historyKey)
    {
        var path = GetBatchHistoryPath();
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
        await _batchHistoryLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(path, historyKey + Environment.NewLine).ConfigureAwait(false);
        }
        finally
        {
            _batchHistoryLock.Release();
        }

        // Rotation: aelteste Eintraege entfernen wenn Datei zu gross wird
        await RotateBatchHistoryIfNeededAsync(path).ConfigureAwait(false);
    }

    /// <summary>
    /// Baut einen stabilen History-Key fuer eine Haltung.
    /// Enthaelt HaltungId + absolute Video/Protokoll-Pfade, um Kollisionen bei gleichen IDs zu vermeiden.
    /// </summary>
    private static string BuildHistoryKey(DiscoveredHaltung h)
    {
        var id = h.HaltungId.Trim().ToUpperInvariant();
        var video = NormalizePathForHistory(h.VideoPath);
        var protocol = NormalizePathForHistory(h.ProtocolSource);
        return $"v2|{id}|{video}|{protocol}";
    }

    private static string NormalizePathForHistory(string path)
    {
        try
        {
            return Path.GetFullPath(path)
                .Trim()
                .Replace('\\', '/')
                .ToLowerInvariant();
        }
        catch
        {
            return path.Trim().Replace('\\', '/').ToLowerInvariant();
        }
    }

    private static async Task RotateBatchHistoryIfNeededAsync(string path)
    {
        const int maxLines = 5000;
        const int trimTo = 4000;

        try
        {
            var lines = await File.ReadAllLinesAsync(path).ConfigureAwait(false);
            if (lines.Length <= maxLines) return;

            // Aelteste Eintraege (oben) entfernen, neueste behalten
            var kept = lines.Skip(lines.Length - trimTo).ToArray();
            await File.WriteAllLinesAsync(path, kept).ConfigureAwait(false);
        }
        catch { /* Best effort — Rotation darf nie die Hauptlogik stoeren */ }
    }
}
