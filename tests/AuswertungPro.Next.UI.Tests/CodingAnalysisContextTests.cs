using System.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAnalysisContextTests
{
    [Fact]
    public void IsAfterTerminalBoundary_reads_current_event_sources()
    {
        var sessionEvents = new List<CodingEvent>();
        var context = Context(sessionEvents: () => sessionEvents);

        Assert.False(context.IsAfterTerminalBoundary(12.5, TimeSpan.FromSeconds(10)));

        sessionEvents.Add(Event("BCE", meter: 12.5));

        Assert.True(context.IsAfterTerminalBoundary(12.5, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void IsFindingTooFarAhead_reads_current_calibration()
    {
        PipeCalibration? calibration = null;
        var context = Context(calibration: () => calibration);
        var finding = new LiveFrameFinding(
            "Riss",
            2,
            null,
            null,
            VsaCodeHint: "BAB",
            BboxX1: 0.68,
            BboxY1: 0.02,
            BboxX2: 0.72,
            BboxY2: 0.06);

        Assert.False(context.IsFindingTooFarAhead(finding));

        calibration = new PipeCalibration
        {
            PipeCenter = new NormalizedPoint(0.7, 0.04),
            NormalizedDiameter = 0.5
        };

        Assert.True(context.IsFindingTooFarAhead(finding));
    }

    [Fact]
    public async Task Build_and_capture_delegate_with_current_calibration_and_cancellation()
    {
        var calibration = new PipeCalibration { NormalizedDiameter = 0.8 };
        PipeCalibration? receivedCalibration = null;
        var expectedSegmented = new List<SegmentedFinding>();
        var expectedBytes = new byte[] { 1, 2, 3 };
        using var cancellation = new CancellationTokenSource();
        CancellationToken receivedToken = default;
        var context = Context(
            calibration: () => calibration,
            buildSegmentedFindings: (_, currentCalibration) =>
            {
                receivedCalibration = currentCalibration;
                return expectedSegmented;
            },
            captureSnapshotAsync: token =>
            {
                receivedToken = token;
                return Task.FromResult<byte[]?>(expectedBytes);
            });

        var segmented = context.BuildSegmentedFindings(Result());
        var bytes = await context.CaptureSnapshotAsync(cancellation.Token);

        Assert.Same(calibration, receivedCalibration);
        Assert.Same(expectedSegmented, segmented);
        Assert.Same(expectedBytes, bytes);
        Assert.Equal(cancellation.Token, receivedToken);
    }

    private static CodingAnalysisContext Context(
        Func<IEnumerable<CodingEvent>?>? sessionEvents = null,
        Func<PipeCalibration?>? calibration = null,
        Func<SingleFrameResult, PipeCalibration?, IReadOnlyList<SegmentedFinding>>? buildSegmentedFindings = null,
        Func<CancellationToken, Task<byte[]?>>? captureSnapshotAsync = null)
        => new(
            sessionEvents ?? (() => null),
            viewEvents: () => null,
            importEvents: () => [],
            calibration ?? (() => null),
            videoAspect: () => 1.0,
            buildSegmentedFindings ?? ((_, _) => []),
            captureSnapshotAsync ?? (_ => Task.FromResult<byte[]?>(null)));

    private static CodingEvent Event(string code, double meter)
        => new()
        {
            Entry = new ProtocolEntry { Code = code, MeterStart = meter },
            MeterAtCapture = meter
        };

    private static SingleFrameResult Result()
        => new(
            IsRelevant: false,
            DinoDetections: [],
            SamResponse: null,
            QuantifiedMasks: [],
            YoloTimeMs: 0,
            DinoTimeMs: 0,
            SamTimeMs: 0,
            Error: null);
}
