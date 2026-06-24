using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Controls;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMeterTimelineControlsTests
{
    [Fact]
    public void Apply_writes_meter_text_and_timeline_current_meter()
    {
        RunOnStaThread(() =>
        {
            var text = new TextBlock();
            var timeline = new PipeGraphTimeline { TotalLength = 10 };
            var apply = FindApplyMethod();
            Assert.NotNull(apply);

            apply.Invoke(null, [text, timeline, 3.456]);

            Assert.Equal("3.46m", text.Text);
            Assert.Equal(3.456, timeline.CurrentMeter);
        });
    }

    [Fact]
    public void SetText_writes_initial_meter_without_requiring_timeline()
    {
        RunOnStaThread(() =>
        {
            var text = new TextBlock { Text = "old" };
            var setText = FindSetTextMethod();
            Assert.NotNull(setText);

            setText.Invoke(null, [text, 0.0]);

            Assert.Equal("0.00m", text.Text);
        });
    }

    private static MethodInfo? FindApplyMethod()
        => typeof(CodingCurrentCodeBadgePolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingMeterTimelineControls")
            ?.GetMethod(
                "Apply",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(TextBlock), typeof(PipeGraphTimeline), typeof(double)],
                modifiers: null);

    private static MethodInfo? FindSetTextMethod()
        => typeof(CodingCurrentCodeBadgePolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingMeterTimelineControls")
            ?.GetMethod(
                "SetText",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(TextBlock), typeof(double)],
                modifiers: null);

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
