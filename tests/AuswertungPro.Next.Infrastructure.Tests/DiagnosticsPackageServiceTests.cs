using System.IO.Compression;
using AuswertungPro.Next.Infrastructure.Diagnostics;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class DiagnosticsPackageServiceTests
{
    [Fact]
    public async Task CreateAsync_ErstelltBereinigtesZipOhneOriginalZuVeraendern()
    {
        var root = Path.Combine(Path.GetTempPath(), $"diagnostics-package-{Guid.NewGuid():N}");
        var logs = Path.Combine(root, "logs");
        var output = Path.Combine(root, "export", "SewerStudio-Diagnose.zip");
        Directory.CreateDirectory(logs);
        var sourcePath = Path.Combine(logs, "app-20260713.log");
        var source = """
            Start
            SEWER_SIDECAR_TOKEN=sehr-geheim
            Authorization: Bearer abc.def-123
            Projekt: "C:\Kunden\Gemeinde Muster\projekt.json"
            Ende
            """;
        File.WriteAllText(sourcePath, source);
        File.WriteAllText(Path.Combine(logs, "settings.json"), "darf nicht ins Paket");

        try
        {
            var service = new DiagnosticsPackageService(
                logs,
                "4.5-test",
                () => new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero));

            var result = await service.CreateAsync(output);

            Assert.True(result.Success, result.UserMessage);
            Assert.Equal(Path.GetFullPath(output), result.PackagePath);
            Assert.Equal(1, result.IncludedLogFileCount);
            Assert.Equal(source, File.ReadAllText(sourcePath));

            using var archive = ZipFile.OpenRead(output);
            Assert.Equal(
                new[] { "logs/app-20260713.log", "system-info.txt" },
                archive.Entries.Select(entry => entry.FullName).Order().ToArray());

            var copiedLog = ReadEntry(archive, "logs/app-20260713.log");
            Assert.DoesNotContain("sehr-geheim", copiedLog, StringComparison.Ordinal);
            Assert.DoesNotContain("abc.def-123", copiedLog, StringComparison.Ordinal);
            Assert.DoesNotContain("Kunden", copiedLog, StringComparison.Ordinal);
            Assert.Contains("<ENTFERNT>", copiedLog, StringComparison.Ordinal);
            Assert.Contains("<PFAD>", copiedLog, StringComparison.Ordinal);

            var systemInfo = ReadEntry(archive, "system-info.txt");
            Assert.Contains("AppVersion: 4.5-test", systemInfo, StringComparison.Ordinal);
            Assert.Contains("Logdateien: 1", systemInfo, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsync_LehntNichtZipZielKontrolliertAb()
    {
        var root = Path.Combine(Path.GetTempPath(), $"diagnostics-package-{Guid.NewGuid():N}");
        try
        {
            var service = new DiagnosticsPackageService(Path.Combine(root, "logs"), "4.5");

            var result = await service.CreateAsync(Path.Combine(root, "paket.txt"));

            Assert.False(result.Success);
            Assert.Null(result.PackagePath);
            Assert.False(File.Exists(Path.Combine(root, "paket.txt")));
            Assert.Contains("konnte nicht erstellt", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsync_UeberspringtGesperrtesLogStattGesamtpaketAbzubrechen()
    {
        var root = Path.Combine(Path.GetTempPath(), $"diagnostics-package-{Guid.NewGuid():N}");
        var logs = Path.Combine(root, "logs");
        var output = Path.Combine(root, "SewerStudio-Diagnose.zip");
        Directory.CreateDirectory(logs);
        var lockedPath = Path.Combine(logs, "app-20260713.log");
        File.WriteAllText(lockedPath, "gerade gesperrt");

        try
        {
            using var fileLock = new FileStream(
                lockedPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            var service = new DiagnosticsPackageService(logs, "4.5-test");

            var result = await service.CreateAsync(output);

            Assert.True(result.Success, result.UserMessage);
            Assert.Equal(0, result.IncludedLogFileCount);
            Assert.Contains("1 nicht lesbar", result.UserMessage, StringComparison.Ordinal);
            using var archive = ZipFile.OpenRead(output);
            Assert.Equal(new[] { "system-info.txt" }, archive.Entries.Select(entry => entry.FullName));
            Assert.Contains("Nicht lesbar: 1", ReadEntry(archive, "system-info.txt"), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        var entry = Assert.Single(archive.Entries, candidate => candidate.FullName == name);
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }
}
