using System.Windows.Controls;
using System.Windows.Documents;
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
            var controls = new CodingSidePanelControllerControls(
                CodingEvents: new ListBox(),
                CodingDefectCount: new Run(),
                CodingOpenCount: new Run(),
                CodingStatAiCriteriaMet: new TextBlock(),
                CodingStatHumanAccepted: new TextBlock(),
                CodingStatHumanCorrected: new TextBlock(),
                CodingStatRejected: new TextBlock(),
                CodingStatOpen: new TextBlock(),
                CodingStatAvgAiConfidence: new TextBlock(),
                InlineDetailCode: new TextBlock(),
                InlineDetailDescription: new TextBlock(),
                InlineDetailDistance: new TextBlock(),
                InlineDetailConfidence: new TextBlock(),
                InlineDetailStatus: new TextBlock(),
                InlineEvidencePreview: new Image(),
                InlineEvidencePreviewStatus: new TextBlock(),
                InlineAccept: new Button(),
                InlineReject: new Button(),
                DefectDetailInline: new Border(),
                DefectDetailColumn: new ColumnDefinition());
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

            set.Initialize(controls, actions);

            Assert.True(set.IsInitialized);
            Assert.NotNull(set.EventsList);
            Assert.NotNull(set.Statistics);
            Assert.NotNull(set.InlineDefectDetail);
            Assert.NotNull(set.EventCreationPostActions);
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
