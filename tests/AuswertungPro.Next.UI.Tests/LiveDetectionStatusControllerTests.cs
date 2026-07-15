using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionStatusControllerTests
{
    [Fact]
    public void SetLiveDetectionBadge_dispatches_to_ui_before_applying_status()
    {
        Action? dispatched = null;
        var calls = new List<string>();
        var controller = new LiveDetectionStatusController(
            Actions(
                hasDispatcherAccess: () => false,
                dispatchToUi: action => dispatched = action,
                showLiveDetectionBadge: (status, _, stage) => calls.Add($"badge:{status}|{stage}")));

        controller.SetLiveDetectionBadge("Analyse", PlayerStatusColors.Warning, "Frame 4");

        Assert.NotNull(dispatched);
        Assert.Empty(calls);

        dispatched();

        Assert.Equal(["badge:Analyse|Frame 4"], calls);
    }

    [Fact]
    public void SetCodingAiState_applies_status_and_controls_pulse_in_existing_order()
    {
        var calls = new List<string>();
        var controller = new LiveDetectionStatusController(
            Actions(
                showCodingAiState: (status, _, stage) => calls.Add($"state:{status}|{stage}"),
                startPulse: () => calls.Add("pulse:start"),
                stopPulse: () => calls.Add("pulse:stop")));

        controller.SetCodingAiState("Läuft", PlayerStatusColors.Success, "KI", pulse: true);
        controller.SetCodingAiState("Bereit", PlayerStatusColors.Muted, "Warten");

        Assert.Equal(
            [
                "state:Läuft|KI",
                "pulse:start",
                "state:Bereit|Warten",
                "pulse:stop"
            ],
            calls);
    }

    [Fact]
    public void UpdateDetectionStatus_forwards_same_detection_without_dispatch()
    {
        LiveDetection? forwarded = null;
        var detection = new LiveDetection(12.5, [], 4.2, Error: null);
        var controller = new LiveDetectionStatusController(
            Actions(showDetectionStatus: value => forwarded = value));

        controller.UpdateDetectionStatus(detection);

        Assert.Same(detection, forwarded);
    }

    private static LiveDetectionStatusControllerActions Actions(
        Func<bool>? hasDispatcherAccess = null,
        Action<Action>? dispatchToUi = null,
        Action<string, System.Windows.Media.Color, string?>? showLiveDetectionBadge = null,
        Action<string, System.Windows.Media.Color, string?>? showCodingAiState = null,
        Action? startPulse = null,
        Action? stopPulse = null,
        Action<LiveDetection>? showDetectionStatus = null)
        => new(
            HasDispatcherAccess: hasDispatcherAccess ?? (() => true),
            DispatchToUi: dispatchToUi ?? (_ => throw new InvalidOperationException("DispatchToUi should not run.")),
            ShowLiveDetectionBadge: showLiveDetectionBadge ?? ((_, _, _) => { }),
            ShowYoloStatus: (_, _, _) => { },
            ShowCodingAiState: showCodingAiState ?? ((_, _, _) => { }),
            StartPulse: startPulse ?? (() => { }),
            StopPulse: stopPulse ?? (() => { }),
            ShowDetectionStatus: showDetectionStatus ?? (_ => { }));
}
