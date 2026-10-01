using System;
using System.IO;
using AuswertungPro.Next.UI;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Altbefund M3 (Audit 2026-08-10, bestaetigt 2026-08-14): Konnte die vorhandene
/// settings.json nicht gelesen werden — gesperrt, Zugriff verweigert, Datentraeger
/// kurz weg — lieferte <see cref="AppSettings.Load()"/> stillschweigend Standardwerte.
/// Der naechste beliebige Speichervorgang (23 Stellen im Programm) schrieb diese
/// Standardwerte dann ueber die echte Datei: alle Einstellungen weg, ohne Meldung.
///
/// Der Weg ueber ungueltiges JSON ist davon getrennt und in Ordnung — dort wird die
/// kaputte Datei zuerst in die Quarantaene kopiert und bleibt erhalten.
/// </summary>
[Collection("EnvironmentVars")]
public sealed class AppSettingsUnreadableFileGuardTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _vorherigeAppData;

    public AppSettingsUnreadableFileGuardTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "settings-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _vorherigeAppData = Environment.GetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR");
        Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", _dir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", _vorherigeAppData);
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    [Fact]
    public void Gesperrte_Datei_wird_beim_Speichern_nicht_ueberschrieben()
    {
        File.WriteAllText(SettingsPath, """{"LastProjectPath":"C:\\echtes\\projekt.json"}""");
        var vorher = File.ReadAllBytes(SettingsPath);

        AppSettings geladen;
        // Exklusive Sperre: File.ReadAllText scheitert mit IOException — genau der Fall,
        // der frueher unbemerkt zu Standardwerten fuehrte.
        using (File.Open(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            geladen = AppSettings.Load();
            Assert.True(geladen.PersistenceBlocked);
        }

        geladen.SaveImmediate();

        Assert.Equal(vorher, File.ReadAllBytes(SettingsPath));
    }

    [Fact]
    public void Gesperrte_Datei_meldet_eine_sichtbare_Warnung()
    {
        File.WriteAllText(SettingsPath, """{"LastProjectPath":"C:\\echtes\\projekt.json"}""");

        using var sperre = File.Open(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var geladen = AppSettings.Load();

        Assert.False(string.IsNullOrWhiteSpace(geladen.PersistenceBlockedWarning));
    }

    [Fact]
    public void Erstlauf_ohne_Datei_darf_normal_speichern()
    {
        var geladen = AppSettings.Load();

        Assert.False(geladen.PersistenceBlocked);
        geladen.SaveImmediate();

        Assert.True(File.Exists(SettingsPath));
    }

    [Fact]
    public void Lesbare_Datei_darf_normal_speichern()
    {
        File.WriteAllText(SettingsPath, """{"LastProjectPath":"C:\\alt.json"}""");

        var geladen = AppSettings.Load();
        Assert.False(geladen.PersistenceBlocked);

        geladen.LastProjectPath = @"C:\neu.json";
        geladen.SaveImmediate();

        Assert.Contains("neu.json", File.ReadAllText(SettingsPath), StringComparison.Ordinal);
    }
}
