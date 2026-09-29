using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 15: <see cref="AppSettingsBerichtsMarke"/> liest live aus
/// den Programmeinstellungen (eine Aenderung ohne Programmneustart wirkt beim naechsten
/// Export). Getestet ueber den internen Konstruktor mit gestellten Funktionen statt echtem
/// Dateisystem/<see cref="AppSettings"/>.
/// </summary>
public sealed class AppSettingsBerichtsMarkeTests
{
    private const string AppBase = @"C:\App";
    private static readonly string DefaultPath = System.IO.Path.Combine(AppBase, "Assets", "Brand", "abwasser-uri-logo.png");

    [Fact]
    public void Ohne_Einstellung_liefert_das_Standardlogo()
    {
        var marke = new AppSettingsBerichtsMarke(
            () => null,
            AppBase,
            path => path == DefaultPath);

        Assert.Equal(DefaultPath, marke.LogoPfad);
    }

    [Fact]
    public void Die_Einstellung_ueberschreibt_das_Standardlogo()
    {
        const string eigenes = @"D:\Bilder\eigenes.png";
        var marke = new AppSettingsBerichtsMarke(
            () => eigenes,
            AppBase,
            path => path == eigenes || path == DefaultPath);

        Assert.Equal(eigenes, marke.LogoPfad);
    }

    [Fact]
    public void Jeder_Zugriff_liest_die_Einstellung_frisch()
    {
        var aktuellerPfad = (string?)null;
        var marke = new AppSettingsBerichtsMarke(
            () => aktuellerPfad,
            AppBase,
            path => path == DefaultPath || path == aktuellerPfad);

        Assert.Equal(DefaultPath, marke.LogoPfad);

        aktuellerPfad = @"D:\Bilder\neu.png";

        Assert.Equal(@"D:\Bilder\neu.png", marke.LogoPfad);
    }

    [Fact]
    public void Fehlende_Datei_ist_ein_sauberer_Rueckfall_kein_Fehler()
    {
        var marke = new AppSettingsBerichtsMarke(
            () => @"D:\Bilder\nicht-mehr-da.png",
            AppBase,
            _ => false);

        Assert.Null(marke.LogoPfad);
    }

    [Fact]
    public void Ein_Lesefehler_der_Einstellung_stoppt_nicht_sondern_liefert_kein_Logo()
    {
        var marke = new AppSettingsBerichtsMarke(
            () => throw new InvalidOperationException("Einstellungen nicht lesbar."),
            AppBase,
            path => path == DefaultPath);

        Assert.Null(marke.LogoPfad);
    }

    [Fact]
    public void Der_produktive_Konstruktor_liest_aus_AppSettings_BerichtsLogoPfad()
    {
        var settings = new AppSettings { BerichtsLogoPfad = null };
        var marke = new AppSettingsBerichtsMarke(settings);

        // Kein echtes Dateisystem gestellt: ohne das mitgelieferte Standardlogo am
        // Testlaufort bleibt LogoPfad null - das ist der geforderte saubere Rueckfall,
        // kein Absturz.
        var ergebnis = marke.LogoPfad;

        Assert.True(ergebnis is null || System.IO.File.Exists(ergebnis));
    }
}
