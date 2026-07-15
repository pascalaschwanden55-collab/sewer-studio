using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionMarkToolControllerTests
{
    [Fact]
    public void Activate_drawing_tool_preserves_activation_and_overlay_ready_order()
    {
        var calls = new List<string>();
        var controller = new LiveDetectionMarkToolController(
            Bindings(
                createActivationActions: ensureOverlayReady => new LiveDetectionManualMarkActivationWorkflowActions(
                    BeginActivation: label => calls.Add($"begin:{label}"),
                    SetMarkToolType: tool => calls.Add($"mark:{tool}"),
                    SetPause: paused => calls.Add($"pause:{paused}"),
                    CancelSchema: () => calls.Add("schema:cancel"),
                    ClearSchemaType: () => calls.Add("schema:clear"),
                    SetManualMarkMode: enabled => calls.Add($"manual:{enabled}"),
                    ActivatePointTool: () => calls.Add("point"),
                    EnsureOverlayReady: ensureOverlayReady,
                    SetActiveTool: tool => calls.Add($"active:{tool}"),
                    ClearCurrentOverlay: () => calls.Add("overlay:clear"),
                    OpenCodingOverlay: () => calls.Add("overlay:open"),
                    UpdateCodingOverlayViewport: () => calls.Add("viewport"),
                    EnableCodingOverlayInput: () => calls.Add("input")),
                createOverlayReadyRequest: () =>
                {
                    calls.Add("ready");
                    return new LiveDetectionMarkOverlayReadyStateRequest(
                        HasOverlayService: true,
                        HasViewModel: true,
                        VideoPath: "video.mp4",
                        Settings: null,
                        ExistingSessionService: null,
                        ExistingOverlayService: null);
                }));

        controller.Activate(OverlayToolType.Rectangle, "Rechteck");

        Assert.Equal(
            [
                "begin:Rechteck",
                "mark:Rectangle",
                "pause:True",
                "schema:cancel",
                "schema:clear",
                "manual:False",
                "ready",
                "active:Rectangle",
                "overlay:clear",
                "overlay:open",
                "viewport",
                "input"
            ],
            calls);
    }

    [Fact]
    public void Deactivate_preserves_detection_and_coding_overlay_cleanup_order()
    {
        var calls = new List<string>();
        var controller = new LiveDetectionMarkToolController(
            Bindings(
                createDeactivationRequest: () => new LiveDetectionManualMarkDeactivationWorkflowRequest(
                    IsCodingMode: false,
                    IsLiveDetectionRunning: true),
                deactivationActions: new LiveDetectionManualMarkDeactivationWorkflowActions(
                    SetMarkToolType: tool => calls.Add($"mark:{tool}"),
                    SetManualMarkMode: enabled => calls.Add($"manual:{enabled}"),
                    ResetToolLabel: () => calls.Add("label"),
                    DeactivateDetectionSide: running => calls.Add($"detection:{running}"),
                    CancelSchema: () => calls.Add("schema"),
                    CancelDraw: () => calls.Add("draw"),
                    SetActiveTool: tool => calls.Add($"active:{tool}"),
                    DeactivateCodingOverlay: () => calls.Add("overlay"))));

        controller.Deactivate();

        Assert.Equal(
            [
                "mark:None",
                "manual:False",
                "label",
                "detection:True",
                "schema",
                "draw",
                "active:None",
                "overlay"
            ],
            calls);
    }

    private static LiveDetectionMarkToolControllerBindings Bindings(
        Action<bool>? toggleManualMarkPopup = null,
        Action? toggleToolsDropdown = null,
        Func<Action, LiveDetectionManualMarkActivationWorkflowActions>? createActivationActions = null,
        Func<LiveDetectionMarkOverlayReadyStateRequest>? createOverlayReadyRequest = null,
        LiveDetectionMarkOverlayReadyApplyActions? overlayReadyActions = null,
        Func<LiveDetectionManualMarkDeactivationWorkflowRequest>? createDeactivationRequest = null,
        LiveDetectionManualMarkDeactivationWorkflowActions? deactivationActions = null)
        => new(
            ToggleManualMarkPopup: toggleManualMarkPopup ?? (_ => { }),
            ToggleToolsDropdown: toggleToolsDropdown ?? (() => { }),
            CreateActivationActions: createActivationActions ?? (ensureOverlayReady => new(
                BeginActivation: _ => { },
                SetMarkToolType: _ => { },
                SetPause: _ => { },
                CancelSchema: () => { },
                ClearSchemaType: () => { },
                SetManualMarkMode: _ => { },
                ActivatePointTool: () => { },
                EnsureOverlayReady: ensureOverlayReady,
                SetActiveTool: _ => { },
                ClearCurrentOverlay: () => { },
                OpenCodingOverlay: () => { },
                UpdateCodingOverlayViewport: () => { },
                EnableCodingOverlayInput: () => { })),
            CreateOverlayReadyRequest: createOverlayReadyRequest ?? (() => new(true, true, "video.mp4", null, null, null)),
            OverlayReadyActions: overlayReadyActions ?? new(
                SetSessionService: _ => { },
                SetOverlayService: _ => { },
                SetViewModel: _ => { }),
            CreateDeactivationRequest: createDeactivationRequest ?? (() => new(false, false)),
            DeactivationActions: deactivationActions ?? new(
                SetMarkToolType: _ => { },
                SetManualMarkMode: _ => { },
                ResetToolLabel: () => { },
                DeactivateDetectionSide: _ => { },
                CancelSchema: () => { },
                CancelDraw: () => { },
                SetActiveTool: _ => { },
                DeactivateCodingOverlay: () => { }));
}
