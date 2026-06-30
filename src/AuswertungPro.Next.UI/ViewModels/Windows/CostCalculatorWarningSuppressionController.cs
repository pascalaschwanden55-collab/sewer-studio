using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed class CostCalculatorWarningSuppressionController
{
    private readonly HashSet<string> _suppressedWarnings = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ConsistencyWarning> FilterVisibleWarnings(IEnumerable<ConsistencyWarning> warnings)
        => warnings
            .Where(w => !_suppressedWarnings.Contains(GetWarningKey(w)))
            .ToList();

    public void SuppressWarning(ConsistencyWarning warning)
    {
        _suppressedWarnings.Add(GetWarningKey(warning));
    }

    public void ResetSuppressedWarnings()
    {
        _suppressedWarnings.Clear();
    }

    public static string GetWarningKey(ConsistencyWarning warning)
        => $"{warning.RuleId}|{warning.MeasureId ?? ""}|{warning.ItemKey ?? ""}";
}
