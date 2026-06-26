using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeStateControllerTests
{
    [Fact]
    public void Coding_mode_starts_disabled()
    {
        var state = new CodingModeStateController();

        Assert.False(state.IsCodingMode);
    }

    [Fact]
    public void Set_updates_coding_mode()
    {
        var state = new CodingModeStateController();

        state.Set(true);

        Assert.True(state.IsCodingMode);

        state.Set(false);

        Assert.False(state.IsCodingMode);
    }
}
