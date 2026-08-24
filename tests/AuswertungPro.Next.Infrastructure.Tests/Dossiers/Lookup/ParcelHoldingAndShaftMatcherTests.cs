using System;

using AuswertungPro.Next.Application.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class ParcelHoldingAndShaftMatcherTests
{
    [Fact]
    public void Findet_die_privaten_Leitungen_ueber_ihren_Namen()
    {
        // Genau diese Leitungen fuehrt der Kanton nicht — ihr Knotenname ist
        // der einzige Hinweis auf die Parzelle.
        var namen = new[] { "439.01-36051", "439.02-439.01", "12345-12346", "77.01-88" };

        var treffer = ParcelHoldingAndShaftMatcher.HoldingsByName(namen, "439");

        Assert.Equal(new[] { "439.01-36051", "439.02-439.01" }, treffer);
    }

    [Fact]
    public void Eine_Nummer_die_nur_aehnlich_aussieht_zaehlt_nicht()
    {
        // "4390.01" gehoert zu Parzelle 4390, nicht zu 439.
        var namen = new[] { "4390.01-36051", "1439.02-99" };

        Assert.Empty(ParcelHoldingAndShaftMatcher.HoldingsByName(namen, "439"));
    }

    [Fact]
    public void Ohne_Parzellennummer_wird_nichts_zugeordnet()
    {
        var namen = new[] { "439.01-36051" };

        Assert.Empty(ParcelHoldingAndShaftMatcher.HoldingsByName(namen, "   "));
        Assert.Empty(ParcelHoldingAndShaftMatcher.HoldingsByName(null, "439"));
    }

    [Fact]
    public void Dieselbe_Leitung_erscheint_nur_einmal()
    {
        var namen = new[] { "439.01-36051", "439.01-36051" };

        Assert.Single(ParcelHoldingAndShaftMatcher.HoldingsByName(namen, "439"));
    }

    [Fact]
    public void Nimmt_die_Schaechte_an_beiden_Enden_der_Leitung()
    {
        var schaechte = new[] { "439.01", "36051", "36329", "77" };

        var treffer = ParcelHoldingAndShaftMatcher.ShaftsOfHoldings(
            new[] { "439.01-36051" }, schaechte);

        Assert.Equal(new[] { "439.01", "36051" }, treffer);
    }

    [Fact]
    public void Ein_Schacht_den_das_Projekt_nicht_kennt_kommt_nicht_ins_Dossier()
    {
        // Lieber eine kurze Liste als ein erfundener Schacht im Brief.
        var treffer = ParcelHoldingAndShaftMatcher.ShaftsOfHoldings(
            new[] { "439.01-99999" }, new[] { "439.01" });

        Assert.Equal(new[] { "439.01" }, treffer);
    }

    [Fact]
    public void Ein_gemeinsamer_Schacht_zweier_Leitungen_steht_nur_einmal()
    {
        var treffer = ParcelHoldingAndShaftMatcher.ShaftsOfHoldings(
            new[] { "439.01-36051", "439.02-439.01" },
            new[] { "439.01", "439.02", "36051" });

        Assert.Equal(new[] { "439.01", "36051", "439.02" }, treffer);
    }

    [Fact]
    public void Die_Schreibweise_des_Projekts_gewinnt()
    {
        // Im Protokoll steht die Nummer so, wie sie das Projekt fuehrt.
        var treffer = ParcelHoldingAndShaftMatcher.ShaftsOfHoldings(
            new[] { "ABC01-36051" }, new[] { "abc01" });

        Assert.Equal(new[] { "abc01" }, treffer);
    }

    [Fact]
    public void Ohne_Leitungen_gibt_es_keine_Schaechte()
    {
        Assert.Empty(ParcelHoldingAndShaftMatcher.ShaftsOfHoldings(
            Array.Empty<string>(), new[] { "439.01" }));

        Assert.Empty(ParcelHoldingAndShaftMatcher.ShaftsOfHoldings(null, new[] { "439.01" }));
    }
}
