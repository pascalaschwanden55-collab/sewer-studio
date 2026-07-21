using System.IO;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class FileImportTransactionJournalTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "itj_" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { } }
    }

    private static ImportTransactionMarker SampleMarker() => new(
        TxId: "tx-123",
        StartedUtc: new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc),
        Label: "PDF",
        StagingRoot: @"C:\P\.import-staging\abc",
        PublishedTargets: [new PublishedFileInfo("Bilder/1.jpg", "AABB"), new PublishedFileInfo("x.pdf", "CCDD")],
        RestorePointPath: @"C:\P\__RESTORE_POINTS\projekt\rp.json");

    [Fact]
    public void Begin_dann_TryRead_liefert_denselben_Marker()
    {
        using var dir = new TempDir();
        var journal = new FileImportTransactionJournal();
        var marker = SampleMarker();

        journal.Begin(dir.Path, marker);
        var read = journal.TryRead(dir.Path);

        Assert.NotNull(read);
        Assert.Equal(marker.TxId, read!.TxId);
        Assert.Equal(marker.Label, read.Label);
        Assert.Equal(marker.StagingRoot, read.StagingRoot);
        Assert.Equal(marker.RestorePointPath, read.RestorePointPath);
        Assert.Equal(2, read.PublishedTargets.Count);
        Assert.Equal("Bilder/1.jpg", read.PublishedTargets[0].RelativePath);
        Assert.Equal("AABB", read.PublishedTargets[0].Sha256);
    }

    [Fact]
    public void Clear_entfernt_den_Marker()
    {
        using var dir = new TempDir();
        var journal = new FileImportTransactionJournal();
        journal.Begin(dir.Path, SampleMarker());

        journal.Clear(dir.Path);

        Assert.Null(journal.TryRead(dir.Path));
    }

    [Fact]
    public void TryRead_ohne_Marker_liefert_null()
    {
        using var dir = new TempDir();
        Assert.Null(new FileImportTransactionJournal().TryRead(dir.Path));
    }

    [Fact]
    public void TryRead_bei_kaputtem_Json_liefert_null_statt_Wurf()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, ".import-transaction.json"), "{ kein gueltiges json");

        Assert.Null(new FileImportTransactionJournal().TryRead(dir.Path));
    }

    [Fact]
    public void Clear_ohne_Marker_wirft_nicht()
    {
        using var dir = new TempDir();
        new FileImportTransactionJournal().Clear(dir.Path);   // idempotent
    }
}
