using System.Reflection;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowVsaMappingTests
{
    [Theory]
    [InlineData("VERFORMUNG", "BAA")]
    [InlineData("OBERFLAECHENSCHADEN", "BAF")]
    [InlineData("VERSATZ", "BAJ")]
    [InlineData("VERSCHIEBUNG", "BAJ")]
    [InlineData("WURZELN", "BBA")]
    [InlineData("BEWUCHS", "BBA")]
    [InlineData("INKRUSTATION", "BBB")]
    [InlineData("ROHRANFANG", "BCD")]
    [InlineData("ROHRENDE", "BCE")]
    [InlineData("ANSCHLUSS", "BCA")]
    [InlineData("BOGEN", "BCC")]
    [InlineData("WASSERSTAND", "BDDC")]
    public void Eingabemarker_keyword_mapping_matches_vsa_kek_manifest(string keyword, string expectedCode)
    {
        var code = PlayerVsaCodeHintResolver.ResolveKeyword(keyword);

        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public void Eingabemarker_keyword_mapping_returns_null_for_empty_keyword()
    {
        Assert.Null(PlayerVsaCodeHintResolver.ResolveKeyword(" "));
    }

    [Theory]
    [InlineData("BAAA")]
    [InlineData("BAFAA")]
    [InlineData("BAJB")]
    [InlineData("BCD")]
    [InlineData("BCE")]
    [InlineData("BCA")]
    [InlineData("BCC")]
    [InlineData("BBC")]
    [InlineData("BDDC")]
    [InlineData("BBA")]
    [InlineData("BBB")]
    [InlineData("BBD")]
    public void Import_fallback_allows_current_player_fallback_families(string code)
    {
        Assert.True(PlayerImportFallbackCodePolicy.IsAllowed(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("BDB")]
    [InlineData("BDC")]
    [InlineData("BAG")]
    [InlineData("BAK")]
    [InlineData("BBH")]
    public void Import_fallback_rejects_codes_outside_current_player_fallback_families(string code)
    {
        Assert.False(PlayerImportFallbackCodePolicy.IsAllowed(code));
    }
}
