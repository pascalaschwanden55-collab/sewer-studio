using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingStructuralClassifierFindingFactory
{
    public static LiveFrameFinding Create(string code, string label)
        => new(
            Label: label,
            Severity: 3,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: code);
}
