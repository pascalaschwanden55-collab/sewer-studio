using System.Windows.Media;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingBoundaryClassifierResultWorkflowTests
{
    [Theory]
    [InlineData("BCD", true)]
    [InlineData("BCE", true)]
    [InlineData("BCA", false)]
    [InlineData(null, false)]
    public void CanHandle_returns_true_only_for_boundary_classifier_codes(string? code, bool expected)
    {
        Assert.Equal(expected, CodingBoundaryClassifierResultWorkflow.CanHandle(Result(code)));
    }

    [Fact]
    public async Task ExecuteAsync_ignores_non_boundary_classifier_result()
    {
        var result = await CodingBoundaryClassifierResultWorkflow.ExecuteAsync(
            new CodingBoundaryClassifierResultWorkflowRequest(
                Result("BCA"),
                Meter: 4.2,
                EndMeter: 45.0,
                VideoTime: TimeSpan.FromSeconds(8),
                ExistingEventCount: 3,
                AnalyzedFrameBytes: [1, 2, 3]),
            NoActions());

        Assert.Equal(CodingBoundaryClassifierResultWorkflowOutcome.NotHandled, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public async Task ExecuteAsync_marks_early_bce_as_possible_boundary_without_adding_event()
    {
        var calls = new List<string>();

        var result = await CodingBoundaryClassifierResultWorkflow.ExecuteAsync(
            new CodingBoundaryClassifierResultWorkflowRequest(
                Result("BCE", confidence: 0.61),
                Meter: 28.68,
                EndMeter: 45.0,
                VideoTime: TimeSpan.FromSeconds(19),
                ExistingEventCount: 2,
                AnalyzedFrameBytes: [4, 5, 6]),
            Actions(
                lookupVsaLabel: code =>
                {
                    calls.Add($"lookup:{code}");
                    return "Rohrende";
                },
                trace: message =>
                {
                    calls.Add("trace");
                    Assert.Contains("BCE", message);
                    Assert.Contains("weiteranalysieren", message);
                },
                clearDetectionOverlays: () => calls.Add("clear-overlays"),
                clearMasks: () => calls.Add("clear-masks"),
                showPossibleBoundary: (code, label) => calls.Add($"possible:{code}:{label}"),
                ensureStartExistsAsync: (_, _, _) => throw new InvalidOperationException("Rohranfang darf nicht angelegt werden."),
                closeTrackedStretchDamages: _ => throw new InvalidOperationException("Streckenschaeden duerfen nicht geschlossen werden."),
                ensureEndExists: (_, _, _) => throw new InvalidOperationException("Rohrende darf noch nicht angelegt werden."),
                setAiState: (status, color, detail) =>
                {
                    calls.Add($"state:{status}|{detail}");
                    Assert.Equal(PlayerStatusColors.Warning, color);
                }));

        Assert.Equal(
            [
                "lookup:BCE",
                "trace",
                $"state:{CodingClassifierDisplayPolicy.PossibleBoundaryEndStatus}|{CodingClassifierDisplayPolicy.PossibleBoundaryEndDetail}",
                "clear-overlays",
                "clear-masks",
                "possible:BCE:Rohrende"
            ],
            calls);
        Assert.Equal(CodingBoundaryClassifierResultWorkflowOutcome.PossibleEndAhead, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public async Task ExecuteAsync_handles_bcd_by_ensuring_start_and_reporting_added_status()
    {
        var calls = new List<string>();
        var frameBytes = new byte[] { 7, 8, 9 };

        var result = await CodingBoundaryClassifierResultWorkflow.ExecuteAsync(
            new CodingBoundaryClassifierResultWorkflowRequest(
                Result("BCD", confidence: 0.72),
                Meter: 0.3,
                EndMeter: 45.0,
                VideoTime: TimeSpan.FromSeconds(2),
                ExistingEventCount: 2,
                AnalyzedFrameBytes: frameBytes),
            Actions(
                lookupVsaLabel: code =>
                {
                    calls.Add($"lookup:{code}");
                    return "Rohranfang";
                },
                ensureStartExistsAsync: (meter, videoTime, analyzedFrameBytes) =>
                {
                    calls.Add($"ensure-start:{meter:F1}:{videoTime.TotalSeconds:F0}");
                    Assert.Same(frameBytes, analyzedFrameBytes);
                    return Task.FromResult(true);
                },
                getCurrentEventCount: () => 2,
                setAiState: (status, color, detail) =>
                {
                    calls.Add($"state:{status}|{detail}");
                    Assert.Equal(PlayerStatusColors.Success, color);
                },
                showBoundary: (code, label) => calls.Add($"show:{code}:{label}")));

        Assert.Equal(
            [
                "ensure-start:0.3:2",
                "lookup:BCD",
                "state:Rohranfang erkannt|Klassifikator 72%",
                "show:BCD:Rohranfang"
            ],
            calls);
        Assert.Equal(CodingBoundaryClassifierResultWorkflowOutcome.BoundaryHandled, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public async Task ExecuteAsync_handles_plausible_bce_by_closing_open_stretches_and_ensuring_end()
    {
        var calls = new List<string>();
        var frameBytes = new byte[] { 10, 11, 12 };

        var result = await CodingBoundaryClassifierResultWorkflow.ExecuteAsync(
            new CodingBoundaryClassifierResultWorkflowRequest(
                Result("BCE", confidence: 0.84),
                Meter: 44.0,
                EndMeter: 45.0,
                VideoTime: TimeSpan.FromSeconds(42),
                ExistingEventCount: 2,
                AnalyzedFrameBytes: frameBytes),
            Actions(
                lookupVsaLabel: code =>
                {
                    calls.Add($"lookup:{code}");
                    return "Rohrende";
                },
                closeTrackedStretchDamages: meter => calls.Add($"close:{meter:F1}"),
                ensureEndExists: (meterEnd, videoTime, analyzedFrameBytes) =>
                {
                    calls.Add($"ensure-end:{meterEnd:F1}:{videoTime.TotalSeconds:F0}");
                    Assert.Same(frameBytes, analyzedFrameBytes);
                },
                clearDetectionOverlays: () => calls.Add("clear-overlays"),
                clearMasks: () => calls.Add("clear-masks"),
                getCurrentEventCount: () => 3,
                setAiState: (status, color, detail) =>
                {
                    calls.Add($"state:{status}|{detail}");
                    Assert.Equal(PlayerStatusColors.Success, color);
                },
                showBoundary: (code, label) => calls.Add($"show:{code}:{label}")));

        Assert.Equal(
            [
                "close:44.0",
                "ensure-end:45.0:42",
                "clear-overlays",
                "clear-masks",
                "lookup:BCE",
                "state:Rohrende erkannt|Klassifikator 84%",
                "show:BCE:Rohrende"
            ],
            calls);
        Assert.Equal(CodingBoundaryClassifierResultWorkflowOutcome.BoundaryHandled, result.Outcome);
        Assert.True(result.Handled);
    }

    private static CodingBoundaryClassifierResultWorkflowActions NoActions()
        => Actions(
            lookupVsaLabel: _ => throw new InvalidOperationException("Lookup should not run."),
            trace: _ => throw new InvalidOperationException("Trace should not run."),
            clearDetectionOverlays: () => throw new InvalidOperationException("Clear overlays should not run."),
            clearMasks: () => throw new InvalidOperationException("Clear masks should not run."),
            showPossibleBoundary: (_, _) => throw new InvalidOperationException("Show possible boundary should not run."),
            showBoundary: (_, _) => throw new InvalidOperationException("Show boundary should not run."),
            ensureStartExistsAsync: (_, _, _) => throw new InvalidOperationException("Ensure start should not run."),
            closeTrackedStretchDamages: _ => throw new InvalidOperationException("Close stretches should not run."),
            ensureEndExists: (_, _, _) => throw new InvalidOperationException("Ensure end should not run."),
            getCurrentEventCount: () => throw new InvalidOperationException("Event count should not be read."),
            setAiState: (_, _, _) => throw new InvalidOperationException("Set state should not run."));

    private static CodingBoundaryClassifierResultWorkflowActions Actions(
        Func<string, string?>? lookupVsaLabel = null,
        Action<string>? trace = null,
        Action? clearDetectionOverlays = null,
        Action? clearMasks = null,
        Action<string, string>? showPossibleBoundary = null,
        Action<string, string>? showBoundary = null,
        Func<double, TimeSpan, byte[]?, Task<bool>>? ensureStartExistsAsync = null,
        Action<double>? closeTrackedStretchDamages = null,
        Action<double, TimeSpan, byte[]?>? ensureEndExists = null,
        Func<int>? getCurrentEventCount = null,
        Action<string, Color, string?>? setAiState = null)
        => new(
            LookupVsaLabel: lookupVsaLabel ?? (_ => null),
            Trace: trace ?? (_ => { }),
            ClearDetectionOverlays: clearDetectionOverlays ?? (() => { }),
            ClearMasks: clearMasks ?? (() => { }),
            ShowPossibleBoundary: showPossibleBoundary ?? ((_, _) => { }),
            ShowBoundary: showBoundary ?? ((_, _) => { }),
            EnsureStartExistsAsync: ensureStartExistsAsync ?? ((_, _, _) => Task.FromResult(false)),
            CloseTrackedStretchDamages: closeTrackedStretchDamages ?? (_ => { }),
            EnsureEndExists: ensureEndExists ?? ((_, _, _) => { }),
            GetCurrentEventCount: getCurrentEventCount ?? (() => 0),
            SetAiState: setAiState ?? ((_, _, _) => { }));

    private static SingleFrameResult Result(string? code, double? confidence = null)
        => new(
            IsRelevant: true,
            DinoDetections: [],
            SamResponse: null,
            QuantifiedMasks: [],
            YoloTimeMs: 0,
            DinoTimeMs: 0,
            SamTimeMs: 0,
            Error: null,
            ClassifierCode: code,
            ClassifierConfidence: confidence);
}
