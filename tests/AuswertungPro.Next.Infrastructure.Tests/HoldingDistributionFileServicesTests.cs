using AuswertungPro.Next.Infrastructure.HoldingDistribution;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingDistributionFileServicesTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "SewerStudio_DistributionFileServices_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureUniquePath_AddsNumber_WhenDestinationExists()
    {
        Directory.CreateDirectory(_root);
        var destination = Path.Combine(_root, "bericht.txt");
        File.WriteAllText(destination, "vorhanden");

        var result = DistributionFileTransfer.EnsureUniquePath(destination, overwrite: false);

        Assert.Equal(Path.Combine(_root, "bericht_01.txt"), result);
    }

    [Fact]
    public void MoveOrCopy_CopiesFile_WithoutRemovingSource()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "quelle.txt");
        var destination = Path.Combine(_root, "ziel.txt");
        File.WriteAllText(source, "inhalt");

        DistributionFileTransfer.MoveOrCopy(source, destination, move: false, overwrite: false);

        Assert.True(File.Exists(source));
        Assert.Equal("inhalt", File.ReadAllText(destination));
    }

    [Fact]
    public void MoveOrCopy_MovesFile_AndRemovesSource()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "verschieben.txt");
        var destination = Path.Combine(_root, "verschoben.txt");
        File.WriteAllText(source, "inhalt");

        DistributionFileTransfer.MoveOrCopy(source, destination, move: true, overwrite: false);

        Assert.False(File.Exists(source));
        Assert.Equal("inhalt", File.ReadAllText(destination));
    }

    [Fact]
    public void BuildAmbiguousInfo_ListsEveryCandidate()
    {
        var result = VideoConflictArtifacts.BuildAmbiguousInfo(
            "bericht.pdf",
            "film.mpg",
            new DateTime(2026, 7, 12),
            "100-200",
            ["eins.mpg", "zwei.mpg"]);

        Assert.Contains("VIDEO AMBIGUOUS", result, StringComparison.Ordinal);
        Assert.Contains("Datum: 12.07.2026", result, StringComparison.Ordinal);
        Assert.Contains("- eins.mpg", result, StringComparison.Ordinal);
        Assert.Contains("- zwei.mpg", result, StringComparison.Ordinal);
    }

    [Fact]
    public void CopyCandidates_UsesStableCandidateNames()
    {
        var sourceFolder = Path.Combine(_root, "source");
        var unmatchedFolder = Path.Combine(_root, "unmatched");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(unmatchedFolder);
        var first = Path.Combine(sourceFolder, "a.mpg");
        var second = Path.Combine(sourceFolder, "b.mp4");
        File.WriteAllText(first, "a");
        File.WriteAllText(second, "b");

        VideoConflictArtifacts.CopyCandidates(
            unmatchedFolder,
            "20260712",
            "100-200",
            [first, second]);

        Assert.Equal("a", File.ReadAllText(Path.Combine(unmatchedFolder, "20260712_100-200_CANDIDATE_01.mpg")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(unmatchedFolder, "20260712_100-200_CANDIDATE_02.mp4")));
    }

    [Fact]
    public void Instanzdienst_ermittelt_freien_Namen_und_kopiert_ohne_Quelle_zu_loeschen()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "quelle.txt");
        var occupied = Path.Combine(_root, "ziel.txt");
        File.WriteAllText(source, "neu");
        File.WriteAllText(occupied, "alt");
        var service = new DistributionFileTransferService();

        var destination = service.EnsureUniquePath(occupied, overwrite: false);
        service.MoveOrCopy(source, destination, move: false, overwrite: false);

        Assert.Equal(Path.Combine(_root, "ziel_01.txt"), destination);
        Assert.True(File.Exists(source));
        Assert.Equal("neu", File.ReadAllText(destination));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
