using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Anzeigestatus eines Defekts nach KI-Verarbeitung und Benutzerentscheidung.
/// </summary>
public enum DefectStatus
{
    AutoAccepted,     // KI-akzeptiert (Green Zone: Confidence >= 0.85)
    Pending,          // Warten auf Review (Yellow Zone: Confidence 0.60–0.84)
    ReviewRequired,   // Manuell erforderlich (Red Zone: Confidence < 0.60)
    Accepted,         // Manuell akzeptiert
    AcceptedWithEdit, // Akzeptiert mit Korrektur
    Rejected          // Abgelehnt
}

/// <summary>
/// Pure Geschaeftslogik: bestimmt den Defekt-Status eines Codier-Events
/// anhand der KI-Konfidenz und der Benutzerentscheidung.
/// Schwellwerte: Green >= 0.85, Yellow >= 0.60, Red &lt; 0.60.
/// </summary>
public static class DefectStatusPolicy
{
    // Konfidenz-Schwellwerte fuer die drei Zonen
    private const double GreenThreshold  = 0.85;
    private const double YellowThreshold = 0.60;

    /// <summary>
    /// Ermittelt den <see cref="DefectStatus"/> fuer ein <see cref="CodingEvent"/>.
    /// Kein KI-Kontext → Pending (noch nicht bewertet).
    /// Manuelle Entscheidung schlaegt Konfidenz-Zone.
    /// </summary>
    public static DefectStatus GetStatus(CodingEvent ev)
    {
        if (ev.AiContext == null) return DefectStatus.Pending;

        return ev.AiContext.Decision switch
        {
            CodingUserDecision.Accepted        => DefectStatus.Accepted,
            CodingUserDecision.AcceptedWithEdit => DefectStatus.AcceptedWithEdit,
            CodingUserDecision.Rejected        => DefectStatus.Rejected,
            _ => ev.AiContext.Confidence switch
            {
                >= GreenThreshold  => DefectStatus.AutoAccepted,
                >= YellowThreshold => DefectStatus.Pending,
                _                  => DefectStatus.ReviewRequired
            }
        };
    }

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
