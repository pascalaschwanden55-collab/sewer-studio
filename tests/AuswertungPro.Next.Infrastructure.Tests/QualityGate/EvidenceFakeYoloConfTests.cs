using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.QualityGate;

// Regressions-/Charakterisierungs-Test fuer D2: dokumentiert, dass ein hartkodierter
// YoloConf=0.8 einen Grenzbefund kuenstlich auf Gruen kippt, und dass das Weglassen
// des Signals ihn korrekt auf Gelb laesst (QualityGate renormalisiert ueber vorhandene
// Signale). Default-Gewichte: WYolo=.10 WDino=.15 WSam=.10 WPlausibility=.10.
public class EvidenceFakeYoloConfTests
{
    private static readonly QualityGateService Gate = new();

    // Grenzbefund: DinoConf=0.8, Sam=0.6, Plaus=0.8
    // MIT YoloConf=0.8:  0.34/0.45 = 0.756 -> Green
    [Fact]
    public void FesterYoloConf_KippteGrenzbefund_KuenstlichAufGruen()
    {
        var mitFestwert = new EvidenceVector(
            YoloConf: 0.8, DinoConf: 0.8, SamMaskStability: 0.6, PlausibilityScore: 0.8);
        Assert.Equal(TrafficLight.Green, Gate.Evaluate(mitFestwert).TrafficLight);
    }

    // OHNE YoloConf: 0.26/0.35 = 0.743 -> Yellow (ehrlich)
    [Fact]
    public void OhneYoloConf_BleibtGrenzbefund_KorrektGelb()
    {
        var ohneYolo = new EvidenceVector(
            DinoConf: 0.8, SamMaskStability: 0.6, PlausibilityScore: 0.8);
        Assert.Equal(TrafficLight.Yellow, Gate.Evaluate(ohneYolo).TrafficLight);
    }
}
