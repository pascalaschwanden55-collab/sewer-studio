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

    /// <summary>
    /// Parst eine Zeile aus dem Primaere_Schaeden Textfeld.
    /// Unterstuetzt alle Import-Formate:
    ///   PDF:  "BCD @0.00m (Rohranfang)" oder "A01 BAFCE @0.00m (Beschreibung)"
    ///   XTF:  "0.00m BCD Rohranfang" oder "2.24m BCCBA Bogen (Details) Q1=15"
    ///   Alt:  "0.00  BCD  Rohranfang"
    /// </summary>
    private static (string Code, double Meter, string Description)? ParsePrimaryDamageLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        // Format 1 (PDF): "CODE @meterM (beschreibung)" — z.B. "BCD @0.00m (Rohranfang)"
        // Auch mit Operator-Code: "A01 BAFCE @0.00m (...)"
        var m1 = System.Text.RegularExpressions.Regex.Match(line,
            @"^(?:[A-Z]\d{1,3}\s+)?(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})?)\s+@(?<meter>\d+(?:[.,]\d+)?)\s*m?\s*(?:\((?<desc>.+)\))?");
        if (m1.Success)
        {
            var meter = TryParseMeterValue(m1.Groups["meter"].Value);
            var desc = m1.Groups["desc"].Success && !string.IsNullOrWhiteSpace(m1.Groups["desc"].Value)
                ? m1.Groups["desc"].Value
                : m1.Groups["code"].Value;
            return (m1.Groups["code"].Value, meter, desc);
        }

        // Format 2 (XTF): "0.00m CODE Beschreibung (Details) Q1=..." — z.B. "2.24m BCCBA Bogen nach rechts"
        var m2 = System.Text.RegularExpressions.Regex.Match(line,
            @"^(?<meter>\d+(?:[.,]\d+)?)\s*m\s+(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})?)\s+(?<desc>.+?)(?:\s+Q\d=.*)?$");
        if (m2.Success)
        {
            var meter = TryParseMeterValue(m2.Groups["meter"].Value);
            return (m2.Groups["code"].Value, meter, CleanDescription(m2.Groups["desc"].Value));
        }

        // Format 3 (Alt/PDF-intern): "0.00  CODE  beschreibung  00:00:00" — z.B. "0.00  BCD  Rohranfang"
        // Auch mit Operator-Code: "0.00 A01 BAFCE  Beschreibung"
        var m3 = System.Text.RegularExpressions.Regex.Match(line,
            @"^(?<meter>\d+(?:[.,]\d+)?)\s+(?:[A-Z]\d{1,3}\s+)?(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})?)\s+(?<desc>.+)$");
        if (m3.Success)
        {
            var meter = TryParseMeterValue(m3.Groups["meter"].Value);
            return (m3.Groups["code"].Value, meter, CleanDescription(m3.Groups["desc"].Value));
        }

        // Format 4: Nur "CODE Beschreibung" ohne Meter
        var m4 = System.Text.RegularExpressions.Regex.Match(line,
            @"^(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})?)\s+(?<desc>.+)$");
        if (m4.Success)
            return (m4.Groups["code"].Value, 0, CleanDescription(m4.Groups["desc"].Value));

        return null;
    }

    private static double TryParseMeterValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        return double.TryParse(raw.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    /// <summary>Entfernt Timestamps, Q1/Q2-Werte und Klammer-Details aus Beschreibung.</summary>
    private static string CleanDescription(string desc)
    {
        if (string.IsNullOrWhiteSpace(desc)) return "";
        // Timestamp am Ende entfernen: "Rohranfang  00:00:00"
        desc = System.Text.RegularExpressions.Regex.Replace(desc, @"\s+\d{2}:\d{2}:\d{2}\b.*$", "");
        // Q1/Q2 am Ende entfernen: "... Q1=15%"
        desc = System.Text.RegularExpressions.Regex.Replace(desc, @"\s+Q\d=\S+", "");
        return desc.Trim();
    }

    /// <summary>
    /// Versucht einen Laenge-Wert aus einem HaltungRecord-Feld zu lesen.
    /// Unterstuetzt Komma und Punkt als Dezimaltrennzeichen.
    /// </summary>
    private static double? TryParseLengthField(HaltungRecord haltung, string fieldName)
    {
        if (!haltung.Fields.TryGetValue(fieldName, out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Replace(',', '.');
        if (double.TryParse(normalized, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var val) && val > 0)
            return val;

        return null;
    }
}
