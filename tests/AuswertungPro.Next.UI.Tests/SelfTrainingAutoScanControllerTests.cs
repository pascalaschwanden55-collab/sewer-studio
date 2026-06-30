using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingAutoScanControllerTests
{
    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, false)]
    [InlineData(0, 0, false)]
    public void ShouldScan_scannt_nur_bei_leerer_liste_und_root_foldern(
        int currentCaseCount,
        int rootFolderCount,
        bool expected)
    {
        Assert.Equal(expected, SelfTrainingAutoScanController.ShouldScan(currentCaseCount, rootFolderCount));
    }

    [Fact]
    public async Task ScanAsync_ueberspringt_fehlende_ordner_und_liefert_cases_in_reihenfolge()
    {
        var scannedFolders = new List<string>();

        var cases = await SelfTrainingAutoScanController.ScanAsync(
            new[] { "missing", "a", "b" },
            folder => folder != "missing",
            folder =>
            {
                scannedFolders.Add(folder);
                return Task.FromResult<IReadOnlyList<TrainingCase>>(new[] { Case(folder) });
            });

        Assert.Equal(new[] { "a", "b" }, scannedFolders);
        Assert.Equal(new[] { "a", "b" }, cases.Select(c => c.CaseId));
    }

    [Fact]
    public async Task RunAsync_scannt_setzt_status_und_haengt_cases_an()
    {
        var status = "";
        var added = new List<TrainingCase>();

        await SelfTrainingAutoScanController.RunAsync(
            currentCaseCount: 0,
            rootFolderCount: 1,
            new[] { "a" },
            _ => true,
            folder => Task.FromResult<IReadOnlyList<TrainingCase>>(new[] { Case(folder) }),
            value => status = value,
            added.Add);

        Assert.Equal(SelfTrainingAutoScanController.StatusText, status);
        Assert.Equal(new[] { "a" }, added.Select(c => c.CaseId));
    }

    [Fact]
    public async Task RunAsync_ueberspringt_scan_wenn_cases_vorhanden_sind()
    {
        var scanCalled = false;
        var added = new List<TrainingCase>();

        await SelfTrainingAutoScanController.RunAsync(
            currentCaseCount: 1,
            rootFolderCount: 1,
            new[] { "a" },
            _ => true,
            _ =>
            {
                scanCalled = true;
                return Task.FromResult<IReadOnlyList<TrainingCase>>(new[] { Case("unexpected") });
            },
            _ => { },
            added.Add);

        Assert.False(scanCalled);
        Assert.Empty(added);
    }

    private static TrainingCase Case(string caseId)
        => new()
        {
            CaseId = caseId,
            ProtocolPath = $@"C:\p\{caseId}.pdf"
        };
}
