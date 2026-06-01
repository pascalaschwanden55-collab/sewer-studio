using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Weg 1: KB-Abgleich-Signal. KI-Code vs. Mehrheit der KB-Top-Codes (Hauptcode-Basis). Deterministisch.
/// </summary>
public sealed class KbCodeAgreementTests
{
    [Fact]
    public void Agreement_WhenKiMatchesKbMajority()
    {
        var r = KbCodeAgreement.Classify("BAI", new List<string> { "BAI", "BAI", "BAF" });
        Assert.Equal(KbCheckResult.KbAgreement, r);
    }

    [Fact]
    public void Agreement_IgnoresSubcodeAndCase_UsesMainCode()
    {
        // KI "bai.a" -> Basis BAI; KB "BAIBB"/"bai" -> Basis BAI
        var r = KbCodeAgreement.Classify("bai.a", new List<string> { "BAIBB", "bai", "BAF" });
        Assert.Equal(KbCheckResult.KbAgreement, r);
    }

    [Fact]
    public void Disagreement_WhenKbMajorityDiffersFromKi()
    {
        var r = KbCodeAgreement.Classify("BAI", new List<string> { "BAF", "BCC", "BAF" });
        Assert.Equal(KbCheckResult.KbDisagreement, r);
    }

    [Fact]
    public void NoSignal_WhenNoKbHits_OrEmptyKiCode()
    {
        Assert.Equal(KbCheckResult.KbNoSignal, KbCodeAgreement.Classify("BAI", new List<string>()));
        Assert.Equal(KbCheckResult.KbNoSignal, KbCodeAgreement.Classify("BAI", null));
        Assert.Equal(KbCheckResult.KbNoSignal, KbCodeAgreement.Classify("", new List<string> { "BAI" }));
    }
}
