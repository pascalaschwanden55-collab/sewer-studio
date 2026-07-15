using System;
using AuswertungPro.Next.Application.Export;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert den Pfadaufbau der Verteil-Varianten: Normal endet am Objektordner,
/// Sanierung haengt genau eine feste Ebene {Datum}_{Objekt}_Saniert {Jahr} an.
/// </summary>
public sealed class DistributionSanierungPathTests
{
    private static readonly DistributionPatternContext Ctx = new(
        Datum: new DateTime(2026, 7, 15),
        Schachtnummer: "80454");

    [Fact]
    public void Normal_endet_am_Objektordner()
    {
        var r = new DistributionDirectoryTreeResolver();
        var pfad = r.ResolveObjectDirectory(
            @"C:\Ziel", null, null, "{Schachtnummer}", Ctx,
            DistributionVariant.Normal, "{Datum}_{Schachtnummer}");

        Assert.EndsWith("80454", pfad);
    }

    [Fact]
    public void Sanierung_haengt_Saniert_Jahr_Ebene_an()
    {
        var r = new DistributionDirectoryTreeResolver();
        var pfad = r.ResolveObjectDirectory(
            @"C:\Ziel", null, null, "{Schachtnummer}", Ctx,
            DistributionVariant.Sanierung, "{Datum}_{Schachtnummer}");

        Assert.EndsWith(@"80454\20260715_80454_Saniert 2026", pfad);
    }

    [Fact]
    public void Alte_Ueberladung_bleibt_Normal()
    {
        var r = new DistributionDirectoryTreeResolver();
        var pfad = r.ResolveObjectDirectory(
            @"C:\Ziel", null, null, "{Schachtnummer}", Ctx);

        Assert.EndsWith("80454", pfad);
    }
}
