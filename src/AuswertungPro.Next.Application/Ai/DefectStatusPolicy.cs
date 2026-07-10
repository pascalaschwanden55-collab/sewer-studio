using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Anzeigestatus eines Defekts nach KI-Verarbeitung und Benutzerentscheidung.
/// </summary>
public enum DefectStatus
{
    AutoAccepted,     // Nur nach zentraler Auto-Freigabepolicy
    Pending,          // Hohe/mittlere Confidence, aber noch nicht sicher auto-freigegeben
    ReviewRequired,   // Manuell erforderlich
    Accepted,         // Manuell akzeptiert
    AcceptedWithEdit, // Akzeptiert mit Korrektur
    Rejected          // Abgelehnt
}

/// <summary>
/// Pure Geschaeftslogik: bestimmt den Defekt-Status eines Codier-Events.
/// Eine hohe Confidence ist nur eine Anzeigezone. AutoAccepted wird ausschliesslich
/// durch <see cref="AiDecisionPolicy"/> vergeben und benoetigt Gate, KB-Nachweis
/// und bestimmte epistemische Unsicherheit.
/// </summary>
public static class DefectStatusPolicy
{
    public const double HighConfidenceZoneThreshold = 0.85;
    public const double ReviewThreshold = 0.60;

    /// <summary>
    /// Ermittelt den <see cref="DefectStatus"/> fuer ein <see cref="CodingEvent"/>.
    /// Kein KI-Kontext → Pending. Manuelle Entscheidung schlaegt die KI-Policy.
    /// </summary>
    public static DefectStatus GetStatus(CodingEvent ev)
    {
        if (ev.AiContext == null)
            return DefectStatus.Pending;

        if (ev.AiContext.Decision == CodingUserDecision.Accepted)
            return DefectStatus.Accepted;
        if (ev.AiContext.Decision == CodingUserDecision.AcceptedWithEdit)
            return DefectStatus.AcceptedWithEdit;
        if (ev.AiContext.Decision == CodingUserDecision.Rejected)
            return DefectStatus.Rejected;

        var approval = AiDecisionPolicy.Evaluate(new AiDecisionEvidence(
            ev.AiContext.Confidence,
            ev.AiContext.QualityGateLevel,
            ev.AiContext.KbCodeAgreement,
            ev.AiContext.EpistemicUncertainty));

        if (approval.IsAutoApproved)
            return DefectStatus.AutoAccepted;

        return ev.AiContext.Confidence >= ReviewThreshold
            ? DefectStatus.Pending
            : DefectStatus.ReviewRequired;
    }

    /// <summary>
    /// Gibt an, ob eine Benutzeraktion (Akzeptieren / Bearbeiten / Ablehnen)
    /// fuer das Event moeglich ist.
    /// </summary>
    public static bool CanAct(CodingEvent? ev)
    {
        if (ev == null)
            return false;

        return GetStatus(ev) is
            DefectStatus.AutoAccepted or
            DefectStatus.Pending or
            DefectStatus.ReviewRequired;
    }
}
