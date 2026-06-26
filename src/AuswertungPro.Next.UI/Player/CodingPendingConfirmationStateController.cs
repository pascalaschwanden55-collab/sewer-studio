using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingPendingConfirmationStateController
{
    public CodingEvent? CodingEvent { get; private set; }

    public QualityGateResult? GateResult { get; private set; }

    public bool HasPendingConfirmation => CodingEvent != null && GateResult != null;

    public void Store(CodingEvent codingEvent, QualityGateResult gateResult)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);
        ArgumentNullException.ThrowIfNull(gateResult);

        CodingEvent = codingEvent;
        GateResult = gateResult;
    }

    public void Clear()
    {
        CodingEvent = null;
        GateResult = null;
    }
}
