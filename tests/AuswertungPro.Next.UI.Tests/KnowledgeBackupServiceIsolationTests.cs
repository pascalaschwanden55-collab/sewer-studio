using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KnowledgeBackupServiceIsolationTests
{
    [Fact]
    public async Task ExportAsync_verwendet_nur_die_uebergebenen_Testpfade()
    {
        using var temp = new TempDirectory();
        var locations = temp.CreateLocations();
        var samplesPath = Path.Combine(locations.KnowledgeRoot, "training_samples.json");
        Directory.CreateDirectory(locations.KnowledgeRoot);
        await File.WriteAllTextAsync(samplesPath, "[{\"sampleId\":\"isoliert\"}]");

        var zipPath = Path.Combine(temp.Path, "wissen.zip");
        var result = await KnowledgeBackupService.ExportAsync(
            zipPath,
            locations,
            flushPendingSettings: () => { },
            flushSqliteWal: _ => { });

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.FileCount);

        using var archive = ZipFile.OpenRead(zipPath);
        var samplesEntry = Assert.Single(
            archive.Entries,
            entry => entry.FullName == "knowledge/training_samples.json");
        using (var reader = new StreamReader(samplesEntry.Open()))
            Assert.Contains("isoliert", await reader.ReadToEndAsync());

        var manifestEntry = Assert.Single(
            archive.Entries,
            entry => entry.FullName == "_manifest.json");
        using var manifest = await JsonDocument.ParseAsync(manifestEntry.Open());
        Assert.Equal(locations.KnowledgeRoot, manifest.RootElement.GetProperty("KnowledgeRoot").GetString());
    }

    [Fact]
    public async Task ImportAsync_schreibt_nur_in_die_uebergebenen_Testpfade()
    {
        using var temp = new TempDirectory();
        var locations = temp.CreateLocations();
        var zipPath = Path.Combine(temp.Path, "wissen.zip");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("knowledge/training_settings.json");
            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("{\"epochs\":7}");
        }

        var result = await KnowledgeBackupService.ImportAsync(
            zipPath,
            locations,
            flushPendingSettings: () => { });

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.FileCount);
        Assert.Equal(
            "{\"epochs\":7}",
            await File.ReadAllTextAsync(Path.Combine(locations.KnowledgeRoot, "training_settings.json")));
    }

    [Fact]
    public async Task ImportAsync_stellt_bei_Abbruch_die_alte_Datei_wieder_her()
    {
        using var temp = new TempDirectory();
        var locations = temp.CreateLocations();
        Directory.CreateDirectory(locations.KnowledgeRoot);
        var settingsPath = Path.Combine(locations.KnowledgeRoot, "training_settings.json");
        await File.WriteAllTextAsync(settingsPath, "alt");

        var zipPath = Path.Combine(temp.Path, "abbruch.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            await WriteEntryAsync(archive, "knowledge/training_settings.json", "neu");
            await WriteEntryAsync(archive, "knowledge/classes.txt", "zweite Datei");
        }

        using var cancellation = new CancellationTokenSource();
        var progress = new SynchronousProgress(message =>
        {
            if (message == "Importiere: training_settings.json")
                cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            KnowledgeBackupService.ImportAsync(
                zipPath,
                locations,
                flushPendingSettings: () => { },
                progress,
                cancellation.Token));

        Assert.Equal("alt", await File.ReadAllTextAsync(settingsPath));
        Assert.False(File.Exists(Path.Combine(locations.KnowledgeRoot, "classes.txt")));
    }

    private static async Task WriteEntryAsync(
        ZipArchive archive,
        string entryName,
        string content)
    {
        var entry = archive.CreateEntry(entryName);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
    }

    private sealed class SynchronousProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-knowledge-backup-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public KnowledgeBackupLocations CreateLocations()
            => new(
                KnowledgeRoot: System.IO.Path.Combine(Path, "knowledge"),
                RoamingAuswertungPro: System.IO.Path.Combine(Path, "roaming-ap"),
                RoamingSewerStudio: System.IO.Path.Combine(Path, "roaming-ss"),
                LocalSewerStudio: System.IO.Path.Combine(Path, "local-ss"),
                TrainingCenterStatePath: System.IO.Path.Combine(Path, "training-center", "training_center.json"),
                TempRoot: System.IO.Path.Combine(Path, "temp"));

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
