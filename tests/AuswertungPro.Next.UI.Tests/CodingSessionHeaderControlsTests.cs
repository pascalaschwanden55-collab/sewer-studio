using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSessionHeaderControlsTests
{
    [Fact]
    public void ApplyCalibration_writes_dn_and_status_text()
    {
        RunOnStaThread(() =>
        {
            var dn = new TextBlock();
            var status = new TextBlock();
            var state = CodingDnCalibrationPolicy.Build(new Dictionary<string, string> { ["DN_mm"] = "400" });
            var apply = FindApplyCalibrationMethod();
            Assert.NotNull(apply);

            apply.Invoke(null, [dn, status, state]);

            Assert.Equal("DN: 400 mm", dn.Text);
            Assert.Equal("Nicht kalibriert", status.Text);
        });
    }

    [Fact]
    public void SetRangeText_formats_end_meter()
    {
        RunOnStaThread(() =>
        {
            var range = new TextBlock();
            var setRange = FindSetRangeTextMethod();
            Assert.NotNull(setRange);

            setRange.Invoke(null, [range, 12.345]);

            Assert.Equal("/ 12.35m", range.Text);
        });
    }

    private static MethodInfo? FindApplyCalibrationMethod()
        => typeof(CodingDnCalibrationPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingSessionHeaderControls")
            ?.GetMethod(
                "ApplyCalibration",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(TextBlock), typeof(TextBlock), typeof(CodingDnCalibrationState)],
                modifiers: null);

    private static MethodInfo? FindSetRangeTextMethod()
        => typeof(CodingDnCalibrationPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingSessionHeaderControls")
            ?.GetMethod(
                "SetRangeText",
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
