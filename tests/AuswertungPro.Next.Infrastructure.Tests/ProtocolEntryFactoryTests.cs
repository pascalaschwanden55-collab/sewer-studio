using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer ProtocolEntryFactory.
/// Sichern das IST-Verhalten der aus FullProtocolGenerationService extrahierten Helfer.
/// </summary>
public sealed class ProtocolEntryFactoryTests
{
    // ── Hilfsmethoden ──────────────────────────────────────────────────────────

    private static RawVideoDetection MakeDetection(
        string label = "Riss",
        double meterStart = 10.0,
        double meterEnd = 10.5,
        string severity = "mid",
        string? vsaCodeHint = null,
        string? positionClock = null,
        int? extentPercent = null,
        int? heightMm = null,
        int? widthMm = null,
        int? intrusionPercent = null,
        int? crossSectionReductionPercent = null,
        int? diameterReductionMm = null)
        => new(
            FindingLabel: label,
            MeterStart: meterStart,
            MeterEnd: meterEnd,
            Severity: severity,
            VsaCodeHint: vsaCodeHint,
            PositionClock: positionClock,
            ExtentPercent: extentPercent,
            HeightMm: heightMm,
            WidthMm: widthMm,
            IntrusionPercent: intrusionPercent,
            CrossSectionReductionPercent: crossSectionReductionPercent,
            DiameterReductionMm: diameterReductionMm);

    private static FullProtocolGenerationRequest MakeRequest(string haltungId = "H-001")
        => new(
            HaltungId: haltungId,
            VideoPath: "test.mp4",
            AllowedCodes: new[] { "BAB", "BAA", "BCD" });

    private static MappedProtocolEntry MakeMapped(
        RawVideoDetection? detection = null,
        string? suggestedCode = "BAB",
        double confidence = 0.8,
        string? reason = "Test-Grund")
    {
        detection ??= MakeDetection();
        return new MappedProtocolEntry(
            Detection: detection,
            SuggestedCode: suggestedCode,
            Confidence: confidence,
            Reason: reason,
            Warnings: Array.Empty<string>());
    }

    // ── BuildKnowledgeQuery ────────────────────────────────────────────────────

    [Fact]
    public void BuildKnowledgeQuery_EnthältLabel_Meter_Severity_Haltung()
    {
        var det = MakeDetection(label: "Riss", meterStart: 5.0, meterEnd: 8.0, severity: "high");
        var req = MakeRequest("H-042");

        var result = ProtocolEntryFactory.BuildKnowledgeQuery(det, req);

        Assert.Contains("Riss", result);
        Assert.Contains("5,00", result.Replace('.', ','));
        Assert.Contains("Severity high", result);
        Assert.Contains("H-042", result);
    }

    [Fact]
    public void BuildKnowledgeQuery_MitVsaCodeHint_EnthältVisionCode()
    {
        var det = MakeDetection(vsaCodeHint: "BAB");
        var req = MakeRequest();

        var result = ProtocolEntryFactory.BuildKnowledgeQuery(det, req);

        Assert.Contains("VisionCode BAB", result);
    }

    [Fact]
    public void BuildKnowledgeQuery_OhneOptionaleFelder_KeineFalschenSegmente()
    {
        var det = MakeDetection(); // keine optionalen Felder gesetzt
        var req = MakeRequest();

        var result = ProtocolEntryFactory.BuildKnowledgeQuery(det, req);

        // Kein Uhrlage-, Ausdehnung- oder Einragungs-Segment
        Assert.DoesNotContain("Uhrlage", result);
        Assert.DoesNotContain("Ausdehnung", result);
        Assert.DoesNotContain("Einragung", result);
    }

    [Fact]
    public void BuildKnowledgeQuery_MitUhrlageUndProzent_EnthältBeideSegmente()
    {
        var det = MakeDetection(
            positionClock: "12:00",
            extentPercent: 30,
            intrusionPercent: 15,
            crossSectionReductionPercent: 20);
        var req = MakeRequest();

        var result = ProtocolEntryFactory.BuildKnowledgeQuery(det, req);

        Assert.Contains("Uhrlage 12:00", result);
        Assert.Contains("Ausdehnung 30%", result);
        Assert.Contains("Einragung 15%", result);
        Assert.Contains("QV 20%", result);
    }

    // ── BuildPrompt ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildPrompt_EnthältBefundUndErlaubteCodes()
    {
        var det = MakeDetection(label: "Wurzeln", meterStart: 3.0, meterEnd: 5.0);
        var req = MakeRequest();
        var kbExamples = Array.Empty<KbExample>();

        var result = ProtocolEntryFactory.BuildPrompt(det, req, string.Empty, kbExamples);

        Assert.Contains("Wurzeln", result);
        Assert.Contains("BAB", result);
        Assert.Contains("BAA", result);
        Assert.Contains("BCD", result);
        Assert.Contains("Erlaubte Codes", result);
    }

    [Fact]
    public void BuildPrompt_MitVsaHint_EnthältHint()
    {
        var det = MakeDetection();
        var req = MakeRequest();
        var hint = "\nVision-Code-Hinweis: BAB";

        var result = ProtocolEntryFactory.BuildPrompt(det, req, hint, Array.Empty<KbExample>());

        Assert.Contains("Vision-Code-Hinweis", result);
    }

    [Fact]
    public void BuildPrompt_MitKbBeispielen_EnthältBeispielzeile()
    {
        var det = MakeDetection();
        var req = MakeRequest();
        var kbExamples = new[]
        {
            new KbExample("BAB", "Laengsriss", 5.0, 6.0, 0.87)
        };

        var result = ProtocolEntryFactory.BuildPrompt(det, req, string.Empty, kbExamples);

        Assert.Contains("Wissensdatenbank", result);
        Assert.Contains("BAB", result);
        Assert.Contains("0,870", result.Replace('.', ','));
    }

    [Fact]
    public void BuildPrompt_MaxDreiKbBeispiele()
    {
        var det = MakeDetection();
        var req = MakeRequest();
        // Vier Beispiele — nur drei duerfen im Prompt landen
        var kbExamples = new[]
        {
            new KbExample("BAB", "Eins", 1.0, 2.0, 0.9),
            new KbExample("BAA", "Zwei", 2.0, 3.0, 0.8),
            new KbExample("BCD", "Drei", 3.0, 4.0, 0.7),
            new KbExample("BCE", "Vier", 4.0, 5.0, 0.6),
        };

        var result = ProtocolEntryFactory.BuildPrompt(det, req, string.Empty, kbExamples);

        // "Vier" darf nicht enthalten sein
        Assert.DoesNotContain("Vier", result);
        Assert.Contains("Eins", result);
    }

    // ── AddClockParameters ──────────────────────────────────────────────────

    [Fact]
    public void AddClockParameters_Bereich_SetztVonUndBis()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ProtocolEntryFactory.AddClockParameters(dict, "9:00-12:00");

        Assert.Equal("9:00", dict["vsa.uhr.von"]);
        Assert.Equal("12:00", dict["vsa.uhr.bis"]);
    }

    [Fact]
    public void AddClockParameters_EinzelneUhrlage_SetztNurVon()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ProtocolEntryFactory.AddClockParameters(dict, "6:00");

        Assert.Equal("6:00", dict["vsa.uhr.von"]);
        Assert.False(dict.ContainsKey("vsa.uhr.bis"));
    }

    [Fact]
    public void AddClockParameters_Null_NichtsEingetragen()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ProtocolEntryFactory.AddClockParameters(dict, null);

        Assert.Empty(dict);
    }

    [Fact]
    public void AddClockParameters_Leer_NichtsEingetragen()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ProtocolEntryFactory.AddClockParameters(dict, "   ");

        Assert.Empty(dict);
    }

    // ── AddMm ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddMm_PositiverWert_WirdEingetragen()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ProtocolEntryFactory.AddMm(dict, "vsa.q1", 42);

        Assert.Equal("42 mm", dict["vsa.q1"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddMm_NullOderNegativ_NichtsEingetragen(int? value)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ProtocolEntryFactory.AddMm(dict, "vsa.q1", value);

        Assert.Empty(dict);
    }

    [Fact]
    public void AddMm_NullWert_NichtsEingetragen()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ProtocolEntryFactory.AddMm(dict, "vsa.q1", null);

        Assert.Empty(dict);
    }

    // ── AddPercent ─────────────────────────────────────────────────────────

    [Fact]
    public void AddPercent_PositiverWert_WirdEingetragen()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ProtocolEntryFactory.AddPercent(dict, "vsa.umfang.prozent", 75);

        Assert.Equal("75", dict["vsa.umfang.prozent"]);
    }

    [Fact]
    public void AddPercent_Null_NichtsEingetragen()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ProtocolEntryFactory.AddPercent(dict, "vsa.umfang.prozent", null);

        Assert.Empty(dict);
    }

    // ── BuildCodeMeta ──────────────────────────────────────────────────────

    [Fact]
    public void BuildCodeMeta_OhneQuantifizierungOhneSeverity_GibtNull()
    {
        // Kein Severity, keine Quantifizierungsfelder -> null erwartet
        var det = MakeDetection(severity: "");
        var mapped = MakeMapped(detection: det, suggestedCode: "BAB");

        var result = ProtocolEntryFactory.BuildCodeMeta(mapped);

        Assert.Null(result);
    }

    [Fact]
    public void BuildCodeMeta_MitSeverity_GibtCodeMetaMitSeverity()
    {
        var det = MakeDetection(severity: "high");
        var mapped = MakeMapped(detection: det, suggestedCode: "BAB");

        var result = ProtocolEntryFactory.BuildCodeMeta(mapped);

        Assert.NotNull(result);
        Assert.Equal("BAB", result!.Code);
        Assert.Equal("high", result.Severity);
    }

    [Fact]
    public void BuildCodeMeta_MitHeightMm_EnthältVsaQ1()
    {
        var det = MakeDetection(severity: "", heightMm: 15);
        var mapped = MakeMapped(detection: det, suggestedCode: "BAB");

        var result = ProtocolEntryFactory.BuildCodeMeta(mapped);

        Assert.NotNull(result);
        Assert.Equal("15 mm", result!.Parameters["vsa.q1"]);
    }

    [Fact]
    public void BuildCodeMeta_MitUhrlageBereich_SetzVonUndBis()
    {
        var det = MakeDetection(severity: "mid", positionClock: "3:00-6:00");
        var mapped = MakeMapped(detection: det, suggestedCode: "BAB");

        var result = ProtocolEntryFactory.BuildCodeMeta(mapped);

        Assert.NotNull(result);
        Assert.Equal("3:00", result!.Parameters["vsa.uhr.von"]);
        Assert.Equal("6:00", result.Parameters["vsa.uhr.bis"]);
    }

    [Fact]
    public void BuildCodeMeta_FallbackAufVsaCodeHint_WennSuggestedCodeNull()
    {
        var det = MakeDetection(severity: "mid", vsaCodeHint: "BAA");
        var mapped = MakeMapped(detection: det, suggestedCode: null);

        var result = ProtocolEntryFactory.BuildCodeMeta(mapped);

        Assert.NotNull(result);
        Assert.Equal("BAA", result!.Code);
    }

    // ── BuildProtocolEntry ─────────────────────────────────────────────────

    [Fact]
    public void BuildProtocolEntry_SetztSource_Ai()
    {
        var mapped = MakeMapped();

        var entry = ProtocolEntryFactory.BuildProtocolEntry(mapped);

        Assert.Equal(ProtocolEntrySource.Ai, entry.Source);
    }

    [Fact]
    public void BuildProtocolEntry_IsStreckenschaden_TrueWennSpanneGroesserAlsZeroFuenf()
    {
        var det = MakeDetection(meterStart: 10.0, meterEnd: 15.0);
        var mapped = MakeMapped(detection: det);

        var entry = ProtocolEntryFactory.BuildProtocolEntry(mapped);

        Assert.True(entry.IsStreckenschaden);
    }

    [Fact]
    public void BuildProtocolEntry_IsStreckenschaden_FalseWennPunktschaden()
    {
        var det = MakeDetection(meterStart: 10.0, meterEnd: 10.02);
        var mapped = MakeMapped(detection: det);

        var entry = ProtocolEntryFactory.BuildProtocolEntry(mapped);

        Assert.False(entry.IsStreckenschaden);
    }

    [Fact]
    public void BuildProtocolEntry_MeterEnd_MindestensGleichMeterStart()
    {
        // MeterEnd < MeterStart darf nicht nach unten rutschen
        var det = MakeDetection(meterStart: 20.0, meterEnd: 15.0);
        var mapped = MakeMapped(detection: det);

        var entry = ProtocolEntryFactory.BuildProtocolEntry(mapped);

        Assert.True(entry.MeterEnd >= entry.MeterStart);
    }

    [Fact]
    public void BuildProtocolEntry_AiMeta_EnthältKonfidenzUndCode()
    {
        var det = MakeDetection(label: "Riss");
        var mapped = MakeMapped(detection: det, suggestedCode: "BAB", confidence: 0.72);

        var entry = ProtocolEntryFactory.BuildProtocolEntry(mapped);

        Assert.NotNull(entry.Ai);
        Assert.Equal("BAB", entry.Ai!.SuggestedCode);
        Assert.Equal(0.72, entry.Ai.Confidence, precision: 3);
        Assert.Equal("Riss", entry.Beschreibung);
    }

    [Fact]
    public void BuildProtocolEntry_EntryId_IstNeuGuid()
    {
        var mapped = MakeMapped();

        var e1 = ProtocolEntryFactory.BuildProtocolEntry(mapped);
        var e2 = ProtocolEntryFactory.BuildProtocolEntry(mapped);

        Assert.NotEqual(e1.EntryId, e2.EntryId);
    }
}
