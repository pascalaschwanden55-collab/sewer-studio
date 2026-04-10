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
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.UI.Ai.Training.Services;
using Microsoft.Extensions.Logging;

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
public sealed class BatchSelfTrainingOrchestrator
{
    private readonly VideoSelfTrainingOrchestrator _videoOrchestrator;
    private readonly ProtocolLoaderFactory _protocolLoader;
    private readonly KbEnrichmentService _enrichment;
    private readonly ILogger? _log;

    // Zustandsverwaltung
    private volatile bool _isPaused;
    private volatile bool _isCancelled;
    private readonly SemaphoreSlim _pauseGate = new(1, 1);

    // Sidecar Auto-Restart: Pfad zum Sidecar-Verzeichnis
    private readonly string? _sidecarDir;

    public bool IsPaused => _isPaused;
    public bool IsRunning { get; private set; }

    public BatchSelfTrainingOrchestrator(
        VideoSelfTrainingOrchestrator videoOrchestrator,
        ProtocolLoaderFactory protocolLoader,
        KbEnrichmentService enrichment,
        ILogger? log = null,
        string? sidecarDir = null)
    {
        _videoOrchestrator = videoOrchestrator ?? throw new ArgumentNullException(nameof(videoOrchestrator));
        _protocolLoader = protocolLoader ?? throw new ArgumentNullException(nameof(protocolLoader));
        _enrichment = enrichment ?? throw new ArgumentNullException(nameof(enrichment));
        _log = log;
        _sidecarDir = sidecarDir;
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
                var processedDirs = await LoadBatchHistoryAsync().ConfigureAwait(false);

                var beforeCount = haltungen.Count;
                haltungen = haltungen
                    .Where(h => !processedDirs.Contains(h.HaltungId))
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
                LearnFromMissed = request.LearnFromMissed
            };

            // Zeitmessung fuer Restzeit-Schaetzung
            var haltungTimes = new List<double>();

            // 2. Haltung fuer Haltung verarbeiten
            for (int i = 0; i < total; i++)
            {
                if (_isCancelled || ct.IsCancellationRequested) break;
                await CheckPauseAsync(ct).ConfigureAwait(false);

                var h = haltungen[i];
                var hSw = Stopwatch.StartNew();

                // Restzeit schaetzen
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

                // Sidecar Auto-Restart: Vor jeder Haltung pruefen ob Sidecar lebt
                await EnsureSidecarRunningAsync(progress, i + 1, total, h.HaltungId, stats, ct)
                    .ConfigureAwait(false);

                try
                {
                    var hSw2 = Stopwatch.StartNew();
                    var result = await ProcessSingleHaltungAsync(
                        h, request, policy, stats, progress, i + 1, total, ct)
                        .ConfigureAwait(false);
                    hSw2.Stop();

                    // IMMER in Datei loggen — Debug/Progress kann verloren gehen
                    LogToFile($"[{i+1}/{total}] {h.HaltungId}: Success={result.Success}, " +
                        $"TP={result.TruePositives}, FN={result.FalseNegatives}, FP={result.FalsePositives}, " +
                        $"KB=+{result.KbIndexed}, Error={result.Error}, Dauer={hSw2.Elapsed.TotalSeconds:F1}s");

                    haltungResults.Add(result);

                    // Haltung IMMER als verarbeitet merken — auch bei Fehler.
                    // Verhindert endloses Wiederholen der gleichen Haltungen.
                    await AppendBatchHistoryAsync(h.HaltungId).ConfigureAwait(false);

                    if (result.Success)
                    {
                        processed++;
                        // Ergebnis anzeigen
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
                        // Fehler anzeigen
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
                    // Auch bei Exception als verarbeitet merken
                    try { await AppendBatchHistoryAsync(h.HaltungId).ConfigureAwait(false); } catch { }
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
        CancellationToken ct)
    {
        var hSw = Stopwatch.StartNew();

        // Phase 1: Protokoll laden
        progress?.Report(new BatchSelfTrainingProgress
        {
            CurrentIndex = currentIndex, TotalHaltungen = total,
            HaltungId = h.HaltungId, Phase = "Import",
            Status = $"{h.HaltungId}: Protokoll importieren...",
            RunningStats = stats
        });

        var (protocol, record) = _protocolLoader.LoadProtocolWithRecord(
            h.ProtocolSource, h.SourceType, h.HaltungId);

        if (protocol is null || protocol.Original.Entries.Count == 0)
        {
            var reason = protocol is null
                ? "PDF konnte nicht gelesen werden"
                : "PDF gelesen aber keine Protokoll-Eintraege gefunden";
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
    private List<DiscoveredHaltung> DiscoverHaltungen(BatchSelfTrainingRequest request)
    {
        var root = request.ExportRootPath;
        if (!Directory.Exists(root)) return [];

        var result = new List<DiscoveredHaltung>();

        // Hauptstrategie: Ordner-pro-Haltung mit PDF + Video (D:\Haltungen-Struktur)
        foreach (var dir in Directory.GetDirectories(root))
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
        }

        _log?.LogInformation("Discovery: {Count} Haltungen mit Video+PDF in {Root}",
            result.Count, root);

        return result;
    }

    // ═══ Pause / Cancel ══════════════════════════════════════════════════

    public void Pause()
    {
        if (!_isPaused)
        {
            _isPaused = true;
            _pauseGate.Wait();
        }
    }

    public void Resume()
    {
        if (_isPaused)
        {
            _isPaused = false;
            _pauseGate.Release();
        }
    }

    public void Cancel() => _isCancelled = true;

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
            var resp = await http.GetAsync("http://localhost:8100/health", ct).ConfigureAwait(false);
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

            var proc = Process.Start(psi);
            if (proc is null)
            {
                _log?.LogWarning("Batch: Sidecar-Prozess konnte nicht gestartet werden");
                return;
            }

            // stdout/stderr im Hintergrund lesen (verhindert Deadlock)
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            _log?.LogInformation("Batch: Sidecar-Prozess gestartet (PID={Pid})", proc.Id);

            // Warten bis Health-Check antwortet (max 45s)
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            for (int attempt = 0; attempt < 15; attempt++)
            {
                await Task.Delay(3000, ct).ConfigureAwait(false);
                try
                {
                    var resp = await http.GetAsync("http://localhost:8100/health", ct).ConfigureAwait(false);
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

    /// <summary>Laedt die Liste bereits verarbeiteter Haltungs-Ordner.</summary>
    private static async Task<HashSet<string>> LoadBatchHistoryAsync()
    {
        var path = GetBatchHistoryPath();
        if (!File.Exists(path))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var lines = await File.ReadAllLinesAsync(path).ConfigureAwait(false);
        return new HashSet<string>(
            lines.Where(l => !string.IsNullOrWhiteSpace(l)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Fuegt eine Haltung zur History hinzu (append, kein volles Neuschreiben).</summary>
    private static async Task AppendBatchHistoryAsync(string haltungId)
    {
        var path = GetBatchHistoryPath();
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
        await File.AppendAllTextAsync(path, haltungId + Environment.NewLine).ConfigureAwait(false);
    }
}
