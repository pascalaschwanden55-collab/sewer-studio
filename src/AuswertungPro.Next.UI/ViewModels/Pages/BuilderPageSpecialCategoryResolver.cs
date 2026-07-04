using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public static class BuilderPageSpecialCategoryResolver
{
    public static bool TryResolve(string combinedText, out SpecialStatsCategory category)
        => SpecialStatsClassifier.TryResolveSpecialStatsCategory(
            new CostLine { Text = combinedText ?? "" },
            out category);

    public static string GetLabel(SpecialStatsCategory category)
        => SpecialStatsClassifier.SpecialStatsConfigs
               .FirstOrDefault(cfg => cfg.Category == category)
               ?.Label
           ?? "Sonstiges";

    public static int GetOrder(SpecialStatsCategory category)
    {
        for (var i = 0; i < SpecialStatsClassifier.SpecialStatsConfigs.Length; i++)
        {
            if (SpecialStatsClassifier.SpecialStatsConfigs[i].Category == category)
                return i;
        }

        return 99;
    }

    public static string NormalizeUnit(string? unit, SpecialStatsCategory category)
    {
        var normalized = SpecialStatsClassifier.NormalizeUnit(unit);
        if (normalized.Length > 0)
            return normalized;

        return SpecialStatsClassifier.SpecialStatsConfigs
                   .FirstOrDefault(cfg => cfg.Category == category)
                   ?.DefaultUnit
               ?? "stk";
    }
}
