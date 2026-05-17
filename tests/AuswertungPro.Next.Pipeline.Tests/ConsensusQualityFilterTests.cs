using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Vision;

namespace AuswertungPro.Next.Pipeline.Tests;

public class ConsensusQualityFilterTests
{
    // Stub: liefert immer Green, damit Konsens-Logik isoliert testbar ist
    // (sonst zieht ein niedriger Composite-Score die Detektion ueber den Red-Filter raus).
    private sealed class AlwaysGreenQg : IQualityGateService
    {
        public QualityGateResult Evaluate(EvidenceVector evidence) =>
            new(1.0, TrafficLight.Green, new Dictionary<string, double>(), "stub");
        public void SetWeights(CategoryWeights weights) { }
    }

    private static RawVideoDetection Det(
        EvidenceVector? evidence = null,
        double? bboxX1 = null, double? bboxY1 = null,
        double? bboxX2 = null, double? bboxY2 = null)
        => new(
            FindingLabel: "label",
            MeterStart: 1.0,
            MeterEnd: 1.0,
            Severity: "mid",
            VsaCodeHint: "BBA",
            Evidence: evidence,
            BboxX1: bboxX1,
            BboxY1: bboxY1,
            BboxX2: bboxX2,
            BboxY2: bboxY2);

    [Fact]
    public void Apply_LeereListe_KeinFehler()
    {
        var list = new List<RawVideoDetection>();
        var removed = ConsensusQualityFilter.Apply(list);
        Assert.Equal(0, removed);
    }

    [Fact]
    public void Apply_BboxZuKlein_EntferntDetektion()
    {
        var list = new List<RawVideoDetection>
        {
            // Flaeche = 0.1*0.1 = 0.01 < 0.03 → raus
            Det(bboxX1: 0.10, bboxY1: 0.10, bboxX2: 0.20, bboxY2: 0.20),
        };
        var removed = ConsensusQualityFilter.Apply(list);
        Assert.Equal(1, removed);
        Assert.Empty(list);
    }

    [Fact]
    public void Apply_BboxGrossGenugUndKeineEvidence_Behalten()
    {
        var list = new List<RawVideoDetection>
        {
            // Flaeche = 0.5*0.5 = 0.25 → ueber Mindestflaeche
            Det(bboxX1: 0.0, bboxY1: 0.0, bboxX2: 0.5, bboxY2: 0.5),
        };
        var removed = ConsensusQualityFilter.Apply(list);
        Assert.Equal(0, removed);
        Assert.Single(list);
    }

    [Fact]
    public void Apply_EvidenceMitNurEinemModell_Entfernt()
    {
        // Nur YOLO bestaetigt → Konsens nicht erreicht (auch mit Green-QG)
        var list = new List<RawVideoDetection>
        {
            Det(evidence: new EvidenceVector(YoloConf: 0.8)),
        };
        var removed = ConsensusQualityFilter.Apply(list, qualityGate: new AlwaysGreenQg());
        Assert.Equal(1, removed);
    }

    [Fact]
    public void Apply_EvidenceMitZweiModellen_Behalten()
    {
        // YOLO + DINO ueber Schwelle → Konsens erreicht
        var list = new List<RawVideoDetection>
        {
            Det(evidence: new EvidenceVector(YoloConf: 0.5, DinoConf: 0.5)),
        };
        var removed = ConsensusQualityFilter.Apply(list, qualityGate: new AlwaysGreenQg());
        Assert.Equal(0, removed);
        Assert.Single(list);
    }

    [Fact]
    public void Apply_QwenUnterSchwelle_KeinKonsens()
    {
        // Qwen 0.50 ist UNTER 0.55-Schwelle → zaehlt nicht
        // YOLO ueber Schwelle → nur eine Bestaetigung → raus
        var list = new List<RawVideoDetection>
        {
            Det(evidence: new EvidenceVector(YoloConf: 0.5, QwenVisionConf: 0.50)),
        };
        var removed = ConsensusQualityFilter.Apply(list, qualityGate: new AlwaysGreenQg());
        Assert.Equal(1, removed);
    }

    [Fact]
    public void Apply_KonfidenzenAufSchwelle_ZaehlenMit()
    {
        // YOLO + DINO genau auf der jeweiligen Schwelle (>= ist erfuellt)
        // → Konsens 2 von 3 → behalten. QG-Stub liefert Green damit nicht der
        // niedrige Composite-Score (~0.22) ueber den Red-Pfad rauswirft.
        var list = new List<RawVideoDetection>
        {
            Det(evidence: new EvidenceVector(
                YoloConf: ConsensusQualityFilter.YoloMinConf,
                DinoConf: ConsensusQualityFilter.DinoMinConf)),
        };
        var removed = ConsensusQualityFilter.Apply(list, qualityGate: new AlwaysGreenQg());
        Assert.Equal(0, removed);
    }

    [Fact]
    public void Apply_OhneEvidence_BehaeltAlsLegacy()
    {
        // Keine Bbox, keine Evidence → Legacy-Pfad behalten
        var list = new List<RawVideoDetection> { Det() };
        var removed = ConsensusQualityFilter.Apply(list);
        Assert.Equal(0, removed);
        Assert.Single(list);
    }

    [Fact]
    public void Apply_MischungAusUngueltigUndGueltig_NurUngueltigeFliegen()
    {
        var list = new List<RawVideoDetection>
        {
            Det(evidence: new EvidenceVector(YoloConf: 0.8, DinoConf: 0.8)), // bleibt
            Det(evidence: new EvidenceVector(YoloConf: 0.8)),                // raus (1 Modell)
            Det(bboxX1: 0.0, bboxY1: 0.0, bboxX2: 0.05, bboxY2: 0.05),       // raus (bbox 0.0025)
        };
        var removed = ConsensusQualityFilter.Apply(list);
        Assert.Equal(2, removed);
        Assert.Single(list);
    }
}
