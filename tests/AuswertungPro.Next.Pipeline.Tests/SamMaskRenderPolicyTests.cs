using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungstests fuer SamMaskRenderPolicy.
/// Stellt sicher dass DecideVisualMode das exakte Ist-Verhalten beibehlt.
/// WICHTIG: Das "immer SubtleFill bei sichtbarer Maske"-Verhalten (Backup-Verhalten
/// nach Maske-verzerrt-Regression vom 11.06) muss unveraendert bleiben.
/// </summary>
public class SamMaskRenderPolicyTests
{
    private static SamMaskResult MakeMask(
        string label = "damage",
        double confidence = 0.80,
        int maskAreaPixels = 100,
        int imageAreaPixels = 1000)
        => new(
            Label: label,
            Confidence: confidence,
            Bbox: new List<double> { 0, 0, 10, 10 },
            MaskRle: "",
            MaskAreaPixels: maskAreaPixels,
            ImageAreaPixels: imageAreaPixels,
            HeightPixels: 10,
            WidthPixels: 10,
            CentroidX: 5,
            CentroidY: 5);

    private static MaskQuantificationService.QuantifiedMask MakeQuant(
        string label = "BAB",
        double confidence = 0.80)
        => new(label, confidence, null, null, null, null, null, null);

    // ── Hintergrund-Labels werden versteckt ─────────────────────────

    [Theory]
    [InlineData("water wall")]
    [InlineData("Water Wall")]
    [InlineData("WATER WALL")]
    [InlineData("structure water wall")]
    [InlineData("pipe wall")]
    [InlineData("black border")]
    [InlineData("osd")]
    public void HintergrundLabel_WirdAlsHiddenMarkiert(string label)
    {
        var mask = MakeMask(label: label);
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, null, 0.9);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        Assert.Equal(SamMaskRenderPolicy.MaskVisualMode.Hidden, decision.Mode);
        Assert.Equal("background_label", decision.Reason);
    }

    [Fact]
    public void HintergrundLabel_MitUnterstrich_WirdErkannt()
    {
        // NormalizeLabel ersetzt '_' durch ' '
        var mask = MakeMask(label: "water_wall");
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, null, 0.9);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        Assert.Equal(SamMaskRenderPolicy.MaskVisualMode.Hidden, decision.Mode);
    }

    [Fact]
    public void HintergrundLabel_MitBindestrichen_WirdErkannt()
    {
        var mask = MakeMask(label: "pipe-wall");
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, null, 0.9);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        Assert.Equal(SamMaskRenderPolicy.MaskVisualMode.Hidden, decision.Mode);
    }

    // ── Konfidenz-Gate ───────────────────────────────────────────────

    [Fact]
    public void NiedrigeKonfidenz_BeideUnterSchwelle_WirdHidden()
    {
        // DetectionConf=0.10, SamConf=0.10, beide < 0.25 -> Hidden
        var mask = MakeMask(confidence: 0.10);
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, null, 0.10);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        Assert.Equal(SamMaskRenderPolicy.MaskVisualMode.Hidden, decision.Mode);
        Assert.Equal("confidence_too_low", decision.Reason);
    }

    [Fact]
    public void NiedrigeDetectionConf_AberHoheSamConf_WirdNichtHidden()
    {
        // DetectionConf=0.10 < 0.25, aber SamConf=0.80 >= 0.25 -> nicht hidden
        var mask = MakeMask(confidence: 0.80);
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, null, 0.10);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        Assert.NotEqual(SamMaskRenderPolicy.MaskVisualMode.Hidden, decision.Mode);
    }

    [Fact]
    public void NullDetectionConf_SamConfAlsErsatz_HoheConf_WirdNichtHidden()
    {
        // DetectionConf=null -> SamConf=0.80 wird als Fallback verwendet
        var mask = MakeMask(confidence: 0.80);
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, null, null);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        Assert.NotEqual(SamMaskRenderPolicy.MaskVisualMode.Hidden, decision.Mode);
    }

    [Fact]
    public void NullDetectionConf_SamConfNiedrig_WirdHidden()
    {
        var mask = MakeMask(confidence: 0.10);
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, null, null);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        Assert.Equal(SamMaskRenderPolicy.MaskVisualMode.Hidden, decision.Mode);
    }

    // ── Backup-Verhalten: immer SubtleFill bei sichtbarer Maske ─────
    // KRITISCH: Diese Tests sichern die Maske-verzerrt-Regression-Fix ab.
    // Jede sichtbare Maske (nicht Hidden) muss SubtleFill zurueckgeben,
    // NIEMALS OutlineOnly (das war die Regression).

    [Fact]
    public void SichtbareMaske_LiefertImmerSubtleFill_NichtOutlineOnly()
    {
        var mask = MakeMask(confidence: 0.80);
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, null, 0.90);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        Assert.Equal(SamMaskRenderPolicy.MaskVisualMode.SubtleFill, decision.Mode);
        Assert.Null(decision.Reason);
    }

    [Fact]
    public void GrosseMaske_LiefertTrotzdemSubtleFill_NichtOutlineOnly()
    {
        // Auch grosse Maske (hoher Area-Ratio) darf NICHT als OutlineOnly rendern.
        // Das war die Regression: grosse Flaeche wurde frueher auf OutlineOnly gesetzt,
        // was "verzerrt" aussah.
        var mask = MakeMask(confidence: 0.80, maskAreaPixels: 900, imageAreaPixels: 1000);
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, null, 0.90);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        Assert.Equal(SamMaskRenderPolicy.MaskVisualMode.SubtleFill, decision.Mode);
    }

    [Fact]
    public void ManuellerMarkPfad_KeineDetectionConf_LiefertSubtleFill()
    {
        // Manueller Mark-Pfad hat keine DetectionConf (null), aber hohe SAM-Conf
        var mask = MakeMask(confidence: 0.70);
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, null, null);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        Assert.Equal(SamMaskRenderPolicy.MaskVisualMode.SubtleFill, decision.Mode);
    }

    // ── QuantifiedMask-Label wird als Fallback verwendet (nur bei null) ─

    [Fact]
    public void NullMaskenLabel_QuantLabelIstHintergrund_WirdHidden()
    {
        // mask.Label ist null -> Fallback auf Quant.Label (via ??)
        var mask = new SamMaskResult(
            Label: null!,
            Confidence: 0.80,
            Bbox: new System.Collections.Generic.List<double> { 0, 0, 10, 10 },
            MaskRle: "",
            MaskAreaPixels: 100,
            ImageAreaPixels: 1000,
            HeightPixels: 10,
            WidthPixels: 10,
            CentroidX: 5,
            CentroidY: 5);
        var quant = MakeQuant(label: "pipe wall");
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, quant, 0.9);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        Assert.Equal(SamMaskRenderPolicy.MaskVisualMode.Hidden, decision.Mode);
    }

    [Fact]
    public void LeeresStringMaskenLabel_QuantLabelHintergrund_WirdNichtHidden()
    {
        // mask.Label ist "" (nicht null) -> ?? greift nicht -> leerer Label -> kein Hidden
        var mask = MakeMask(label: "");
        var quant = MakeQuant(label: "pipe wall");
        var candidate = new SamMaskRenderPolicy.MaskRenderCandidate(mask, quant, 0.9);
        var decision = SamMaskRenderPolicy.DecideVisualMode(candidate);
        // Leerer Label ist kein Hintergrund-Token -> SubtleFill (hohe Konfidenz)
        Assert.Equal(SamMaskRenderPolicy.MaskVisualMode.SubtleFill, decision.Mode);
    }

    // ── GetAreaRatio ─────────────────────────────────────────────────

    [Fact]
    public void GetAreaRatio_KorrektBerechnet()
    {
        var mask = MakeMask(maskAreaPixels: 300, imageAreaPixels: 1000);
        double ratio = SamMaskRenderPolicy.GetAreaRatio(mask);
        Assert.Equal(0.30, ratio, precision: 5);
    }

    [Fact]
    public void GetAreaRatio_ImageAreaNull_GibtNull()
    {
        var mask = MakeMask(maskAreaPixels: 100, imageAreaPixels: 0);
        double ratio = SamMaskRenderPolicy.GetAreaRatio(mask);
        Assert.Equal(0.0, ratio);
    }

    // ── NormalizeLabel ───────────────────────────────────────────────

    [Fact]
    public void NormalizeLabel_Trim_Underscores_Lowercase()
    {
        string result = SamMaskRenderPolicy.NormalizeLabel("  Water_Wall  ");
        Assert.Equal("water wall", result);
    }

    [Fact]
    public void NormalizeLabel_Bindestriche_WerdenZuLeerzeichen()
    {
        string result = SamMaskRenderPolicy.NormalizeLabel("pipe-wall");
        Assert.Equal("pipe wall", result);
    }

    // ── WinCan-Voreinstellung ────────────────────────────────────────

    [Fact]
    public void WinCanStyle_LargeFindingOutlineAreaRatio_IstDreissigProzent()
    {
        Assert.Equal(0.30, SamMaskRenderPolicy.RenderOptions.WinCanStyle.LargeFindingOutlineAreaRatio);
    }

    [Fact]
    public void WinCanStyle_MinimumVisibleKonfidenz_Ist025()
    {
        Assert.Equal(0.25, SamMaskRenderPolicy.RenderOptions.WinCanStyle.MinimumVisibleDetectionConfidence);
        Assert.Equal(0.25, SamMaskRenderPolicy.RenderOptions.WinCanStyle.MinimumVisibleSamConfidence);
    }

    [Fact]
    public void WinCanStyle_FillAlpha_Ist24()
    {
        Assert.Equal((byte)24, SamMaskRenderPolicy.RenderOptions.WinCanStyle.FillAlpha);
    }

    [Fact]
    public void WinCanStyle_StrokeAlpha_Ist230()
    {
        Assert.Equal((byte)230, SamMaskRenderPolicy.RenderOptions.WinCanStyle.StrokeAlpha);
    }
}
