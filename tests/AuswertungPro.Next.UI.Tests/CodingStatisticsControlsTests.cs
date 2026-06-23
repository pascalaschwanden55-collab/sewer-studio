using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStatisticsControlsTests
{
    [Fact]
    public void Apply_writes_summary_to_side_panel_controls()
    {
        RunOnStaThread(() =>
        {
            var total = new Run();
            var open = new Run();
            var autoAccepted = new TextBlock();
            var pending = new TextBlock();
            var reviewRequired = new TextBlock();
            var averageConfidence = new TextBlock();
            var controls = new CodingStatisticsControls(
                total,
                open,
                autoAccepted,
                pending,
                reviewRequired,
                averageConfidence);

            controls.Apply(new CodingStatisticsSummary(
                Total: 12,
                Open: 3,
                AutoAccepted: 7,
                Pending: 2,
                ReviewRequired: 1,
                AverageConfidenceText: "82%"));

            Assert.Equal("12", total.Text);
            Assert.Equal("3", open.Text);
            Assert.Equal("7", autoAccepted.Text);
            Assert.Equal("2", pending.Text);
            Assert.Equal("1", reviewRequired.Text);
            Assert.Equal("82%", averageConfidence.Text);
        });
    }

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
