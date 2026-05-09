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
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.UI.Ai.Training.Services;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai.Training;
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
public sealed partial class BatchSelfTrainingOrchestrator : AuswertungPro.Next.Application.Ai.Training.IBatchSelfTrainingOrchestrator
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
    /// Phase 2.3b: Provenance-Wrapper. Jeder Batch-Lauf wird als ein einzelner
    /// <see cref="TrainingRun"/> erfasst. Bei Erfolg Complete mit Anzahl
    /// erfolgreich verarbeiteter Haltungen als samplesAffected. Wenn nichts
    /// entdeckt/zu tun war (TotalHaltungen==0 und Processed==0) wird der Run
    /// als Cancelled markiert. Exceptions werden zu Fail/Cancel und re-thrown.
    /// </summary>
    public async Task<BatchSelfTrainingResult> RunAsync(
        BatchSelfTrainingRequest request,
        IProgress<BatchSelfTrainingProgress>? progress = null,
        CancellationToken ct = default)
    {
        var run = await TrainingRunsStore.BeginRunAsync(
            TrainingRunTriggers.SelfTraining,
            notes: $"batch root={request.ExportRootPath}").ConfigureAwait(false);

        try
        {
            var result = await RunCoreAsync(request, progress, ct).ConfigureAwait(false);

            // Nichts zu tun (keine Haltungen entdeckt oder alle bereits verarbeitet)
            if (result.TotalHaltungen == 0 && result.Processed == 0)
            {
                await TrainingRunsStore.CancelRunAsync(
                    run.RunId,
                    notes: $"Skipped={result.Skipped}, keine neuen Haltungen").ConfigureAwait(false);
            }
            else
            {
                await TrainingRunsStore.CompleteRunAsync(
                    run.RunId,
                    samplesAffected: result.Processed,
                    notes: $"processed={result.Processed} failed={result.Failed} skipped={result.Skipped} kbIndexed={result.FinalStats.KbIndexed}")
                    .ConfigureAwait(false);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            await TrainingRunsStore.CancelRunAsync(run.RunId, "Cancelled").ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await TrainingRunsStore.FailRunAsync(run.RunId, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Startet den Batch-Durchlauf.
    /// Scannt den Export-Ordner, findet alle Haltungen mit Video + Protokoll,
    /// und verarbeitet sie nacheinander.
    /// </summary>
    private async Task<BatchSelfTrainingResult> RunCoreAsync(
        BatchSelfTrainingRequest request,
        IProgress<BatchSelfTrainingProgress>? progress,
        CancellationToken ct)
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

}
