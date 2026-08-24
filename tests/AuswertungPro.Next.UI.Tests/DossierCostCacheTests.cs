using System;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die Kostendateien des Dossier-Cockpits. Sie liegen zusammen bei knapp einem
/// halben Megabyte; sie bei jedem Klick auf eine Liegenschaft neu einzulesen
/// laesst die Oberflaeche stocken.
///
/// Der Seitenzustand wird bei jeder Navigation neu gebaut, deshalb ist der
/// Stand beim Betreten der Seite immer frisch. „Aktualisieren" liest erneut.
/// </summary>
public sealed class DossierCostCacheTests
{
    private static DossierCostSnapshot Stand() => new(new ProjectCostStore(), new ProjectCostStore());

    [Fact]
    public void Zweimal_abfragen_liest_die_Dateien_nur_einmal()
    {
        var gelesen = 0;
        var cache = new DossierCostCache(() => { gelesen++; return Stand(); });

        cache.Get();
        cache.Get();
        cache.Get();

        Assert.Equal(1, gelesen);
    }

    [Fact]
    public void Derselbe_Stand_kommt_zurueck()
    {
        var stand = Stand();
        var cache = new DossierCostCache(() => stand);

        Assert.Same(stand, cache.Get());
        Assert.Same(stand, cache.Get());
    }

    [Fact]
    public void Nach_dem_Verwerfen_wird_neu_gelesen()
    {
        var gelesen = 0;
        var cache = new DossierCostCache(() => { gelesen++; return Stand(); });

        cache.Get();
        cache.Invalidate();
        cache.Get();

        Assert.Equal(2, gelesen);
    }

    [Fact]
    public void Verwerfen_ohne_vorherige_Abfrage_liest_nichts()
    {
        var gelesen = 0;
        var cache = new DossierCostCache(() => { gelesen++; return Stand(); });

        cache.Invalidate();

        Assert.Equal(0, gelesen);
    }

    [Fact]
    public void Ohne_Ladefunktion_gibt_es_keinen_Cache()
        => Assert.Throws<ArgumentNullException>(() => new DossierCostCache(null!));
}
