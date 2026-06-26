using System.Windows.Controls;
using System.Windows.Documents;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSidePanelControllerSetTests
{
    [Fact]
    public void Initialize_stores_side_panel_controllers()
    {
        RunOnStaThread(() =>
        {
            var set = new CodingSidePanelControllerSet();
            var eventsList = new CodingEventsListControls(new ListBox());
            var statistics = new CodingStatisticsControls(
                new Run(),
                new Run(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock());
            var inlineDetail = new CodingInlineDefectDetailControls(
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new TextBlock(),
                new Image(),
                new TextBlock(),
                new Button(),
                new Button(),
                new Border(),
                new ColumnDefinition());
            var postActions = new CodingEventCreationPostActions(
                RefreshEvents: () => { },
                SelectCreatedEvent: _ => { },
                CancelSchema: () => { },
                ClearCurrentOverlay: () => { },
                ClearSelectedCode: () => { },
                RedrawCanvas: () => { },
                ClearSelectedCodeText: () => { },
                DisableCreateEvent: () => { },
                ClearOverlayInfo: () => { });

            set.Initialize(eventsList, statistics, inlineDetail, postActions);

            Assert.True(set.IsInitialized);
            Assert.Same(eventsList, set.EventsList);
            Assert.Same(statistics, set.Statistics);
            Assert.Same(inlineDetail, set.InlineDefectDetail);
            Assert.Same(postActions, set.EventCreationPostActions);
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
