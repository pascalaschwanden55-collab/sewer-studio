using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Entscheidet zentral, welche Codier-Ereignisse das Fachprotokoll veraendern duerfen.
/// KI-Vorschlaege brauchen immer eine ausdrueckliche Benutzerfreigabe.
/// </summary>
public static class CodingEventProtocolApplyPolicy
{
    public static bool CanApply(CodingEvent? codingEvent)
    {
        if (codingEvent is null || string.IsNullOrWhiteSpace(codingEvent.Entry.Code))
            return false;

        if (codingEvent.AiContext is null)
            return true;

        return codingEvent.AiContext.Decision is
            CodingUserDecision.Accepted or
            CodingUserDecision.AcceptedWithEdit;
    }

    public static IReadOnlyList<CodingEvent> Filter(IEnumerable<CodingEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return events.Where(CanApply).ToList();
    }
}
