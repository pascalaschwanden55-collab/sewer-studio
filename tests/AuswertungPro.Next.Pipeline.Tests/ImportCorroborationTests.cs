using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ImportCorroborationTests
{
    [Fact]
    public void ExactNearbyImport_PromotesWeakQwenBendToGreen()
    {
        var match = ImportCorroboration.FindNearest(
            "BCCBY",
            5.87,
            new[] { (Code: "BCCBY", Meter: 6.08) });

        var evidence = ImportCorroboration.BuildQwenEvidence(
            severity: 1,
            detectedCode: "BCCBY",
            importMatch: match);
        var result = new QualityGateService().Evaluate(evidence);

        Assert.NotNull(match);
        Assert.True(match.ExactCodeMatch);
        Assert.Equal(TrafficLight.Green, result.TrafficLight);
    }

    [Fact]
    public void MissingImport_KeepsWeakQwenFindingRed()
    {
        var evidence = ImportCorroboration.BuildQwenEvidence(
            severity: 1,
            detectedCode: "BCCBY",
            importMatch: null);
        var result = new QualityGateService().Evaluate(evidence);

        Assert.Equal(TrafficLight.Red, result.TrafficLight);
    }

    [Fact]
    public void FarImport_DoesNotCorroborateFinding()
    {
        var match = ImportCorroboration.FindNearest(
            "BCCBY",
            5.87,
            new[] { (Code: "BCCBY", Meter: 6.80) });

        Assert.Null(match);
    }

    [Fact]
    public void GenericBendCode_MatchesSpecificImportedBendFamily()
    {
        var match = ImportCorroboration.FindNearest(
            "BCC",
            5.87,
            new[] { (Code: "BCCBY", Meter: 6.08) });

        Assert.NotNull(match);
        Assert.False(match.ExactCodeMatch);
        Assert.Equal("BCCBY", match.Code);
    }
}
