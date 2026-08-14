using System.Linq;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class QualityGateServiceTests
{
    [Fact]
    public void FullEvidence_HighSignals_ReturnsGreen()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(
            YoloConf: 0.95,
            DinoConf: 0.88,
            SamMaskStability: 0.90,
            QwenVisionConf: 0.85,
            LlmCodeConf: 0.92,
            KbSimilarity: 0.80,
            KbCodeAgreement: true,
            PlausibilityScore: 0.95);

        var result = svc.Evaluate(ev);

        Assert.Equal(TrafficLight.Green, result.TrafficLight);
        Assert.True(result.CompositeConfidence >= QualityGateService.GreenThreshold);
    }

    [Fact]
    public void MixedEvidence_ReturnsYellow()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(
            YoloConf: 0.80,
            DinoConf: 0.55,
            LlmCodeConf: 0.50,
            KbCodeAgreement: false,
            PlausibilityScore: 0.60);

        var result = svc.Evaluate(ev);

        Assert.Equal(TrafficLight.Yellow, result.TrafficLight);
    }

    [Fact]
    public void LowEvidence_ReturnsRed()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(
            LlmCodeConf: 0.20,
            KbCodeAgreement: false,
            PlausibilityScore: 0.15);

        var result = svc.Evaluate(ev);

        Assert.Equal(TrafficLight.Red, result.TrafficLight);
        Assert.True(result.CompositeConfidence < QualityGateService.YellowThreshold);
    }

    [Fact]
    public void NullSignals_AreSkipped_WeightsRenormalized()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(LlmCodeConf: 0.90, PlausibilityScore: 0.85);

        var result = svc.Evaluate(ev);

        Assert.Equal(2, result.WeightsUsed.Count);
        Assert.True(result.CompositeConfidence > 0.80);
    }

    [Fact]
    public void EmptyEvidence_ReturnsRed()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector();

        var result = svc.Evaluate(ev);

        Assert.Equal(TrafficLight.Red, result.TrafficLight);
        Assert.Equal(0.0, result.CompositeConfidence);
    }

    [Fact]
    public void CustomCategoryWeights_AreUsed()
    {
        var svc = new QualityGateService();
        var weights = new CategoryWeights
        {
            Category = "BAB",
            WLlm = 0.80,
            WPlausibility = 0.20,
            WYolo = 0, WDino = 0, WSam = 0, WQwen = 0, WKb = 0, WKbAgreement = 0
        };
        svc.SetWeights(weights);

        var ev = new EvidenceVector(LlmCodeConf: 0.95, PlausibilityScore: 0.50, DamageCategory: "BAB");
        var result = svc.Evaluate(ev);

        // With 80% weight on LLM (0.95) and 20% on Plausibility (0.50):
        // Composite ≈ (0.80*0.95 + 0.20*0.50) / 1.0 = 0.86
        Assert.True(result.CompositeConfidence > 0.80);

        // Die Ampel bleibt trotzdem Gelb: Sprachmodell und die daraus abgeleitete
        // Plausibilitaet sind EINE Belegquelle (Gesamtaudit 2026-08-14, P1-4). Vorher
        // ergab genau diese Kombination Gruen — das war der Befund.
        Assert.Equal(TrafficLight.Yellow, result.TrafficLight);
    }

    [Fact]
    public void ExplanationContainsSignalInfo()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(LlmCodeConf: 0.70, DinoConf: 0.60);

        var result = svc.Evaluate(ev);

        Assert.Contains("LlmCodeConf", result.Explanation);
        Assert.Contains("DinoConf", result.Explanation);
    }

    // QualityGate-Ehrlichkeit: ein einzelnes hohes Signal darf NICHT "Green" werden,
    // auch wenn der Composite-Score ueber der Green-Schwelle liegt. "Green" verlangt
    // Kreuzvalidierung durch >= MinSignalsForGreen unabhaengige Signale.
    [Fact]
    public void SingleHighSignal_IsCappedToYellow_NotGreen()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(YoloConf: 0.95); // nur EIN Signal (z.B. evtl. halluzinierte YOLO-Box)

        var result = svc.Evaluate(ev);

        Assert.Single(result.WeightsUsed);
        Assert.True(result.CompositeConfidence >= QualityGateService.GreenThreshold,
            "Composite waere ohne die Mindest-Signal-Regel Green");
        Assert.Equal(TrafficLight.Yellow, result.TrafficLight);
        Assert.Contains("auf Gelb begrenzt", result.Explanation);
    }

    [Fact]
    public void TwoHighSignals_AllowGreen()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(YoloConf: 0.95, DinoConf: 0.90); // zwei unabhaengige Signale

        var result = svc.Evaluate(ev);

        Assert.Equal(2, result.WeightsUsed.Count);
        Assert.Equal(TrafficLight.Green, result.TrafficLight);
    }

    // ---- Gesamtaudit 2026-08-14, P1-4: Gruen verlangt zwei unabhaengige QUELLEN,
    // ---- nicht zwei Zahlenfelder aus derselben Quelle.

    [Fact]
    public void Sprachmodell_und_daraus_abgeleitete_Plausibilitaet_ergeben_kein_Gruen()
    {
        // Genau der Fall aus dem Protokollweg: PlausibilityScore wird dort aus derselben
        // Pruefung uebernommen wie LlmCodeConf. Zwei Felder, aber nur ein Beleg.
        var svc = new QualityGateService();
        var ev = new EvidenceVector(LlmCodeConf: 0.95, PlausibilityScore: 0.95);

        var result = svc.Evaluate(ev);

        Assert.True(result.CompositeConfidence >= QualityGateService.GreenThreshold,
            "Der Zahlenwert allein waere Green - genau deshalb braucht es die Quellenregel");
        Assert.Equal(TrafficLight.Yellow, result.TrafficLight);
        Assert.Contains("unabhaengige Belegquelle", result.Explanation);
    }

    [Fact]
    public void Prompt_Beispiele_zaehlen_nicht_als_zweiter_Beleg()
    {
        // KbSimilarity ist die Aehnlichkeit der Beispiele, die das Sprachmodell im Prompt
        // gesehen hat. Sie kann das Modell nicht bestaetigen.
        var svc = new QualityGateService();
        var ev = new EvidenceVector(LlmCodeConf: 0.95, KbSimilarity: 0.95, QwenVisionConf: 0.95);

        var result = svc.Evaluate(ev);

        Assert.Equal(3, result.WeightsUsed.Count);
        Assert.Equal(TrafficLight.Yellow, result.TrafficLight);
    }

    [Fact]
    public void Sprachmodell_plus_blinder_Datenbankabgleich_ergibt_Gruen()
    {
        // Der blinde Abgleich sucht ohne Code- und Haltungshinweis - er ist unabhaengig.
        var svc = new QualityGateService();
        var ev = new EvidenceVector(LlmCodeConf: 0.95, PlausibilityScore: 0.95, KbCodeAgreement: true);

        var result = svc.Evaluate(ev);

        Assert.Equal(TrafficLight.Green, result.TrafficLight);
    }

    [Fact]
    public void Bildmodell_plus_Sprachmodell_ergibt_Gruen()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(YoloConf: 0.95, LlmCodeConf: 0.92, PlausibilityScore: 0.92);

        var result = svc.Evaluate(ev);

        Assert.Equal(TrafficLight.Green, result.TrafficLight);
    }

    [Fact]
    public void Die_Erklaerung_nennt_die_Zahl_der_Quellen()
    {
        var svc = new QualityGateService();
        var ev = new EvidenceVector(YoloConf: 0.95, DinoConf: 0.90, LlmCodeConf: 0.80);

        var result = svc.Evaluate(ev);

        Assert.Contains("aus 3 Quelle(n)", result.Explanation);
    }
}

/// <summary>
/// Die reine Zuordnungsregel Signal -> Belegquelle (Gesamtaudit 2026-08-14, P1-4).
/// </summary>
public sealed class EvidenceSourceGroupingTests
{
    [Fact]
    public void Alle_Sprachmodell_Signale_teilen_eine_Quelle()
    {
        var quellen = EvidenceSourceGrouping.DistinctSources(new[]
        {
            nameof(EvidenceVector.LlmCodeConf),
            nameof(EvidenceVector.QwenVisionConf),
            nameof(EvidenceVector.PlausibilityScore),
            nameof(EvidenceVector.KbSimilarity)
        });

        Assert.Single(quellen);
        Assert.Equal(EvidenceSourceGrouping.SourceLanguageModel, quellen.Single());
    }

    [Fact]
    public void Bildmodelle_und_blinder_Abgleich_sind_eigene_Quellen()
    {
        var quellen = EvidenceSourceGrouping.DistinctSources(new[]
        {
            nameof(EvidenceVector.YoloConf),
            nameof(EvidenceVector.DinoConf),
            nameof(EvidenceVector.SamMaskStability),
            nameof(EvidenceVector.KbCodeAgreement)
        });

        Assert.Equal(4, quellen.Count);
    }

    [Fact]
    public void Ein_unbekanntes_Signal_verschwindet_nicht_stillschweigend()
    {
        // Neue Signale gelten zunaechst als eigene Quelle und muessen bewusst zugeordnet
        // werden - sie duerfen aber nicht unter den Tisch fallen.
        Assert.Equal("NeuesSignalXY", EvidenceSourceGrouping.SourceOf("NeuesSignalXY"));
    }
}
