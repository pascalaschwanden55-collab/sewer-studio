using System;
using System.IO;
using AuswertungPro.Next.UI;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer SettingsQuarantine.
/// </summary>
public sealed class SettingsQuarantineTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsQuarantineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SettingsQuarantineTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // ---- BuildQuarantinePath (reine Funktion) ----

    [Fact]
    public void BuildQuarantinePath_EnthaeltZeitstempel_UndKorrektesFormat()
    {
        var utc = new DateTime(2026, 6, 21, 14, 30, 55, 123, DateTimeKind.Utc);
        var result = SettingsQuarantine.BuildQuarantinePath(@"C:\app", utc);
        Assert.Equal(@"C:\app\settings.corrupt-20260621-143055123.json", result);
    }

    [Fact]
    public void BuildQuarantinePath_LiegtImAppDataDir()
    {
        var utc = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        var dir = @"C:\some\data\dir";
        var result = SettingsQuarantine.BuildQuarantinePath(dir, utc);
        Assert.StartsWith(dir, result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuarantinePath_EndetMitJsonErweiterung()
    {
        var result = SettingsQuarantine.BuildQuarantinePath(_tempDir, DateTime.UtcNow);
        Assert.EndsWith(".json", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildQuarantinePath_EnthaeltCorruptInDateiname()
    {
        var result = SettingsQuarantine.BuildQuarantinePath(_tempDir, DateTime.UtcNow);
        Assert.Contains("corrupt", Path.GetFileName(result), StringComparison.Ordinal);
    }

    // ---- TryMoveToQuarantine (I/O) ----

    [Fact]
    public void TryMoveToQuarantine_VerschiebtDatei_WennSettingsExistiert()
    {
        // Arrange
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsPath, "{kaputt");
        var logged = new System.Collections.Generic.List<string>();
        void Log(string msg, Exception? _) => logged.Add(msg);

        // Act
        SettingsQuarantine.TryMoveToQuarantine(settingsPath, _tempDir, new Exception("test"), Log);

        // Assert — Original wurde entfernt, Quarantaene-Datei existiert
        Assert.False(File.Exists(settingsPath), "Originaldatei haette verschoben werden sollen.");
        var quarantineFiles = Directory.GetFiles(_tempDir, "settings.corrupt-*.json");
        Assert.Single(quarantineFiles);
        Assert.Contains("{kaputt", File.ReadAllText(quarantineFiles[0]));
    }

    [Fact]
    public void TryMoveToQuarantine_LoggtNachricht_WennDateiNichtExistiert()
    {
        // Arrange
        var settingsPath = Path.Combine(_tempDir, "nicht_vorhanden.json");
        var logged = new System.Collections.Generic.List<string>();
        void Log(string msg, Exception? _) => logged.Add(msg);

        // Act
        SettingsQuarantine.TryMoveToQuarantine(settingsPath, _tempDir, new Exception("test"), Log);

        // Assert
        Assert.Single(logged);
        Assert.Contains("nicht gefunden", logged[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryMoveToQuarantine_WirftKeineFehler_BeiFehlschlagenden_I_O_Operationen()
    {
        // Arrange — Verzeichnis existiert nicht, sodass Directory.CreateDirectory noetig ist
        var appDataDir = Path.Combine(_tempDir, "neues_subdir");
        var settingsPath = Path.Combine(appDataDir, "settings.json");
        Directory.CreateDirectory(appDataDir);
        File.WriteAllText(settingsPath, "{}");

        // Act — darf keine Exception werfen
        var ex = Record.Exception(() =>
            SettingsQuarantine.TryMoveToQuarantine(settingsPath, appDataDir, new Exception("corrupt"), (_, _) => { }));

        Assert.Null(ex);
    }
}
