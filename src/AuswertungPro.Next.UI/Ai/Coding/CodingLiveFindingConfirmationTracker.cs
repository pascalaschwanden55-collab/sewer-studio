using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingLiveFindingConfirmationTracker
{
    public CodingEvent? Event { get; private set; }
    public QualityGateResult? Gate { get; private set; }
    public bool HasPendingConfirmation => Event != null && Gate != null;

    public void Observe(CodingEvent codingEvent, QualityGateResult gateResult, LiveFrameFinding finding)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);
        ArgumentNullException.ThrowIfNull(gateResult);
        ArgumentNullException.ThrowIfNull(finding);

        if (HasPendingConfirmation)
            return;

        if (!CodingLiveFindingAcceptancePolicy.NeedsConfirmation(gateResult, finding))
            return;

        Event = codingEvent;
        Gate = gateResult;
    }
}
