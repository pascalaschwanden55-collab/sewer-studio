using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerLastOpenedWindowOwnerTests
{
    [Fact]
    public void Current_is_null_initially_and_after_clear()
    {
        var owner = new PlayerLastOpenedWindowOwner<object>();
        var window = new object();

        Assert.Null(owner.Current);
        Assert.False(owner.HasCurrent);

        owner.Set(window);
        owner.Clear();

        Assert.Null(owner.Current);
        Assert.False(owner.HasCurrent);
    }

    [Fact]
    public void Set_stores_current_window_by_reference()
    {
        var owner = new PlayerLastOpenedWindowOwner<object>();
        var window = new object();

        owner.Set(window);

        Assert.Same(window, owner.Current);
        Assert.True(owner.HasCurrent);
        Assert.True(owner.IsCurrent(window));
        Assert.False(owner.IsCurrent(new object()));
    }

    [Fact]
    public void ClearIfCurrent_only_clears_matching_window()
    {
        var owner = new PlayerLastOpenedWindowOwner<object>();
        var current = new object();
        var other = new object();
        owner.Set(current);

        Assert.False(owner.ClearIfCurrent(other));
        Assert.Same(current, owner.Current);

        Assert.True(owner.ClearIfCurrent(current));
        Assert.Null(owner.Current);
    }

    [Fact]
    public void Set_throws_for_null_window()
    {
        Assert.Throws<ArgumentNullException>(() => new PlayerLastOpenedWindowOwner<object>().Set(null!));
    }

    [Fact]
    public void IsCurrent_throws_for_null_window()
    {
        Assert.Throws<ArgumentNullException>(() => new PlayerLastOpenedWindowOwner<object>().IsCurrent(null!));
    }

    [Fact]
    public void ClearIfCurrent_throws_for_null_window()
    {
        Assert.Throws<ArgumentNullException>(() => new PlayerLastOpenedWindowOwner<object>().ClearIfCurrent(null!));
    }
}
