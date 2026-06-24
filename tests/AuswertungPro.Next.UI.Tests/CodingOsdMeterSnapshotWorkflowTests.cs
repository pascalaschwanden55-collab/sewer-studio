using System.Globalization;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOsdMeterSnapshotWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_without_live_detection()
    {
        var calls = new List<string>();

        var result = await CodingOsdMeterSnapshotWorkflow.ExecuteAsync(
            new CodingOsdMeterSnapshotWorkflowRequest(
                HasLiveDetection: false,
                PlayerTimeMilliseconds: 12500),
            Actions(calls));

        Assert.Equal(CodingOsdMeterSnapshotWorkflowOutcome.NoLiveDetection, result.Outcome);
        Assert.Null(result.Meter);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task ExecuteAsync_captures_snapshot_and_reads_meter_with_player_timestamp()
    {
        var calls = new List<string>();

        var result = await CodingOsdMeterSnapshotWorkflow.ExecuteAsync(
            new CodingOsdMeterSnapshotWorkflowRequest(
                HasLiveDetection: true,
                PlayerTimeMilliseconds: 12500),
            Actions(calls));

        Assert.Equal(CodingOsdMeterSnapshotWorkflowOutcome.Read, result.Outcome);
        Assert.Equal(4.2, result.Meter);
        Assert.Equal(
            [
                "capture",
                "read:3:12.5"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_uses_null_timestamp_when_player_time_is_missing_or_negative()
    {
        var calls = new List<string>();

        var result = await CodingOsdMeterSnapshotWorkflow.ExecuteAsync(
            new CodingOsdMeterSnapshotWorkflowRequest(
                HasLiveDetection: true,
                PlayerTimeMilliseconds: -1),
            Actions(calls));

        Assert.Equal(CodingOsdMeterSnapshotWorkflowOutcome.Read, result.Outcome);
        Assert.Equal(["capture", "read:3:null"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_skips_empty_snapshot_without_reading()
    {
        var calls = new List<string>();

        var result = await CodingOsdMeterSnapshotWorkflow.ExecuteAsync(
            new CodingOsdMeterSnapshotWorkflowRequest(
                HasLiveDetection: true,
                PlayerTimeMilliseconds: null),
            Actions(
                calls,
                captureSnapshotAsync: () =>
                {
                    calls.Add("capture");
                    return Task.FromResult<byte[]?>([]);
                }));

        Assert.Equal(CodingOsdMeterSnapshotWorkflowOutcome.NoSnapshot, result.Outcome);
        Assert.Null(result.Meter);
        Assert.Equal(["capture"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_swallows_snapshot_errors()
    {
        var calls = new List<string>();

        var result = await CodingOsdMeterSnapshotWorkflow.ExecuteAsync(
            new CodingOsdMeterSnapshotWorkflowRequest(
                HasLiveDetection: true,
                PlayerTimeMilliseconds: 12500),
            Actions(
                calls,
                captureSnapshotAsync: () =>
                {
                    calls.Add("capture");
                    throw new InvalidOperationException("snapshot failed");
                }));

        Assert.Equal(CodingOsdMeterSnapshotWorkflowOutcome.ErrorSwallowed, result.Outcome);
        Assert.Null(result.Meter);
        Assert.Equal(["capture"], calls);
    }

    private static CodingOsdMeterSnapshotWorkflowActions Actions(
        List<string> calls,
        Func<Task<byte[]?>>? captureSnapshotAsync = null,
        Func<byte[], double?, Task<double?>>? readOsdMeterAsync = null)
        => new(
            CaptureSnapshotAsync: captureSnapshotAsync ?? (() =>
            {
                calls.Add("capture");
                return Task.FromResult<byte[]?>([1, 2, 3]);
            }),
            ReadOsdMeterAsync: readOsdMeterAsync ?? ((pngBytes, timestampSeconds) =>
            {
                calls.Add(
                    $"read:{pngBytes.Length}:{(timestampSeconds.HasValue ? timestampSeconds.Value.ToString("F1", CultureInfo.InvariantCulture) : "null")}");
                return Task.FromResult<double?>(4.2);
            }));
}
