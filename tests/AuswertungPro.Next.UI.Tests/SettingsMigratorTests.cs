using System;
using System.IO;
using AuswertungPro.Next.UI;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer SettingsMigrator.
/// </summary>
public sealed class SettingsMigratorTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsMigratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SettingsMigratorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void MigrateLegacyIfNeeded_KopierteInhalt_WennNurLegacyExistiert()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "legacy_settings.json");
        var targetPath = Path.Combine(_tempDir, "new_settings.json");
        var appDataDir = Path.Combine(_tempDir, "appdata");
        File.WriteAllText(legacyPath, "{\"EnableDiagnostics\":false}");

        // Act
        SettingsMigrator.MigrateLegacyIfNeeded(targetPath, legacyPath, appDataDir);

        // Assert
        Assert.True(File.Exists(targetPath), "Settings-Datei wurde nicht kopiert.");
        Assert.Contains("EnableDiagnostics", File.ReadAllText(targetPath));
    }

    [Fact]
    public void MigrateLegacyIfNeeded_UeberschreibtNicht_WennZieldateiExistiert()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "legacy_settings.json");
        var targetPath = Path.Combine(_tempDir, "new_settings.json");
        var appDataDir = Path.Combine(_tempDir, "appdata");
        File.WriteAllText(legacyPath, "{\"EnableDiagnostics\":false}");
        File.WriteAllText(targetPath, "{\"EnableDiagnostics\":true}");

        // Act
        SettingsMigrator.MigrateLegacyIfNeeded(targetPath, legacyPath, appDataDir);

        // Assert — Zieldatei wurde nicht ueberschrieben
        Assert.Contains("true", File.ReadAllText(targetPath));
    }

    [Fact]
    public void MigrateLegacyIfNeeded_TutNichts_WennKeineQuelleExistiert()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "non_existent_legacy.json");
        var targetPath = Path.Combine(_tempDir, "new_settings.json");
        var appDataDir = Path.Combine(_tempDir, "appdata");

        // Act — kein Fehler erwartet
        SettingsMigrator.MigrateLegacyIfNeeded(targetPath, legacyPath, appDataDir);

        // Assert
        Assert.False(File.Exists(targetPath));
    }

    [Fact]
    public void MigrateLegacyIfNeeded_ErzeugtAppDataDir_BeiErfolgreicherMigration()
    {
        // Arrange
        var legacyPath = Path.Combine(_tempDir, "legacy.json");
        var appDataDir = Path.Combine(_tempDir, "neues_appdata_verzeichnis");
        var targetPath = Path.Combine(appDataDir, "settings.json");
        File.WriteAllText(legacyPath, "{}");
        Assert.False(Directory.Exists(appDataDir), "Vorbedingung: Verzeichnis darf noch nicht existieren.");

        // Act
        SettingsMigrator.MigrateLegacyIfNeeded(targetPath, legacyPath, appDataDir);

        // Assert
        Assert.True(Directory.Exists(appDataDir));
        Assert.True(File.Exists(targetPath));
    }
}
