using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingUnappliedChangesClosePolicyTests
{
    [Fact]
    public void ShouldClose_returns_false_for_cancel_without_applying_changes()
    {
        var applyCalled = false;

        var result = CodingUnappliedChangesClosePolicy.ShouldClose(
            DialogConfirm.Cancel,
            () =>
            {
                applyCalled = true;
                return true;
            });

        Assert.False(result);
        Assert.False(applyCalled);
    }

    [Fact]
    public void ShouldClose_applies_changes_for_yes()
    {
        var applyCalled = false;

        var result = CodingUnappliedChangesClosePolicy.ShouldClose(
            DialogConfirm.Yes,
            () =>
            {
                applyCalled = true;
                return true;
            });

        Assert.True(result);
        Assert.True(applyCalled);
    }

    [Fact]
    public void ShouldClose_returns_true_for_no_without_applying_changes()
    {
        var applyCalled = false;

        var result = CodingUnappliedChangesClosePolicy.ShouldClose(
            DialogConfirm.No,
            () =>
            {
                applyCalled = true;
                return false;
            });

        Assert.True(result);
        Assert.False(applyCalled);
    }
}
