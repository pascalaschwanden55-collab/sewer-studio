using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using InfraKnowledgeBase = AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Steuert den Codier-Durchlauf einer Haltung von 0.00m bis Haltungsende.
/// </summary>
public sealed class CodingSessionService : ICodingSessionService
{
    private readonly Func<OllamaConfig?> _ollamaConfigProvider;
    private readonly Func<IReadOnlySet<string>> _evalHashesProvider;
    private readonly Func<IReadOnlySet<string>> _evalHaltungKeysProvider;
    private readonly ITrainingSampleStore _trainingSamples;
    private CodingSession? _session;

    public CodingSessionService(
        Func<OllamaConfig?>? ollamaConfigProvider = null,
        Func<IReadOnlySet<string>>? evalHashesProvider = null,
        Func<IReadOnlySet<string>>? evalHaltungKeysProvider = null,
        ITrainingSampleStore? trainingSamples = null)
    {
        _ollamaConfigProvider = ollamaConfigProvider ?? (() => null);
        _evalHashesProvider = evalHashesProvider
            ?? (() => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        _evalHaltungKeysProvider = evalHaltungKeysProvider
            ?? (() => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        _trainingSamples = trainingSamples ?? TrainingSamplesStore.Current;
    }

    // --- Session-Lifecycle ---

    public CodingSession StartSession(HaltungRecord haltung, string? videoPath)
    {
        if (_session != null && _session.State == CodingSessionState.Running)
            throw new InvalidOperationException("Es laeuft bereits eine Codier-Session.");

        // Haltungslaenge aus Feldern lesen (Fallback-Kette)
        double endMeter = TryParseLengthField(haltung, "Haltungslaenge_m")
                       ?? TryParseLengthField(haltung, "Laenge_m")
                       ?? 0;

        if (endMeter <= 0)
            throw new InvalidOperationException("Haltungslaenge muss > 0 sein.");

        _session = new CodingSession
        {
            HaltungId = haltung.Id,
            HaltungName = haltung.GetFieldValue("Haltungsname"),
            StartMeter = 0.0,
            EndMeter = endMeter,
            CurrentMeter = 0.0,
            State = CodingSessionState.Running,
            VideoPath = videoPath,
            StartedAt = DateTimeOffset.UtcNow
        };

        // Auto-Kalibrierung aus DN wenn vorhanden
        if (haltung.Fields.TryGetValue("DN_mm", out var dnStr)
            && int.TryParse(dnStr, out var dn) && dn > 0)
        {
            _session.Calibration = new PipeCalibration
            {
                NominalDiameterMm = dn
            };
        }

        // Bestehende Beobachtungen aus dem Protokoll laden
        LoadExistingObservations(haltung);

        StateChanged?.Invoke(this, _session.State);
        MeterChanged?.Invoke(this, _session.CurrentMeter);
        return _session;
    }

    public void PauseSession()
    {
        EnsureActiveSession();
        _session!.State = CodingSessionState.Paused;
        StateChanged?.Invoke(this, _session.State);
    }

    public void ResumeSession()
    {
        EnsureSession();
        if (_session!.State != CodingSessionState.Paused
            && _session.State != CodingSessionState.WaitingForUserInput)
            throw new InvalidOperationException($"Session kann nicht fortgesetzt werden (State={_session.State}).");

        _session.State = CodingSessionState.Running;
        StateChanged?.Invoke(this, _session.State);
    }

    /// <summary>
    /// Session in WaitingForUserInput versetzen — KI hat unsicheren Fund,
    /// Video wird pausiert bis User bestaetigt/korrigiert/verwirft.
    /// </summary>
    public void SetWaitingForInput()
    {
        EnsureSession();
        if (_session!.State != CodingSessionState.Running
            && _session.State != CodingSessionState.Paused)
            return; // Nur aus Running/Paused moeglich
        _session.State = CodingSessionState.WaitingForUserInput;
        StateChanged?.Invoke(this, _session.State);
    }

    public void AbortSession(string reason)
    {
        EnsureSession();
        _session!.State = CodingSessionState.Aborted;
        _session.AbortReason = reason;
        _session.CompletedAt = DateTimeOffset.UtcNow;
        StateChanged?.Invoke(this, _session.State);
    }

    public ProtocolDocument CompleteSession()
    {
        EnsureSession();
        _session!.State = CodingSessionState.Completed;
        _session.CompletedAt = DateTimeOffset.UtcNow;
        CaptureAiDecisionAudits(_session);

        // Protokoll aus gesammelten Events generieren
        var doc = new ProtocolDocument
        {
            HaltungId = _session.HaltungName
        };

        var revision = new ProtocolRevision
        {
            CreatedBy = "Codier-Modus",
            Comment = $"Codier-Session {_session.StartedAt:yyyy-MM-dd HH:mm} – {_session.Events.Count} Ereignisse"
        };

        var acceptedEvents = AiProtocolAcceptancePolicy
            .FilterCodingEvents(_session.Events)
            .OrderBy(e => e.MeterAtCapture)
            .ToList();
        revision.Comment = $"Codier-Session {_session.StartedAt:yyyy-MM-dd HH:mm} - {acceptedEvents.Count} uebernommene Ereignisse";

        foreach (var ev in acceptedEvents)
        {
            revision.Entries.Add(ev.Entry);
            revision.Changes.Add(new ProtocolChange
            {
                Kind = ProtocolChangeKind.Add,
                EntryId = ev.Entry.EntryId,
                User = "Codier-Modus"
            });
        }

        // Rohranfang (BCD) bei 0.00m und Rohrende (BCE) sicherstellen
        ProtocolBoundaryService.EnsureBoundaries(revision.Entries, _session.EndMeter);

        doc.Original = ProtocolRevisionCloner.CloneRevision(revision, "Codier-Modus", "Original aus Codier-Session");
        doc.Current = ProtocolRevisionCloner.CloneRevision(revision, "Codier-Modus", revision.Comment);
        doc.Current.BasedOnRevisionId = doc.Original.RevisionId;

        // Feedback-Loop: CodingEvents → TrainingSamples persistieren
        // Alter Sync-Aufrufer: Persistierung laeuft im Hintergrund, UI nutzt CompleteSessionAsync.
        _ = PersistTrainingSamplesFromEventsAsync(_session, CancellationToken.None);

        StateChanged?.Invoke(this, _session.State);
        return doc;
    }

    public async Task<ProtocolDocument> CompleteSessionAsync(CancellationToken ct = default)
    {
        EnsureSession();
        _session!.State = CodingSessionState.Completed;
        _session.CompletedAt = DateTimeOffset.UtcNow;
        CaptureAiDecisionAudits(_session);

        // Protokoll aus gesammelten Events generieren
        var doc = new ProtocolDocument
        {
            HaltungId = _session.HaltungName
        };

        var revision = new ProtocolRevision
        {
            CreatedBy = "Codier-Modus",
            Comment = $"Codier-Session {_session.StartedAt:yyyy-MM-dd HH:mm} – {_session.Events.Count} Ereignisse"
        };

        var acceptedEvents = AiProtocolAcceptancePolicy
            .FilterCodingEvents(_session.Events)
            .OrderBy(e => e.MeterAtCapture)
            .ToList();
        revision.Comment = $"Codier-Session {_session.StartedAt:yyyy-MM-dd HH:mm} - {acceptedEvents.Count} uebernommene Ereignisse";

        foreach (var ev in acceptedEvents)
        {
            revision.Entries.Add(ev.Entry);
            revision.Changes.Add(new ProtocolChange
            {
                Kind = ProtocolChangeKind.Add,
                EntryId = ev.Entry.EntryId,
                User = "Codier-Modus"
            });
        }

        // Rohranfang (BCD) bei 0.00m und Rohrende (BCE) sicherstellen
        ProtocolBoundaryService.EnsureBoundaries(revision.Entries, _session.EndMeter);

        doc.Original = ProtocolRevisionCloner.CloneRevision(revision, "Codier-Modus", "Original aus Codier-Session");
        doc.Current = ProtocolRevisionCloner.CloneRevision(revision, "Codier-Modus", revision.Comment);
        doc.Current.BasedOnRevisionId = doc.Original.RevisionId;

        await PersistTrainingSamplesFromEventsAsync(_session, ct).ConfigureAwait(false);

        StateChanged?.Invoke(this, _session.State);
        return doc;
    }

    private void CaptureAiDecisionAudits(CodingSession session)
    {
        OllamaConfig? config = null;
        try
        {
            config = _ollamaConfigProvider();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[CodingSession] Modellkonfiguration fuer Entscheidungsbeleg nicht lesbar: {ex.Message}");
        }

        foreach (var codingEvent in session.Events)
        {
            if (codingEvent.AiContext is null)
                continue;

            AiDecisionAuditMapper.CaptureCodingEvent(
                codingEvent,
                config?.VisionModel,
                config?.TextModel,
                QualityGateService.PolicyVersion);
        }
    }

    /// <summary>
    /// Konvertiert alle CodingEvents der Session in TrainingSamples
    /// und speichert sie via TrainingSamplesStore.
    /// Schliesst den Feedback-Loop: KI-Vorschlag → User-Entscheidung → Trainingsdaten.
    /// </summary>
    private async Task PersistTrainingSamplesFromEventsAsync(CodingSession session, CancellationToken ct)
    {
        try
        {
            var caseId = session.HaltungName ?? "unknown";
            var samples = new List<TrainingSample>();

            // Snapshot: der UI-Thread darf session.Events waehrend dieser Fire-and-Forget-
            // Persistierung weiter mutieren (AddEvent/RemoveEvent). Ohne Kopie wuerde die Iteration
            // mit InvalidOperationException abbrechen und die Samples der Session still verlieren.
            foreach (var ev in session.Events.ToList())
            {
                // Erstes Foto als Frame-Pfad verwenden (falls vorhanden)
                var framePath = ev.Entry.FotoPaths.Count > 0
                    ? ev.Entry.FotoPaths[0]
                    : null;

                var sample = CodingEventToSampleMapper.FromCodingEvent(ev, caseId, framePath);
                samples.Add(sample);
            }

            if (samples.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                await _trainingSamples.MergeAndSaveAsync(samples).ConfigureAwait(false);

                // KB-Indexierung weiterhin fire-and-forget (optional, nicht kritisch)
                _ = IndexApprovedSamplesToKbAsync(samples);
            }
        }
        catch (Exception ex)
        {
            // Stilles Fehlschlagen — Session-Abschluss darf nie scheitern wegen Training-Persistierung
            BestEffort.ReportWarning(
                $"[CodingSession] Training-Persistierung fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Indexiert Approved-Samples in die KB (Embedding + SQLite).
    /// Nur wenn Ollama verfuegbar — stilles Fehlschlagen bei Offline.
    /// </summary>
    private Task IndexApprovedSamplesToKbAsync(List<TrainingSample> samples)
    {
        var approved = samples
            .Where(sample =>
                ManualGoldTrainingPolicy.IsManuallyConfirmed(sample)
                && !string.IsNullOrWhiteSpace(sample.FramePath))
            .ToList();
        return IndexAndPersistAsync(approved);
    }

    /// <inheritdoc />
    public Task IndexConfirmedSampleAsync(TrainingSample sample, CancellationToken ct = default)
    {
        // Nur bestaetigtes Gold indexieren — abgelehnte/negative Samples gehoeren nicht in die positive KB.
        if (sample is null
            || !ManualGoldTrainingPolicy.IsManuallyConfirmed(sample)
            || string.IsNullOrWhiteSpace(sample.FramePath))
            return Task.CompletedTask;
        return IndexAndPersistAsync(new List<TrainingSample> { sample }, ct);
    }

    /// <summary>
    /// Gemeinsamer Index-Pfad fuer Session-Abschluss (Liste) UND Live-Bestaetigung (Einzel).
    /// Indexiert jedes Approved-Sample, schreibt das Ergebnis als KbIndexState zurueck in
    /// training_samples.json und ist robust gegen Ollama-Offline (-> Pending) und echte
    /// Schreibfehler (-> Error). Ohne diese Rueckschreibung blieb KbIndexState auf None stehen,
    /// obwohl indexiert wurde ("stiller" Haenger) — und es gab keinen Weg, solche Samples
    /// spaeter wiederzufinden. Wirft nie (robustes Gehirn: Codieren darf nie an der KB scheitern).
    /// </summary>
    private async Task IndexAndPersistAsync(List<TrainingSample> approved, CancellationToken ct = default)
    {
        if (approved is null || approved.Count == 0) return;

        try
        {
            // Innerhalb des try: _ollamaConfigProvider() kann werfen; bei Fire-and-Forget (Session-
            // Abschluss) wuerde der Task sonst unbeobachtet faulten und der Docstring "Wirft nie" waere verletzt.
            var cfg = _ollamaConfigProvider();
            if (cfg is null) return;

            // HttpClient besitzen + disponieren -> kein Socket-/Handle-Leck pro Aufruf.
            using var http = new System.Net.Http.HttpClient { Timeout = cfg.RequestTimeout };
            var embedder = new InfraKnowledgeBase.EmbeddingService(http, cfg);

            using var db = new InfraKnowledgeBase.KnowledgeBaseContext();
            var kbManager = new InfraKnowledgeBase.KnowledgeBaseManager(
                db, embedder, _evalHashesProvider(), _evalHaltungKeysProvider());

            var touched = new List<TrainingSample>();
            foreach (var sample in approved)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var ok = await kbManager.IndexSampleAsync(sample, ct);
                    // true = in KB. false hat ZWEI Bedeutungen, die wir trennen muessen:
                    //  - dauerhaft uebersprungen (Eval-Schutz/nicht index-wuerdig) -> Skipped (NICHT erneut versuchen)
                    //  - sonst echter (transienter) Misserfolg -> Error (Nachhol-Lauf darf erneut versuchen)
                    sample.KbIndexState = ok
                        ? KbIndexState.Indexed
                        : (kbManager.IsPermanentlySkipped(sample) ? KbIndexState.Skipped : KbIndexState.Error);
                    touched.Add(sample);
                }
                catch (Exception ex) when (IsTransientOllamaFailure(ex))
                {
                    // Ollama offline/Timeout: KB-Embedding wird uebersprungen und protokolliert.
                    // Pending markieren, damit "Gold in KB nachholen" im TrainingCenter es spaeter aufgreift.
                    sample.KbIndexState = KbIndexState.Pending;
                    touched.Add(sample);
                    BestEffort.ReportWarning(
                        $"[CodingSession] KB-Index uebersprungen (Ollama offline/Timeout): {sample.Code} @ {sample.CaseId}");
                }
                catch (Exception ex)
                {
                    // ECHTER KB-Schreibfehler (SQLite gesperrt/korrupt, Embedding-Dimension-Mismatch o.ae.).
                    // NICHT als "offline" kaschieren — sonst glaubt der Nutzer faelschlich, der bestaetigte
                    // Befund liege in der KB. Ein fehlerhaftes Sample blockiert die uebrigen nicht.
                    sample.KbIndexState = KbIndexState.Error;
                    touched.Add(sample);
                    BestEffort.ReportWarning(
                        $"[CodingSession] KB-SCHREIBFEHLER (NICHT offline) fuer {sample.Code} @ {sample.CaseId}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            // Status zurueck in die JSON (Merge, kein Voll-Save -> kein Ueberschreiben paralleler Writes).
            if (touched.Count > 0)
            {
                try { await _trainingSamples.MergeOrUpdateAsync(touched); }
                catch (Exception ex)
                {
                    BestEffort.ReportWarning(
                        $"[CodingSession] KbIndexState-Rueckschreibung fehlgeschlagen: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { /* Abbruch ist ok — Sample bleibt fuer Nachhol-Lauf */ }
        catch (Exception ex) when (IsTransientOllamaFailure(ex))
        {
            // Aufbau (HttpClient/KB-Context/Embedder) scheiterte transient und wird protokolliert.
            BestEffort.ReportWarning(
                $"[CodingSession] KB-Update uebersprungen (Ollama offline/Timeout): {ex.Message}");
        }
        catch (Exception ex)
        {
            // Nicht-transienter Aufbaufehler -> sichtbar markieren, nicht als "offline" verschleiern.
            BestEffort.ReportWarning(
                $"[CodingSession] KB-Update fehlgeschlagen (NICHT offline): {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// True bei voruebergehenden Ollama-/Netz-Fehlern (offline, Timeout) — diese sind beim
    /// Session-Abschluss erwartet (KB-Embedding wird dann nur uebersprungen). Alles andere
    /// (SQLite/IO/Embedding-Fehler) ist ein echter KB-Schreibfehler und darf NICHT als
    /// "offline" verschluckt werden. Prueft auch verschachtelte InnerExceptions.
    /// </summary>
    private static bool IsTransientOllamaFailure(Exception ex)
        => ex is System.Net.Http.HttpRequestException
           || ex is System.Threading.Tasks.TaskCanceledException
           || ex is OperationCanceledException
           || ex is System.Net.Sockets.SocketException
           || (ex.InnerException is not null && IsTransientOllamaFailure(ex.InnerException));

    // --- Navigation ---

    public double CurrentMeter => _session?.CurrentMeter ?? 0;
    public double EndMeter => _session?.EndMeter ?? 0;
    public double ProgressPercent => _session?.ProgressPercent ?? 0;

    public void MoveNext(double stepSizeM = 0.5)
    {
        EnsureActiveSession();
        var newMeter = Math.Min(_session!.CurrentMeter + stepSizeM, _session.EndMeter);
        _session.CurrentMeter = Math.Round(newMeter, 2);
        MeterChanged?.Invoke(this, _session.CurrentMeter);
    }

    public void MovePrevious(double stepSizeM = 0.5)
    {
        EnsureActiveSession();
        var newMeter = Math.Max(_session!.CurrentMeter - stepSizeM, _session.StartMeter);
        _session.CurrentMeter = Math.Round(newMeter, 2);
        MeterChanged?.Invoke(this, _session.CurrentMeter);
    }

    public void MoveToMeter(double meter)
    {
        EnsureActiveSession();
        _session!.CurrentMeter = Math.Round(
            Math.Clamp(meter, _session.StartMeter, _session.EndMeter), 2);
        MeterChanged?.Invoke(this, _session.CurrentMeter);
    }

    // --- Event-Erfassung ---

    public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null)
    {
        EnsureSession();
        var session = _session!;

        // MeterStart aus Entry priorisieren (z.B. nach Bearbeitung im VSA-Explorer).
        entry.MeterStart ??= session.CurrentMeter;

        var ev = new CodingEvent
        {
            Entry = entry,
            Overlay = overlay,
            MeterAtCapture = entry.MeterStart ?? session.CurrentMeter,
            VideoTimestamp = entry.Zeit ?? TimeSpan.Zero
        };

        session.Events.Add(ev);
        EventAdded?.Invoke(this, ev);
        return ev;
    }

    public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null)
    {
        EnsureSession();
        var ev = _session!.Events.FirstOrDefault(e => e.EventId == eventId);
        if (ev == null)
        {
            // Event nicht in Session — z.B. aus anderem Code-Pfad erzeugt.
            // Statt Crash: neues Event anlegen und in Session einfuegen.
            BestEffort.ReportWarning(
                $"[CodingSession] UpdateEvent: Event {eventId} nicht in Session, wird nachgetragen.");
            ev = new CodingEvent { Entry = entry, Overlay = overlay };
            _session.Events.Add(ev);
            EventAdded?.Invoke(this, ev);
            return;
        }

        ev.Entry = entry;
        if (overlay != null) ev.Overlay = overlay;
    }

    public void RemoveEvent(Guid eventId)
    {
        EnsureSession();
        _session!.Events.RemoveAll(e => e.EventId == eventId);
    }

    // --- Zustand ---

    public CodingSession? ActiveSession => _session;
    public IReadOnlyList<CodingEvent> Events => _session != null
        ? _session.Events.AsReadOnly()
        : (IReadOnlyList<CodingEvent>)Array.Empty<CodingEvent>();

    public event EventHandler<CodingSessionState>? StateChanged;
    public event EventHandler<double>? MeterChanged;
    public event EventHandler<CodingEvent>? EventAdded;

    // --- Hilfs-Methoden ---

    private void EnsureSession()
    {
        if (_session == null)
            throw new InvalidOperationException("Keine Codier-Session aktiv.");
    }

    private void EnsureActiveSession()
    {
        EnsureSession();
        if (_session!.State != CodingSessionState.Running
            && _session.State != CodingSessionState.Paused
            && _session.State != CodingSessionState.WaitingForUserInput)
            throw new InvalidOperationException($"Session ist nicht aktiv (State={_session.State}).");
    }

    /// <summary>
    /// Laedt bestehende Beobachtungen aus dem Protokoll der Haltung
    /// in die Session-Events, damit sie in der Codier-Liste sichtbar sind.
    /// Quellen (in Prioritaet):
    /// 1. ProtocolDocument.Current.Entries (strukturierte Eintraege)
    /// 2. Primaere_Schaeden Textfeld (Fallback, aus PDF/XTF importiert)
    /// </summary>
    private void LoadExistingObservations(HaltungRecord haltung)
    {
        if (_session == null) return;

        // Strategie 1: Strukturiertes Protokoll (beste Qualitaet)
        var protocol = haltung.Protocol;
        if (protocol?.Current?.Entries != null && protocol.Current.Entries.Count > 0)
        {
            foreach (var entry in protocol.Current.Entries.Where(e => !e.IsDeleted))
            {
                var ev = new CodingEvent
                {
                    Entry = entry,
                    MeterAtCapture = entry.MeterStart ?? 0,
                    VideoTimestamp = entry.Zeit ?? TimeSpan.Zero,
                    AiContext = null // Importiert, nicht KI
                };
                _session.Events.Add(ev);
                EventAdded?.Invoke(this, ev);
            }
            return;
        }

        // Strategie 2: Primaere_Schaeden Textfeld parsen
        var schaeden = haltung.GetFieldValue("Primaere_Schaeden");
        if (string.IsNullOrWhiteSpace(schaeden))
            return;

        foreach (var line in schaeden.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parsed = ParsePrimaryDamageLine(line.Trim());
            if (parsed == null) continue;

            var entry = new ProtocolEntry
            {
                Code = parsed.Value.Code,
                Beschreibung = parsed.Value.Description,
                MeterStart = parsed.Value.Meter,
                Source = ProtocolEntrySource.Imported
            };

            var ev = new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = parsed.Value.Meter,
                AiContext = null
            };
            _session.Events.Add(ev);
            EventAdded?.Invoke(this, ev);
        }
    }

    // Delegiert an PrimaryDamageLineParser (pure-static, kein Session-/IO-Zustand).
    private static (string Code, double Meter, string Description)? ParsePrimaryDamageLine(string line)
        => PrimaryDamageLineParser.ParsePrimaryDamageLine(line);

    private static double? TryParseLengthField(HaltungRecord haltung, string fieldName)
        => PrimaryDamageLineParser.TryParseLengthField(haltung, fieldName);
}
