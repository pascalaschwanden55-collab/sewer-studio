using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ResolveFromClassifierTests
{
    private static IReadOnlyList<YoloClassifyPrediction> Preds(params (string Code, double Conf)[] preds)
    {
        var list = new List<YoloClassifyPrediction>();
        foreach (var (code, conf) in preds)
            list.Add(new YoloClassifyPrediction(code, conf));
        return list;
    }

    [Fact]
    public void BcdAmRohranfangWirdBestaetigt()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BCD", 0.9)), currentMeter: 0.2, totalLength: 50);
        Assert.NotNull(r);
        Assert.Equal("BCD", r!.Code);
    }

    [Fact]
    public void BcdMittenImRohrWirdVerworfen()
    {
        // Negativ-Gate: Rohranfang gibt es nicht bei 8m
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BCD", 0.9)), currentMeter: 8.0, totalLength: 50);
        Assert.Null(r);
    }

    [Fact]
    public void BceMittenImRohrWirdVerworfen()
    {
        // Pilot 2026-06-10: offener Anschluss bei 1.7m von 12.5m wurde als BCE codiert
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BCE", 0.95)), currentMeter: 1.7, totalLength: 12.5);
        Assert.Null(r);
    }

    [Fact]
    public void BceAmRohrendeWirdBestaetigt()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BCE", 0.95)), currentMeter: 12.2, totalLength: 12.5);
        Assert.NotNull(r);
        Assert.Equal("BCE", r!.Code);
    }

    [Fact]
    public void BefundcodeMitHoherKonfidenzWirdUebernommen()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BAB", 0.72)), currentMeter: 5.0, totalLength: 50);
        Assert.NotNull(r);
        Assert.Equal("BAB", r!.Code);
    }

    [Fact]
    public void NiedrigeKonfidenzLiefertKeineEntscheidung()
    {
        var r = VsaCodeResolver.ResolveFromClassifier(Preds(("BAB", 0.30)), currentMeter: 5.0, totalLength: 50);
        Assert.Null(r);
    }
}
