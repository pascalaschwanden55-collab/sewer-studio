using System.Windows.Media;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingConfirmationPanelControlsOwner
{
    public CodingConfirmationPanelControls Controls { get; private set; } = null!;

    public bool IsInitialized => Controls is not null;

    public void Initialize(CodingConfirmationPanelControls controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        Controls = controls;
    }

    public Color Apply(CodingEvent codingEvent, QualityGateResult gateResult)
        => Controls.Apply(codingEvent, gateResult);

    public void Hide()
    {
        Controls.Hide();
    }
}
