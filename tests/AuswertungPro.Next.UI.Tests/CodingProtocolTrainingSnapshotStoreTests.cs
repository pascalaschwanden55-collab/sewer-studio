using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolTrainingSnapshotStoreTests
{
    [Fact]
    public void CopySnapshotToTrainingImages_returns_null_when_snapshot_is_missing()
    {
        var copied = false;
        var store = new CodingProtocolTrainingSnapshotStore(
            () => @"C:\teacher\images",
            _ => false,
            (_, _, _) => copied = true,
            _ => { });

        var result = store.CopySnapshotToTrainingImages(@"C:\temp\snap.png", "abc123");

        Assert.Null(result);
        Assert.False(copied);
    }

    [Fact]
    public void CopySnapshotToTrainingImages_copies_snapshot_to_teacher_image_folder()
    {
        (string Source, string Destination, bool Overwrite)? copy = null;
        var store = new CodingProtocolTrainingSnapshotStore(
            () => @"C:\teacher\images",
            path => path == @"C:\temp\snap.png",
            (source, destination, overwrite) => copy = (source, destination, overwrite),
            _ => { });

        var result = store.CopySnapshotToTrainingImages(@"C:\temp\snap.png", "abc123");

        Assert.Equal(@"C:\teacher\images\mark_abc123.png", result);
        Assert.Equal((@"C:\temp\snap.png", @"C:\teacher\images\mark_abc123.png", true), copy);
    }

    [Fact]
    public void DeleteSnapshot_uses_best_effort_delete_for_existing_snapshot()
    {
        string? deleted = null;
        var store = new CodingProtocolTrainingSnapshotStore(
            () => @"C:\teacher\images",
            path => path == @"C:\temp\snap.png",
            (_, _, _) => { },
            path => deleted = path);

        store.DeleteSnapshot(@"C:\temp\snap.png");

        Assert.Equal(@"C:\temp\snap.png", deleted);
    }
}
