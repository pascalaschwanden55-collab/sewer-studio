using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Entscheidet zentral, welche Codier-Ereignisse das Fachprotokoll veraendern duerfen.
/// KI-Vorschlaege brauchen immer eine ausdrueckliche Benutzerfreigabe.
/// </summary>
public static class CodingEventProtocolApplyPolicy
{
    public static bool CanApply(CodingEvent? codingEvent)
        => AiProtocolAcceptancePolicy.CanApply(codingEvent);

    public static IReadOnlyList<CodingEvent> Filter(IEnumerable<CodingEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return AiProtocolAcceptancePolicy.FilterCodingEvents(events);
    }
}
