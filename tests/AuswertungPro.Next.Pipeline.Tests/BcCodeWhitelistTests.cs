using AuswertungPro.Next.Application.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Regression-Tests fuer die BC-Whitelist im Soft-Filter
/// (Audit 2026-05-17, Codex-Patch BC-Whitelist).
///
/// Sichert dass VSA-KEK-Pflichtmeldungen NICHT durch den view_type-Filter
/// in EnhancedVisionAnalysisService unterdrueckt werden.
/// </summary>
public class BcCodeWhitelistTests
{
    [Theory]
    [InlineData("BCD")]
    [InlineData("BCE")]
    [InlineData("BCC")]
    [InlineData("BCA")]
    public void IsMandatory_StandardBcCodes(string code)
        => Assert.True(BcCodeWhitelist.IsMandatory(code));

    [Theory]
    [InlineData("BCCAY")]   // Bogen nach links
    [InlineData("BCCBY")]   // Bogen nach rechts
    [InlineData("BCAAA")]   // Anschluss-Char1+2
    public void IsMandatory_BcCodesMitCharakterisierung(string code)
        => Assert.True(BcCodeWhitelist.IsMandatory(code));

    [Theory]
    [InlineData("bcd")]
    [InlineData("Bce")]
    [InlineData(" BCC ")]
    public void IsMandatory_CaseInsensitiveUndTrimmt(string code)
        => Assert.True(BcCodeWhitelist.IsMandatory(code));

    [Theory]
    [InlineData("BAB")]     // Riss → Schaden, kein BC
    [InlineData("BBA")]     // Wurzel → Schaden
    [InlineData("BBC")]     // Ablagerung → Schaden
    [InlineData("BBF")]     // Infiltration → Schaden
    [InlineData("BDDC")]    // Wasserspiegel → BD, nicht BC
    [InlineData("AEDXO")]   // Material → AED
    [InlineData("BCF")]     // Hypothetischer BC* der NICHT in Whitelist ist
    public void IsMandatory_FalseFuerNichtBcCodes(string code)
        => Assert.False(BcCodeWhitelist.IsMandatory(code));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsMandatory_NullOderLeer(string? code)
        => Assert.False(BcCodeWhitelist.IsMandatory(code));
}
