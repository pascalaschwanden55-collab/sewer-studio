using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Settings;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SettingsRestorePointStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"SettingsRestorePointStoreTests_{Guid.NewGuid():N}");

    [Fact]
    public void InstanceService_CreatesReadableCopyThroughContract()
    {
        var sourcePath = Path.Combine(_tempDirectory, "settings.json");
        var restoreRoot = Path.Combine(_tempDirectory, "restore-points");
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(sourcePath, "{\"language\":\"de\"}");
        ISettingsRestorePointStore store = new SettingsRestorePointStore();

        store.TryCreate(sourcePath, restoreRoot, "settings");

        var snapshot = Assert.Single(
            Directory.GetFiles(Path.Combine(restoreRoot, "settings")));
        Assert.Equal(File.ReadAllText(sourcePath), File.ReadAllText(snapshot));
    }

    [Fact]
    public async Task InstanceService_SerializesParallelRestorePoints()
    {
        var sourcePath = Path.Combine(_tempDirectory, "settings.json");
        var restoreRoot = Path.Combine(_tempDirectory, "restore-points");
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(sourcePath, "{}");
        ISettingsRestorePointStore store = new SettingsRestorePointStore();

        await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() =>
                    store.TryCreate(sourcePath, restoreRoot, "settings"))));

        Assert.Equal(
            8,
            Directory.GetFiles(Path.Combine(restoreRoot, "settings")).Length);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen ist best effort.
        }
    }
}
