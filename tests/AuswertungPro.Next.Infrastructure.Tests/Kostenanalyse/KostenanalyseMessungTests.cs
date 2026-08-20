using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenanalyseMessungTests
{
    private static Kostenfall Fall(string name, params MassnahmePosition[] positionen) => new()
    {
        Haltung = name,
        Merkmale = new KostenfallMerkmale
        {
            DnMm = 300,
            LaengeM = 40,
            Schaeden = [new SchadensMerkmal("BAF", 1, false)]
        },
        Positionen = positionen.Length > 0
            ? [.. positionen]
            : [new MassnahmePosition("SCHLAUCHLINER_GFK", 40m, "m")]
    };

    [Fact]
    public void Jeder_Fall_wird_ohne_sich_selbst_vorhergesagt()
    {
        // 5 gleiche Faelle: Jeder wird aus den 4 anderen exakt getroffen.
        var faelle = Enumerable.Range(0, 5).Select(i => Fall($"H{i}")).ToList();

        var ergebnis = KostenanalyseMessung.Messe(faelle);

        Assert.Equal(5, ergebnis.Gesamt);
        Assert.Equal(5, ergebnis.MitVorschlag);
        Assert.Equal(0, ergebnis.Enthalten);
        Assert.Equal(5, ergebnis.PositionenRichtig);
        Assert.Equal(0, ergebnis.PositionenFehlend);
        Assert.Equal(0, ergebnis.PositionenZuviel);
        Assert.Equal(1.0, ergebnis.Abdeckung);
    }

    [Fact]
    public void Zu_kleine_Bestaende_ergeben_lauter_Enthaltungen()
    {
        var ergebnis = KostenanalyseMessung.Messe([Fall("H1"), Fall("H2"), Fall("H3")]);

        // Jeder Fall sieht nur 2 andere -> unter MindestNachbarn.
        Assert.Equal(3, ergebnis.Gesamt);
        Assert.Equal(0, ergebnis.MitVorschlag);
        Assert.Equal(3, ergebnis.Enthalten);
        Assert.Equal(0.0, ergebnis.Abdeckung);
    }

    [Fact]
    public void Eine_vergessene_Position_wird_gezaehlt()
    {
        // 4 Faelle nur mit Liner, der fuenfte hat zusaetzlich Manschetten.
        var faelle = new List<Kostenfall>
        {
            Fall("H1"), Fall("H2"), Fall("H3"), Fall("H4"),
            Fall("H5",
                new MassnahmePosition("SCHLAUCHLINER_GFK", 40m, "m"),
                new MassnahmePosition("MANSCHETTE_EDELSTAHL", 2m, "Stk"))
        };

        var ergebnis = KostenanalyseMessung.Messe(faelle);

        // Fuer H5 fehlt die Manschette im Vorschlag.
        Assert.True(ergebnis.PositionenFehlend >= 1);
    }

    [Fact]
    public void Ein_leerer_Bestand_ergibt_ein_leeres_Ergebnis()
    {
        var ergebnis = KostenanalyseMessung.Messe([]);

        Assert.Equal(0, ergebnis.Gesamt);
        Assert.Equal(0.0, ergebnis.Abdeckung);
    }

    [Fact]
    public void Nur_unbeeinflusste_Faelle_werden_gemessen()
    {
        var faelle = Enumerable.Range(0, 5)
            .Select(i => Fall($"H{i}") with
            {
                Herkunft = i == 0 ? KostenfallHerkunft.VorschlagGesehen : KostenfallHerkunft.Unbeeinflusst
            })
            .ToList();

        var ergebnis = KostenanalyseMessung.Messe(faelle);

        // Der beeinflusste Fall bleibt Lernmaterial, wird aber nicht bewertet.
        Assert.Equal(4, ergebnis.Gesamt);
    }
}
