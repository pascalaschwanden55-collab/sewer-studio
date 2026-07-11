using System;
using AuswertungPro.Next.UI.Controls.Animations;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Gestaffelte Einblend-Verzoegerung: linear pro Index, mit Deckel.</summary>
public sealed class StaggerDelayPolicyTests
{
    [Fact]
    public void First_element_starts_immediately()
    {
        Assert.Equal(TimeSpan.Zero, StaggerDelayPolicy.DelayFor(0));
    }

    [Fact]
    public void Delay_grows_linearly_with_index()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(40), StaggerDelayPolicy.DelayFor(1));
        Assert.Equal(TimeSpan.FromMilliseconds(120), StaggerDelayPolicy.DelayFor(3));
    }

    [Fact]
    public void Delay_is_capped_so_long_lists_do_not_stagger_forever()
    {
        var atCap = StaggerDelayPolicy.DelayFor(12);
        Assert.Equal(atCap, StaggerDelayPolicy.DelayFor(13));
        Assert.Equal(atCap, StaggerDelayPolicy.DelayFor(200));
    }

    [Fact]
    public void Negative_index_is_treated_as_zero()
    {
        Assert.Equal(TimeSpan.Zero, StaggerDelayPolicy.DelayFor(-5));
    }

    [Fact]
    public void Custom_step_is_respected()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(200), StaggerDelayPolicy.DelayFor(2, TimeSpan.FromMilliseconds(100)));
    }
}
