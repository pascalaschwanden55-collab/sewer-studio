using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportScanWorkflowControllerTests
{
    [Fact]
    public async Task RunAsync_ersetzt_cases_und_liefert_cases_mit_protokoll()
    {
        var existingCases = new List<TrainingCase> { Case("old") };
        var found = new List<TrainingCase> { Case("found-1"), Case("found-2") };
        var withProtocol = new List<TrainingCase> { found[1] };
        var logLines = new List<string>();
        var statuses = new List<string>();

        var result = await TrainingBatchImportScanWorkflowController.RunAsync(
            rootFolderCount: 2,
            () => Task.FromResult(new TrainingBatchImportScanResult(found, withProtocol)),
            existingCases,
            logLines.Add,
            statuses.Add);

        Assert.False(result.ShouldStop);
        Assert.Same(withProtocol, result.CasesWithProtocol);
        Assert.Equal(new[] { "found-1", "found-2" }, existingCases.Select(c => c.CaseId));
        Assert.Equal("Scanne 2 Ordner...", logLines[0]);
        Assert.Contains("Gefunden: 2 Ordner, 1 mit Protokoll", statuses);
    }

    [Fact]
    public async Task RunAsync_stoppt_wenn_keine_cases_mit_protokoll_gefunden_wurden()
    {
        var existingCases = new List<TrainingCase> { Case("old") };
        var found = new List<TrainingCase> { Case("found") };
        var logLines = new List<string>();
        var statuses = new List<string>();

        var result = await TrainingBatchImportScanWorkflowController.RunAsync(
            rootFolderCount: 1,
            () => Task.FromResult(new TrainingBatchImportScanResult(found, Array.Empty<TrainingCase>())),
            existingCases,
            logLines.Add,
            statuses.Add);

        Assert.True(result.ShouldStop);
        Assert.Empty(result.CasesWithProtocol);
        Assert.Equal(new[] { "found" }, existingCases.Select(c => c.CaseId));
        Assert.Contains("STOP: Keine Ordner mit Protokoll-Dateien gefunden.", logLines);
        Assert.Equal("Keine Ordner mit Protokoll-Dateien gefunden.", statuses[^1]);
    }

    private static TrainingCase Case(string id)
        => new()
        {
            CaseId = id,
            FolderPath = id
        };
}
