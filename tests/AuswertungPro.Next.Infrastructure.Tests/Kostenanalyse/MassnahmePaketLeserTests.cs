using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class MassnahmePaketLeserTests
{
    private static HoldingCost Kosten(params CostLine[] zeilen) => new()
    {
        Holding = "H-1",
        Measures = [new MeasureCost { MeasureId = "M", MeasureName = "Massnahme", Lines = [.. zeilen] }]
    };

    private static CostLine Z(string key, decimal menge, string einheit, bool gewaehlt = true)
        => new() { ItemKey = key, Text = key, Qty = menge, Unit = einheit, UnitPrice = 100m, Selected = gewaehlt };

    [Fact]
    public void Uebernimmt_ItemKey_Menge_und_Einheit()
    {
        var paket = MassnahmePaketLeser.Lies(Kosten(Z("SCHLAUCHLINER_GFK", 42.5m, "m")));

        var position = Assert.Single(paket);
        Assert.Equal("SCHLAUCHLINER_GFK", position.ItemKey);
        Assert.Equal(42.5m, position.Menge);
        Assert.Equal("m", position.Einheit);
    }

    [Fact]
    public void Nicht_gewaehlte_Zeilen_bleiben_draussen()
    {
        var paket = MassnahmePaketLeser.Lies(Kosten(
            Z("SCHLAUCHLINER_GFK", 40m, "m"),
            Z("SPUELEN", 40m, "m", gewaehlt: false)));

        Assert.Equal("SCHLAUCHLINER_GFK", Assert.Single(paket).ItemKey);
    }

    [Fact]
    public void Gleiche_Position_mehrfach_wird_zusammengezaehlt()
    {
        var paket = MassnahmePaketLeser.Lies(Kosten(
            Z("MANSCHETTE_EDELSTAHL", 2m, "Stk"),
            Z("MANSCHETTE_EDELSTAHL", 3m, "Stk")));

        Assert.Equal(5m, Assert.Single(paket).Menge);
    }

    [Fact]
    public void Positionen_ohne_Menge_zaehlen_nicht()
    {
        Assert.Empty(MassnahmePaketLeser.Lies(Kosten(Z("SCHLAUCHLINER_GFK", 0m, "m"))));
    }

    [Fact]
    public void Ohne_ItemKey_dient_der_Text_als_Schluessel()
    {
        var zeile = new CostLine { ItemKey = "", Text = "Sonderposition", Qty = 1m, Unit = "pl", Selected = true };

        Assert.Equal("Sonderposition", Assert.Single(MassnahmePaketLeser.Lies(Kosten(zeile))).ItemKey);
    }

    [Fact]
    public void Die_Reihenfolge_ist_stabil()
    {
        var paket = MassnahmePaketLeser.Lies(Kosten(
            Z("MANSCHETTE_EDELSTAHL", 1m, "Stk"),
            Z("SCHLAUCHLINER_GFK", 1m, "m")));

        Assert.Equal(["MANSCHETTE_EDELSTAHL", "SCHLAUCHLINER_GFK"], paket.Select(p => p.ItemKey));
    }

    [Fact]
    public void Ohne_Kosten_kommt_ein_leeres_Paket()
    {
        Assert.Empty(MassnahmePaketLeser.Lies(null));
    }
}
