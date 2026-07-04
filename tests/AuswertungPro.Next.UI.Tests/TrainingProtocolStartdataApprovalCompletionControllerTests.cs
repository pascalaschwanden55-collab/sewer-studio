using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingProtocolStartdataApprovalCompletionControllerTests
{
    [Fact]
    public void Apply_loggt_fehler_und_setzt_status_auf_ui_thread()
    {
        var result = new TrainingProtocolStartdataApprovalResult(
            ApprovedCount: 1,
            ItemCount: 2,
            StatusText: "1/2 Protokoll-Startdaten freigegeben.",
            ErrorLogTexts:
            [
                "Startdaten-Freigabe Fehler (BAB): defekt",
                "Startdaten-Freigabe Fehler (BBA): fehlt"
            ]);
        var logs = new List<string>();
        var uiCalls = 0;
        var statusText = "";

        TrainingProtocolStartdataApprovalCompletionController.Apply(
            result,
            logs.Add,
            action =>
            {
                uiCalls++;
                action();
            },
            value => statusText = value);

        Assert.Equal(
            [
                "Startdaten-Freigabe Fehler (BAB): defekt",
                "Startdaten-Freigabe Fehler (BBA): fehlt"
            ],
            logs);
        Assert.Equal(1, uiCalls);
        Assert.Equal("1/2 Protokoll-Startdaten freigegeben.", statusText);
    }
}
