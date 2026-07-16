using System;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Sichert die Staffelung: Karten kommen nacheinander an, aber die letzte darf nicht spuerbar
/// warten muessen — darum ist der Versatz gedeckelt.
/// </summary>
public sealed class EntranceFxTests
{
    [Fact]
    public void Delay_grows_with_each_card()
    {
        Assert.Equal(TimeSpan.Zero, EntranceFx.DelayFor(0));
        Assert.Equal(TimeSpan.FromMilliseconds(45), EntranceFx.DelayFor(1));
        Assert.Equal(TimeSpan.FromMilliseconds(135), EntranceFx.DelayFor(3));
    }

    [Fact]
    public void Delay_is_capped_so_late_cards_do_not_lag_behind()
    {
        var cap = EntranceFx.DelayFor(EntranceFx.MaxStaggeredChildren - 1);

        Assert.Equal(TimeSpan.FromMilliseconds(405), cap);
        Assert.Equal(cap, EntranceFx.DelayFor(50));
        Assert.Equal(cap, EntranceFx.DelayFor(1000));
    }

    [Fact]
    public void Negative_index_is_treated_as_the_first_card()
    {
        Assert.Equal(TimeSpan.Zero, EntranceFx.DelayFor(-5));
    }
}
