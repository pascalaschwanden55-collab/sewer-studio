using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Steuert den Codier-Durchlauf einer Haltung von 0.00m bis Haltungsende.
/// Jede Haltung wird komplett durchcodiert, Abbruch nur explizit.
/// </summary>
public interface ICodingSessionService
{
    // --- Session-Lifecycle ---

    /// <summary>Neue Codier-Session starten.</summary>
    CodingSession StartSession(HaltungRecord haltung, string? videoPath);

    /// <summary>Session pausieren (Video anhalten, Zustand merken).</summary>
    void PauseSession();

    /// <summary>Pausierte Session fortsetzen.</summary>
    void ResumeSession();

    /// <summary>Session abbrechen (mit Begruendung).</summary>
    void AbortSession(string reason);

    /// <summary>Session abschliessen → Protokoll generieren.</summary>
    ProtocolDocument CompleteSession();

    // --- Navigation ---

    /// <summary>Aktueller Meter-Stand.</summary>
    double CurrentMeter { get; }

    /// <summary>Haltungsende in Meter.</summary>
    double EndMeter { get; }

    /// <summary>Fortschritt 0–100%.</summary>
    double ProgressPercent { get; }

    /// <summary>Vorwaerts navigieren (Standard: 0.5m Schritte).</summary>
    void MoveNext(double stepSizeM = 0.5);

    /// <summary>Rueckwaerts navigieren.</summary>
    void MovePrevious(double stepSizeM = 0.5);

    /// <summary>Direkt zu einer Meter-Position springen.</summary>
    void MoveToMeter(double meter);

    // --- Event-Erfassung ---

    /// <summary>Codier-Ereignis hinzufuegen (Schaden mit Code + optionalem Overlay).</summary>
    CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null);

    /// <summary>Bestehendes Ereignis aktualisieren.</summary>
    void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null);

    /// <summary>Ereignis entfernen.</summary>
    void RemoveEvent(Guid eventId);

    // --- Zustand ---

    /// <summary>Aktive Session (null wenn keine laeuft).</summary>
    CodingSession? ActiveSession { get; }

    /// <summary>Alle erfassten Ereignisse der aktiven Session.</summary>
    IReadOnlyList<CodingEvent> Events { get; }

    /// <summary>Session-Zustand geaendert (fuer UI-Binding).</summary>
    event EventHandler<CodingSessionState>? StateChanged;

    /// <summary>Meter-Position geaendert (fuer UI-Binding).</summary>
    event EventHandler<double>? MeterChanged;

    /// <summary>Neues Event hinzugefuegt (fuer UI-Binding).</summary>
    event EventHandler<CodingEvent>? EventAdded;
}
