using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProtocolTrainingFileStoreTests
{
    [Fact]
    public void AddSample_PersistsDataAndKeepsBackup()
    {
        WithStore((store, path) =>
        {
            store.AddSample(
                new ProtocolEntry { Code = "BAB", Beschreibung = "Laengsriss", MeterStart = 1.2 },
                "H-001");
            store.AddSample(
                new ProtocolEntry { Code = "BBA", Beschreibung = "Ablagerung", MeterStart = 2.4 },
                "H-001");

            var samples = store.LoadRecent(10);

            Assert.Equal(2, samples.Count);
            Assert.True(File.Exists(path));
            Assert.True(File.Exists(path + ".bak"));
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        });
    }

    [Fact]
    public void AddSample_ParallelCallsDoNotLoseSamples()
    {
        WithStore((store, _) =>
        {
            Parallel.For(0, 24, index =>
                store.AddSample(
                    new ProtocolEntry { Code = $"CODE-{index}", MeterStart = index },
                    "H-001"));

            var samples = store.LoadRecent(100);

            Assert.Equal(24, samples.Count);
            Assert.Equal(24, samples.Select(sample => sample.Code).Distinct().Count());
        });
    }

    [Theory]
    [InlineData("{ keine gueltige JSON-Datei")]
    [InlineData("null")]
    public void AddSample_CorruptExistingFile_FailsClosedAndKeepsOriginal(string corruptJson)
    {
        WithStore((store, path) =>
        {
            File.WriteAllText(path, corruptJson);

            var error = Assert.Throws<InvalidOperationException>(() =>
                store.AddSample(
                    new ProtocolEntry { Code = "BAB", MeterStart = 1.2 },
                    "H-001"));

            Assert.Contains("NICHT veraendert", error.Message, StringComparison.Ordinal);
            Assert.Equal(corruptJson, File.ReadAllText(path));
        });
    }

    [Fact]
    public void AddSample_LockedExistingFile_FailsClosedAndKeepsOriginal()
    {
        WithStore((store, path) =>
        {
            store.AddSample(
                new ProtocolEntry { Code = "BCC", Beschreibung = "Bogen" },
                "H-001");
            var original = File.ReadAllBytes(path);

            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Throws<InvalidOperationException>(() =>
                    store.AddSample(
                        new ProtocolEntry { Code = "BAB", Beschreibung = "Riss" },
                        "H-001"));
            }

            Assert.Equal(original, File.ReadAllBytes(path));
        });
    }

    private static void WithStore(Action<ProtocolTrainingFileStore, string> body)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "sewer-protocol-training-file-store-tests",
            Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "protocol_training.json");
        Directory.CreateDirectory(directory);

        try
        {
            body(new ProtocolTrainingFileStore(path), path);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }
}
