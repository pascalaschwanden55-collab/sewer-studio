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
        if (ev.AiContext is null)
            return ev.ReviewContext is null
                ? DefectStatus.Pending
                : MapUserDecision(ev.ReviewContext.Decision, DefectStatus.Pending);

        return MapUserDecision(
            ev.AiContext.Decision,
            MapCentralDecision(ev.AiContext));
    }

    private static DefectStatus MapUserDecision(
        CodingUserDecision decision,
        DefectStatus ignoredStatus)
        => decision switch
        {
            CodingUserDecision.Accepted => DefectStatus.Accepted,
            CodingUserDecision.AcceptedWithEdit => DefectStatus.AcceptedWithEdit,
            CodingUserDecision.Rejected => DefectStatus.Rejected,
            _ => ignoredStatus
        };

    // Noch nicht vom Nutzer entschieden: zentrale Freigabe-Regel anwenden.
    private static DefectStatus MapCentralDecision(CodingEventAiContext ctx)
        => GetCentralDecision(ctx).Outcome switch
        {
            AiDecisionOutcome.AutoAccept => DefectStatus.AutoAccepted,
            AiDecisionOutcome.Review     => DefectStatus.Pending,
            _                            => DefectStatus.ReviewRequired
        };

    /// <summary>
    /// Bewertet den gespeicherten KI-Kontext mit der aktuellen zentralen Regel.
    /// Der Aufrufer kann das Ergebnis anschliessend versioniert persistieren.
    /// </summary>
    public static AiDecision GetCentralDecision(CodingEventAiContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var signals = new AiDecisionSignals(
            Confidence: ctx.Confidence,
            QualityGate: ParseLight(ctx.QualityGateLevel),
            KbAgreement: ctx.Evidence?.KbCodeAgreement);

        return StandardAiDecisionPolicy.Default.Decide(signals);
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
