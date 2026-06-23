using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingProtocolGreenMatchTrainingRunner
{
    public static async Task<CodingProtocolMatchOverlayState?> AcceptGreenMatchesAsync(
        CodingMatchRouting routing,
        IEnumerable<CodingEvent> importEvents,
        Func<CodingEvent, Task<bool>> confirmAsync)
    {
        ArgumentNullException.ThrowIfNull(routing);
        ArgumentNullException.ThrowIfNull(importEvents);
        ArgumentNullException.ThrowIfNull(confirmAsync);

        if (routing.Trainingskandidaten.Count == 0)
            return null;

        var accepted = 0;
        foreach (var importEvent in CodingProtocolTrainingCandidateResolver.ResolveImportEvents(
                     routing.Trainingskandidaten,
                     importEvents))
        {
            if (await confirmAsync(importEvent))
                accepted++;
        }

        return CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay(accepted);
    }
}
