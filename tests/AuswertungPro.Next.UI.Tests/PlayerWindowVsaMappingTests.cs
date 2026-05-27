using System.Reflection;
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
    public void Eingabemarker_keyword_mapping_matches_vsa_kek_manifest(string keyword, string expectedCode)
    {
        var method = typeof(PlayerWindow).GetMethod(
            "ResolveEingabemarkerCodeHint",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var code = Assert.IsType<string>(method!.Invoke(null, [keyword]));
        Assert.Equal(expectedCode, code);
    }

    [Theory]
    [InlineData("BAAA")]
    [InlineData("BAFAA")]
    [InlineData("BAJB")]
    public void Import_fallback_allows_current_vsa_kek_ba_families(string code)
    {
        var method = typeof(PlayerWindow).GetMethod(
            "IsAllowedImportFallbackCode",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.True(Assert.IsType<bool>(method!.Invoke(null, [code])));
    }
}
