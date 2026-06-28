using System;
using System.IO;
using AuswertungPro.Next.UI;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer SettingsStore.
/// </summary>
public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SettingsStoreTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Persist_SchreibtInhalt_WennDateiNochNichtExistiert()
    {
        // Arrange
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        const string json = "{\"EnableDiagnostics\":true}";

        // Act
        SettingsStore.Persist(json, settingsPath, _tempDir, enableRestorePoints: false);

        // Assert
        Assert.True(File.Exists(settingsPath));
        Assert.Equal(json, File.ReadAllText(settingsPath));
    }

    [Fact]
    public void Persist_UeberschreibtVorhandeneSettings_Atomar()
    {
        // Arrange
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsPath, "{\"alt\":true}");
        const string neuerInhalt = "{\"neu\":true}";

        // Act
        SettingsStore.Persist(neuerInhalt, settingsPath, _tempDir, enableRestorePoints: false);

        // Assert
        Assert.Equal(neuerInhalt, File.ReadAllText(settingsPath));
    }

    [Fact]
    public void Persist_HinterlaessstKeineTempDatei()
    {
        // Arrange
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        const string json = "{}";

        // Act
        SettingsStore.Persist(json, settingsPath, _tempDir, enableRestorePoints: false);

        // Assert — keine .tmp-Datei darf zurueckbleiben
        var tmpFiles = Directory.GetFiles(_tempDir, "*.tmp");
        Assert.Empty(tmpFiles);
    }

    [Fact]
    public void Persist_ErzeugtBackupDatei_WennSettingsVorherExistierte()
    {
        // Arrange
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsPath, "{\"alt\":true}");
        const string neuerInhalt = "{\"neu\":true}";

        // Act
        SettingsStore.Persist(neuerInhalt, settingsPath, _tempDir, enableRestorePoints: false);

        // Assert — .bak-Datei wird angelegt (entweder via File.Replace oder Copy+Move)
        Assert.True(File.Exists(settingsPath + ".bak"), "Backup-Datei fehlt.");
    }

    [Fact]
    public void Persist_ErzeugtAppDataDir_FallsNichtVorhanden()
    {
        // Arrange
        var subDir = Path.Combine(_tempDir, "neues_verzeichnis");
        var settingsPath = Path.Combine(subDir, "settings.json");
        Assert.False(Directory.Exists(subDir));

        // Act
        SettingsStore.Persist("{}", settingsPath, subDir, enableRestorePoints: false);

        // Assert
        Assert.True(Directory.Exists(subDir));
        Assert.True(File.Exists(settingsPath));
    }

    [Fact]
    public void Persist_MitRestorePointsTrue_WirftKeineAusnahme_WennQuelleDateiNichtExistiert()
    {
        // Arrange — enableRestorePoints=true, aber die Settings-Datei existiert noch nicht
        var settingsPath = Path.Combine(_tempDir, "settings.json");

        // Act — RestorePointService.TryCreate ignoriert fehlende Quelldatei
        var ex = Record.Exception(() =>
            SettingsStore.Persist("{}", settingsPath, _tempDir, enableRestorePoints: true));

        // Assert
        Assert.Null(ex);
        Assert.True(File.Exists(settingsPath));
    }
}
