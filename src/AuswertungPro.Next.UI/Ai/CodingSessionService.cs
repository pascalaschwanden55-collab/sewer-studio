using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Steuert den Codier-Durchlauf einer Haltung von 0.00m bis Haltungsende.
/// </summary>
public sealed class CodingSessionService : ICodingSessionService
{
    private CodingSession? _session;

    // --- Session-Lifecycle ---

    public CodingSession StartSession(HaltungRecord haltung, string? videoPath)
    {
        if (_session != null && _session.State == CodingSessionState.Running)
            throw new InvalidOperationException("Es laeuft bereits eine Codier-Session.");

        // Haltungslaenge aus Feldern lesen
        double endMeter = 0;
        if (haltung.Fields.TryGetValue("Haltungslaenge_m", out var lenStr)
            && double.TryParse(lenStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var len))
        {
            endMeter = len;
        }

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

        foreach (var ev in _session.Events.OrderBy(e => e.MeterAtCapture))
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

        doc.Original = revision;
        doc.Current = revision;

        StateChanged?.Invoke(this, _session.State);
        return doc;
    }

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
        var ev = _session!.Events.FirstOrDefault(e => e.EventId == eventId)
            ?? throw new InvalidOperationException($"Event {eventId} nicht gefunden.");

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
}
