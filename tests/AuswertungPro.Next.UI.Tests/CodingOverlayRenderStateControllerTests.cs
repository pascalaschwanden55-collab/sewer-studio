using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOverlayRenderStateControllerTests
{
    [Fact]
    public void Defaults_to_unknown_aspect_and_hidden_reference_dn()
    {
        var state = new CodingOverlayRenderStateController();

        Assert.Equal(0, state.VideoAspect);
        Assert.False(state.ShowReferenceDn);
    }

    [Fact]
    public void Aspect_and_reference_dn_state_can_be_updated()
    {
        var state = new CodingOverlayRenderStateController();

        state.SetVideoAspect(16d / 9d);
        state.ShowReferenceDiameter();

        Assert.Equal(16d / 9d, state.VideoAspect);
        Assert.True(state.ShowReferenceDn);
    }
}
