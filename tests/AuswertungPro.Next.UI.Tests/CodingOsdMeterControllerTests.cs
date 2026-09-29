using System.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOsdMeterControllerTests
{
    [Theory]
    [InlineData(119, true, CodingMeterSource.RecentOsd, 10.74)]
    [InlineData(115, false, CodingMeterSource.VideoEstimate, 12)]
    public void Resolved_meter_preserves_recent_cache_source_without_claiming_same_frame(
        double cachedSeconds, bool isOsd, CodingMeterSource source, double expectedMeter)
    {
        var controller = new CodingOsdMeterController();
        controller.ApplyState(new CodingOsdMeterState(10.74, cachedSeconds, "OSD"));
        var resolution = controller.ResolveMeterWithSource(new(120, null, 121, 200, 20, 0));
        var frame = CodingAnalyzedFrameEvidence.FromResolution([1, 2, 3], TimeSpan.FromSeconds(120), resolution);
        controller.ApplyState(new CodingOsdMeterState(99, 130, "später"));
        Assert.Equal(expectedMeter, frame.Meter);
        Assert.Equal(isOsd, frame.MeterFromOsd);
        Assert.Equal(source, frame.MeterSource);
        Assert.False(frame.HasSameFrameOsd);
        var entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry();
        frame.WriteAnalysisMetadata(entry);
        Assert.Equal(isOsd ? "recent_osd" : "video_estimate", entry.CodeMeta!.Parameters["ai.frame.meter_source"]);
    }

    [Fact]
    public void ApplyState_and_reset_recent_meter_manage_cached_osd_meter()
    {
        var controller = new CodingOsdMeterController();

        controller.ApplyState(new CodingOsdMeterState(12.345, 4.5, "12.35m (OSD)"));

        Assert.Equal(12.345, controller.LastMeter);
        Assert.Equal(4.5, controller.LastTimestampSeconds);

        controller.ResetRecentMeter();

        Assert.Null(controller.LastMeter);
        Assert.Null(controller.LastTimestampSeconds);
    }

    [Fact]
    public void ResolveMeter_marks_when_value_comes_from_osd()
    {
        var controller = new CodingOsdMeterController();

        var meter = controller.ResolveMeter(new CodingOsdMeterResolveRequest(
            FrameTimestampSeconds: 10,
            SameFrameOsdMeter: 8.25,
            CurrentPlayerSeconds: 10,
            DurationSeconds: 100,
            EndMeter: 50,
            CurrentMeter: 4));

        Assert.Equal(8.25, meter);
        Assert.True(controller.LastResolvedMeterIsOsd);
    }

    [Fact]
    public void TryBeginRead_uses_policy_and_prevents_parallel_osd_reads()
    {
        var controller = new CodingOsdMeterController();
        var context = new CodingOsdTimerContext(
            IsClosing: false,
            HasPlayer: true,
            IsCodingMode: true,
            IsCodingAnalyzing: false,
            HasLiveDetection: true);

        Assert.True(controller.TryBeginRead(context));
        Assert.True(controller.IsReading);
        Assert.False(controller.TryBeginRead(context));

        controller.EndRead();

        Assert.False(controller.IsReading);
    }

    [Fact]
    public void StartTimer_and_stop_timer_own_dispatcher_timer_state()
    {
        Exception? threadError = null;
        var started = false;
        var stopped = false;

        var thread = new Thread(() =>
        {
            try
            {
                var controller = new CodingOsdMeterController();

                controller.StartTimer(
                    () => new CodingOsdTimerContext(
                        IsClosing: false,
                        HasPlayer: true,
                        IsCodingMode: true,
                        IsCodingAnalyzing: false,
                        HasLiveDetection: true),
                    () => Task.CompletedTask);

                started = controller.Timer?.IsEnabled == true;
                controller.StopTimer();
                stopped = controller.Timer is null && !controller.IsReading;
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
        Assert.True(started);
        Assert.True(stopped);
    }

    [Fact]
    public void StartTimer_builds_timer_context_from_state_callbacks()
    {
        Exception? threadError = null;
        var started = false;
        var stopped = false;

        var thread = new Thread(() =>
        {
            try
            {
                var controller = new CodingOsdMeterController();

                controller.StartTimer(
                    () => false,
                    () => true,
                    () => true,
                    () => false,
                    () => true,
                    () => Task.CompletedTask);

                started = controller.Timer?.IsEnabled == true;
                controller.StopTimer();
                stopped = controller.Timer is null && !controller.IsReading;
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
        Assert.True(started);
        Assert.True(stopped);
    }
}
