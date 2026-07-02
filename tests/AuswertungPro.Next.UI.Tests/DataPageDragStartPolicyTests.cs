using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageDragStartPolicyTests
{
    [Theory]
    [InlineData(6, 0)]
    [InlineData(0, 6)]
    [InlineData(6, 6)]
    public void ShouldStartDrag_allows_drag_when_project_ready_left_button_pressed_and_threshold_exceeded(
        double deltaX,
        double deltaY)
    {
        var shouldStart = DataPageDragStartPolicy.ShouldStartDrag(
            isProjectReady: true,
            isLeftButtonPressed: true,
            isEditingTextBox: false,
            deltaX,
            deltaY,
            minimumHorizontalDragDistance: 5,
            minimumVerticalDragDistance: 5);

        Assert.True(shouldStart);
    }

    [Theory]
    [InlineData(false, true, false, 6, 0)]
    [InlineData(true, false, false, 6, 0)]
    [InlineData(true, true, true, 6, 0)]
    [InlineData(true, true, false, 5, 5)]
    [InlineData(true, true, false, 4, 4)]
    public void ShouldStartDrag_blocks_when_guard_or_threshold_is_not_satisfied(
        bool isProjectReady,
        bool isLeftButtonPressed,
        bool isEditingTextBox,
        double deltaX,
        double deltaY)
    {
        var shouldStart = DataPageDragStartPolicy.ShouldStartDrag(
            isProjectReady,
            isLeftButtonPressed,
            isEditingTextBox,
            deltaX,
            deltaY,
            minimumHorizontalDragDistance: 5,
            minimumVerticalDragDistance: 5);

        Assert.False(shouldStart);
    }
}
