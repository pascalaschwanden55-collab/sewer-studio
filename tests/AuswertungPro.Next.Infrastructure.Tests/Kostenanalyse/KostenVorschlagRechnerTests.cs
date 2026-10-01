using AuswertungPro.Next.Application.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenVorschlagRechnerTests
{
    private static KostenfallMerkmale Ziel(double laenge) => new()
    {
        DnMm = 300,
        LaengeM = laenge,
        Schaeden = [new SchadensMerkmal("BAF", 1, false)]
    };

    private static Kostenfall Nachbar(string name, double laenge, params MassnahmePosition[] positionen) => new()
    {
        Haltung = name,
        Merkmale = new KostenfallMerkmale
        {
            DnMm = 300,
            LaengeM = laenge,
            Schaeden = [new SchadensMerkmal("BAF", 1, false)]
        },
        Positionen = [.. positionen]
    };

    private static MassnahmePosition P(string key, decimal menge, string einheit)
        => new(key, menge, einheit);

    [Fact]
    public void Meterpositionen_werden_auf_die_Laenge_umgerechnet()
    {
        // Alle Nachbarn linern auf voller Laenge -> das Ziel auch.
        var nachbarn = new[]
        {
            Nachbar("A", 20, P("SCHLAUCHLINER_GFK", 20m, "m")),
            Nachbar("B", 40, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("C", 60, P("SCHLAUCHLINER_GFK", 60m, "m"))
        };

        var position = Assert.Single(KostenVorschlagRechner.Rechne(Ziel(50), nachbarn));

        Assert.Equal("SCHLAUCHLINER_GFK", position.ItemKey);
        Assert.Equal(50m, position.Menge);
        Assert.Equal("m", position.Einheit);
    }

    [Fact]
    public void Stueckpositionen_nehmen_den_Median()
    {
        var nachbarn = new[]
        {
            Nachbar("A", 40, P("MANSCHETTE_EDELSTAHL", 1m, "Stk")),
            Nachbar("B", 40, P("MANSCHETTE_EDELSTAHL", 2m, "Stk")),
            Nachbar("C", 40, P("MANSCHETTE_EDELSTAHL", 9m, "Stk"))
        };

        // Der Mittelwert waere 4 — der Ausreisser darf nicht durchschlagen.
        Assert.Equal(2m, Assert.Single(KostenVorschlagRechner.Rechne(Ziel(40), nachbarn)).Menge);
    }

    [Fact]
    public void Eine_Position_ohne_Mehrheit_erscheint_nicht()
    {
        var nachbarn = new[]
        {
            Nachbar("A", 40, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("B", 40, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("C", 40, P("SCHLAUCHLINER_GFK", 40m, "m"), P("SONDERPOSITION", 1m, "pl"))
        };

        Assert.Equal("SCHLAUCHLINER_GFK", Assert.Single(KostenVorschlagRechner.Rechne(Ziel(40), nachbarn)).ItemKey);
    }

    [Fact]
    public void Genau_die_Haelfte_reicht_nicht()
    {
        var nachbarn = new[]
        {
            Nachbar("A", 40, P("MANSCHETTE_EDELSTAHL", 1m, "Stk")),
            Nachbar("B", 40, P("SCHLAUCHLINER_GFK", 40m, "m"))
        };

        Assert.Empty(KostenVorschlagRechner.Rechne(Ziel(40), nachbarn));
    }

    [Fact]
    public void Ohne_Nachbarn_kommt_nichts()
    {
        Assert.Empty(KostenVorschlagRechner.Rechne(Ziel(40), []));
    }

    [Fact]
    public void Ein_Nachbar_ohne_Laenge_verdirbt_die_Umrechnung_nicht()
    {
        var nachbarn = new[]
        {
            Nachbar("A", 0, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("B", 40, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("C", 40, P("SCHLAUCHLINER_GFK", 40m, "m"))
        };

        Assert.Equal(40m, Assert.Single(KostenVorschlagRechner.Rechne(Ziel(40), nachbarn)).Menge);
    }

    [Fact]
    public void Stueckmengen_werden_ganzzahlig_gerundet()
    {
        var nachbarn = new[]
        {
            Nachbar("A", 40, P("ANSCHLUSS_EINBINDEN", 2m, "Stk")),
            Nachbar("B", 40, P("ANSCHLUSS_EINBINDEN", 3m, "Stk"))
        };

        // Median von 2 und 3 ist 2.5 -> aufgerundet 3, weil es keine halben Anschluesse gibt.
        Assert.Equal(3m, Assert.Single(KostenVorschlagRechner.Rechne(Ziel(40), nachbarn)).Menge);
    }

    [Fact]
    public void Eine_kurze_Haltung_bekommt_weniger_Liner()
    {
        var nachbarn = new[]
        {
            Nachbar("A", 40, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("B", 40, P("SCHLAUCHLINER_GFK", 40m, "m")),
            Nachbar("C", 40, P("SCHLAUCHLINER_GFK", 40m, "m"))
        };

        Assert.Equal(10m, Assert.Single(KostenVorschlagRechner.Rechne(Ziel(10), nachbarn)).Menge);
    }
}
