using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 15: die reine Regel hinter der gemeinsamen Quelle fuer
/// das Berichts-Logo. Kein echter Dateizugriff — <c>fileExists</c> wird gestellt.
/// </summary>
public sealed class BerichtsLogoResolverTests
{
    private const string AppBase = @"C:\App";
    private static readonly string DefaultPath = System.IO.Path.Combine(AppBase, "Assets", "Brand", "abwasser-uri-logo.png");

    [Fact]
    public void Ohne_Einstellung_liefert_das_vorhandene_Standardlogo()
    {
        var result = BerichtsLogoResolver.Resolve(
            configuredPath: null,
            appBaseDirectory: AppBase,
            fileExists: path => path == DefaultPath);

        Assert.Equal(DefaultPath, result);
    }

    [Fact]
    public void Eine_gesetzte_und_vorhandene_Einstellung_ueberschreibt_den_Standard()
    {
        const string eigenes = @"D:\Bilder\eigenes.png";

        var result = BerichtsLogoResolver.Resolve(
            configuredPath: eigenes,
            appBaseDirectory: AppBase,
            fileExists: path => path == eigenes || path == DefaultPath);

        Assert.Equal(eigenes, result);
    }

    [Fact]
    public void Eine_gesetzte_aber_fehlende_Datei_faellt_auf_das_Standardlogo_zurueck()
    {
        const string geloescht = @"D:\Bilder\nicht-mehr-da.png";

        var result = BerichtsLogoResolver.Resolve(
            configuredPath: geloescht,
            appBaseDirectory: AppBase,
            fileExists: path => path == DefaultPath);

        Assert.Equal(DefaultPath, result);
    }

    [Fact]
    public void Fehlen_Einstellung_und_Standardlogo_gibt_es_kein_Logo()
    {
        var result = BerichtsLogoResolver.Resolve(
            configuredPath: null,
            appBaseDirectory: AppBase,
            fileExists: _ => false);

        Assert.Null(result);
    }

    [Fact]
    public void Eine_leere_oder_reine_Leerzeichen_Einstellung_gilt_als_nicht_gesetzt()
    {
        var result = BerichtsLogoResolver.Resolve(
            configuredPath: "   ",
            appBaseDirectory: AppBase,
            fileExists: path => path == DefaultPath);

        Assert.Equal(DefaultPath, result);
    }

    [Fact]
    public void DefaultLogoPath_liefert_den_Standardpfad_ohne_Dateipruefung()
    {
        Assert.Equal(DefaultPath, BerichtsLogoResolver.DefaultLogoPath(AppBase));
    }
}
