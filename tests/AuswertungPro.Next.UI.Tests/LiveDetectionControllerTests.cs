using System.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionControllerTests
{
    [Fact]
    public void StartRuntime_stores_runtime_prepares_detection_state_and_runs_ui_actions_in_order()
    {
        Exception? threadError = null;
        var calls = new List<string>();
        var modelName = string.Empty;
        var isDetecting = false;
        var hasCancellation = false;
        var timerRunning = false;

        var thread = new Thread(() =>
        {
            try
            {
                var controller = new LiveDetectionController();
                var runtime = new LiveDetectionRuntime(null!, null!, "models/qwen2.5-vl:7b");

                controller.StartRuntime(
                    runtime,
                    new LiveDetectionControllerStartActions(
                        ShowOverlay: () =>
                        {
                            Assert.True(controller.IsDetecting);
                            calls.Add("overlay");
                        },
                        ApplyActiveStatus: status =>
                        {
                            Assert.Equal("KI aktiv", status.BadgeText);
                            Assert.Equal("Modell: qwen2.5-vl:7b", status.BadgeDetails);
                            calls.Add("status");
                        },
                        ShowWaitingForFrame: () => calls.Add("waiting"),
                        TimerTick: (_, _) => { },
                        RunFirstDetection: () =>
                        {
                            Assert.True(controller.IsDetectionTimerRunning);
                            calls.Add("run");
                        }));

                modelName = controller.ModelName;
                isDetecting = controller.IsDetecting;
                hasCancellation = controller.DetectionCancellation is not null;
                timerRunning = controller.IsDetectionTimerRunning;
                controller.Stop();
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
        Assert.Equal("models/qwen2.5-vl:7b", modelName);
        Assert.True(isDetecting);
        Assert.True(hasCancellation);
        Assert.True(timerRunning);
        Assert.Equal(["overlay", "status", "waiting", "run"], calls);
    }

    [Fact]
    public void Stop_clears_detection_state_and_stops_timer()
    {
        Exception? threadError = null;
        var isDetecting = true;
        var isInFlight = true;
        var hasCancellation = true;
        var timerRunning = true;
        var modelName = "not-cleared";
        var findingCount = 1;

        var thread = new Thread(() =>
        {
            try
            {
                var controller = new LiveDetectionController();
                controller.StartRuntime(
                    new LiveDetectionRuntime(null!, null!, "models/qwen2.5-vl:7b"),
                    new LiveDetectionControllerStartActions(
                        ShowOverlay: () => { },
                        ApplyActiveStatus: _ => { },
                        ShowWaitingForFrame: () => { },
                        TimerTick: (_, _) => { },
                        RunFirstDetection: () => { }));
                controller.BeginDetection();
                controller.ApplyDetectionResult(new LiveDetection(
                    12.5,
                    [new LiveFrameFinding("Riss", 3, "3", 20)],
                    MeterReading: null,
                    Error: null));

                controller.Stop();

                isDetecting = controller.IsDetecting;
                isInFlight = controller.IsDetectionInFlight;
                hasCancellation = controller.DetectionCancellation is not null;
                timerRunning = controller.IsDetectionTimerRunning;
                modelName = controller.ModelName;
                findingCount = controller.CurrentFindings.Count;
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
        Assert.False(isDetecting);
        Assert.False(isInFlight);
        Assert.False(hasCancellation);
        Assert.False(timerRunning);
        Assert.Equal(string.Empty, modelName);
        Assert.Equal(0, findingCount);
    }

    [Fact]
    public void CreateAnalyzeFrameAsync_returns_null_until_runtime_has_service()
    {
        Exception? threadError = null;
        var hasAnalyzerBeforeRuntime = true;
        var hasAnalyzerAfterRuntime = false;

        var thread = new Thread(() =>
        {
            try
            {
                var controller = new LiveDetectionController();
                hasAnalyzerBeforeRuntime = controller.CreateAnalyzeFrameAsync() is not null;

                controller.StartRuntime(
                    new LiveDetectionRuntime(
                        null!,
                        new LiveDetectionService(null!, "models/qwen2.5-vl:7b"),
                        "models/qwen2.5-vl:7b"),
                    new LiveDetectionControllerStartActions(
                        ShowOverlay: () => { },
                        ApplyActiveStatus: _ => { },
                        ShowWaitingForFrame: () => { },
                        TimerTick: (_, _) => { },
                        RunFirstDetection: () => { }));

                hasAnalyzerAfterRuntime = controller.CreateAnalyzeFrameAsync() is not null;
                controller.Stop();
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
        Assert.False(hasAnalyzerBeforeRuntime);
        Assert.True(hasAnalyzerAfterRuntime);
    }
}
