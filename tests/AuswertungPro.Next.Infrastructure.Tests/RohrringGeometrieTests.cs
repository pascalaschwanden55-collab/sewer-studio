using System.Linq;
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class RohrringGeometrieTests
{
    private static ProtocolEntry E(string code, string? von = null, string? bis = null, string? stufe = null, double? meter = null)
    {
        var e = new ProtocolEntry { Code = code, Beschreibung = code, MeterStart = meter, CodeMeta = new ProtocolEntryCodeMeta { Code = code, Severity = stufe } };
        if (von is not null) e.CodeMeta.Parameters["Uhr_von"] = von;
        if (bis is not null) e.CodeMeta.Parameters["Uhr_bis"] = bis;
        return e;
    }

    [Fact]
    public void Uhrlage_bestimmt_Start_und_Sweep()
    {
        var b = Assert.Single(RohrringGeometrie.Boegen(new[] { E("BAB", "12", "02", "3") }));
        Assert.Equal(0, b.StartGrad); Assert.Equal(60, b.SweepGrad); Assert.Equal(3, b.Stufe);
    }

    [Fact]
    public void Einzelne_Uhr_ergibt_dreissig_Grad_und_Sohle_liegt_unten()
    {
        var b = Assert.Single(RohrringGeometrie.Boegen(new[] { E("BBC", "06") }));
        Assert.Equal(180, b.StartGrad); Assert.Equal(30, b.SweepGrad); Assert.Equal(1, b.Stufe);
    }

    [Fact]
    public void Ohne_Uhrlage_gilt_die_Indexregel_und_hoechstens_drei()
    {
        var boegen = RohrringGeometrie.Boegen(new[] { E("BAB"), E("BAC"), E("BBA"), E("BBC") });
        Assert.Equal(3, boegen.Count);
        Assert.Equal(new[] { 0.0, 70.0, 140.0 }, boegen.Select(b => b.StartGrad).ToArray());
    }

    /// <summary>
    /// F1: Rohranfang, Rohrende, Anschluss und Bogen sind Bestandsaufnahme, kein Schaden.
    /// Vorher zeichnete der Ring sie als erste drei Eintraege und verdraengte damit echte Schaeden.
    /// </summary>
    [Fact]
    public void Bestandscodes_werden_nicht_gezeichnet()
    {
        var boegen = RohrringGeometrie.Boegen(new[] { E("BCD", "12"), E("BCE", "12"), E("BCA", "03"), E("BAB", "09") });

        var bogen = Assert.Single(boegen);
        Assert.Equal(270, bogen.StartGrad);
        Assert.StartsWith("BAB", bogen.Tooltip, System.StringComparison.Ordinal);
    }

    /// <summary>F1: Die schwersten Schaeden zuerst, nicht die zuerst erfassten.</summary>
    [Fact]
    public void Schwere_Schaeden_stehen_vor_leichten()
    {
        var boegen = RohrringGeometrie.Boegen(new[]
        {
            E("BBC", stufe: "1", meter: 1.0),
            E("BAC", stufe: "4", meter: 20.0),
            E("BAB", stufe: "2", meter: 5.0)
        });

        Assert.Equal(new[] { 4, 2, 1 }, boegen.Select(b => b.Stufe).ToArray());
    }

    /// <summary>Bei gleicher Stufe entscheidet der kleinere Meterwert.</summary>
    [Fact]
    public void Bei_gleicher_Stufe_gilt_der_kleinere_Meterwert()
    {
        var schaeden = RohrringGeometrie.Schaeden(new[]
        {
            E("BAB", stufe: "3", meter: 12.0),
            E("BAC", stufe: "3", meter: 4.0)
        });

        Assert.Equal(new[] { "BAC", "BAB" }, schaeden.Select(s => s.Code).ToArray());
    }

    [Fact]
    public void Geloeschte_Eintraege_bleiben_draussen()
    {
        var geloescht = E("BAB", stufe: "5");
        geloescht.IsDeleted = true;

        Assert.Empty(RohrringGeometrie.Schaeden(new[] { geloescht }));
    }
}
