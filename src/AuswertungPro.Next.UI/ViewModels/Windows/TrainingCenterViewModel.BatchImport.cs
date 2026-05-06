using System;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Application.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training.Services;
using AuswertungPro.Next.UI.Services;
using AiTrack = AuswertungPro.Next.UI.Services.AiActivityTracker;

/// <summary>
/// Phase 6.2: Batch-Import + KB-Indexing + KB-Check ausgelagert aus
/// TrainingCenterViewModel. Reduziert Hauptdatei um ~660 Zeilen.
/// </summary>
public partial class TrainingCenterViewModel
{
    /// <summary>
    /// Batch-Import: Scannt alle Ordner, generiert Samples, approved automatisch,
    /// indiziert in die Knowledge Base. Alles in einem Durchlauf.
    /// </summary>
    [RelayCommand]
    private async Task BatchImportAndIndexAsync()
    {
        if (IsBusy) return;
        if (_rootFolders.Count == 0)
        {
            StatusText = "Bitte zuerst einen oder mehrere Ordner wählen.";
            return;
        }

        var ct = RotateGenCts();

        using var _aiToken = AiTrack.Begin("Training Center");
        try
        {
            IsBusy = true;
            LogText = "";
            ProgressValue = 0;
            ProgressMax = 1;
            ClearLivePreview();
            ResetSelfTrainingVisuals(); // Ergebnis-Verlauf + Code-Verteilung + Match-Rate zuruecksetzen

            // 1. Scan aller Root-Ordner
            Log($"Scanne {_rootFolders.Count} Ordner...");
            StatusText = "Scanne Ordner...";
            var found = new List<TrainingCase>();
            foreach (var folder in _rootFolders)
            {
                if (!Directory.Exists(folder))
                {
                    Log($"  WARNUNG: Ordner existiert nicht: {folder}");
                    continue;
                }
                Log($"  Scanne: {folder}");
                var result = await _import.ScanAsync(folder);
                found.AddRange(result);
            }
            var casesWithProtocol = found.Where(c => !string.IsNullOrEmpty(c.ProtocolPath)).ToList();

            Log($"Gefunden: {found.Count} Ordner, {casesWithProtocol.Count} mit Protokoll");
            foreach (var c in found)
            {
                var hasVideo = !string.IsNullOrEmpty(c.VideoPath) ? "Video" : "kein Video";
                var hasProto = !string.IsNullOrEmpty(c.ProtocolPath) ? Path.GetFileName(c.ProtocolPath) : "kein Protokoll";
                Log($"  {c.CaseId}: {hasVideo}, {hasProto}");
            }

            StatusText = $"Gefunden: {found.Count} Ordner, {casesWithProtocol.Count} mit Protokoll";

            // Status bestehender Faelle erhalten (Merge statt Clear)
            var existingStatus = new Dictionary<string, TrainingCaseStatus>();
            foreach (var c in Cases)
                existingStatus.TryAdd(c.CaseId, c.Status);
            Cases.Clear();
            foreach (var c in found)
            {
                if (existingStatus.TryGetValue(c.CaseId, out var prevStatus))
                    c.Status = prevStatus;
                Cases.Add(c);
            }

            if (casesWithProtocol.Count == 0)
            {
                Log("STOP: Keine Ordner mit Protokoll-Dateien gefunden.");
                StatusText = "Keine Ordner mit Protokoll-Dateien gefunden.";
                return;
            }

            // 2. Generate samples for all cases
            var cfg = AiRuntimeConfigLoader.Load();
            Log($"AI Config: Enabled={cfg.Enabled}, ffmpeg={cfg.FfmpegPath}");

            var settings = await TrainingCenterSettingsStore.LoadAsync();
            var meterSvc = CreateMeterTimelineService(cfg, settings.GpuConcurrency);
            var generator = new TrainingSampleGenerator(cfg, meterSvc, settings);

            var allSamples = await TrainingSamplesStore.LoadAsync();
            var existingSigs = allSamples.Select(s => s.Signature)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet(StringComparer.Ordinal);

            // ── Case-Level Skip: Haltungen die bereits Samples haben komplett ueberspringen ──
            var existingCaseIds = allSamples
                .Where(s => !string.IsNullOrEmpty(s.CaseId))
                .Select(s => s.CaseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var casesToProcess = new List<TrainingCase>();
            var caseSkipped = 0;
            foreach (var c in casesWithProtocol)
            {
                if (existingCaseIds.Contains(c.CaseId))
                {
                    caseSkipped++;
                    c.Status = TrainingCaseStatus.BatchImported; // UI-Status aktualisieren
                }
                else
                {
                    casesToProcess.Add(c);
                }
            }

            Log($"Bestehende Samples: {allSamples.Count} ({existingSigs.Count} Signaturen, {existingCaseIds.Count} CaseIds)");
            if (caseSkipped > 0)
                Log($"Case-Level Skip: {caseSkipped} Haltungen bereits verarbeitet, {casesToProcess.Count} neu");
            else
                Log($"Keine bereits verarbeiteten Haltungen gefunden — alle {casesToProcess.Count} werden verarbeitet");

            if (casesToProcess.Count == 0)
            {
                StatusText = $"Alle {casesWithProtocol.Count} Haltungen bereits verarbeitet. KB ist aktuell.";
                Log($"FERTIG: Alle {casesWithProtocol.Count} Haltungen haben bereits Samples in der KB.");
                // KB-Nachholpfad trotzdem pruefen (unten)
            }

            // Ollama-Verbindung einmalig pruefen + KB-Objekte vorbereiten
            var ollamaConfig = OllamaConfigExtensions.Load();
            var ollamaReachable = await CheckOllamaReachableAsync(ollamaConfig, ct);
            KnowledgeBaseContext? kbCtx = null;
            KnowledgeBaseManager? kbManager = null;
            if (ollamaReachable)
            {
                _kbHttpClient ??= new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
                kbCtx = new KnowledgeBaseContext();
                kbManager = new KnowledgeBaseManager(kbCtx, new EmbeddingService(_kbHttpClient, ollamaConfig));
                Log($"Ollama bereit: {ollamaConfig.BaseUri}, Embed-Modell: {ollamaConfig.EmbedModel}");
            }
            else
            {
                Log($"Ollama NICHT erreichbar auf {ollamaConfig.BaseUri} — Samples werden gespeichert, KB-Indexierung uebersprungen.");
            }

            // ── KB-Nachholpfad: Approved Samples die noch nicht in der KB sind nachindizieren ──
            // Deckt den Fall ab: Crash nach MergeAndSave aber vor IndexSampleAsync,
            // oder vorheriger Lauf ohne Ollama.
            if (kbManager is not null && allSamples.Count > 0)
            {
                // Pending + Error Samples immer nachindizieren
                var unindexed = allSamples
                    .Where(s => s.Status == TrainingSampleStatus.Approved)
                    .Where(s => s.KbIndexState is KbIndexState.Pending or KbIndexState.Error)
                    .ToList();

                // Migration-Fallback: Alte Samples mit KbIndexState.None (noch nie durch die neue Pipeline)
                var noneState = allSamples
                    .Where(s => s.Status == TrainingSampleStatus.Approved && s.KbIndexState == KbIndexState.None)
                    .ToList();
                if (noneState.Count > 0)
                {
                    var notInKb = noneState.Where(s => !kbManager.IsIndexed(s.SampleId)).ToList();
                    foreach (var s in notInKb)
                        s.KbIndexState = KbIndexState.Pending;
                    // Bereits indexierte als Indexed markieren
                    foreach (var s in noneState.Except(notInKb))
                        s.KbIndexState = KbIndexState.Indexed;
                    if (noneState.Count > 0)
                        await TrainingSamplesStore.MergeOrUpdateAsync(noneState);
                    unindexed.AddRange(notInKb);
                }

                if (unindexed.Count > 0)
                {
                    Log($"KB-Nachholpfad: {unindexed.Count} Samples noch nicht in KB — indexiere nach...");
                    StatusText = $"KB-Nachholpfad: {unindexed.Count} Samples nachindizieren...";
                    ProgressMax = unindexed.Count;
                    try
                    {
                        var indexedIds = await kbManager.IndexSamplesAsync(unindexed, ct);
                        var indexedSet = indexedIds.ToHashSet();
                        foreach (var s in unindexed)
                            s.KbIndexState = indexedSet.Contains(s.SampleId)
                                ? KbIndexState.Indexed
                                : KbIndexState.Error;
                        await TrainingSamplesStore.MergeOrUpdateAsync(unindexed);
                        Log($"KB-Nachholpfad fertig: {indexedIds.Count}/{unindexed.Count} nachindiziert");
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Log($"  KB-Nachhol Fehler: {ex.Message}");
                        // KbIndexState bleibt Pending/Error → naechster Lauf versucht es erneut
                    }
                }
            }

            ProgressMax = casesToProcess.Count;
            var totalNew = 0;
            var totalIndexed = 0;
            var errors = 0;
            var lastError = "";
            var emptyProtocols = 0;
            var duplicateOnlyCases = 0;
            var missingProtocols = 0;
            var unreadableProtocols = 0;

            try // try-finally fuer kbCtx/kbHttp Dispose
            {
            for (var i = 0; i < casesToProcess.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var tc = casesToProcess[i];
                ProgressValue = i + 1;
                StatusText = $"[{i + 1}/{casesToProcess.Count}] {tc.CaseId}...";
                Log($"--- [{i + 1}/{casesToProcess.Count}] {tc.CaseId} ---");
                Log($"  Protokoll: {tc.ProtocolPath}");
                Log($"  Video: {(string.IsNullOrEmpty(tc.VideoPath) ? "keins" : tc.VideoPath)}");

                try
                {
                    // Preview-Frame extrahieren
                    var previewFrame = await ExtractPreviewFrameAsync(tc, cfg, ct);
                    if (!string.IsNullOrEmpty(previewFrame))
                        UpdateLivePreview(tc.CaseId, "Verarbeite...", "—", previewFrame);
                    else
                        UpdateLivePreview(tc.CaseId, "Verarbeite...", "—", null);

                    var generation = await generator.GenerateWithDiagnosticsAsync(tc, existingSigs, framesDir: null, ct, skipVideoTimeline: true);
                    var newSamples = generation.Samples;

                    if (newSamples.Count == 0)
                    {
                        string skipReason;
                        switch (generation.Outcome)
                        {
                            case TrainingSampleGenerationOutcome.OnlyDuplicates:
                                duplicateOnlyCases++;
                                skipReason = $"{generation.ParsedEntries} Duplikate";
                                Log($"  -> 0 Samples (alle {generation.ParsedEntries} Eintraege bereits vorhanden)");
                                UpdateLivePreview(tc.CaseId, skipReason, "bereits vorhanden", previewFrame);
                                break;
                            case TrainingSampleGenerationOutcome.ProtocolFileMissing:
                                missingProtocols++;
                                skipReason = "Protokoll fehlt";
                                Log("  -> 0 Samples (Protokolldatei fehlt)");
                                UpdateLivePreview(tc.CaseId, "—", skipReason, previewFrame);
                                break;
                            case TrainingSampleGenerationOutcome.ProtocolUnreadable:
                                unreadableProtocols++;
                                skipReason = "nicht lesbar";
                                Log("  -> 0 Samples (Protokoll nicht lesbar)");
                                UpdateLivePreview(tc.CaseId, "—", skipReason, previewFrame);
                                break;
                            default:
                                emptyProtocols++;
                                skipReason = "keine Eintraege";
                                Log("  -> 0 Samples (keine Protokolleintraege erkannt)");
                                UpdateLivePreview(tc.CaseId, "—", skipReason, previewFrame);
                                break;
                        }

                        // Uebersprungene Haltungen trotzdem im Ergebnis-Verlauf zeigen
                        void AddSkipped()
                        {
                            SelfTrainingResults.Add(new SelfTrainingEntryResult
                            {
                                Index = SelfTrainingResults.Count + 1,
                                VsaCode = tc.CaseId,
                                Meter = 0,
                                Level = MatchLevel.NoFindings,
                                Summary = skipReason
                            });
                        }
                        if (System.Windows.Application.Current?.Dispatcher is { } dSkip && !dSkip.CheckAccess())
                            dSkip.Invoke(AddSkipped);
                        else
                            AddSkipped();

                        continue; // Naechster Case
                    }

                    // ── QualityGate: Samples pruefen bevor sie gespeichert werden ──
                    var qgBatch = _sampleQualityGate.EvaluateBatch(newSamples);
                    if (qgBatch.Red > 0)
                    {
                        Log($"  QualityGate: {qgBatch.Red} Samples abgelehnt (Red)");
                        foreach (var (rs, rr) in qgBatch.Results.Where(r => !r.Result.IsAcceptable))
                            Log($"     REJECT {rs.Code} @ {rs.MeterStart:F2}m: {string.Join(", ", rr.Issues)}");
                    }
                    // Nur akzeptierte Samples weiterverarbeiten
                    newSamples = qgBatch.Accepted.ToList();

                    // Smart-Approve basierend auf QualityGate-Ergebnis
                    foreach (var s in newSamples)
                    {
                        var qr = qgBatch.Results.First(r => r.Sample == s).Result;
                        s.Status = qr.IsGreen
                            ? TrainingSampleStatus.Approved
                            : TrainingSampleStatus.New; // Yellow → Review Queue
                        existingSigs.Add(s.Signature);

                        // Live-Frame pro Sample (nicht nur pro Case)
                        var sampleFrame = !string.IsNullOrEmpty(s.FramePath) ? s.FramePath : previewFrame;
                        UpdateLivePreview(tc.CaseId, s.Code, $"{s.MeterStart:F2} – {s.MeterEnd:F2} m", sampleFrame);

                        // Ergebnis-Verlauf (Yellow = brauchbar mit Maengeln, nicht "nichts gefunden")
                        var level = qr.IsGreen ? MatchLevel.ExactMatch : MatchLevel.PartialMatch;
                        void AddResult()
                        {
                            SelfTrainingResults.Add(new SelfTrainingEntryResult
                            {
                                Index = SelfTrainingResults.Count + 1,
                                VsaCode = s.Code,
                                Meter = s.MeterStart,
                                Level = level,
                                Summary = qr.IsGreen ? s.Beschreibung : $"[Yellow] {string.Join(", ", qr.Issues)}"
                            });
                            UpdateCodeDistribution(s.Code, level);
                        }
                        if (System.Windows.Application.Current?.Dispatcher is { } dp && !dp.CheckAccess())
                            dp.Invoke(AddResult);
                        else
                            AddResult();
                    }

                    var autoApproved = newSamples.Count(s => s.Status == TrainingSampleStatus.Approved);
                    var needsReview = newSamples.Count - autoApproved;
                    totalNew += newSamples.Count;

                    Log($"  -> {newSamples.Count} Samples (QG: {qgBatch.Green}G/{qgBatch.Yellow}Y/{qgBatch.Red}R, {autoApproved} approved):");
                    foreach (var s in newSamples)
                        Log($"     {s.Code} @ {s.MeterStart:F2}m [{s.Status}] - {s.Beschreibung}");

                    // Approved Samples als Pending markieren (vor dem Speichern)
                    foreach (var s in newSamples.Where(s => s.Status == TrainingSampleStatus.Approved))
                        s.KbIndexState = KbIndexState.Pending;

                    // ══════════════════════════════════════════════════════════════════
                    // SOFORT SPEICHERN — nur QualityGate-akzeptierte Samples
                    // ══════════════════════════════════════════════════════════════════
                    await TrainingSamplesStore.MergeAndSaveAsync(newSamples);

                    // SOFORT in KB indexieren (inkrementell, kein Rebuild/Delete)
                    if (kbManager is not null)
                    {
                        var approvedForKb = newSamples
                            .Where(s => s.Status == TrainingSampleStatus.Approved)
                            .ToList();

                        if (approvedForKb.Count > 0)
                        {
                            try
                            {
                                var indexedIds = await kbManager.IndexSamplesAsync(approvedForKb, ct);
                                totalIndexed += indexedIds.Count;
                                // Pro Sample: Indexed oder Error je nach Ergebnis
                                var indexedSet = indexedIds.ToHashSet();
                                foreach (var s in approvedForKb)
                                    s.KbIndexState = indexedSet.Contains(s.SampleId)
                                        ? KbIndexState.Indexed
                                        : KbIndexState.Error;
                                await TrainingSamplesStore.MergeOrUpdateAsync(approvedForKb);
                            }
                            catch (Exception kbEx) when (kbEx is not OperationCanceledException)
                            {
                                Log($"     KB-Index Fehler: {kbEx.Message}");
                                // KbIndexState bleibt Pending → Nachholpfad beim naechsten Lauf
                            }
                        }
                    }

                    // UI-Zaehler aktualisieren (Samples + Codes)
                    allSamples.AddRange(newSamples);
                    var distinctCodes = allSamples.Select(s => s.Code).Distinct().Count();
                    void UpdateCounters()
                    {
                        KbSampleCount = allSamples.Count;
                        KbCodesCovered = distinctCodes;
                    }
                    if (System.Windows.Application.Current?.Dispatcher is { } disp && !disp.CheckAccess())
                        disp.Invoke(UpdateCounters);
                    else
                        UpdateCounters();

                    Log($"  Gespeichert + KB: {autoApproved} indexiert | Gesamt: {allSamples.Count} Samples, {distinctCodes} Codes");

                    // Fall als BatchImported markieren
                    tc.Status = TrainingCaseStatus.BatchImported;

                    // Case-State periodisch sichern (alle 10 Haltungen),
                    // damit die UI nach einem Crash den Fortschritt korrekt anzeigt.
                    if ((i + 1) % 5 == 0)
                    {
                        try
                        {
                            await _store.SaveAsync(new TrainingCenterState
                            {
                                Cases = Cases.ToList(),
                                UpdatedUtc = DateTime.UtcNow
                            });
                        }
                        catch { /* best-effort, Samples sind bereits gesichert */ }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    errors++;
                    lastError = ex.Message;
                    Log($"  FEHLER: {ex.Message}");
                }
            }
            } // end try
            finally
            {
                kbCtx?.Dispose();
                // _kbHttpClient wird wiederverwendet, nicht disposen
            }

            // KB-Version erstellen (nach allen Cases)
            if (totalIndexed > 0)
            {
                try
                {
                    _kbHttpClient ??= new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
                    using var finalKbCtx = new KnowledgeBaseContext();
                    var finalManager = new KnowledgeBaseManager(finalKbCtx, new EmbeddingService(_kbHttpClient, ollamaConfig));
                    finalManager.CreateVersion($"Batch-Import {DateTime.Now:yyyy-MM-dd HH:mm}");
                }
                catch { /* Version-Erstellung ist optional */ }
            }

            // Abschlussmeldung
            Samples.Clear();
            allSamples = await TrainingSamplesStore.LoadAsync();
            foreach (var s in allSamples)
                Samples.Add(s);

            if (totalNew == 0 && casesToProcess.Count == 0 && caseSkipped > 0)
            {
                // Alle Haltungen komplett uebersprungen (Case-Level Skip)
                var diag = $"Alle {caseSkipped} Haltungen bereits verarbeitet — KB ist aktuell.";
                Log(diag);
                StatusText = diag;
            }
            else if (totalNew == 0 && casesToProcess.Count > 0)
            {
                var diag = $"0 neue Samples aus {casesToProcess.Count} Faellen.";
                if (caseSkipped > 0) diag += $" {caseSkipped} bereits verarbeitet.";
                if (errors > 0) diag += $" {errors} Fehler (letzter: {lastError}).";
                if (emptyProtocols > 0) diag += $" {emptyProtocols} ohne Eintraege.";
                if (duplicateOnlyCases > 0) diag += $" {duplicateOnlyCases} nur Duplikate.";
                if (missingProtocols > 0) diag += $" {missingProtocols} fehlende Protokolle.";
                if (unreadableProtocols > 0) diag += $" {unreadableProtocols} nicht lesbar.";
                Log(diag);
                StatusText = diag;
            }
            else
            {
                var finalStatus = $"Fertig! {totalNew} Samples gespeichert, {totalIndexed} in KB indexiert";
                if (caseSkipped > 0) finalStatus += $", {caseSkipped} uebersprungen";
                if (errors > 0) finalStatus += $", {errors} Fehler";
                if (!ollamaReachable) finalStatus += " (KB-Indexierung uebersprungen: Ollama offline)";
                Log(finalStatus);
                StatusText = finalStatus;
            }

            await RefreshKbStatusAsync();

            // 5. Save cases
            await _store.SaveAsync(new TrainingCenterState
            {
                Cases = Cases.ToList(),
                UpdatedUtc = DateTime.UtcNow
            });
            Log("Fälle gespeichert. Batch-Import abgeschlossen.");
        }
        catch (OperationCanceledException)
        {
            Log("Batch-Import abgebrochen durch Benutzer.");
            StatusText = "Batch-Import abgebrochen.";
        }
        catch (Exception ex)
        {
            Log($"FATALER FEHLER: {ex.Message}");
            StatusText = $"Fehler beim Batch-Import: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelBatch()
    {
        _genCts?.Cancel();
        StatusText = "Abbruch angefordert...";
    }

    /// <summary>
    /// V4.3: Indexiert alle Approved-Samples nachtraeglich in die KB,
    /// die noch nicht indexiert sind (KbIndexState != Indexed).
    /// Faengt verlorene Approve-Aktionen auf wenn Ollama temporaer down war.
    /// </summary>
    [RelayCommand]
    private async Task ReindexPendingSamplesAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            StatusText = "Lade ausstehende Samples...";

            var allSamples = await TrainingSamplesStore.LoadAsync();
            var pending = allSamples
                .Where(s => s.Status == TrainingSampleStatus.Approved
                            && s.KbIndexState != KbIndexState.Indexed
                            && s.KbIndexState != KbIndexState.Deduplicated)
                .ToList();

            if (pending.Count == 0)
            {
                StatusText = "Keine ausstehenden Samples — alles bereits indexiert.";
                Log("Re-Index: Nichts zu tun, alle Approved-Samples sind bereits in der KB.");
                return;
            }

            Log($"Re-Index: {pending.Count} Approved-Samples werden indexiert...");
            StatusText = $"Re-Index laeuft: {pending.Count} Samples...";

            var indexed = await IncrementalKbUpdateAsync(pending, CancellationToken.None);

            foreach (var s in pending)
            {
                s.KbIndexState = indexed.Contains(s.SampleId)
                    ? KbIndexState.Indexed
                    : KbIndexState.Error;
            }
            await TrainingSamplesStore.MergeOrUpdateAsync(pending);

            Log($"Re-Index fertig: {indexed.Count} von {pending.Count} Samples indexiert.");
            StatusText = $"Re-Index fertig: {indexed.Count}/{pending.Count} Samples in KB.";

            await RefreshKbStatusAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Re-Index Fehler: {ex.Message}";
            Log($"Re-Index FEHLER: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckKnowledgeBaseAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            StatusText = "Prüfe Knowledge Base...";

            var summary = await Task.Run(() =>
            {
                using var db = new KnowledgeBaseContext();
                var diag = new KnowledgeBaseDiagnosticsService(db);
                return diag.ReadSummary(12);
            });

            Log($"KB-Stand: Samples={summary.SampleCount}, Embeddings={summary.EmbeddingCount}, Versionen={summary.VersionCount}");
            if (summary.LatestVersionAtUtc is not null)
            {
                var latest = summary.LatestVersionAtUtc.Value.ToLocalTime();
                var notes = string.IsNullOrWhiteSpace(summary.LatestVersionNotes)
                    ? "-"
                    : summary.LatestVersionNotes;
                Log($"Letzte Version: {latest:yyyy-MM-dd HH:mm} ({summary.LatestVersionSampleCount} Samples) | Notiz: {notes}");
            }

            if (summary.TopCodes.Count > 0)
            {
                Log("Top-Codes:");
                foreach (var c in summary.TopCodes)
                    Log($"  {c.VsaCode}: {c.Count}");
            }
            else
            {
                Log("Top-Codes: keine Einträge vorhanden.");
            }

            StatusText = $"KB geprüft: {summary.SampleCount} Samples, {summary.EmbeddingCount} Embeddings, {summary.VersionCount} Versionen.";

            await RefreshKbStatusAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"KB-Prüfung fehlgeschlagen: {ex.Message}";
            Log($"KB-Prüfung FEHLER: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedSampleChanged(TrainingSample? value)
    {
        ApproveSampleCommand.NotifyCanExecuteChanged();
        RejectSampleCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedSampleCodeLabel));
    }

    /// <summary>
    /// VSA-Code mit Klartext fuer die Sample-Details-Anzeige.
    /// Beispiel: "BBCC — Ablagerung verfestigt".
    /// </summary>
    public string SelectedSampleCodeLabel
    {
        get
        {
            var code = SelectedSample?.Code;
            if (string.IsNullOrWhiteSpace(code)) return "";
            var label = Ai.VsaCodeResolver.LookupLabel(code);
            return string.IsNullOrWhiteSpace(label) ? code : $"{code} — {label}";
        }
    }

    /// <summary>
    /// Extrahiert einen einzelnen Preview-Frame aus dem Video (bei Sekunde 2).
    /// Wird für die Live-Vorschau genutzt, auch wenn keine neuen Samples generiert werden.
    /// </summary>
    private static async Task<string?> ExtractPreviewFrameAsync(TrainingCase tc, AiRuntimeConfig cfg, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tc.VideoPath) || !File.Exists(tc.VideoPath))
            return null;

        var ffmpeg = cfg.FfmpegPath ?? "ffmpeg";
        var sampleId = $"preview_{Regex.Replace(tc.CaseId, @"[^\w\-]", "_")}";
        try
        {
            return await FrameStore.ExtractAndStoreAsync(ffmpeg, tc.VideoPath, 2.0, sampleId, null, ct);
        }
        catch
        {
            return null;
        }
    }

    private static MeterTimelineService CreateMeterTimelineService(AiRuntimeConfig cfg, int concurrency = 1)
    {
        if (!cfg.Enabled)
            return new MeterTimelineService(cfg);

        var ollamaClient = cfg.CreateOllamaClient();
        var vision = new OllamaVisionFindingsService(ollamaClient, cfg.VisionModel);
        var osd = new OsdMeterDetectionService(vision);
        return new MeterTimelineService(cfg, osd, concurrency);
    }
}