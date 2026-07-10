using System;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Anzeigestatus eines Defekts nach KI-Verarbeitung und Benutzerentscheidung.
/// </summary>
public enum DefectStatus
{
    AutoAccepted,     // KI-akzeptiert (zentrale Regel: mehrere Belege bestaetigt)
    Pending,          // Warten auf Review (Belege unvollstaendig)
    ReviewRequired,   // Manuell erforderlich (abgelehnt)
    Accepted,         // Manuell akzeptiert
    AcceptedWithEdit, // Akzeptiert mit Korrektur
    Rejected          // Abgelehnt
}

/// <summary>
/// Pure Geschaeftslogik: bestimmt den Defekt-Status eines Codier-Events
/// anhand der Benutzerentscheidung bzw. — wenn noch offen — der zentralen
/// Freigabe-Regel (<see cref="StandardAiDecisionPolicy"/>).
/// </summary>
public static class DefectStatusPolicy
{
    /// <summary>
    /// Ermittelt den <see cref="DefectStatus"/> fuer ein <see cref="CodingEvent"/>.
    /// Kein KI-Kontext → Pending (noch nicht bewertet).
    /// Manuelle Entscheidung schlaegt die zentrale Freigabe-Regel.
    /// </summary>
    public static DefectStatus GetStatus(CodingEvent ev)
    {
        if (ev.AiContext == null) return DefectStatus.Pending;

        return ev.AiContext.Decision switch
        {
            CodingUserDecision.Accepted        => DefectStatus.Accepted,
            CodingUserDecision.AcceptedWithEdit => DefectStatus.AcceptedWithEdit,
            CodingUserDecision.Rejected        => DefectStatus.Rejected,
            _ => MapCentralDecision(ev.AiContext)
        };
    }

    // Noch nicht vom Nutzer entschieden: zentrale Freigabe-Regel anwenden. Der Live-Kontext
    // liefert nur Sicherheit + Ampel; Datenbank-Abgleich/Unsicherheit sind hier nicht vorhanden.
    private static DefectStatus MapCentralDecision(CodingEventAiContext ctx)
    {
        var signals = new AiDecisionSignals(
            Confidence: ctx.Confidence,
            QualityGate: ParseLight(ctx.QualityGateLevel));

        return StandardAiDecisionPolicy.Default.Decide(signals).Outcome switch
        {
            AiDecisionOutcome.AutoAccept => DefectStatus.AutoAccepted,
            AiDecisionOutcome.Review     => DefectStatus.Pending,
            _                            => DefectStatus.ReviewRequired
        };
    }

    private static TrafficLight? ParseLight(string? level)
        => Enum.TryParse<TrafficLight>(level, ignoreCase: true, out var tl) ? tl : null;

    /// <summary>
    /// Gibt an, ob eine Benutzeraktion (Akzeptieren / Bearbeiten / Ablehnen)
    /// fuer das Event moeglich ist.
    /// Nur Events im noch-nicht-entschiedenen Zustand (Auto/Pending/Review) koennen bewertet werden.
    /// </summary>
    public static bool CanAct(CodingEvent? ev)
    {
        if (ev == null) return false;

        return GetStatus(ev) is
            DefectStatus.AutoAccepted or
            DefectStatus.Pending or
            DefectStatus.ReviewRequired;
    }
}
