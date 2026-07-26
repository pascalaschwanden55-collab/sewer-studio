using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiResultWorkflowTests
{
    [Fact]
    public void Execute_shows_error_and_clears_findings_without_touching_readiness()
    {
        var calls = new List<string>();

        var result = CodingAiResultWorkflow.Execute(
            new CodingAiResultWorkflowRequest(
                Result: new LiveDetection(1, [], null, "Timeout"),
                ModelName: "models/qwen3-vl:8b",
                HasCodingViewModel: true,
                IsOverlayPopupOpen: false,
                PlayerTimeSeconds: 5),
            Actions(
                setAiState: (status, color, detail) =>
                {
                    calls.Add($"state:{status}|{detail}");
                    Assert.Equal(PlayerStatusColors.Error, color);
                },
                clearFindings: () => calls.Add("clear-findings"),
                updateFrameReadiness: _ => throw new InvalidOperationException("Readiness must not run on error.")));

        Assert.Equal(["state:Fehler: Timeout|Modell: qwen3-vl:8b", "clear-findings"], calls);
        Assert.Equal(CodingAiResultWorkflowOutcome.Error, result.Outcome);
    }

    [Fact]
    public void Execute_buffers_warmup_result_before_clearing_findings_and_canvas()
    {
        var calls = new List<string>();
        var detection = new LiveDetection(2, [], null, null);

        var result = CodingAiResultWorkflow.Execute(
            new CodingAiResultWorkflowRequest(
                detection,
                ModelName: "qwen",
                HasCodingViewModel: true,
                IsOverlayPopupOpen: false,
                PlayerTimeSeconds: 0),
            Actions(
                updateFrameReadiness: seen =>
                {
                    calls.Add("update-readiness");
                    Assert.Same(detection, seen);
                },
                isFrameReady: () => false,
                storePendingWarmupResult: seen =>
                {
                    calls.Add("store-warmup");
                    Assert.Same(detection, seen);
                },
                getSkippedFrames: () => 2,
                setAiState: (status, color, detail) =>
                {
                    calls.Add($"state:{status}|{detail}");
                    Assert.Equal(PlayerStatusColors.Muted, color);
                },
                clearFindingsAndCanvas: () => calls.Add("clear-findings-canvas"),
                selectReadyResult: _ => throw new InvalidOperationException("Warmup must not select a ready result.")));

        Assert.Equal(
            ["update-readiness", "store-warmup", "state:Dateneinblendung erkannt - uebersprungen|Warte auf Videobild... (Bild 2 von 3)", "clear-findings-canvas"],
            calls);
        Assert.Equal(CodingAiResultWorkflowOutcome.Warmup, result.Outcome);
    }

    [Fact]
    public void Execute_processes_ready_findings_before_rendering_new_overlay()
    {
        var calls = new List<string>();
        var finding = new LiveFrameFinding("Riss", 4, "3", 20, VsaCodeHint: "BAB");
        var detection = new LiveDetection(7, [finding], 4.2, null);
        var osdState = new CodingOsdMeterState(4.2, 7, "4.20m");

        var result = CodingAiResultWorkflow.Execute(
            new CodingAiResultWorkflowRequest(
                detection,
                ModelName: "qwen",
                HasCodingViewModel: true,
                IsOverlayPopupOpen: false,
                PlayerTimeSeconds: 8.5),
            Actions(
                updateFrameReadiness: _ => calls.Add("update-readiness"),
                isFrameReady: () => true,
                selectReadyResult: seen =>
                {
                    calls.Add("select-ready");
                    return seen;
                },
                resolveOsdMeterState: seen =>
                {
                    calls.Add("resolve-osd");
                    Assert.Same(detection, seen);
                    return osdState;
                },
                applyOsdMeterState: state =>
                {
                    calls.Add("apply-osd");
                    Assert.Equal(osdState, state);
                },
                moveSessionToMeter: meter =>
                {
                    calls.Add($"move:{meter:0.0}");
                    Assert.Equal(4.2, meter);
                },
                resolveCurrentMeter: (timestamp, meterReading) =>
                {
                    calls.Add("resolve-meter");
                    Assert.Equal(7, timestamp);
                    Assert.Equal(4.2, meterReading);
                    return 4.2;
                },
                filterValidFindings: (raw, meter) =>
                {
                    calls.Add("filter");
                    Assert.Equal(4.2, meter);
                    return raw;
                },
                setAiState: (status, color, detail) =>
                {
                    calls.Add($"state:{detail}");
                    Assert.Contains("1 Befund", status);
                    Assert.Equal(PlayerStatusColors.Success, color);
                },
                showFindings: findings =>
                {
                    calls.Add("show-findings");
                    Assert.Same(finding, Assert.Single(findings));
                },
                selectFindingsToDraw: (findings, meter) =>
                {
                    calls.Add("select-draw");
                    Assert.Equal(4.2, meter);
                    return findings;
                },
                addAiFindingsAsEvents: (seen, findings) =>
                {
                    calls.Add("add-events");
                    Assert.Same(detection, seen);
                    Assert.Same(finding, Assert.Single(findings));
                },
                showDetectionOverlay: () => calls.Add("show-overlay"),
                renderDetectionOverlay: (findings, timestamp) =>
                {
                    calls.Add($"render:{timestamp:0.0}");
                    Assert.Same(finding, Assert.Single(findings));
                },
                scheduleDetectionAutoHide: () => calls.Add("schedule-hide")));

        Assert.Equal(
            [
                "update-readiness",
                "select-ready",
                "resolve-osd",
                "apply-osd",
                "move:4.2",
                "resolve-meter",
                "filter",
                "state:Schritt 3 von 3: Overlay und Events",
                "show-findings",
                "select-draw",
                "add-events",
                "show-overlay",
                "render:8.5",
                "schedule-hide"
            ],
            calls);
        Assert.Equal(CodingAiResultWorkflowOutcome.FindingsShown, result.Outcome);
        Assert.Equal(1, result.ValidFindingCount);
        Assert.Equal(1, result.DrawnFindingCount);
    }

    private static CodingAiResultWorkflowActions Actions(
        Action<string, Color, string?>? setAiState = null,
        Action? clearFindings = null,
        Action? clearFindingsAndCanvas = null,
        Action? clearVisuals = null,
        Action<LiveDetection>? updateFrameReadiness = null,
        Func<bool>? isFrameReady = null,
        Action<LiveDetection>? storePendingWarmupResult = null,
        Func<int>? getSkippedFrames = null,
        Func<LiveDetection, LiveDetection>? selectReadyResult = null,
        Func<LiveDetection, CodingOsdMeterState?>? resolveOsdMeterState = null,
        Action<CodingOsdMeterState>? applyOsdMeterState = null,
        Action<double>? moveSessionToMeter = null,
        Func<double?, double?, double>? resolveCurrentMeter = null,
        Func<IReadOnlyList<LiveFrameFinding>, double, IReadOnlyList<LiveFrameFinding>>? filterValidFindings = null,
        Action<IReadOnlyList<LiveFrameFinding>>? showFindings = null,
        Func<IReadOnlyList<LiveFrameFinding>, double, IReadOnlyList<LiveFrameFinding>>? selectFindingsToDraw = null,
        Action<LiveDetection, IReadOnlyList<LiveFrameFinding>>? addAiFindingsAsEvents = null,
        Action? showDetectionOverlay = null,
        Action<IReadOnlyList<LiveFrameFinding>, double>? renderDetectionOverlay = null,
        Action? scheduleDetectionAutoHide = null)
        => new(
            SetAiState: setAiState ?? ((_, _, _) => throw new InvalidOperationException("SetAiState should not run.")),
            ClearFindings: clearFindings ?? (() => throw new InvalidOperationException("ClearFindings should not run.")),
            ClearFindingsAndCanvas: clearFindingsAndCanvas ?? (() => throw new InvalidOperationException("ClearFindingsAndCanvas should not run.")),
            ClearVisuals: clearVisuals ?? (() => throw new InvalidOperationException("ClearVisuals should not run.")),
            UpdateFrameReadiness: updateFrameReadiness ?? (_ => throw new InvalidOperationException("UpdateFrameReadiness should not run.")),
            IsFrameReady: isFrameReady ?? (() => throw new InvalidOperationException("IsFrameReady should not run.")),
            StorePendingWarmupResult: storePendingWarmupResult ?? (_ => throw new InvalidOperationException("StorePendingWarmupResult should not run.")),
            GetSkippedFrames: getSkippedFrames ?? (() => throw new InvalidOperationException("GetSkippedFrames should not run.")),
            SelectReadyResult: selectReadyResult ?? (_ => throw new InvalidOperationException("SelectReadyResult should not run.")),
            ResolveOsdMeterState: resolveOsdMeterState ?? (_ => throw new InvalidOperationException("ResolveOsdMeterState should not run.")),
            ApplyOsdMeterState: applyOsdMeterState ?? (_ => throw new InvalidOperationException("ApplyOsdMeterState should not run.")),
            MoveSessionToMeter: moveSessionToMeter ?? (_ => throw new InvalidOperationException("MoveSessionToMeter should not run.")),
            ResolveCurrentMeter: resolveCurrentMeter ?? ((_, _) => throw new InvalidOperationException("ResolveCurrentMeter should not run.")),
            FilterValidFindings: filterValidFindings ?? ((_, _) => throw new InvalidOperationException("FilterValidFindings should not run.")),
            ShowFindings: showFindings ?? (_ => throw new InvalidOperationException("ShowFindings should not run.")),
            SelectFindingsToDraw: selectFindingsToDraw ?? ((_, _) => throw new InvalidOperationException("SelectFindingsToDraw should not run.")),
            AddAiFindingsAsEvents: addAiFindingsAsEvents ?? ((_, _) => throw new InvalidOperationException("AddAiFindingsAsEvents should not run.")),
            ShowDetectionOverlay: showDetectionOverlay ?? (() => throw new InvalidOperationException("ShowDetectionOverlay should not run.")),
            RenderDetectionOverlay: renderDetectionOverlay ?? ((_, _) => throw new InvalidOperationException("RenderDetectionOverlay should not run.")),
            ScheduleDetectionAutoHide: scheduleDetectionAutoHide ?? (() => throw new InvalidOperationException("ScheduleDetectionAutoHide should not run.")));
}
