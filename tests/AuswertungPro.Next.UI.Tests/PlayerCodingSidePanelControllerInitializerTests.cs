using System.Threading;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerCodingSidePanelControllerInitializerTests
{
    [Fact]
    public void Initialize_maps_side_panel_controls_to_controller_set()
    {
        RunOnStaThread(() =>
        {
            var sidePanel = new PlayerCodingSidePanel();
            var controllers = new CodingSidePanelControllerSet();
            var actions = new CodingSidePanelControllerActions(
                RefreshEvents: () => { },
                SelectCreatedEvent: _ => { },
                CancelSchema: () => { },
                ClearCurrentOverlay: () => { },
                ClearSelectedCode: () => { },
                RedrawCanvas: () => { },
                ClearSelectedCodeText: () => { },
                DisableCreateEvent: () => { },
                ClearOverlayInfo: () => { });

            PlayerCodingSidePanelControllerInitializer.Initialize(controllers, sidePanel, actions);

            Assert.True(controllers.IsInitialized);

            var items = new object[] { "sample" };
            controllers.EventsList.SetItemsSource(items);
            controllers.Statistics.Apply(new CodingStatisticsSummary(
                Total: 7,
                Open: 2,
                AiCriteriaMet: 3,
                HumanAccepted: 2,
                HumanCorrected: 1,
                Rejected: 1,
                AverageAiConfidenceText: "82%"));

            Assert.Same(items, sidePanel.LstCodingEvents.ItemsSource);
            Assert.Equal("7", sidePanel.RunCodingDefectCount.Text);
            Assert.Equal("2", sidePanel.RunCodingOpenCount.Text);
            Assert.Equal("3", sidePanel.TxtCodingStatAiCriteriaMet.Text);
            Assert.Equal("2", sidePanel.TxtCodingStatHumanAccepted.Text);
            Assert.Equal("1", sidePanel.TxtCodingStatHumanCorrected.Text);
            Assert.Equal("1", sidePanel.TxtCodingStatRejected.Text);
            Assert.Equal("2", sidePanel.TxtCodingStatOpen.Text);
            Assert.Equal("82%", sidePanel.TxtCodingStatAvgAiConfidence.Text);
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
