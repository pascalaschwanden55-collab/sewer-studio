using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportCaseProgressUiControllerTests
{
    [Fact]
    public void Apply_setzt_progress_status_und_loggt_case_zeilen()
    {
        var trainingCase = new TrainingCase
        {
            CaseId = "101.1-102.1",
            ProtocolPath = @"C:\Import\protokoll.pdf",
            VideoPath = @"C:\Import\haltung.mp4"
        };
        var calls = new List<string>();
        var ui = new TrainingBatchUiSink(
            setBusy: _ => { },
            setLogText: _ => { },
            setProgressValue: value => calls.Add($"progress:{value}"),
            setProgressMax: _ => { },
            setStatusText: value => calls.Add($"status:{value}"),
            log: value => calls.Add($"log:{value}"));

        TrainingBatchImportCaseProgressUiController.Apply(
            zeroBasedIndex: 1,
            totalCount: 4,
            trainingCase,
            ui);

        Assert.Equal(
            new[]
            {
                "progress:2",
                "status:[2/4] 101.1-102.1...",
                "log:--- [2/4] 101.1-102.1 ---",
                @"log:  Protokoll: C:\Import\protokoll.pdf",
                @"log:  Video: C:\Import\haltung.mp4"
            },
            calls);
    }
}
