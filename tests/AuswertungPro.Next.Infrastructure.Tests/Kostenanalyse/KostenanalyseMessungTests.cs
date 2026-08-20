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

/// <summary>
/// Die meisten Positionen kommen in fast jeder Haltung vor (Reinigung, TV-Vorkontrolle,
/// Abnahme). Sie zu treffen ist keine Kunst und schoenfaerbt jede Gesamtzahl. Gemessen
/// wird deshalb getrennt — und immer gegen die stumpfe Gegenprobe "immer dasselbe
/// Standardpaket".
/// </summary>
public sealed class KostenanalyseMessungTrennungTests
{
    private static Kostenfall Fall(string name, params string[] itemKeys) => new()
    {
        Haltung = name,
        Merkmale = new KostenfallMerkmale
        {
            DnMm = 300,
            LaengeM = 40,
            Schaeden = [new SchadensMerkmal("BAF", 1, false)]
        },
        Positionen = [.. itemKeys.Select(k => new MassnahmePosition(k, 1m, "Stk"))]
    };

    [Fact]
    public void Routinepositionen_und_entscheidende_werden_getrennt()
    {
        // REINIGUNG in allen 5, LINER nur in 2 -> Routine bzw. entscheidend.
        var faelle = new List<Kostenfall>
        {
            Fall("H1", "REINIGUNG", "LINER"),
            Fall("H2", "REINIGUNG", "LINER"),
            Fall("H3", "REINIGUNG"),
            Fall("H4", "REINIGUNG"),
            Fall("H5", "REINIGUNG")
        };

        var ergebnis = KostenanalyseMessung.Messe(faelle);

        Assert.Contains("REINIGUNG", ergebnis.RoutinePositionen);
        Assert.Contains("LINER", ergebnis.EntscheidendePositionen);
    }

    [Fact]
    public void Die_Gegenprobe_wird_mitgemessen()
    {
        var faelle = Enumerable.Range(0, 5)
            .Select(i => Fall($"H{i}", "REINIGUNG", "LINER"))
            .ToList();

        var ergebnis = KostenanalyseMessung.Messe(faelle);

        // Bei voellig gleichen Faellen sind Modell und Gegenprobe gleich gut.
        Assert.Equal(ergebnis.EntscheidendRichtig, ergebnis.BasisRichtig);
    }

    [Fact]
    public void Bei_einheitlichem_Bestand_gibt_es_keine_entscheidenden_Positionen()
    {
        var faelle = Enumerable.Range(0, 5)
            .Select(i => Fall($"H{i}", "REINIGUNG"))
            .ToList();

        var ergebnis = KostenanalyseMessung.Messe(faelle);

        Assert.Empty(ergebnis.EntscheidendePositionen);
    }
}
