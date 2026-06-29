using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunSummaryTests
{
    [Fact]
    public void BuildNoNewStatus_reports_errors_and_skip_counters_in_existing_order()
    {
        var summary = new TrainingBatchImportRunSummary();
        summary.RecordError("kaputt");
        summary.RecordSkip(TrainingCenterBatchSkipKind.EmptyProtocol);
        summary.RecordSkip(TrainingCenterBatchSkipKind.DuplicateOnly);
        summary.RecordSkip(TrainingCenterBatchSkipKind.MissingProtocol);
        summary.RecordSkip(TrainingCenterBatchSkipKind.UnreadableProtocol);

        var status = summary.BuildNoNewStatus(processedCaseCount: 4);

        Assert.Equal(
            "0 neue Samples aus 4 Faellen. 1 Fehler (letzter: kaputt). 1 ohne Eintraege. 1 nur Duplikate. 1 fehlende Protokolle. 1 nicht lesbar.",
            status);
    }

    [Fact]
    public void BuildNoNewStatus_returns_null_when_samples_were_created()
    {
        var summary = new TrainingBatchImportRunSummary();
        summary.AddNewSamples(2);

        var status = summary.BuildNoNewStatus(processedCaseCount: 4);

        Assert.Null(status);
    }

    [Fact]
    public void BuildCompletionStatus_reports_total_new_samples_and_errors()
    {
        var summary = new TrainingBatchImportRunSummary();
        summary.AddNewSamples(5);
        summary.RecordError("kaputt");

        var status = summary.BuildCompletionStatus();

        Assert.Equal(
            "Fertig! 5 Kandidaten gespeichert (Status: Neu). Freigabe ueber Review (Modul I) \u2014 kein Auto-Index. 1 Fehler.",
            status);
    }
}
