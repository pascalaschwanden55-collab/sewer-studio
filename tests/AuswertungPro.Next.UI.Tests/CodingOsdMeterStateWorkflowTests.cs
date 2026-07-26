using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOsdMeterStateWorkflowTests
{
    [Fact]
    public void FromReadResult_builds_meter_state_for_accepted_meter()
    {
        var method = FindFromReadResultMethod();
        Assert.NotNull(method);

        var state = method.Invoke(null, [
            CodingOsdMeterReadResult.Accepted(12.345, "12.345", candidate: 12.345, recentMeter: null),
            8.5
        ]);

        AssertState(state, 12.345, 8.5, "12.35m (OSD)");
    }

    [Fact]
    public void FromReadResult_returns_null_without_accepted_meter()
    {
        var method = FindFromReadResultMethod();
        Assert.NotNull(method);

        var rejected = method.Invoke(null, [
            CodingOsdMeterReadResult.Rejected("unlesbar", candidate: null, recentMeter: null),
            8.5
        ]);
        var failed = method.Invoke(null, [
            CodingOsdMeterReadResult.Failed("timeout"),
            8.5
        ]);

        Assert.Null(rejected);
        Assert.Null(failed);
    }

    [Fact]
    public void FromDetectionResult_builds_meter_state_for_plausible_live_detection_meter()
    {
        var method = FindFromDetectionResultMethod();
        Assert.NotNull(method);

        var state = method.Invoke(null, [
            new LiveDetection(4.25, Array.Empty<LiveFrameFinding>(), 7.891, Error: null)
        ]);

        AssertState(state, 7.891, 4.25, "7.89m (OSD)");
    }

    [Fact]
    public void FromDetectionResult_returns_null_for_missing_or_implausible_meter()
    {
        var method = FindFromDetectionResultMethod();
        Assert.NotNull(method);

        var missing = method.Invoke(null, [
            new LiveDetection(4.25, Array.Empty<LiveFrameFinding>(), MeterReading: null, Error: null)
        ]);
        var negative = method.Invoke(null, [
            new LiveDetection(4.25, Array.Empty<LiveFrameFinding>(), -1, Error: null)
        ]);
        var tooLarge = method.Invoke(null, [
            new LiveDetection(4.25, Array.Empty<LiveFrameFinding>(), 501, Error: null)
        ]);

        Assert.Null(missing);
        Assert.Null(negative);
        Assert.Null(tooLarge);
    }

    private static Type? WorkflowType
        => typeof(CodingOsdBadgeDisplayPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingOsdMeterStateWorkflow");

    private static MethodInfo? FindFromReadResultMethod()
        => WorkflowType?.GetMethod(
            "FromReadResult",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(CodingOsdMeterReadResult), typeof(double?)],
            modifiers: null);

    private static MethodInfo? FindFromDetectionResultMethod()
        => WorkflowType?.GetMethod(
            "FromDetectionResult",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(LiveDetection)],
            modifiers: null);

    private static void AssertState(
        object? state,
        double expectedMeter,
        double? expectedTimestampSeconds,
        string expectedBadgeText)
    {
        Assert.NotNull(state);
        var type = state.GetType();
        Assert.Equal(expectedMeter, type.GetProperty("Meter")?.GetValue(state));
        Assert.Equal(expectedTimestampSeconds, type.GetProperty("TimestampSeconds")?.GetValue(state));
        Assert.Equal(expectedBadgeText, type.GetProperty("BadgeText")?.GetValue(state));
    }
}
