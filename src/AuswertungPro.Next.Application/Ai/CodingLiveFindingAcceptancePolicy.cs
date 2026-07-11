using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Application.Ai;

public static class CodingLiveFindingAcceptancePolicy
{
    private const int CriticalSeverity = 4;

    public static bool ShouldSkipAsTooFarAhead(string? code, bool isTooFarAhead)
        => isTooFarAhead && !CodingDedupPolicy.IsOneTimeCode(code);

    public static bool NeedsConfirmation(QualityGateResult gateResult, LiveFrameFinding finding)
        => !gateResult.IsGreen || finding.Severity >= CriticalSeverity;
}
