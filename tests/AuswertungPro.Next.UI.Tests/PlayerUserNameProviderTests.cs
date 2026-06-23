using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerUserNameProviderTests
{
    [Fact]
    public void Current_uses_supplied_provider()
    {
        var user = PlayerUserNameProvider.Current(() => "tester");

        Assert.Equal("tester", user);
    }
}
