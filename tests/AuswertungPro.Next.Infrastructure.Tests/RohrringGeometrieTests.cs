using System.Linq;
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class RohrringGeometrieTests
{
    private static ProtocolEntry E(string code, string? von = null, string? bis = null, string? stufe = null)
    {
        var e = new ProtocolEntry { Code = code, Beschreibung = code, CodeMeta = new ProtocolEntryCodeMeta { Code = code, Severity = stufe } };
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
        var boegen = RohrringGeometrie.Boegen(new[] { E("A"), E("B"), E("C"), E("D") });
        Assert.Equal(3, boegen.Count);
        Assert.Equal(new[] { 0.0, 70.0, 140.0 }, boegen.Select(b => b.StartGrad).ToArray());
    }
}
