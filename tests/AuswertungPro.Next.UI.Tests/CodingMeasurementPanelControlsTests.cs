using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMeasurementPanelControlsTests
{
    [Fact]
    public void Apply_sets_all_measurement_texts_and_shows_panel()
    {
        RunOnStaThread(() =>
        {
            var q1 = new TextBlock();
            var q2 = new TextBlock();
            var clock = new TextBlock();
            var arc = new TextBlock();
            var measurement = new TextBlock();
            var panel = new Border { Visibility = Visibility.Collapsed };
            var state = new CodingOverlayMeasurementPanelState(
                IsVisible: true,
                Q1Text: "Q1: 10.0 mm",
                Q2Text: "Q2: 20.0 mm",
                ClockText: "Uhr: 3.0",
                ArcText: "Bogen: 45 deg",
                MeasurementText: "Q1:10.0mm");
            var apply = FindApplyMethod();
            Assert.NotNull(apply);

            apply.Invoke(null, [q1, q2, clock, arc, measurement, panel, state]);

            Assert.Equal("Q1: 10.0 mm", q1.Text);
            Assert.Equal("Q2: 20.0 mm", q2.Text);
            Assert.Equal("Uhr: 3.0", clock.Text);
            Assert.Equal("Bogen: 45 deg", arc.Text);
            Assert.Equal("Q1:10.0mm", measurement.Text);
            Assert.Equal(Visibility.Visible, panel.Visibility);
        });
    }

    [Fact]
    public void Apply_collapses_panel_when_state_is_not_visible()
    {
        RunOnStaThread(() =>
        {
            var q1 = new TextBlock { Text = "alt" };
            var q2 = new TextBlock();
            var clock = new TextBlock();
            var arc = new TextBlock();
            var measurement = new TextBlock();
            var panel = new Border { Visibility = Visibility.Visible };
            var state = new CodingOverlayMeasurementPanelState(
                IsVisible: false,
                Q1Text: "Q1: -",
                Q2Text: "Q2: -",
                ClockText: "Uhr: -",
                ArcText: "Bogen: -",
                MeasurementText: "");
            var apply = FindApplyMethod();
            Assert.NotNull(apply);

            apply.Invoke(null, [q1, q2, clock, arc, measurement, panel, state]);

            Assert.Equal("Q1: -", q1.Text);
            Assert.Equal(Visibility.Collapsed, panel.Visibility);
        });
    }

    private static MethodInfo? FindApplyMethod()
        => typeof(CodingOverlayMeasurementFormatter).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingMeasurementPanelControls")
            ?.GetMethod(
                "Apply",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types:
                [
                    typeof(TextBlock),
                    typeof(TextBlock),
                    typeof(TextBlock),
                    typeof(TextBlock),
                    typeof(TextBlock),
                    typeof(FrameworkElement),
                    typeof(CodingOverlayMeasurementPanelState)
                ],
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
