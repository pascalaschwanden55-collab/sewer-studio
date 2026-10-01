using SelfTrainingHarness;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SelfTrainingHarnessStoreSnapshotTests
{
    [Fact]
    public void TryRestore_WhenStoreChangedAfterHarness_FailsAndKeepsExternalState()
    {
        WithStore((path, original) =>
        {
            var snapshot = GuardedStoreSnapshot.Create(path);
            File.WriteAllText(path, "harness");
            snapshot.MarkHarnessWritesComplete();
            File.WriteAllText(path, "extern");

            var restored = snapshot.TryRestore(() => false, out var reason);

            Assert.False(restored);
            Assert.Contains("parallel veraendert", reason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("extern", File.ReadAllText(path));
            Assert.True(File.Exists(snapshot.BackupPath));
            Assert.Equal(original, File.ReadAllText(snapshot.BackupPath!));
        });
    }

    [Fact]
    public void TryRestore_WhenSewerStudioRuns_FailsAndKeepsCurrentState()
    {
        WithStore((path, _) =>
        {
            var snapshot = GuardedStoreSnapshot.Create(path);
            File.WriteAllText(path, "harness");
            snapshot.MarkHarnessWritesComplete();

            var restored = snapshot.TryRestore(() => true, out var reason);

            Assert.False(restored);
            Assert.Contains("SewerStudio laeuft", reason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("harness", File.ReadAllText(path));
            Assert.True(File.Exists(snapshot.BackupPath));
        });
    }

    [Fact]
    public void TryRestore_WhenStateIsUnchanged_RestoresOriginalAndDeletesBackup()
    {
        WithStore((path, original) =>
        {
            var snapshot = GuardedStoreSnapshot.Create(path);
            File.WriteAllText(path, "harness");
            snapshot.MarkHarnessWritesComplete();

            var restored = snapshot.TryRestore(() => false, out var reason);

            Assert.True(restored, reason);
            Assert.Equal(original, File.ReadAllText(path));
            Assert.False(File.Exists(snapshot.BackupPath));
        });
    }

    [Fact]
    public void TryRestore_WhenStoreDidNotExist_RemovesOnlyUnchangedHarnessFile()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "sewer-self-training-harness-new-store-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "training_samples.json");

        try
        {
            var snapshot = GuardedStoreSnapshot.Create(path);
            File.WriteAllText(path, "harness");
            snapshot.MarkHarnessWritesComplete();

            var restored = snapshot.TryRestore(() => false, out var reason);

            Assert.True(restored, reason);
            Assert.False(File.Exists(path));
            Assert.Null(snapshot.BackupPath);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    private static void WithStore(Action<string, string> body)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "sewer-self-training-harness-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "training_samples.json");
        const string original = "original";
        File.WriteAllText(path, original);

        try
        {
            body(path, original);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }
}
