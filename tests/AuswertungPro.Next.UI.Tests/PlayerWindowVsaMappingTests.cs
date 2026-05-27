using System.Reflection;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowVsaMappingTests
{
    [Theory]
    [InlineData("VERFORMUNG", "BAF")]
    [InlineData("OBERFLAECHENSCHADEN", "BAJ")]
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
}
