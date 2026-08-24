using System;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

/// <summary>
/// Prueft, dass die Schaechte einer Parzelle auch dann gefunden werden, wenn
/// keine passende Leitung im Projekt liegt.
///
/// Der Anlass ist der Bestand selbst: aufgenommen wird manchmal nur der
/// Schacht, oder seine Leitung traegt einen Namen ohne Parzellenbezug. Bisher
/// kamen Schaechte ausschliesslich ueber die Knoten der Leitungen mit — ein
/// solcher Schacht fehlte im Dossier ersatzlos.
/// </summary>
public sealed class ParcelShaftMatchingTests
{
    [Fact]
    public void Ein_Schacht_mit_Parzellennamen_wird_auch_ohne_Leitung_gefunden()
    {
        var schaechte = ParcelHoldingAndShaftMatcher.ShaftsOnParcel(
            new[] { "439.01", "439.02", "36051", "512.01" }, "439");

        Assert.Equal(new[] { "439.01", "439.02" }, schaechte);
    }

    [Fact]
    public void Ein_Bestandsname_mit_einer_Ziffer_erfindet_keine_Parzelle()
    {
        // "7.34854" ist ein Schachtname aus dem Bestand. Ohne diese Schranke
        // landete er in jedem Dossier der Parzelle 7.
        var schaechte = ParcelHoldingAndShaftMatcher.ShaftsOnParcel(
            new[] { "7.34854" }, "7");

        Assert.Empty(schaechte);
    }

    [Fact]
    public void Eine_fremde_Parzelle_bringt_ihre_Schaechte_nicht_mit()
    {
        var schaechte = ParcelHoldingAndShaftMatcher.ShaftsOnParcel(
            new[] { "439.01", "440.01" }, "439");

        Assert.Equal(new[] { "439.01" }, schaechte);
    }

    [Fact]
    public void Beide_Wege_zusammen_ohne_Doppelnennung()
    {
        // "439.01" haengt an der Leitung UND traegt den Parzellennamen.
        var schaechte = ParcelHoldingAndShaftMatcher.ShaftsForParcel(
            new[] { "439.01-36051" },
            new[] { "439.01", "36051", "439.02" },
            "439");

        Assert.Equal(new[] { "439.01", "36051", "439.02" }, schaechte);
    }

    [Fact]
    public void Die_Schaechte_der_Leitungen_stehen_zuerst()
    {
        // Sie sind die, die der Empfaenger im Protokoll wiederfindet.
        var schaechte = ParcelHoldingAndShaftMatcher.ShaftsForParcel(
            new[] { "36051-36329" },
            new[] { "439.07", "36051", "36329" },
            "439");

        Assert.Equal(new[] { "36051", "36329", "439.07" }, schaechte);
    }

    [Fact]
    public void Nur_was_das_Projekt_wirklich_fuehrt()
    {
        // Der Name der Leitung nennt "439.09"; das Projekt kennt diesen
        // Schacht nicht. Ein erfundener Schacht im Dossier waere schlimmer
        // als eine kurze Liste.
        var schaechte = ParcelHoldingAndShaftMatcher.ShaftsForParcel(
            new[] { "439.09-36051" },
            new[] { "36051" },
            "439");

        Assert.Equal(new[] { "36051" }, schaechte);
    }

    [Fact]
    public void Ohne_Parzellennummer_bleibt_es_beim_bisherigen_Verhalten()
    {
        var schaechte = ParcelHoldingAndShaftMatcher.ShaftsForParcel(
            new[] { "439.01-36051" },
            new[] { "439.01", "36051", "439.02" },
            null);

        Assert.Equal(new[] { "439.01", "36051" }, schaechte);
    }

    [Fact]
    public void Ohne_Leitungen_findet_die_Parzelle_ihre_Schaechte_trotzdem()
    {
        var schaechte = ParcelHoldingAndShaftMatcher.ShaftsForParcel(
            Array.Empty<string>(),
            new[] { "439.01", "36051" },
            "439");

        Assert.Equal(new[] { "439.01" }, schaechte);
    }

    [Fact]
    public void Die_Schreibweise_des_Projekts_gewinnt()
    {
        var schaechte = ParcelHoldingAndShaftMatcher.ShaftsForParcel(
            new[] { "439.01-36051" },
            new[] { "439.01", "36051" },
            "439");

        Assert.Contains("439.01", schaechte);
        Assert.Single(schaechte.Where(s =>
            string.Equals(s, "439.01", StringComparison.OrdinalIgnoreCase)));
    }
}
