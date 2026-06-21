using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class ClassifierOnlyStructuralPolicyTests
{
    private static IReadOnlyList<YoloClassifyPrediction> Preds(params (string c, double p)[] xs)
    {
        var list = new List<YoloClassifyPrediction>();
        foreach (var (c, p) in xs) list.Add(new YoloClassifyPrediction(c, p));
        return list;
    }

    [Fact]
    public void Bcd_HighConfidence_AtPipeStart_ResolvesToBcd()
    {
        var r = ClassifierOnlyStructuralPolicy.TryResolve(
            Preds(("BCD", 0.91)), meter: 0.2, reachLength: 50, isBend: false, minConfidence: 0.60);
        Assert.NotNull(r);
        Assert.Equal("BCD", r!.Code);
    }

    [Fact]
    public void DamageCode_IsRejected_NotGrundgeruest()
    {
        var r = ClassifierOnlyStructuralPolicy.TryResolve(
            Preds(("BAB", 0.95)), meter: 10, reachLength: 50, isBend: false, minConfidence: 0.60);
        Assert.Null(r);
    }

    [Fact]
    public void BelowMinConfidence_IsRejected()
    {
        var r = ClassifierOnlyStructuralPolicy.TryResolve(
            Preds(("BCA", 0.40)), meter: 10, reachLength: 50, isBend: false, minConfidence: 0.60);
        Assert.Null(r);
    }

    [Fact]
    public void Bend_ResolvesToBcc_ViaVeto()
    {
        var r = ClassifierOnlyStructuralPolicy.TryResolve(
            Preds(("BCE", 0.55)), meter: 12, reachLength: 50, isBend: true, minConfidence: 0.60);
        Assert.NotNull(r);
        Assert.Equal("BCC", r!.Code);
    }

    [Fact]
    public void NoPredictions_ReturnsNull()
    {
        var r = ClassifierOnlyStructuralPolicy.TryResolve(
            null, meter: 5, reachLength: 50, isBend: false, minConfidence: 0.60);
        Assert.Null(r);
    }
}
