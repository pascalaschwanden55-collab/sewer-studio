using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingStatisticsRefreshPolicy
{
    public static bool ShouldRefresh(string? propertyName)
        => propertyName is nameof(CodingSessionViewModel.StatAiCriteriaMet)
            or nameof(CodingSessionViewModel.StatHumanAccepted)
            or nameof(CodingSessionViewModel.StatHumanCorrected)
            or nameof(CodingSessionViewModel.StatRejected)
            or nameof(CodingSessionViewModel.StatOpen)
            or nameof(CodingSessionViewModel.StatAverageAiConfidenceText)
            or nameof(CodingSessionViewModel.EventCount)
            or null;
}
