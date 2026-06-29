using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportScanControllerTests
{
    [Fact]
    public async Task ScanAsync_skips_missing_folders_and_scans_existing_folders()
    {
        var scannedFolders = new List<string>();
        var logs = new List<string>();

        var result = await TrainingBatchImportScanController.ScanAsync(
            ["missing", "root-a"],
            folder => folder == "root-a",
            folder =>
            {
                scannedFolders.Add(folder);
                IReadOnlyList<TrainingCase> cases =
                [
                    new()
                    {
                        CaseId = "101.1-102.1",
                        VideoPath = @"C:\Import\haltung.mp4",
                        ProtocolPath = @"C:\Import\protokoll.pdf"
                    },
                    new() { CaseId = "102.1-103.1" }
                ];
                return Task.FromResult(cases);
            },
            logs.Add);

        Assert.Equal(["root-a"], scannedFolders);
        Assert.Equal(2, result.Found.Count);
        Assert.Single(result.CasesWithProtocol);
        Assert.Equal("101.1-102.1", result.CasesWithProtocol[0].CaseId);
        Assert.Contains("  WARNUNG: Ordner existiert nicht: missing", logs);
        Assert.Contains("  Scanne: root-a", logs);
        Assert.Contains("Gefunden: 2 Ordner, 1 mit Protokoll", logs);
        Assert.Contains("  101.1-102.1: Video, protokoll.pdf", logs);
        Assert.Contains("  102.1-103.1: kein Video, kein Protokoll", logs);
    }

    [Fact]
    public async Task ScanAsync_accepts_empty_root_folder_list()
    {
        var logs = new List<string>();

        var result = await TrainingBatchImportScanController.ScanAsync(
            [],
            _ => throw new InvalidOperationException("Should not check folders."),
            _ => throw new InvalidOperationException("Should not scan folders."),
            logs.Add);

        Assert.Empty(result.Found);
        Assert.Empty(result.CasesWithProtocol);
        Assert.Equal(["Gefunden: 0 Ordner, 0 mit Protokoll"], logs);
    }
}
