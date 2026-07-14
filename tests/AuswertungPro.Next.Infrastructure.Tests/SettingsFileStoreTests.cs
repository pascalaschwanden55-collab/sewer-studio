using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Settings;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SettingsFileStoreTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "SettingsFileStoreTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Persist_schreibt_atomar_und_ruft_den_Rettungspunkt_auf()
    {
        var settingsPath = Path.Combine(_tempDirectory, "settings.json");
        string? restoreSource = null;
        var store = new SettingsFileStore(path => restoreSource = path);

        store.Persist(
            "{\"neu\":true}",
            settingsPath,
            _tempDirectory,
            enableRestorePoints: true);

        Assert.Equal(settingsPath, restoreSource);
        Assert.Equal("{\"neu\":true}", File.ReadAllText(settingsPath));
        Assert.Empty(Directory.GetFiles(_tempDirectory, "*.tmp"));
    }

    [Fact]
    public void Persist_ueberspringt_den_Rettungspunkt_wenn_er_deaktiviert_ist()
    {
        var restoreCalls = 0;
        var store = new SettingsFileStore(_ => restoreCalls++);

        store.Persist(
            "{}",
            Path.Combine(_tempDirectory, "settings.json"),
            _tempDirectory,
            enableRestorePoints: false);

        Assert.Equal(0, restoreCalls);
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
