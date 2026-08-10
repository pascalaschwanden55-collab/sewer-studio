using System;
using System.IO;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// AP-2 (Audit 2026-08-10): Die Kachelgroesse wird ueber die Live-Instanz
/// geschrieben. Wer eine eigene Momentaufnahme laedt und speichert, verwirft
/// jede Aenderung, die seit dem Laden an anderer Stelle geschah.
/// </summary>
[Collection("EnvironmentVars")]
public sealed class PhotoGalleryTileSizeStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string? _vorherigeAppData;

    public PhotoGalleryTileSizeStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "tiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _vorherigeAppData = Environment.GetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR");
        Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", _dir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", _vorherigeAppData);
        ViewCustomizationStore.ResetForTests();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Kachelgroesse_aendern_verwirft_keine_anderen_Einstellungen()
    {
        var live = AppSettings.Load();
        ViewCustomizationStore.Configure(live);
        // Eine andere Einstellung aendert sich NACH dem "Laden" des Reglers.
        live.LastProjectPath = @"D:\Projekte\Zone X\projekt.json";

        ViewCustomizationStore.SetPhotoGalleryTileSize(180);
        live.SaveImmediate();

        var neu = AppSettings.Load();
        Assert.Equal(180, neu.PhotoGalleryTileSize);
        Assert.Equal(@"D:\Projekte\Zone X\projekt.json", neu.LastProjectPath);
    }

    [Fact]
    public void Ohne_konfigurierte_Live_Instanz_wird_nichts_geschrieben()
    {
        ViewCustomizationStore.ResetForTests();

        // Darf still nichts tun — keine eigene Dateikopie, kein Fehler.
        ViewCustomizationStore.SetPhotoGalleryTileSize(200);

        Assert.False(File.Exists(Path.Combine(_dir, "settings.json")));
        Assert.Equal(124, ViewCustomizationStore.GetPhotoGalleryTileSize());
    }
}
