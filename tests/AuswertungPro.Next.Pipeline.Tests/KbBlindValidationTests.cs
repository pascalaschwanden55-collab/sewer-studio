using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Fehlerpruefung 11.07., Kritisch 1: Der KB-Beleg muss unabhaengig sein — kein
/// Selbst-Treffer aus derselben Haltung, kein unbestaetigtes Material, kein
/// schwacher Score, und die Suche darf weder Haltungs-ID noch Code enthalten.
/// </summary>
public sealed class KbBlindValidationTests
{
    private static KbValidationHit Hit(string caseId, string code, double score, bool? confirmed = true)
        => new(caseId, code, score, confirmed);

    [Fact]
    public void SameCase_Treffer_zaehlt_nicht_als_Beleg()
    {
        var r = KbBlindValidationService.EvaluateHits(
            new[] { Hit("10081-8993", "BAB", 0.99) }, "10081-8993", "BAB");
        Assert.False(r.Agrees);
    }

    [Fact]
    public void Score_unter_Mindestwert_kein_Agreement()
    {
        var r = KbBlindValidationService.EvaluateHits(
            new[] { Hit("FREMD-1", "BAB", 0.60) }, "10081-8993", "BAB");
        Assert.False(r.Agrees);
    }

    [Fact]
    public void Unbestaetigter_Treffer_kein_Agreement()
    {
        var r = KbBlindValidationService.EvaluateHits(
            new[] { Hit("FREMD-1", "BAB", 0.95, confirmed: null) }, "10081-8993", "BAB");
        Assert.False(r.Agrees);
    }

    [Fact]
    public void Bestaetigter_fremder_Treffer_mit_gleichem_Code_ist_Beleg()
    {
        var r = KbBlindValidationService.EvaluateHits(
            new[] { Hit("FREMD-1", "BAB", 0.85) }, "10081-8993", "BAB");
        Assert.True(r.Agrees);
        Assert.Equal("FREMD-1", r.BestHit!.CaseId);
    }

    [Fact]
    public void Bester_Gold_Treffer_mit_anderem_Code_widerspricht()
    {
        var r = KbBlindValidationService.EvaluateHits(
            new[] { Hit("FREMD-1", "BBC", 0.90), Hit("FREMD-2", "BAB", 0.80) }, "H", "BAB");
        Assert.False(r.Agrees); // der BESTE gueltige Treffer entscheidet
    }

    [Fact]
    public void BlindeQuery_enthaelt_weder_HaltungsId_noch_Code()
    {
        var d = new RawVideoDetection("Riss laengs", 12.4, 12.4, "high",
            VsaCodeHint: "BAB", PositionClock: "6:00");

        var query = KbBlindValidationService.BuildBlindQuery(d);

        Assert.DoesNotContain("BAB", query);
        Assert.DoesNotContain("Haltung", query);
        Assert.Contains("Riss laengs", query);
        Assert.Contains("Uhrlage 6:00", query);
    }
}
