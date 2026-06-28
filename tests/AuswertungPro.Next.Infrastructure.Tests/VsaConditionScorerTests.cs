using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Vsa;
using AuswertungPro.Next.Infrastructure.Vsa;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer VsaConditionScorer.
/// Pruefen das IST-Verhalten der reinen Berechnungslogik.
/// </summary>
public sealed class VsaConditionScorerTests
{
    // ── ComputeLengthFactor ──────────────────────────────────────────────

    [Fact]
    public void ComputeLengthFactor_PunktSchaden_GibtMinLength()
    {
        // Ohne SchadenlageAnfang/Ende und ohne MeterStart/End = Punktschaden -> minLength
        var finding = new VsaFinding { KanalSchadencode = "BAB" };
        var lf = VsaConditionScorer.ComputeLengthFactor(finding, minLength: 3.0);
        Assert.Equal(3.0, lf);
    }

    [Fact]
    public void ComputeLengthFactor_Streckenschaden_GibtTatsaechlicheLaenge()
    {
        // Laenge 8.0m > minLength 3.0m -> reale Laenge
        var finding = new VsaFinding
        {
            KanalSchadencode = "BAF",
            SchadenlageAnfang = 2.0,
            SchadenlageEnde = 10.0
        };
        var lf = VsaConditionScorer.ComputeLengthFactor(finding, minLength: 3.0);
        Assert.Equal(8.0, lf);
    }

    [Fact]
    public void ComputeLengthFactor_KurzeStrecke_GibtMinLength()
    {
        // Laenge 1.0m < minLength 3.0m -> minLength
        var finding = new VsaFinding
        {
            KanalSchadencode = "BAF",
            MeterStart = 5.0,
            MeterEnd = 6.0
        };
        var lf = VsaConditionScorer.ComputeLengthFactor(finding, minLength: 3.0);
        Assert.Equal(3.0, lf);
    }

    // ── MapDringlichkeit ─────────────────────────────────────────────────

    [Theory]
    [InlineData(30.0,  "Sofort")]
    [InlineData(49.99, "Sofort")]
    [InlineData(50.0,  "Kurzfristig (3J)")]
    [InlineData(149.99,"Kurzfristig (3J)")]
    [InlineData(150.0, "Mittelfristig (8J)")]
    [InlineData(249.99,"Mittelfristig (8J)")]
    [InlineData(250.0, "Langfristig")]
    [InlineData(349.99,"Langfristig")]
    [InlineData(350.0, "Keine")]
    [InlineData(null,  "n/a")]
    public void MapDringlichkeit_GibtKorrekteKlasse(double? dz, string expected)
    {
        Assert.Equal(expected, VsaConditionScorer.MapDringlichkeit(dz));
    }

    // ── MapZustandsklasse ────────────────────────────────────────────────

    [Theory]
    [InlineData(4.0, "4")]
    [InlineData(3.5, "4")]
    [InlineData(3.4, "3")]
    [InlineData(2.5, "3")]  // Runden AwayFromZero: 2.5 -> 3
    [InlineData(0.0, "0")]
    [InlineData(null, "n/a")]
    public void MapZustandsklasse_GibtKorrekteKlasse(double? note, string expected)
    {
        Assert.Equal(expected, VsaConditionScorer.MapZustandsklasse(note));
    }

    // ── BuildPruefungsresultat ───────────────────────────────────────────

    [Theory]
    [InlineData(4.0,  "i.O.")]
    [InlineData(3.0,  "i.O.")]
    [InlineData(2.99, "beobachten")]
    [InlineData(1.5,  "beobachten")]
    [InlineData(1.49, "Sanierungsbedarf")]
    [InlineData(0.0,  "Sanierungsbedarf")]
    [InlineData(null, "n/a")]
    public void BuildPruefungsresultat_GibtKorrektenText(double? note, string expected)
    {
        Assert.Equal(expected, VsaConditionScorer.BuildPruefungsresultat(note));
    }

    // ── ComputeForRequirement ────────────────────────────────────────────

    [Fact]
    public void ComputeForRequirement_KeineFindings_GibtZustandsklasse4()
    {
        // Keine Schadenscodes -> Leitung i.O. -> ZN=4.00, DZ=400 (ohne Randbedingungen)
        var classified = new List<ClassifiedFinding>();
        var result = VsaConditionScorer.ComputeForRequirement(
            VsaRequirement.Standsicherheit, classified,
            assessmentLength: 10.0, minLength: 3.0, randbedingungen: 1.0);

        Assert.Equal(4.00, result.Zustandsnote);
        Assert.Equal(400.0, result.Dringlichkeitszahl);
    }

    [Fact]
    public void ComputeForRequirement_NurUnbekannteFindings_GibtNull()
    {
        // Unbekannte Codes -> keine Bewertung moeglich
        var finding = new VsaFinding { KanalSchadencode = "ZZZ", Raw = "" };
        var classified = new List<ClassifiedFinding>
        {
            new(finding, new VsaClassificationResult(null, null, null), IsUnknown: true)
        };

        var result = VsaConditionScorer.ComputeForRequirement(
            VsaRequirement.Standsicherheit, classified,
            assessmentLength: 10.0, minLength: 3.0, randbedingungen: 1.0);

        Assert.Null(result.Zustandsnote);
        Assert.Null(result.Dringlichkeitszahl);
    }

    [Fact]
    public void ComputeForRequirement_EzMin0_GibtZnStartMinus0()
    {
        // EZmin=0 -> ZN_start=0.4; Abminderung kann max 0.8 sein; ZN >= 0
        var finding = new VsaFinding { KanalSchadencode = "BAC", Raw = "" };
        var classified = new List<ClassifiedFinding>
        {
            new(finding, new VsaClassificationResult(EZD: 0, EZS: 0, EZB: 0), IsUnknown: false)
        };

        var result = VsaConditionScorer.ComputeForRequirement(
            VsaRequirement.Standsicherheit, classified,
            assessmentLength: 10.0, minLength: 3.0, randbedingungen: 1.0);

        Assert.NotNull(result.Zustandsnote);
        Assert.True(result.Zustandsnote!.Value >= 0.0);
        Assert.True(result.Zustandsnote.Value <= 0.4);
    }

    [Fact]
    public void ComputeForRequirement_MitRandbedingungen_SkalierungDZ()
    {
        // EZ=4 (beste Note) -> ZN=4.00; DZ=4.00*100*RB
        var finding = new VsaFinding { KanalSchadencode = "BCD", Raw = "" };
        var classified = new List<ClassifiedFinding>
        {
            new(finding, new VsaClassificationResult(EZD: null, EZS: 4, EZB: null), IsUnknown: false)
        };

        var result = VsaConditionScorer.ComputeForRequirement(
            VsaRequirement.Standsicherheit, classified,
            assessmentLength: 10.0, minLength: 3.0, randbedingungen: 0.9);

        Assert.Equal(4.00, result.Zustandsnote);
        Assert.Equal(360.00, result.Dringlichkeitszahl);
    }

    // ── ComputeRandbedingungen ───────────────────────────────────────────

    [Fact]
    public void ComputeRandbedingungen_AlleFehlend_Gibt1()
    {
        // Fehlende Werte -> alle B=1.0 -> Produkt=1.0
        var record = new HaltungRecord();
        var rb = VsaConditionScorer.ComputeRandbedingungen(record);
        Assert.Equal(1.0, rb, precision: 6);
    }

    [Fact]
    public void ComputeRandbedingungen_Schutzzonen_ReduziertRandbedingung()
    {
        // Gewaesserschutz S -> B1=0.90
        var record = new HaltungRecord();
        record.SetFieldValue("Gewaesserschutz", "S", FieldSource.Legacy, userEdited: false);
        var rb = VsaConditionScorer.ComputeRandbedingungen(record);
        Assert.Equal(0.90, rb, precision: 6);
    }
}
