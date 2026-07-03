using System;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Haertung des Settings-Speicherns. Hintergrund: Am 02.07.2026 schlug der
/// Settings-Save mit UnauthorizedAccessException fehl (nur geloggt) — dabei ging
/// die Projekt-Merkliste verloren und Projekte "verschwanden" aus der Uebersicht.
/// Persist muss kurzzeitige Sperren (Virenscanner, zweiter Prozess) und ein
/// schreibgeschuetztes Ziel ueberleben.
/// </summary>
public sealed class SettingsStorePersistHardeningTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsStorePersistHardeningTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SettingsStoreHardening_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.GetFiles(_tempDir))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void Persist_UeberschreibtSchreibgeschuetzteZielDatei()
    {
        // Arrange — settings.json ist ReadOnly (z.B. durch Backup-/Sync-Tool gesetzt)
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsPath, "{\"alt\":true}");
        File.SetAttributes(settingsPath, FileAttributes.ReadOnly);

        // Act
        SettingsStore.Persist("{\"neu\":true}", settingsPath, _tempDir, enableRestorePoints: false);

        // Assert — Inhalt neu, Schreibschutz aufgehoben
        Assert.Equal("{\"neu\":true}", File.ReadAllText(settingsPath));
        Assert.False(File.GetAttributes(settingsPath).HasFlag(FileAttributes.ReadOnly));
    }

    [Fact]
    public async Task Persist_UeberstehtKurzzeitigeExklusivSperre()
    {
        // Arrange — Zieldatei ist kurzzeitig exklusiv gesperrt (Virenscanner/zweiter Prozess)
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsPath, "{\"alt\":true}");

        var sperre = new FileStream(settingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var freigabe = Task.Run(async () =>
        {
            await Task.Delay(250);
            sperre.Dispose();
        });

        // Act — Persist muss die Sperre per Retry ueberleben
        SettingsStore.Persist(
            "{\"neu\":true}", settingsPath, _tempDir,
            enableRestorePoints: false,
            maxAttempts: 10,
            retryDelayMs: 100);

        await freigabe;

        // Assert
        Assert.Equal("{\"neu\":true}", File.ReadAllText(settingsPath));
    }

    [Fact]
    public void Persist_WirftWeiter_WennZielDauerhaftGesperrt()
    {
        // Arrange — dauerhafte Exklusiv-Sperre: Persist darf nicht endlos haengen,
        // sondern muss den Fehler nach den Versuchen weiterreichen (Aufrufer loggt).
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsPath, "{\"alt\":true}");

        using var sperre = new FileStream(settingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.ThrowsAny<Exception>(() => SettingsStore.Persist(
            "{\"neu\":true}", settingsPath, _tempDir,
            enableRestorePoints: false,
            maxAttempts: 2,
            retryDelayMs: 10));
    }
}
