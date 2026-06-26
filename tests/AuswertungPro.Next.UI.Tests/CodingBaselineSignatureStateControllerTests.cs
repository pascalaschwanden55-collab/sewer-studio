using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingBaselineSignatureStateControllerTests
{
    [Fact]
    public void Baseline_signature_starts_empty()
    {
        var state = new CodingBaselineSignatureStateController();

        Assert.Equal(string.Empty, state.BaselineSignature);
    }

    [Fact]
    public void Set_updates_baseline_signature()
    {
        var state = new CodingBaselineSignatureStateController();

        state.Set("entry-1|entry-2");

        Assert.Equal("entry-1|entry-2", state.BaselineSignature);
    }
}
