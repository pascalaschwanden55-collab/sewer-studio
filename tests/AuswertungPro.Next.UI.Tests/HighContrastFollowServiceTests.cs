using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13, Fix-Runde 1 (MINOR 6): prueft die injizierbare
/// Reader-Funktion von HighContrastFollowService, ohne auf ein echtes
/// SystemParameters.HighContrast (Windows-Einstellung) oder das nur intern von WPF ausgeloeste
/// SystemParameters.StaticPropertyChanged angewiesen zu sein. Braucht KEINEN isolierten
/// WPF-Kindprozess: ThemeManager baut seine Ressourcen-URIs vollqualifiziert
/// (pack://application:,,,/&lt;Assembly&gt;;component/...), das loest auch ohne eine laufende
/// System.Windows.Application auf (siehe ThemeManager.ComponentUri-Kommentar).
/// </summary>
public sealed class HighContrastFollowServiceTests
{
    [Fact]
    public void Start_wendet_sofort_den_vom_Reader_gemeldeten_Zustand_an_eingeschaltet()
    {
        var resources = new ResourceDictionary();
        using var service = new HighContrastFollowService(resources, () => true);

        service.Start();

        Assert.True(ThemeManager.IsHighContrastOverlayApplied(resources));
    }

    [Fact]
    public void Start_wendet_sofort_den_vom_Reader_gemeldeten_Zustand_an_ausgeschaltet()
    {
        var resources = new ResourceDictionary();
        using var service = new HighContrastFollowService(resources, () => false);

        service.Start();

        Assert.False(ThemeManager.IsHighContrastOverlayApplied(resources));
    }

    [Fact]
    public void Start_ist_zweimal_aufrufbar_ohne_Ausnahme_und_ohne_die_Ueberlagerung_zu_verdoppeln()
    {
        var resources = new ResourceDictionary();
        using var service = new HighContrastFollowService(resources, () => true);

        service.Start();
        service.Start();

        Assert.True(ThemeManager.IsHighContrastOverlayApplied(resources));
        Assert.Single(resources.MergedDictionaries);
    }

    [Fact]
    public void Dispose_ist_zweimal_aufrufbar_auch_ohne_vorheriges_Start()
    {
        var resources = new ResourceDictionary();
        var service = new HighContrastFollowService(resources, () => true);

        service.Dispose();
        service.Dispose();
    }

    [Fact]
    public void Dispose_nach_Start_wirft_nicht()
    {
        var resources = new ResourceDictionary();
        var service = new HighContrastFollowService(resources, () => true);
        service.Start();

        service.Dispose();
        service.Dispose();
    }

    [Fact]
    public void Konstruktor_verlangt_rootResources()
    {
        Assert.Throws<System.ArgumentNullException>(() => new HighContrastFollowService(null!));
    }

    [Fact]
    public void Ohne_expliziten_Reader_wird_das_echte_SystemParameters_HighContrast_verwendet()
    {
        // Kein Verhalten pruefen, das vom tatsaechlichen Testrechner abhaengt (der laeuft
        // ueblicherweise ohne Hochkontrast) - nur, dass der Standardpfad nicht wirft.
        var resources = new ResourceDictionary();
        using var service = new HighContrastFollowService(resources);

        var ausnahme = Record.Exception(service.Start);

        Assert.Null(ausnahme);
    }
}
