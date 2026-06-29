using System;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageDragStartPolicy
{
    public static bool ShouldStartDrag(
        bool isProjectReady,
        bool isLeftButtonPressed,
        bool isEditingTextBox,
        double deltaX,
        double deltaY,
        double minimumHorizontalDragDistance,
        double minimumVerticalDragDistance)
    {
        if (!isProjectReady || !isLeftButtonPressed || isEditingTextBox)
            return false;

        return Math.Abs(deltaX) > minimumHorizontalDragDistance
            || Math.Abs(deltaY) > minimumVerticalDragDistance;
    }
}
