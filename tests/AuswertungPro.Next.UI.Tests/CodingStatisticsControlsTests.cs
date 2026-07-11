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
            var aiCriteriaMet = new TextBlock();
            var humanAccepted = new TextBlock();
            var humanCorrected = new TextBlock();
            var rejected = new TextBlock();
            var openTile = new TextBlock();
            var averageAiConfidence = new TextBlock();
            var controls = new CodingStatisticsControls(
                total,
                open,
                aiCriteriaMet,
                humanAccepted,
                humanCorrected,
                rejected,
                openTile,
                averageAiConfidence);

            controls.Apply(new CodingStatisticsSummary(
                Total: 12,
                Open: 3,
                AiCriteriaMet: 4,
                HumanAccepted: 3,
                HumanCorrected: 2,
                Rejected: 1,
                AverageAiConfidenceText: "82%"));

            Assert.Equal("12", total.Text);
            Assert.Equal("3", open.Text);
            Assert.Equal("4", aiCriteriaMet.Text);
            Assert.Equal("3", humanAccepted.Text);
            Assert.Equal("2", humanCorrected.Text);
            Assert.Equal("1", rejected.Text);
            Assert.Equal("3", openTile.Text);
            Assert.Equal("82%", averageAiConfidence.Text);
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
