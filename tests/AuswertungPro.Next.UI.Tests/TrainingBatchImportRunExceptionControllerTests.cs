using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunExceptionControllerTests
{
    [Fact]
    public void RecordCaseFailure_erhoeht_fehlerzaehler_und_loggt_message()
    {
        var summary = new TrainingBatchImportRunSummary();
        var logLines = new List<string>();

        TrainingBatchImportRunExceptionController.RecordCaseFailure(
            new InvalidOperationException("Kaputt"),
            summary,
            logLines.Add);

        Assert.Equal(1, summary.Errors);
        Assert.Single(logLines, "  FEHLER: Kaputt");
        Assert.Contains("1 Fehler (letzter: Kaputt)", summary.BuildNoNewStatus(processedCaseCount: 1));
    }

    [Fact]
    public void ApplyCanceled_loggt_und_setzt_abbruchstatus()
    {
        var logLines = new List<string>();
        var statusText = "";

        TrainingBatchImportRunExceptionController.ApplyCanceled(
            logLines.Add,
            value => statusText = value);

        Assert.Single(logLines, "Batch-Import abgebrochen durch Benutzer.");
        Assert.Equal("Batch-Import abgebrochen.", statusText);
    }

    [Fact]
    public void ApplyFatal_loggt_und_setzt_fehlerstatus()
    {
        var logLines = new List<string>();
        var statusText = "";

        TrainingBatchImportRunExceptionController.ApplyFatal(
            new InvalidOperationException("Kaputt"),
            logLines.Add,
            value => statusText = value);

        Assert.Single(logLines, "FATALER FEHLER: Kaputt");
        Assert.Equal("Fehler beim Batch-Import: Kaputt", statusText);
    }
}
