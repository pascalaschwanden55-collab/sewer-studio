using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Nova-Etappe 1: feste Spaltenansichten der Haltungsliste.</summary>
public sealed class DataPageColumnViewCatalogTests
{
    /// <summary>
    /// Nova-Etappe 2b, Task 3: Kompakt ist exakt der Prototyp v2 — sechs Feldspalten und die
    /// vier virtuellen Statusspalten in dieser Reihenfolge.
    /// </summary>
    [Fact]
    public void Kompakt_zeigt_die_zehn_Spalten_des_Prototyps()
    {
        var v = DataPageColumnViewCatalog.Resolve("kompakt");
        Assert.Equal(
            new[]
            {
                FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.PipeMaterial,
                FieldKeys.NominalDiameterMm, FieldKeys.HoldingLengthMeters, FieldKeys.ConditionClass,
                NovaStatusSpalten.Ki, NovaStatusSpalten.Pruefung, NovaStatusSpalten.Video, NovaStatusSpalten.Protokoll
            },
            v.Felder);
    }

    /// <summary>Die Bewertung zeigt zusaetzlich den Pruefstand; KI, Video und Protokoll nicht.</summary>
    [Fact]
    public void Bewertung_zeigt_zusaetzlich_die_Pruefung()
    {
        var v = DataPageColumnViewCatalog.Resolve("bewertung");
        Assert.True(v.Enthaelt(NovaStatusSpalten.Pruefung));
        Assert.False(v.Enthaelt(NovaStatusSpalten.Ki));
        Assert.False(v.Enthaelt(NovaStatusSpalten.Video));
        Assert.False(v.Enthaelt(NovaStatusSpalten.Protokoll));
    }

    /// <summary>Der rohe Videopfad ist in Kompakt durch die Spalte "Video" ersetzt.</summary>
    [Fact]
    public void Kompakt_fuehrt_den_rohen_Videopfad_nicht_mehr()
    {
        Assert.False(DataPageColumnViewCatalog.Resolve("kompakt").Enthaelt(FieldKeys.Link));
        Assert.True(DataPageColumnViewCatalog.Resolve("alle").Enthaelt(FieldKeys.Link));
    }

    /// <summary>
    /// Fix-Runde 1 (F5): In der alten Haltungsansicht gibt es die Statusspalten nicht. Dort
    /// fuehrt "Kompakt" weiter den rohen Videopfad, sonst waere die Videoangabe ersatzlos weg.
    /// </summary>
    [Fact]
    public void Die_Altansicht_kennt_keine_virtuellen_Spalten_und_behaelt_den_Videopfad()
    {
        var altKompakt = DataPageColumnViewCatalog.Resolve("kompakt", nova: false);
        Assert.True(altKompakt.Enthaelt(FieldKeys.Link));
        Assert.Equal(7, altKompakt.Felder!.Count);

        foreach (var view in DataPageColumnViewCatalog.AltansichtViews)
            foreach (var feld in view.Felder ?? Array.Empty<string>())
                Assert.False(NovaStatusSpalten.IstVirtuell(feld), $"{view.Key}: {feld}");

        // Schluessel, Titel und Reihenfolge bleiben in beiden Layouts gleich.
        Assert.Equal(
            DataPageColumnViewCatalog.Views.Select(v => v.Key),
            DataPageColumnViewCatalog.AltansichtViews.Select(v => v.Key));
        Assert.Same(DataPageColumnViewCatalog.Views, DataPageColumnViewCatalog.ViewsFuer(nova: true));
        Assert.Same(DataPageColumnViewCatalog.AltansichtViews, DataPageColumnViewCatalog.ViewsFuer(nova: false));
    }

    /// <summary>Alle Felder der Altansicht sind echte Felder des Katalogs.</summary>
    [Fact]
    public void Jedes_Feld_der_Altansicht_existiert_im_Feldkatalog()
    {
        var bekannt = new HashSet<string>(FieldCatalog.ColumnOrder, StringComparer.Ordinal);
        foreach (var v in DataPageColumnViewCatalog.AltansichtViews)
            foreach (var f in v.Felder ?? Array.Empty<string>())
                Assert.True(bekannt.Contains(f), $"{v.Key}: {f} fehlt im FieldCatalog");
    }

    [Fact]
    public void Alle_hat_keine_Feldliste_und_ist_der_Rueckfall()
    {
        Assert.Null(DataPageColumnViewCatalog.Resolve("alle").Felder);
        Assert.Equal("alle", DataPageColumnViewCatalog.Resolve(null).Key);
        Assert.Equal("alle", DataPageColumnViewCatalog.Resolve("gibt-es-nicht").Key);
        Assert.Equal("kompakt", DataPageColumnViewCatalog.Resolve("KOMPAKT").Key);
    }

    [Fact]
    public void Jedes_Feld_einer_Ansicht_existiert_im_Feldkatalog()
    {
        var bekannt = new HashSet<string>(FieldCatalog.ColumnOrder, StringComparer.Ordinal);
        foreach (var v in DataPageColumnViewCatalog.Views)
            foreach (var f in v.Felder ?? Array.Empty<string>())
            {
                // Nova-Etappe 2b: Die vier Statusspalten sind bewusst KEINE Felder — sie stehen
                // nicht im FieldCatalog, gehen in keinen Export und werden nie gespeichert.
                if (NovaStatusSpalten.IstVirtuell(f))
                    continue;
                Assert.True(bekannt.Contains(f), $"{v.Key}: {f} fehlt im FieldCatalog");
            }
    }

    [Fact]
    public void Der_Haltungsname_steht_in_jeder_Ansicht_vorn_und_Schluessel_sind_eindeutig()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in DataPageColumnViewCatalog.Views)
        {
            Assert.True(keys.Add(v.Key), $"Schluessel {v.Key} doppelt");
            if (v.Felder is not null)
                Assert.Equal(FieldKeys.HoldingName, v.Felder[0]);
        }
        Assert.Equal(6, DataPageColumnViewCatalog.Views.Count);
    }

    [Fact]
    public void Jede_Ansicht_nennt_ihre_Spaltenzahl()
    {
        Assert.Equal(10, DataPageColumnViewCatalog.Resolve("kompakt").Anzahl(40));
        Assert.Equal(40, DataPageColumnViewCatalog.Resolve("alle").Anzahl(40));
    }

    /// <summary>
    /// Nova-Etappe 2b, Task 1: "NR" ist nur in "Alle Spalten" wahr. Keine der benannten
    /// Ansichten fuehrt "NR" in ihrer Feldliste, und <see cref="DataPageColumnView.Enthaelt"/>
    /// bleibt bei gesetzter Feldliste strikt (kein Rueckfall auf "alle Felder gelten").
    /// </summary>
    [Fact]
    public void Kompakt_zeigt_keine_NR_Spalte()
    {
        foreach (var v in DataPageColumnViewCatalog.Views.Where(v => v.Key != "alle"))
            Assert.False(v.Enthaelt("NR"), $"{v.Key} zeigt NR");

        Assert.True(DataPageColumnViewCatalog.Resolve("alle").Enthaelt("NR"));
    }
}
