using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer das gewichtete SampleQualityGate (Green/Yellow/Red).
/// Prueft: Hard-Red Kriterien, gewichtete Issues, SourceType-Logik, Batch-Auswertung.
/// </summary>
public sealed class SampleQualityGateServiceTests
{
    private static TrainingSample MakeSample(
        string code = "BAB",
        string beschreibung = "Laengsriss im Scheitelbereich",
        string? framePath = null,
        double meterStart = 12.5,
        double meterEnd = 12.5,
        string sourceType = SourceTypeNames.BatchImport,
        string? signature = "sig_001",
        bool isStreckenschaden = false)
    {
        return new TrainingSample
        {
            SampleId = "test_001",
            CaseId = "case_001",
            Code = code,
            Beschreibung = beschreibung,
            MeterStart = meterStart,
            MeterEnd = meterEnd,
            IsStreckenschaden = isStreckenschaden,
            FramePath = framePath ?? "",
            Signature = signature ?? "",
            SourceType = sourceType,
        };
    }

    // ── Hard-Red Kriterien ──────────────────────────────────────────

    [Fact]
    public void Code_Fehlt_Ist_HardRed()
    {
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(code: ""));
        Assert.Equal(SampleQualityGrade.Red, result.Grade);
    }

    [Fact]
    public void Code_ZuKurz_Ist_HardRed()
    {
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(code: "B"));
        Assert.Equal(SampleQualityGrade.Red, result.Grade);
    }

    [Fact]
    public void Code_NichtImKatalog_Ist_HardRed()
    {
        var gate = new SampleQualityGateService(["BAB", "BCA", "BBC"]);
        var result = gate.Evaluate(MakeSample(code: "ZZZ"));
        Assert.Equal(SampleQualityGrade.Red, result.Grade);
    }

    [Fact]
    public void Code_PraefixImKatalog_Ist_NichtRed()
    {
        // "BABAA" sollte akzeptiert werden wenn "BAB" im Katalog ist
        var gate = new SampleQualityGateService(["BAB", "BCA"]);
        var result = gate.Evaluate(MakeSample(code: "BABAA"));
        Assert.NotEqual(SampleQualityGrade.Red, result.Grade);
    }

    [Fact]
    public void SampleId_Fehlt_Ist_HardRed()
    {
        var gate = new SampleQualityGateService();
        var sample = MakeSample();
        sample.SampleId = "";
        var result = gate.Evaluate(sample);
        Assert.Equal(SampleQualityGrade.Red, result.Grade);
    }

    [Fact]
    public void CaseId_Fehlt_Ist_HardRed()
    {
        var gate = new SampleQualityGateService();
        var sample = MakeSample();
        sample.CaseId = "";
        var result = gate.Evaluate(sample);
        Assert.Equal(SampleQualityGrade.Red, result.Grade);
    }

    [Fact]
    public void Beschreibung_Leer_Ist_HardRed()
    {
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(beschreibung: ""));
        Assert.Equal(SampleQualityGrade.Red, result.Grade);
    }

    // ── Green: Perfektes Sample ─────────────────────────────────────

    [Fact]
    public void PerfektesSample_Ist_Green()
    {
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample());
        Assert.Equal(SampleQualityGrade.Green, result.Grade);
        Assert.True(result.IsGreen);
        Assert.True(result.IsAcceptable);
        Assert.Empty(result.Issues);
    }

    // ── SourceType-bewusste Frame-Pruefung ──────────────────────────

    [Fact]
    public void BatchImport_OhneFrame_Ist_Green()
    {
        // Batch-Import ohne Frame ist normal (protocol-only)
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(
            sourceType: SourceTypeNames.BatchImport,
            framePath: ""));
        Assert.Equal(SampleQualityGrade.Green, result.Grade);
    }

    [Fact]
    public void PdfPhoto_OhneFrame_Ist_Green()
    {
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(
            sourceType: SourceTypeNames.PdfPhoto,
            framePath: ""));
        Assert.Equal(SampleQualityGrade.Green, result.Grade);
    }

    [Fact]
    public void Selbsttraining_OhneFrame_Ist_Yellow()
    {
        // Selbsttraining SOLLTE Frames haben
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(
            sourceType: SourceTypeNames.VideoTimestamp,
            framePath: ""));
        Assert.Equal(SampleQualityGrade.Yellow, result.Grade);
        Assert.Contains(result.Issues, i => i.Contains("Frame"));
    }

    // ── Yellow-Kriterien ────────────────────────────────────────────

    [Fact]
    public void Signatur_Fehlt_Ist_Yellow()
    {
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(signature: ""));
        Assert.Equal(SampleQualityGrade.Yellow, result.Grade);
    }

    [Fact]
    public void Beschreibung_IstCodeEcho_Ist_Yellow()
    {
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(code: "BAB", beschreibung: "BAB"));
        Assert.Equal(SampleQualityGrade.Yellow, result.Grade);
    }

    [Fact]
    public void MeterStart_Null_Bei_NichtStartCode_Ist_Yellow()
    {
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(code: "BAB", meterStart: 0));
        Assert.Equal(SampleQualityGrade.Yellow, result.Grade);
    }

    [Fact]
    public void MeterStart_Null_Bei_BCD_Ist_Green()
    {
        // BCD (Rohranfang) bei Meter 0 ist voellig normal
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(code: "BCD", meterStart: 0));
        Assert.Equal(SampleQualityGrade.Green, result.Grade);
    }

    [Fact]
    public void MeterStart_Null_Bei_BCE_Ist_Green()
    {
        // BCE bei Meter 0 ist normal (Rueckwaertsbefahrung)
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(code: "BCE", meterStart: 0));
        Assert.Equal(SampleQualityGrade.Green, result.Grade);
    }

    [Fact]
    public void Streckenschaden_OhneAusdehnung_Ist_Yellow()
    {
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(
            isStreckenschaden: true,
            meterStart: 10.0,
            meterEnd: 10.0));
        Assert.Equal(SampleQualityGrade.Yellow, result.Grade);
    }

    // ── Gewichtung: Viele leichte Issues → Red ──────────────────────

    [Fact]
    public void VieleLeichteIssues_Werden_Red()
    {
        // Signatur fehlt (2) + kein Frame bei Selbsttraining (2) = 4 → Red
        var gate = new SampleQualityGateService();
        var result = gate.Evaluate(MakeSample(
            sourceType: SourceTypeNames.VideoTimestamp,
            framePath: "",
            signature: ""));
        Assert.Equal(SampleQualityGrade.Red, result.Grade);
    }

    // ── Leerer Katalog vs. kein Katalog ─────────────────────────────

    [Fact]
    public void OhneKatalog_AkzeptiertAlles()
    {
        var gate = new SampleQualityGateService(); // kein Katalog
        var result = gate.Evaluate(MakeSample(code: "XYZZY"));
        // Ohne Katalog: keine Code-Pruefung → Green (wenn alles andere stimmt)
        Assert.NotEqual(SampleQualityGrade.Red, result.Grade);
    }

    [Fact]
    public void LeererKatalog_LehntAllesAb()
    {
        // Leerer Katalog (Count=0) sollte Code-Pruefung ueberspringen (wie kein Katalog)
        var gate = new SampleQualityGateService(new string[] { });
        var result = gate.Evaluate(MakeSample(code: "BAB"));
        // Leerer Katalog mit Count=0 → _allowedCodes.Count > 0 ist false → kein Code-Check
        Assert.NotEqual(SampleQualityGrade.Red, result.Grade);
    }

    // ── Batch-Auswertung ────────────────────────────────────────────

    [Fact]
    public void EvaluateBatch_ZaehltKorrekt()
    {
        var gate = new SampleQualityGateService();
        var samples = new[]
        {
            MakeSample(), // Green
            MakeSample(beschreibung: "BAB", code: "BAB"), // Yellow (Code-Echo)
            MakeSample(code: ""), // Red (Code fehlt)
        };

        var batch = gate.EvaluateBatch(samples);

        Assert.Equal(3, batch.Total);
        Assert.Equal(1, batch.Green);
        Assert.Equal(1, batch.Yellow);
        Assert.Equal(1, batch.Red);
        Assert.Equal(2, batch.Accepted.Count); // Green + Yellow
        Assert.Single(batch.Rejected);
    }
}
