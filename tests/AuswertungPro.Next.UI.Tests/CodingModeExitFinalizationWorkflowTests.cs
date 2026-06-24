using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeExitFinalizationWorkflowTests
{
    [Fact]
    public void Execute_skips_finalization_when_no_coding_events_exist()
    {
        var calls = new List<string>();

        var result = CodingModeExitFinalizationWorkflow.Execute(
            new CodingModeExitFinalizationWorkflowRequest(
                Events: [],
                LastOsdMeter: 8.5,
                EndMeter: 12.0,
                EndTime: TimeSpan.FromSeconds(90),
                AnalyzedFrameBytes: [1, 2, 3]),
            Actions(
                closeTrackedStreckenschaeden: meter => calls.Add($"tracked:{meter:F1}"),
                closeOpenStreckenschaeden: meter =>
                {
                    calls.Add($"open:{meter:F1}");
                    return true;
                },
                ensureRohrendeExists: (_, _, _) => calls.Add("rohrende")));

        Assert.True(result.CanExit);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_closes_stretch_damage_and_creates_missing_rohrende_with_analyzed_frame()
    {
        var calls = new List<string>();
        var frameBytes = new byte[] { 1, 2, 3 };

        var result = CodingModeExitFinalizationWorkflow.Execute(
            new CodingModeExitFinalizationWorkflowRequest(
                Events: [Event("BCA")],
                LastOsdMeter: 8.5,
                EndMeter: 12.0,
                EndTime: TimeSpan.FromSeconds(90),
                AnalyzedFrameBytes: frameBytes),
            Actions(
                closeTrackedStreckenschaeden: meter => calls.Add($"tracked:{meter:F1}"),
                closeOpenStreckenschaeden: meter =>
                {
                    calls.Add($"open:{meter:F1}");
                    return true;
                },
                ensureRohrendeExists: (meter, time, bytes) =>
                {
                    calls.Add($"rohrende:{meter:F1}:{time.TotalSeconds:F0}:{ReferenceEquals(frameBytes, bytes)}");
                }));

        Assert.True(result.CanExit);
        Assert.Equal(["tracked:8.5", "open:8.5", "rohrende:12.0:90:True"], calls);
    }

    [Fact]
    public void Execute_keeps_coding_mode_open_when_manual_stretch_damage_close_is_cancelled()
    {
        var calls = new List<string>();

        var result = CodingModeExitFinalizationWorkflow.Execute(
            new CodingModeExitFinalizationWorkflowRequest(
                Events: [Event("BCA")],
                LastOsdMeter: null,
                EndMeter: 12.0,
                EndTime: TimeSpan.FromSeconds(90),
                AnalyzedFrameBytes: [1, 2, 3]),
            Actions(
                closeTrackedStreckenschaeden: meter => calls.Add($"tracked:{meter:F1}"),
                closeOpenStreckenschaeden: meter =>
                {
                    calls.Add($"open:{meter:F1}");
                    return false;
                },
                ensureRohrendeExists: (_, _, _) => calls.Add("rohrende")));

        Assert.False(result.CanExit);
        Assert.Equal(["tracked:12.0", "open:12.0"], calls);
    }

    [Theory]
    [InlineData("BCE")]
    [InlineData("BDC")]
    public void Execute_does_not_create_rohrende_when_terminal_boundary_exists(string terminalCode)
    {
        var calls = new List<string>();

        var result = CodingModeExitFinalizationWorkflow.Execute(
            new CodingModeExitFinalizationWorkflowRequest(
                Events: [Event(terminalCode)],
                LastOsdMeter: 8.5,
                EndMeter: 12.0,
                EndTime: TimeSpan.FromSeconds(90),
                AnalyzedFrameBytes: [1, 2, 3]),
            Actions(
                closeTrackedStreckenschaeden: meter => calls.Add($"tracked:{meter:F1}"),
                closeOpenStreckenschaeden: meter =>
                {
                    calls.Add($"open:{meter:F1}");
                    return true;
                },
                ensureRohrendeExists: (_, _, _) => calls.Add("rohrende")));

        Assert.True(result.CanExit);
        Assert.Equal(["tracked:8.5", "open:8.5"], calls);
    }

    private static CodingModeExitFinalizationWorkflowActions Actions(
        Action<double>? closeTrackedStreckenschaeden = null,
        Func<double, bool>? closeOpenStreckenschaeden = null,
        Action<double, TimeSpan, byte[]?>? ensureRohrendeExists = null)
        => new(
            CloseTrackedStreckenschaeden: closeTrackedStreckenschaeden ?? (_ => { }),
            CloseOpenStreckenschaeden: closeOpenStreckenschaeden ?? (_ => true),
            EnsureRohrendeExists: ensureRohrendeExists ?? ((_, _, _) => { }));

    private static CodingEvent Event(string code)
        => new()
        {
            Entry = new ProtocolEntry { Code = code }
        };
}
