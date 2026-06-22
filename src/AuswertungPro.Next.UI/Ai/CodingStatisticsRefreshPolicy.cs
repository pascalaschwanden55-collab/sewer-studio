using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingStatisticsRefreshPolicy
{
    public static bool ShouldRefresh(string? propertyName)
        => propertyName is nameof(CodingSessionViewModel.StatAutoAccepted)
            or nameof(CodingSessionViewModel.StatPending)
            or nameof(CodingSessionViewModel.StatReviewRequired)
            or nameof(CodingSessionViewModel.StatAverageConfidence)
            or nameof(CodingSessionViewModel.EventCount)
            or null;
}
