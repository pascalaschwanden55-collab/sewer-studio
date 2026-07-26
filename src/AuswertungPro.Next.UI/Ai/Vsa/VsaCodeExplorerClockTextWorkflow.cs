using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public sealed record VsaCodeExplorerClockVonChangedResult(
    string ClockVon,
    string? ClockBisText,
    string TransferText);

public sealed record VsaCodeExplorerClockBisChangedResult(
    string ClockBis,
    string TransferText);

public static class VsaCodeExplorerClockTextWorkflow
{
    public static VsaCodeExplorerClockVonChangedResult ApplyVonChanged(
        string clockVonText,
        string currentClockBisText,
        string clockMode)
    {
        var resolvedClockBis = string.Equals(clockMode, "single", StringComparison.Ordinal)
            ? string.IsNullOrWhiteSpace(clockVonText) ? string.Empty : "00"
            : null;

        return new VsaCodeExplorerClockVonChangedResult(
            ClockVon: clockVonText,
            ClockBisText: resolvedClockBis,
            TransferText: ClockTransferFormatter.Format(
                clockVonText,
                resolvedClockBis ?? currentClockBisText));
    }

    public static VsaCodeExplorerClockBisChangedResult ApplyBisChanged(
        string clockVonText,
        string clockBisText)
        => new(
            ClockBis: clockBisText,
            TransferText: ClockTransferFormatter.Format(clockVonText, clockBisText));
}
