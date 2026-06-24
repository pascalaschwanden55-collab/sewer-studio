using System;
using System.Windows.Media;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public sealed record LiveDetectionRuntimeStartStatus(
    string BadgeText,
    Color StatusColor,
    string BadgeDetails,
    string YoloText,
    string ModelLabel);

public sealed record LiveDetectionRuntimeStartActions(
    Action<LiveDetectionRuntime> StoreRuntime,
    Action ResetCancellation,
    Action MarkDetecting,
    Action ShowOverlay,
    Action<LiveDetectionRuntimeStartStatus> ApplyActiveStatus,
    Action ShowWaitingForFrame,
    Action StartTimer,
    Action RunFirstDetection);

public static class LiveDetectionRuntimeStartWorkflow
{
    public static void Start(
        LiveDetectionRuntime runtime,
        LiveDetectionRuntimeStartActions actions)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(actions);

        var modelLabel = LiveDetectionDisplayPolicy.CompactModelName(runtime.VisionModel);

        actions.StoreRuntime(runtime);
        actions.ResetCancellation();
        actions.MarkDetecting();
        actions.ShowOverlay();
        actions.ApplyActiveStatus(new LiveDetectionRuntimeStartStatus(
            BadgeText: "KI aktiv",
            StatusColor: PlayerStatusColors.Success,
            BadgeDetails: $"Modell: {modelLabel}",
            YoloText: "Aktiv",
            ModelLabel: modelLabel));
        actions.ShowWaitingForFrame();
        actions.StartTimer();
        actions.RunFirstDetection();
    }
}
