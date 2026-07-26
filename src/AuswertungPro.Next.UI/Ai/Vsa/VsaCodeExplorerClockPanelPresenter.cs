using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public sealed record VsaCodeExplorerClockPanelPresentation(
    bool ShowPanel,
    string Title,
    string? Hint,
    bool ShowHint,
    bool ShowSinglePanel,
    bool ShowRangePanel,
    string UsageHint,
    bool ShowRightPreset,
    bool ShowGesamtPreset,
    string? ClockBisText,
    string? ClockSingleValue,
    string? ClockRangeFrom,
    string? ClockRangeTo,
    string? TransferText);

public static class VsaCodeExplorerClockPanelPresenter
{
    public static VsaCodeExplorerClockPanelPresentation Build(
        string clockMode,
        string? clockHint,
        string? clockVonText,
        string? clockBisText)
    {
        if (clockMode == "none")
        {
            return new VsaCodeExplorerClockPanelPresentation(
                ShowPanel: false,
                Title: "",
                Hint: null,
                ShowHint: false,
                ShowSinglePanel: false,
                ShowRangePanel: false,
                UsageHint: "",
                ShowRightPreset: false,
                ShowGesamtPreset: false,
                ClockBisText: null,
                ClockSingleValue: null,
                ClockRangeFrom: null,
                ClockRangeTo: null,
                TransferText: null);
        }

        var isSingle = clockMode == "single";
        var isRange = clockMode == "range";
        var effectiveBis = clockBisText;
        string? clockSingleValue = null;
        string? clockRangeFrom = null;
        string? clockRangeTo = null;

        if (isSingle)
        {
            effectiveBis = string.IsNullOrWhiteSpace(clockVonText) ? string.Empty : "00";
            clockSingleValue = HideZeroClock(clockVonText);
        }
        else if (isRange)
        {
            clockRangeFrom = HideZeroClock(clockVonText);
            clockRangeTo = HideZeroClock(clockBisText);
        }

        return new VsaCodeExplorerClockPanelPresentation(
            ShowPanel: true,
            Title: isSingle ? "LAGE AM UMFANG (PUNKT)" : "LAGE AM UMFANG (VON-BIS)",
            Hint: clockHint,
            ShowHint: clockHint is not null,
            ShowSinglePanel: isSingle,
            ShowRangePanel: isRange,
            UsageHint: isSingle
                ? "Klick = Punkt (Mitte der Feststellung)"
                : "1. Klick = Von, 2. Klick = Bis (im Uhrzeigersinn)",
            ShowRightPreset: !isSingle,
            ShowGesamtPreset: !isSingle,
            ClockBisText: isSingle ? effectiveBis : null,
            ClockSingleValue: clockSingleValue,
            ClockRangeFrom: clockRangeFrom,
            ClockRangeTo: clockRangeTo,
            TransferText: ClockTransferFormatter.Format(clockVonText, effectiveBis));
    }

    private static string HideZeroClock(string? value)
        => string.Equals(value?.Trim(), "00", StringComparison.Ordinal)
            ? ""
            : value ?? string.Empty;
}
