using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionFrameCaptureWorkflowTests
{
    [Fact]
    public async Task CaptureAsync_creates_service_and_delegates_frame_capture()
    {
        var calls = new List<string>();
        var expected = new byte[] { 4, 2 };
        using var cts = new CancellationTokenSource();

        var result = await LiveDetectionFrameCaptureWorkflow.CaptureAsync(
            takeSnapshot: (path, width) =>
            {
                calls.Add($"snapshot:{path}:{width}");
                return true;
            },
            isUnavailable: () =>
            {
                calls.Add("check");
                return false;
            },
            cts.Token,
            new LiveDetectionFrameCaptureWorkflowActions(
                CreateService: takeSnapshot =>
                {
                    calls.Add("service");
                    return new LiveDetectionFrameCaptureService(
                        takeSnapshot,
                        createTempPath: () => @"C:\temp\live.png",
                        delayAsync: (_, token) =>
                        {
                            Assert.Equal(cts.Token, token);
                            calls.Add("delay");
                            return Task.CompletedTask;
                        },
                        fileExists: _ => true,
                        readAllBytesAsync: (path, token) =>
                        {
                            Assert.Equal(@"C:\temp\live.png", path);
                            Assert.Equal(cts.Token, token);
                            calls.Add("read");
                            return Task.FromResult(expected);
                        },
                        deleteFile: path => calls.Add($"delete:{path}"));
                }));

        Assert.Same(expected, result);
        Assert.Equal(
            [
                "service",
                "check",
                @"snapshot:C:\temp\live.png:640",
                "check",
                "delay",
                "read",
                @"delete:C:\temp\live.png"
            ],
            calls);
    }
}
