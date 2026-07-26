using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMoveByCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_prepare_and_osd_read_when_coding_view_model_is_missing()
    {
        var calls = new List<string>();

        var result = await CodingMoveByCommandWorkflow.ExecuteAsync(
            new CodingMoveByCommandRequest(
                HasCodingViewModel: false,
                TraceName: "CodingNext_Click"),
            Actions(calls));

        Assert.Equal(CodingMoveByCommandOutcome.Skipped, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task ExecuteAsync_skips_osd_read_when_prepare_returns_false()
    {
        var calls = new List<string>();

        var result = await CodingMoveByCommandWorkflow.ExecuteAsync(
            new CodingMoveByCommandRequest(
                HasCodingViewModel: true,
                TraceName: "CodingNext_Click"),
            Actions(
                calls,
                prepareMoveByCommand: () =>
                {
                    calls.Add("prepare:false");
                    return false;
                }));

        Assert.Equal(CodingMoveByCommandOutcome.Skipped, result.Outcome);
        Assert.Equal(["prepare:false"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_reads_osd_meter_after_successful_prepare()
    {
        var calls = new List<string>();

        var result = await CodingMoveByCommandWorkflow.ExecuteAsync(
            new CodingMoveByCommandRequest(
                HasCodingViewModel: true,
                TraceName: "CodingNext_Click"),
            Actions(calls));

        Assert.Equal(CodingMoveByCommandOutcome.Moved, result.Outcome);
        Assert.Equal(["prepare:true", "read-osd"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_traces_prepare_errors_without_throwing()
    {
        var calls = new List<string>();

        var result = await CodingMoveByCommandWorkflow.ExecuteAsync(
            new CodingMoveByCommandRequest(
                HasCodingViewModel: true,
                TraceName: "CodingPrevious_Click"),
            Actions(
                calls,
                prepareMoveByCommand: () =>
                {
                    calls.Add("prepare:throw");
                    throw new InvalidOperationException("boom");
                }));

        Assert.Equal(CodingMoveByCommandOutcome.Failed, result.Outcome);
        Assert.Equal(
            [
                "prepare:throw",
                "[PlayerWindow] CodingPrevious_Click error: boom"
            ],
            calls);
    }

    private static CodingMoveByCommandActions Actions(
        List<string> calls,
        Func<bool>? prepareMoveByCommand = null)
        => new(
            PrepareMoveByCommand: prepareMoveByCommand ?? (() =>
            {
                calls.Add("prepare:true");
                return true;
            }),
            ReadOsdMeterAsync: () =>
            {
                calls.Add("read-osd");
                return Task.FromResult<double?>(12.3);
            },
            TraceError: message => calls.Add(message));
}
